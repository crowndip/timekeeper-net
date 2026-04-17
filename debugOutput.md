[16:26:10 INF] Starting Parental Control Web Service

[16:26:10 INF] Starting Parental Control Web Service

[16:26:11 INF] Database migrations applied successfully

[16:26:11 INF] Database migrations applied successfully



1 WRN] Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be persisted outside of the container. Protected data will be unavailable when container is destroyed. For more information go to https://aka.ms/aspnet/dataprotectionwarning



1 WRN] Storing keys in a directory '/root/.aspnet/DataProtection-Keys' that may not be persisted outside of the container. Protected data will be unavailable when container is destroyed. For more information go to https://aka.ms/aspnet/dataprotectionwarning

[16:26:11 WRN] No XML encryptor configured. Key {cd513940-2146-4bb2-8390-823e71f8dae1} may be persisted to storage in unencrypted form.

[16:26:11 WRN] No XML encryptor configured. Key {cd513940-2146-4bb2-8390-823e71f8dae1} may be persisted to storage in unencrypted form.

[16:26:11 WRN] Overriding HTTP_PORTS '8080' and HTTPS_PORTS ''. Binding to values defined by URLS instead 'http://+:80'.

[16:26:11 WRN] Overriding HTTP_PORTS '8080' and HTTPS_PORTS ''. Binding to values defined by URLS instead 'http://+:80'.

[16:26:11 WRN] The WebRootPath was not found: /app/wwwroot. Static files may be unavailable.

[16:26:11 WRN] The WebRootPath was not found: /app/wwwroot. Static files may be unavailable.

[16:26:11 INF] Now listening on: http://[::]:80

[16:26:11 INF] Now listening on: http://[::]:80

[16:26:11 INF] Application started. Press Ctrl+C to shut down.

[16:26:11 INF] Application started. Press Ctrl+C to shut down.

[16:26:11 INF] Hosting environment: Production

[16:26:11 INF] Hosting environment: Production

[16:26:11 INF] Content root path: /app

[16:26:11 INF] Content root path: /app

[16:26:31 ERR] Failed executing DbCommand (4ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

WHERE u."IsActive" AND u."AccountType" = 'Child'

[16:26:31 ERR] Failed executing DbCommand (4ms) [Parameters=[], CommandType='Text', CommandTimeout='30']

SELECT count(*)::int

FROM "Users" AS u

WHERE u."IsActive" AND u."AccountType" = 'Child'

[16:26:31 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

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

[16:26:31 ERR] An exception occurred while iterating over the results of a query for context type 'ParentalControl.WebService.Data.AppDbContext'.

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
