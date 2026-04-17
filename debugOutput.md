[17:19:58 INF] Starting Parental Control Web Service

[17:19:58 INF] Starting Parental Control Web Service

[17:19:58 INF] Database migrations applied successfully

[17:19:58 INF] Database migrations applied successfully



8 WRN] Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be persisted outside of the container. Protected data will be unavailable when container is destroyed. For more information go to https://aka.ms/aspnet/dataprotectionwarning



8 WRN] Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be persisted outside of the container. Protected data will be unavailable when container is destroyed. For more information go to https://aka.ms/aspnet/dataprotectionwarning

[17:19:58 WRN] No XML encryptor configured. Key {538a4343-58e6-424a-805a-ed60527dcd0c} may be persisted to storage in unencrypted form.

[17:19:58 WRN] No XML encryptor configured. Key {538a4343-58e6-424a-805a-ed60527dcd0c} may be persisted to storage in unencrypted form.

[17:19:58 WRN] Overriding HTTP_PORTS '8080' and HTTPS_PORTS ''. Binding to values defined by URLS instead 'http://+:80'.

[17:19:58 WRN] Overriding HTTP_PORTS '8080' and HTTPS_PORTS ''. Binding to values defined by URLS instead 'http://+:80'.

[17:19:58 WRN] The WebRootPath was not found: /app/wwwroot. Static files may be unavailable.

[17:19:58 WRN] The WebRootPath was not found: /app/wwwroot. Static files may be unavailable.

[17:19:58 INF] Now listening on: http://[::]:80

[17:19:58 INF] Now listening on: http://[::]:80

[17:19:58 INF] Application started. Press Ctrl+C to shut down.

[17:19:58 INF] Application started. Press Ctrl+C to shut down.

[17:19:58 INF] Hosting environment: Production

[17:19:58 INF] Hosting environment: Production

[17:19:58 INF] Content root path: /app

[17:19:58 INF] Content root path: /app

[17:20:00 INF] HTTP POST /_blazor/negotiate responded 200 in 13.8355 ms

[17:20:00 INF] HTTP POST /_blazor/negotiate responded 200 in 13.8355 ms

[17:20:03 ERR] Failed executing DbCommand (3ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:03 ERR] Failed executing DbCommand (3ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:03 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:03 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:03 INF] HTTP GET / responded 302 in 220.1884 ms

[17:20:03 INF] HTTP GET / responded 302 in 220.1884 ms

[17:20:03 INF] HTTP GET /setup responded 200 in 84.7566 ms

[17:20:03 INF] HTTP GET /setup responded 200 in 84.7566 ms

[17:20:03 INF] HTTP GET /_blazor responded 101 in 2802.0827 ms

[17:20:03 INF] HTTP GET /_blazor responded 101 in 2802.0827 ms

[17:20:03 INF] HTTP POST /_blazor/disconnect responded 400 in 10.2977 ms

[17:20:03 INF] HTTP POST /_blazor/disconnect responded 400 in 10.2977 ms

[17:20:03 INF] HTTP GET /_framework/blazor.server.js responded 304 in 4.2549 ms

[17:20:03 INF] HTTP GET /_framework/blazor.server.js responded 304 in 4.2549 ms

[17:20:03 INF] HTTP GET /_blazor/initializers responded 200 in 3.4625 ms

[17:20:03 INF] HTTP GET /_blazor/initializers responded 200 in 3.4625 ms

[17:20:03 INF] HTTP POST /_blazor/negotiate responded 200 in 0.7667 ms

[17:20:03 INF] HTTP POST /_blazor/negotiate responded 200 in 0.7667 ms

[17:20:26 INF] Initializing database...

[17:20:26 INF] Initializing database...

[17:20:26 INF] Database initialized successfully

[17:20:26 INF] Database initialized successfully

[17:20:28 ERR] Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:28 ERR] Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:28 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:28 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:28 INF] HTTP GET / responded 302 in 29.0346 ms

[17:20:28 INF] HTTP GET / responded 302 in 29.0346 ms

[17:20:28 INF] HTTP GET /setup responded 200 in 7.4231 ms

[17:20:28 INF] HTTP GET /setup responded 200 in 7.4231 ms

[17:20:28 INF] HTTP GET /_blazor responded 101 in 24915.1367 ms

[17:20:28 INF] HTTP GET /_blazor responded 101 in 24915.1367 ms

[17:20:28 INF] HTTP POST /_blazor/disconnect responded 200 in 6.2441 ms

[17:20:28 INF] HTTP POST /_blazor/disconnect responded 200 in 6.2441 ms

[17:20:28 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.4147 ms

[17:20:28 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.4147 ms

[17:20:28 INF] HTTP GET /_blazor/initializers responded 200 in 0.2163 ms

[17:20:28 INF] HTTP GET /_blazor/initializers responded 200 in 0.2163 ms

[17:20:28 INF] HTTP POST /_blazor/negotiate responded 200 in 0.4199 ms

[17:20:28 INF] HTTP POST /_blazor/negotiate responded 200 in 0.4199 ms

[17:20:35 ERR] Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:35 ERR] Failed executing DbCommand (0ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

[17:20:35 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:35 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

Npgsql.PostgresException (0x80004005): 42P01: relation "Users" does not exist


POSITION: 27

   at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)

   at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteReader(Boolean async, CommandBehavior behavior, CancellationToken cancellationToken)

   at Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteReaderAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.InitializeReaderAsync(AsyncEnumerator enumerator, CancellationToken cancellationToken)

   at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)

   at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()

  Exception data:

    Severity: ERROR

    SqlState: 42P01

    MessageText: relation "Users" does not exist

    Position: 27

    File: parse_relation.c

    Line: 1449

    Routine: parserOpenTable

[17:20:35 INF] HTTP GET /setup responded 200 in 8.7859 ms

[17:20:35 INF] HTTP GET /setup responded 200 in 8.7859 ms

[17:20:35 INF] HTTP GET /_blazor responded 101 in 6802.9449 ms

[17:20:35 INF] HTTP GET /_blazor responded 101 in 6802.9449 ms

[17:20:35 INF] HTTP POST /_blazor/disconnect responded 200 in 1.0831 ms

[17:20:35 INF] HTTP POST /_blazor/disconnect responded 200 in 1.0831 ms

[17:20:35 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.2075 ms

[17:20:35 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.2075 ms

[17:20:35 INF] HTTP GET /_blazor/initializers responded 200 in 0.3558 ms

[17:20:35 INF] HTTP GET /_blazor/initializers responded 200 in 0.3558 ms

[17:20:35 INF] HTTP POST /_blazor/negotiate responded 200 in 0.3946 ms

[17:20:35 INF] HTTP POST /_blazor/negotiate responded 200 in 0.3946 ms

[17:20:44 INF] HTTP GET /setup responded 200 in 9.9195 ms

[17:20:44 INF] HTTP GET /setup responded 200 in 9.9195 ms

[17:20:44 INF] HTTP GET /_blazor responded 101 in 8595.1737 ms

[17:20:44 INF] HTTP GET /_blazor responded 101 in 8595.1737 ms

[17:20:44 INF] HTTP POST /_blazor/disconnect responded 200 in 0.6488 ms

[17:20:44 INF] HTTP POST /_blazor/disconnect responded 200 in 0.6488 ms

[17:20:44 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.1232 ms

[17:20:44 INF] HTTP GET /_framework/blazor.server.js responded 304 in 0.1232 ms

[17:20:44 INF] HTTP GET /_blazor/initializers responded 200 in 0.0907 ms

[17:20:44 INF] HTTP GET /_blazor/initializers responded 200 in 0.0907 ms

[17:20:44 INF] HTTP POST /_blazor/negotiate responded 200 in 0.3260 ms

[17:20:44 INF] HTTP POST /_blazor/negotiate responded 200 in 0.3260 ms