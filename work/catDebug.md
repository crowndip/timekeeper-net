filip@filip-ThinkPad-T540p:~$ sudo /opt/parental-control/ParentalControl.Client set server-url https://tracking.jerhot.eu
Server URL set to: https://tracking.jerhot.eu
filip@filip-ThinkPad-T540p:~$ sudo tee /opt/parental-control/appsettings.json > /dev/null <<EOF
{
  "ParentalControl": {
    "ReverseProxy": {
      "Enabled": true,
      "Username": "xx",
      "Password": "xxx"
    }
  }
}
EOF



2026-04-18 22:56:05.891 +02:00 [INF] End processing HTTP request after 13.9102ms - 200
2026-04-18 22:57:05.905 +02:00 [INF] Start processing HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:57:05.905 +02:00 [INF] Sending HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:57:05.930 +02:00 [INF] Received HTTP response headers after 24.5199ms - 200
2026-04-18 22:57:05.930 +02:00 [INF] End processing HTTP request after 25.2834ms - 200
2026-04-18 22:58:05.943 +02:00 [INF] Start processing HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:58:05.943 +02:00 [INF] Sending HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:58:05.958 +02:00 [INF] Received HTTP response headers after 14.2738ms - 200
2026-04-18 22:58:05.958 +02:00 [INF] End processing HTTP request after 14.8297ms - 200
2026-04-18 22:59:05.971 +02:00 [INF] Start processing HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:59:05.971 +02:00 [INF] Sending HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 22:59:05.992 +02:00 [INF] Received HTTP response headers after 21.3678ms - 200
2026-04-18 22:59:05.993 +02:00 [INF] End processing HTTP request after 21.6996ms - 200
2026-04-18 23:00:06.006 +02:00 [INF] Start processing HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 23:00:06.006 +02:00 [INF] Sending HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 23:00:06.059 +02:00 [INF] Received HTTP response headers after 52.3168ms - 200
2026-04-18 23:00:06.059 +02:00 [INF] End processing HTTP request after 52.7542ms - 200
2026-04-18 23:01:06.069 +02:00 [INF] Start processing HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 23:01:06.070 +02:00 [INF] Sending HTTP request POST http://10.0.0.55:8081/api/client/usage
2026-04-18 23:01:06.089 +02:00 [INF] Received HTTP response headers after 19.7803ms - 200
2026-04-18 23:01:06.089 +02:00 [INF] End processing HTTP request after 20.1359ms - 200
