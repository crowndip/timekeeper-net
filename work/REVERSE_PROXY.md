# Reverse Proxy Configuration

## Overview

When clients are outside the local network, they can access the server through a reverse proxy (e.g., Nginx, Apache, Traefik) using HTTPS on port 443 with Basic Authentication.

## Architecture

```
Client (External) 
    ↓ HTTPS (port 443) + Basic Auth
Reverse Proxy (nginx/apache/traefik)
    ↓ HTTP (port 8080)
Parental Control Server (internal)
```

## Client Configuration

### Linux Client

Edit `/opt/parental-control/appsettings.json`:

```json
{
  "ParentalControl": {
    "ServerUrl": "https://your-domain.com",
    "TickIntervalSeconds": 60,
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    },
    "ReverseProxy": {
      "Enabled": true,
      "Username": "client-user",
      "Password": "secure-password"
    }
  }
}
```

Restart service:
```bash
sudo systemctl restart parental-control-client
```

### Windows Client

Edit `C:\ProgramData\ParentalControl\appsettings.json`:

```json
{
  "ParentalControl": {
    "ServerUrl": "https://your-domain.com",
    "TickIntervalSeconds": 60,
    "OfflineMode": {
      "Enabled": true,
      "CacheDurationMinutes": 1440
    },
    "ReverseProxy": {
      "Enabled": true,
      "Username": "client-user",
      "Password": "secure-password"
    }
  }
}
```

Restart service:
```powershell
Restart-Service ParentalControlClient
```

## Reverse Proxy Configuration Examples

### Nginx

```nginx
server {
    listen 443 ssl http2;
    server_name your-domain.com;

    ssl_certificate /etc/ssl/certs/your-cert.pem;
    ssl_certificate_key /etc/ssl/private/your-key.pem;

    location / {
        # Basic Authentication
        auth_basic "Parental Control";
        auth_basic_user_file /etc/nginx/.htpasswd;

        # Forward to internal server
        proxy_pass http://internal-server:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Create password file:
```bash
sudo htpasswd -c /etc/nginx/.htpasswd client-user
```

### Apache

```apache
<VirtualHost *:443>
    ServerName your-domain.com

    SSLEngine on
    SSLCertificateFile /etc/ssl/certs/your-cert.pem
    SSLCertificateKeyFile /etc/ssl/private/your-key.pem

    <Location />
        AuthType Basic
        AuthName "Parental Control"
        AuthUserFile /etc/apache2/.htpasswd
        Require valid-user

        ProxyPass http://internal-server:8080/
        ProxyPassReverse http://internal-server:8080/
    </Location>
</VirtualHost>
```

Create password file:
```bash
sudo htpasswd -c /etc/apache2/.htpasswd client-user
```

### Traefik (docker-compose.yml)

```yaml
services:
  traefik:
    image: traefik:v2.10
    command:
      - "--providers.docker=true"
      - "--entrypoints.websecure.address=:443"
      - "--certificatesresolvers.myresolver.acme.tlschallenge=true"
      - "--certificatesresolvers.myresolver.acme.email=admin@example.com"
    ports:
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock

  parental-control:
    image: parental-control-webservice:latest
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.pc.rule=Host(`your-domain.com`)"
      - "traefik.http.routers.pc.entrypoints=websecure"
      - "traefik.http.routers.pc.tls.certresolver=myresolver"
      - "traefik.http.routers.pc.middlewares=pc-auth"
      - "traefik.http.middlewares.pc-auth.basicauth.users=client-user:$$apr1$$hash$$here"
```

Generate password hash:
```bash
htpasswd -nb client-user password
```

## Security Considerations

1. **Use HTTPS**: Always use SSL/TLS for external access
2. **Strong Passwords**: Use strong, unique passwords for Basic Auth
3. **Firewall**: Block direct access to port 8080 from external networks
4. **Certificate**: Use valid SSL certificates (Let's Encrypt recommended)
5. **Rate Limiting**: Configure rate limiting on reverse proxy
6. **IP Whitelisting**: Optionally restrict to known IP ranges

## Testing

### Test from client machine:

```bash
# Linux
curl -u client-user:password https://your-domain.com/api/health

# Windows (PowerShell)
$cred = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("client-user:password"))
Invoke-WebRequest -Uri "https://your-domain.com/api/health" -Headers @{Authorization="Basic $cred"}
```

### Check client logs:

```bash
# Linux
sudo journalctl -u parental-control-client -f

# Windows
Get-Content "C:\ProgramData\ParentalControl\Logs\client-*.log" -Tail 50 -Wait
```

Look for: `Basic authentication configured for reverse proxy`

## Troubleshooting

### Authentication Failed
- Verify username/password in client config
- Check reverse proxy password file
- Ensure password file permissions are correct

### SSL Certificate Errors
- Ensure valid SSL certificate
- Check certificate expiration
- Verify certificate chain

### Connection Timeout
- Check firewall rules
- Verify reverse proxy is running
- Test direct connection to reverse proxy

### Server Not Responding
- Check internal server is running
- Verify reverse proxy can reach internal server
- Check proxy_pass/ProxyPass configuration

## Local Network vs External Access

**Local Network** (no proxy):
```json
{
  "ServerUrl": "http://192.168.1.100:8080",
  "ReverseProxy": {
    "Enabled": false
  }
}
```

**External Access** (with proxy):
```json
{
  "ServerUrl": "https://your-domain.com",
  "ReverseProxy": {
    "Enabled": true,
    "Username": "client-user",
    "Password": "secure-password"
  }
}
```

## Notes

- Basic Auth credentials are sent with every request
- Credentials are base64 encoded (not encrypted) - HTTPS is required
- Reverse proxy handles SSL termination
- Internal server remains on HTTP (no SSL overhead)
- Client automatically adds Authorization header when ReverseProxy.Enabled = true
