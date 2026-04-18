ilip@filip-ThinkPad-T540p:~$ [Tray] Loading configuration...
[Tray] Server URL: https://tracking.jerhot.eu
[Tray] Computer ID: a111db2a-dc6c-4d61-bdcd-44eef7a53101
[Tray] Username: filip
[Tray] Proxy user loaded from: /etc/parental-control/proxy-user
[Tray] Proxy password loaded from: /etc/parental-control/proxy-pass
[Tray] Basic authentication configured
[Tray] Configuration loaded successfully
[Tray] Sending usage report to server...
[Tray] Server response: OK
[Tray] Error updating time: Call from invalid thread
Unhandled exception. System.InvalidOperationException: Call from invalid thread
   at Avalonia.Threading.Dispatcher.<VerifyAccess>g__ThrowVerifyAccess|16_0()
   at Avalonia.Threading.Dispatcher.VerifyAccess()
   at Avalonia.AvaloniaObject.VerifyAccess()
   at Avalonia.AvaloniaObject.SetValue[T](StyledProperty`1 property, T value, BindingPriority priority)
   at Avalonia.Controls.TrayIcon.set_ToolTipText(String value)
   at ParentalControl.TrayIcon.TrayApp.UpdateTime() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.TrayIcon/Program.cs:line 215
   at System.Threading.Tasks.Task.<>c.<ThrowAsync>b__128_1(Object state)
   at System.Threading.ThreadPoolWorkQueue.Dispatch()
   at System.Threading.PortableThreadPool.WorkerThread.WorkerThreadStart()

