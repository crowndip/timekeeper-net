# Linux Client Freeze Analysis - Kubuntu / KDE Plasma

## Problem Statement

When the time limit is reached on Kubuntu (KDE Plasma), instead of logging off the user cleanly, the system completely freezes — mouse cursor stops moving, display is unresponsive, and the machine requires a hard reset.

---

## Environment

- OS: Kubuntu (KDE Plasma 6, Wayland by default)
- Display manager: SDDM
- Compositor / window manager: `kwin_wayland`
- Service: `parental-control-client.service` (runs as root)
- Enforcement method: `loginctl terminate-user {username}`

---

## Root Cause Chain

### Stage 1: Enforcement triggers `loginctl terminate-user`

When the server responds with `ShouldEnforce = true` and `EnforcementAction = "logout"`, the following executes:

**`EnforcementEngine.cs:116-141`**
```csharp
var process = Process.Start(new ProcessStartInfo
{
    FileName = "loginctl",
    Arguments = $"terminate-user {username}",
    UseShellExecute = false
});
if (process != null)
{
    await process.WaitForExitAsync();   // no timeout
    ...
}
```

`loginctl terminate-user {username}` instructs `systemd-logind` to terminate **all** sessions and **all** processes belonging to `{username}` simultaneously. This is equivalent to an immediate, system-wide SIGTERM-then-SIGKILL sweep against every process the user owns.

### Stage 2: `kwin_wayland` (Wayland compositor) is killed

On Kubuntu, `kwin_wayland` is both the **KDE window manager** and the **Wayland display server (compositor)**. It owns:

- The DRM master lock on the GPU
- All KMS display pipelines (framebuffers, cursor planes, CRTC state)
- All Wayland client connections
- The cursor position and hardware cursor plane

When `loginctl terminate-user` sends SIGTERM (and then SIGKILL after `DefaultTimeoutStopSec`, usually 90 s) to `kwin_wayland`, one of two things happens:

**Scenario A – SIGTERM received, cleanup partially completes:**
- `kwin_wayland` tries to release DRM resources gracefully
- Some GPU drivers (especially AMD `amdgpu`, Intel `i915`) may have an in-flight pageflip pending
- A pending pageflip that is never acknowledged leaves the display engine in a locked state
- `kwin_wayland` closes its DRM fd, but the GPU scanout stalls at the last committed buffer
- SDDM gets a `SessionRemoved` signal from logind, attempts to open the DRM device on VT1
- The DRM master is released (kernel clears it on fd close), but a stuck pageflip prevents SDDM from setting a new mode
- Display: last frame frozen, hardware cursor plane abandoned (cursor stops moving)

**Scenario B – SIGKILL sent before graceful cleanup:**
- `kwin_wayland` is killed mid-frame — no DRM cleanup occurs
- The DRM master lock is released by the kernel when the file descriptor is closed (SIGKILL closes all fds)
- However, outstanding GEM buffer object references, pending display flips, or NVIDIA proprietary driver state may not be released
- SDDM cannot open the DRM device cleanly
- Result: black or frozen display, no VT switch, no login screen

In both scenarios the Wayland compositor is gone. Without it:
- No process handles Wayland protocol messages
- libinput events flow into the kernel evdev layer but no compositor consumes them
- The hardware cursor is managed via a KMS cursor plane that `kwin_wayland` owned; when it dies the cursor plane update stops, freezing the cursor visually on screen
- The kernel is running normally, but there is nothing to render to the display

This explains the "even the mouse cursor freezes" symptom — it is not a kernel hang, it is a display-layer collapse.

### Stage 3: SDDM fails to recover the display

SDDM runs as the `sddm` system user (filtered out of session monitoring correctly). It normally waits for a `SessionRemoved` event from `systemd-logind`, then:
1. Switches to its own VT (usually VT1 or VT7)
2. Starts a new Xorg or compositor instance for the login screen

This recovery can fail for several reasons after an abrupt `terminate-user`:

| Failure point | Why it fails |
|---|---|
| VT switch | If `kwin_wayland` held the active VT and its VT release wasn't signalled, the kernel VT subsystem may not automatically switch |
| DRM device open | On NVIDIA (if installed), the driver keeps GPU context alive until all references are dropped, which SIGKILL may not fully trigger |
| Wayland socket cleanup | The Wayland socket at `/run/user/{uid}/wayland-0` may linger; SDDM starting a new compositor session can conflict |
| user@{uid}.service | systemd's user session manager may be in `deactivating` state while SDDM tries to start — logind serialises these operations |

The combined result is a display that is frozen at the last KDE desktop frame, with the cursor immobile, while the machine is otherwise alive (kernel running, systemd processing, network active).

---

## Secondary Bugs That Worsen the Situation

### Bug 1: No timeout on `WaitForExitAsync`

**`EnforcementEngine.cs:130`**
```csharp
await process.WaitForExitAsync();   // blocks forever if loginctl hangs
```

`loginctl terminate-user` sends a D-Bus message to `systemd-logind` and waits for acknowledgement. If logind is itself busy processing the user scope teardown (stopping `user@{uid}.service`, releasing cgroup resources), `loginctl` can block for `DefaultTimeoutStopSec` seconds (default: 90 s). During this time the enforcement method is blocked, and the worker tick cannot proceed to the next iteration.

More critically: if SDDM's display recovery also gets stuck (see Stage 3), you end up with:
- `loginctl` blocking in the root service (waiting on logind)
- logind waiting for all user processes to die (including SDDM's attempt to open the DRM device)
- SDDM stuck because the DRM device isn't usable

This is a deadlock-adjacent situation, though at the OS level rather than inside the .NET process.

### Bug 2: No graceful logout attempt before hard termination

`loginctl terminate-user` is the equivalent of a forced kill — there is no opportunity for KDE to save session state, close files, or perform a clean DRM handoff.

KDE Plasma provides a D-Bus method specifically for graceful logoff:
```
org.kde.Shutdown /Shutdown org.kde.Shutdown.logout
```

If this is called first, KDE:
1. Saves session state
2. Closes all client windows
3. Releases the Wayland compositor cleanly (proper DRM mode unset, VT release)
4. Notifies SDDM via logind, which then switches VT and shows the login screen

The root service cannot call this directly (it has no session D-Bus bus address), but it can call it as the target user:
```bash
sudo -u {username} DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/{uid}/bus \
  qdbus org.kde.Shutdown /Shutdown org.kde.Shutdown.logout
```

### Bug 3: No VT switch before session termination

On systems using a VT-based display (X11 or Wayland with VTs), the correct procedure before destroying a session is to tell the kernel to switch to SDDM's VT first. Without this, even if SDDM gets the `SessionRemoved` event, it is starting from a "background VT" perspective and its attempt to activate its own VT may be ignored or delayed.

The active VT switch can be done with:
```bash
chvt 1   # switch to VT1 where SDDM runs
```

### Bug 4: `terminate-user` vs `terminate-session`

`loginctl terminate-user {username}` destroys ALL sessions for the user and also terminates the user's systemd lingering scope. `loginctl terminate-session {sessionId}` ends only a specific session, which allows logind to manage the transition more gracefully (e.g., giving SDDM time to prepare before the VT is released).

Using the session ID (already available in the code via `sessionId` parameter) would be less destructive.

---

## Code Flow Summary

```
ProcessTickAsync()
  -> SubmitUsageAsync(userRecords)          # sends usage to server
  -> CheckAndEnforceAsync(response, ...)    # response.ShouldEnforce = true
    -> LogoutUserAsync(username)
      -> loginctl terminate-user {username} # KILLS kwin_wayland
      -> WaitForExitAsync()                 # may block up to 90 s
                                            # display frozen from this point on
```

---

## Recommended Fix: Graduated Shutdown Sequence

The enforcement should attempt logoff in order from most graceful to most forceful, with a timeout at each step:

```
1. DBUS graceful KDE logout (as user, via /run/user/{uid}/bus)
   Wait up to 10 seconds for session to end

2. loginctl terminate-session {sessionId}
   Wait up to 15 seconds for session to end

3. loginctl terminate-user {username}  (current, only approach)
   Wait up to 15 seconds with a hard timeout

4. pkill -KILL -u {username}  (nuclear, direct kernel kill)
```

Each step should check whether the session is still present in `loginctl list-sessions` before escalating.

Additionally, `WaitForExitAsync` must be given a `CancellationToken` with a `CancellationTokenSource` set to a reasonable timeout (e.g., 30 seconds) to prevent indefinite blocking.

---

## Relevant Files

| File | Issue |
|---|---|
| `src/ParentalControl.Client/Services/EnforcementEngine.cs:116-141` | `LogoutUserAsync` uses only `terminate-user`, no timeout on wait, no graceful fallback |
| `src/ParentalControl.Client/Services/EnforcementEngine.cs:143-168` | `LockSessionAsync` would be safer on Wayland (uses `lock-session`) but also has no timeout |
| `src/ParentalControl.Client/ParentalControlWorker.cs:108` | Enforcement called correctly with username/sessionId, but the enforcement itself is destructive |
| `scripts/parental-control-client.service` | Service hardening (`PrivateTmp=true`) does not affect the loginctl D-Bus call, but lack of `KillSignal` directive means the service itself may not shut down cleanly on restart during an active enforcement |

---

## Verification Steps

To confirm the diagnosis after the freeze occurs (if SSH or serial console is accessible):

```bash
# Check if kernel is still alive
# (via SSH from another machine)

# Check logind state
loginctl list-sessions
loginctl list-users

# Check if kwin_wayland is dead
ps aux | grep kwin

# Check if SDDM is stuck
systemctl status sddm

# Check DRM state (requires root)
ls /dev/dri/
cat /sys/kernel/debug/dri/0/state   # or dri/1 depending on GPU

# Check systemd journal from the freeze
journalctl -u parental-control-client --since "10 min ago"
journalctl -u sddm --since "10 min ago"
journalctl _SYSTEMD_UNIT=user@1000.service --since "10 min ago"
```

To test the fix manually before deploying:
```bash
# Get the child user's UID
id -u {username}

# Attempt graceful KDE logout
sudo -u {username} \
  DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/$(id -u {username})/bus \
  qdbus org.kde.Shutdown /Shutdown org.kde.Shutdown.logout

# If above fails after 10 s, try:
loginctl terminate-session $(loginctl show-user {username} -p Sessions --value | cut -d' ' -f1)

# Confirm SDDM shows login screen - display should NOT freeze
```
