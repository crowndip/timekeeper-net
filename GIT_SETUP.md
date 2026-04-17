# Git Setup Commands

## Initial Push to GitHub

### Prerequisites
1. Create empty repository on GitHub: https://github.com/crowndip/timekeeper-net
2. Generate Personal Access Token (PAT): https://github.com/settings/tokens
   - Scopes needed: `repo` (full control of private repositories)

### Commands

**Step 1: Initialize and commit**
```bash
cd /config/timekeeper-net
git init
git add .
git commit -m "Initial commit: Parental Control System .NET 10"
```

**Step 2: Add remote with PAT**
```bash
git remote add origin https://YOUR_PAT@github.com/crowndip/timekeeper-net.git
```

**Step 3: Push to GitHub**
```bash
git branch -M main
git push -u origin main
```

### One-Liner
```bash
cd /config/timekeeper-net && \
git init && \
git add . && \
git commit -m "Initial commit: Parental Control System .NET 10" && \
git remote add origin https://YOUR_PAT@github.com/crowndip/timekeeper-net.git && \
git branch -M main && \
git push -u origin main
```

### Alternative: Credential Helper (More Secure)
```bash
cd /config/timekeeper-net
git init
git add .
git commit -m "Initial commit: Parental Control System .NET 10"
git remote add origin https://github.com/crowndip/timekeeper-net.git
git config credential.helper store
git branch -M main
git push -u origin main
# Enter username: crowndip
# Enter password: YOUR_PAT
```

### Verify
```bash
git remote -v
git log --oneline
```

## Notes

- Replace `YOUR_PAT` with your actual Personal Access Token
- PAT is like a password - keep it secure
- Don't commit PAT to repository
- Token stored in `.git/config` (not tracked)
- For credential helper, token stored in `~/.git-credentials`

## Future Commits

After initial setup:
```bash
git add .
git commit -m "Your commit message"
git push
```
