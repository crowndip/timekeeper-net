# Simple Password Authentication

Added basic password authentication to protect the web dashboard.

## Configuration

Set the admin password in `docker-compose.yml`:

```yaml
environment:
  AdminPassword: ${ADMIN_PASSWORD:-admin}
```

Or set the environment variable:

```bash
export ADMIN_PASSWORD=your_secure_password
```

Default password: `admin`

## How It Works

- All pages require authentication (except `/login`)
- Password stored in sessionStorage (8-hour timeout)
- Simple session-based authentication
- No database required

## Usage

1. Visit `http://localhost:8080`
2. Redirected to `/login`
3. Enter password
4. Access dashboard

## Security Notes

- This is basic authentication for internal use
- Use strong passwords in production
- Consider adding HTTPS
- Client API endpoints remain unauthenticated (by design)
