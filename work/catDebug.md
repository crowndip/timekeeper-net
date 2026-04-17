filip@filip-ThinkPad-T540p:~$ cat /var/log/parental-control/client20260417.log
2026-04-17 22:57:38.523 +02:00 [INF] Starting Parental Control Client
2026-04-17 22:57:38.793 +02:00 [INF] Parental Control Client started, tick interval: 60s
2026-04-17 22:57:38.793 +02:00 [INF] Application started. Hosting environment: Production; Content root path: /opt/parental-control
2026-04-17 23:13:16.610 +02:00 [INF] Starting Parental Control Client
2026-04-17 23:13:16.809 +02:00 [INF] Application started. Hosting environment: Production; Content root path: /opt/parental-control
2026-04-17 23:13:16.809 +02:00 [INF] Parental Control Client started, tick interval: 60s
2026-04-17 23:13:21.835 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:14:21.858 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:15:21.873 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:16:21.886 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:17:21.899 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:18:21.908 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
2026-04-17 23:19:21.924 +02:00 [ERR] Error processing tick
System.FormatException: Unrecognized Guid format.
   at System.Guid.GuidResult.SetFailure(ParseFailure failureKind)
   at System.Guid.TryParseGuid(ReadOnlySpan`1 guidString, GuidResult& result)
   at System.Guid.Parse(ReadOnlySpan`1 input)
   at System.Guid.Parse(String input)
   at ParentalControl.Client.Services.TimeTracker.RecordMinuteAsync(UserSession session) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Services/TimeTracker.cs:line 27
   at ParentalControl.Client.ParentalControlWorker.ProcessTickAsync() in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 75
   at ParentalControl.Client.ParentalControlWorker.ExecuteAsync(CancellationToken stoppingToken) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/ParentalControlWorker.cs:line 43
filip@filip-ThinkPad-T540p:~$ 

