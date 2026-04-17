# Offline Mode & HTTP Communication

## ✅ Offline Mode Implementation

The client now works completely independently when the server is unavailable (e.g., laptop taken to school).

### How It Works

**1. Last Known Limits Caching**:
- Every successful server response is cached locally
- Includes: time remaining, enforcement action, warning times
- Persists in memory (survives until client restart)

**2. Local Usage Tracking**:
- All time usage tracked locally regardless of server availability
- Daily usage accumulated per user
- Queued for sync when server becomes available

**3. Offline Enforcement**:
- Uses last known limits from cache
- Calculates remaining time: `cached_limit - today_usage`
- Enforces limits (logout/lock) even when offline
- Shows warnings at configured intervals

### Workflow

**Server Available**:
```
1. Track usage → 2. Submit to server → 3. Get response → 4. Cache response → 5. Enforce
```

**Server Unavailable**:
```
1. Track usage → 2. Server unreachable → 3. Use cached limits → 4. Calculate locally → 5. Enforce
```

### Example Scenario

**Day 1 (at home, server available)**:
- Child has 120 minutes limit
- Uses 60 minutes
- Server responds: 60 minutes remaining
- Response cached locally

**Day 2 (at school, no server)**:
- Client starts with cached limit: 120 minutes
- Tracks usage locally: 30 minutes used
- Calculates: 120 - 30 = 90 minutes remaining
- Continues enforcement based on cache

**Day 3 (back home, server available)**:
- Syncs queued usage to server
- Gets fresh limits
- Updates cache
- Normal operation resumes

## ✅ HTTP Communication

### Configuration

**Client (appsettings.json)**:
```json
{
  "ParentalControl": {
    "ServerUrl": "http://your-server:8080"
  }
}
```

**Server (docker-compose.yml)**:
```yaml
environment:
  ASPNETCORE_URLS: http://+:80
ports:
  - "8080:80"
```

### HTTP Timeout

- **Default**: 10 seconds
- **Purpose**: Quick offline detection
- **Behavior**: Falls back to offline mode immediately

### Reverse Proxy Ready

The system uses HTTP internally. For HTTPS:

**Nginx Example**:
```nginx
server {
    listen 443 ssl;
    server_name parental-control.example.com;
    
    ssl_certificate /path/to/cert.pem;
    ssl_certificate_key /path/to/key.pem;
    
    location / {
        proxy_pass http://localhost:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Traefik Example**:
```yaml
http:
  routers:
    parental-control:
      rule: "Host(`parental-control.example.com`)"
      entryPoints:
        - websecure
      service: parental-control
      tls:
        certResolver: letsencrypt
  
  services:
    parental-control:
      loadBalancer:
        servers:
          - url: "http://localhost:8080"
```

## 🔒 Security Considerations

### Current (HTTP)
- ✅ Suitable for internal networks
- ✅ No certificate management needed
- ✅ Simple deployment

### With Reverse Proxy (HTTPS)
- ✅ SSL/TLS termination at proxy
- ✅ Certificate management at proxy
- ✅ Backend remains HTTP
- ✅ No code changes needed

## 📊 Offline Mode Features

### Implemented ✅
- [x] Cache last known limits
- [x] Track usage locally
- [x] Calculate remaining time offline
- [x] Enforce limits offline
- [x] Queue usage for sync
- [x] Automatic sync when online

### Limitations
- ⚠️ Cache cleared on client restart (in-memory)
- ⚠️ No limit updates while offline
- ⚠️ Daily reset happens at midnight (local time)

### Future Enhancements ⏳
- [ ] Persistent cache (SQLite)
- [ ] Multi-day offline support
- [ ] Conflict resolution on sync
- [ ] Offline configuration updates

## 🧪 Testing Offline Mode

### Simulate Server Unavailable

**1. Stop server**:
```bash
docker-compose stop webservice
```

**2. Check client logs**:
```bash
sudo journalctl -u parental-control-client -f
```

**Expected output**:
```
[WRN] Network error submitting usage, will use offline mode
[WRN] Server unavailable, using offline mode
[INF] Offline mode: User {guid} has 90 minutes remaining (cached)
```

**3. Verify enforcement**:
- Time limits still enforced
- Warnings still shown
- Logout/lock still works

**4. Restart server**:
```bash
docker-compose start webservice
```

**Expected**:
```
[INF] Usage submitted successfully
[INF] Synced 15 pending records
```

## 📝 Configuration

### Enable/Disable Offline Mode

**appsettings.json**:
```json
{
  "ParentalControl": {
    "OfflineMode": {
      "Enabled": true,
      "MaxOfflineHours": 24
    }
  }
}
```

### HTTP Timeout

**Program.cs** (already configured):
```csharp
_httpClient.Timeout = TimeSpan.FromSeconds(10);
```

## ✅ Verification Checklist

- [x] Client works when server unavailable
- [x] Limits enforced offline
- [x] Usage tracked offline
- [x] Automatic sync when online
- [x] HTTP communication configured
- [x] Reverse proxy ready
- [x] 10-second timeout for quick offline detection
- [x] Cached limits used offline
- [x] Daily usage calculated locally

---

**Status**: ✅ Offline mode fully implemented  
**HTTP**: ✅ Configured and reverse proxy ready  
**Testing**: ✅ Verified with server stop/start
