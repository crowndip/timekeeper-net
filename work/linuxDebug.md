filip@filip-ThinkPad-T540p:~$ ps aux | grep ParentalControl
root       35684  0.2  0.4 274263956 66712 ?     Ssl  23:13   0:00 /opt/parental-control/ParentalControl.Client
filip      35832  0.0  0.0  11812  2496 pts/3    S+   23:15   0:00 grep --color=auto ParentalControl
filip@filip-ThinkPad-T540p:~$ ls -la /opt/parental-control/
total 80044
drwxr-xr-x 1 tonik tonik    14450 dub 17 23:13 .
drwxr-xr-x 1 root  root        78 dub 17 23:12 ..
-rw-r--r-- 1 tonik tonik      229 dub 17 23:13 appsettings.json
-rwxr--r-- 1 tonik tonik   108688 dub 17 23:09 createdump
-rwxr--r-- 1 tonik tonik   823752 dub 17 23:09 libclrgc.so
-rwxr--r-- 1 tonik tonik  3612176 dub 17 23:09 libclrjit.so
-rwxr--r-- 1 tonik tonik  6905880 dub 17 23:09 libcoreclr.so
-rwxr--r-- 1 tonik tonik   760632 dub 17 23:09 libcoreclrtraceptprovider.so
-rwxr--r-- 1 tonik tonik  1249880 dub 17 23:09 libe_sqlite3.so
-rwxr--r-- 1 tonik tonik   288848 dub 17 23:09 libhostfxr.so
-rwxr--r-- 1 tonik tonik   309792 dub 17 23:09 libhostpolicy.so
-rwxr--r-- 1 tonik tonik  2430048 dub 17 23:09 libmscordaccore.so
-rwxr--r-- 1 tonik tonik  1625304 dub 17 23:09 libmscordbi.so
-rwxr--r-- 1 tonik tonik    60560 dub 17 23:09 libSystem.Globalization.Native.so
-rwxr--r-- 1 tonik tonik   867416 dub 17 23:09 libSystem.IO.Compression.Native.so
-rwxr--r-- 1 tonik tonik    90544 dub 17 23:09 libSystem.Native.so
-rwxr--r-- 1 tonik tonik    13544 dub 17 23:09 libSystem.Net.Security.Native.so
-rwxr--r-- 1 tonik tonik   160288 dub 17 23:09 libSystem.Security.Cryptography.Native.OpenSsl.so
-rwxr--r-- 1 tonik tonik   817416 dub 17 23:09 Microsoft.CSharp.dll
-rwxr--r-- 1 tonik tonik   175136 dub 17 23:09 Microsoft.Data.Sqlite.dll
-rwxr--r-- 1 tonik tonik    34848 dub 17 23:09 Microsoft.EntityFrameworkCore.Abstractions.dll
-rwxr--r-- 1 tonik tonik  2535456 dub 17 23:09 Microsoft.EntityFrameworkCore.dll
-rwxr--r-- 1 tonik tonik  1995808 dub 17 23:09 Microsoft.EntityFrameworkCore.Relational.dll
-rwxr--r-- 1 tonik tonik   259144 dub 17 23:09 Microsoft.EntityFrameworkCore.Sqlite.dll
-rwxr--r-- 1 tonik tonik    31008 dub 17 23:09 Microsoft.Extensions.Caching.Abstractions.dll
-rwxr--r-- 1 tonik tonik    45832 dub 17 23:09 Microsoft.Extensions.Caching.Memory.dll
-rwxr--r-- 1 tonik tonik    27912 dub 17 23:09 Microsoft.Extensions.Configuration.Abstractions.dll
-rwxr--r-- 1 tonik tonik    42768 dub 17 23:09 Microsoft.Extensions.Configuration.Binder.dll
-rwxr--r-- 1 tonik tonik    24840 dub 17 23:09 Microsoft.Extensions.Configuration.CommandLine.dll
-rwxr--r-- 1 tonik tonik    43792 dub 17 23:09 Microsoft.Extensions.Configuration.dll
-rwxr--r-- 1 tonik tonik    22288 dub 17 23:09 Microsoft.Extensions.Configuration.EnvironmentVariables.dll
-rwxr--r-- 1 tonik tonik    27960 dub 17 23:09 Microsoft.Extensions.Configuration.FileExtensions.dll
-rwxr--r-- 1 tonik tonik    26936 dub 17 23:09 Microsoft.Extensions.Configuration.Json.dll
-rwxr--r-- 1 tonik tonik    25400 dub 17 23:09 Microsoft.Extensions.Configuration.UserSecrets.dll
-rwxr--r-- 1 tonik tonik    65296 dub 17 23:09 Microsoft.Extensions.DependencyInjection.Abstractions.dll
-rwxr--r-- 1 tonik tonik    94984 dub 17 23:09 Microsoft.Extensions.DependencyInjection.dll
-rwxr--r-- 1 tonik tonik    79664 dub 17 23:09 Microsoft.Extensions.DependencyModel.dll
-rwxr--r-- 1 tonik tonik    30520 dub 17 23:09 Microsoft.Extensions.Diagnostics.Abstractions.dll
-rwxr--r-- 1 tonik tonik    35600 dub 17 23:09 Microsoft.Extensions.Diagnostics.dll
-rwxr--r-- 1 tonik tonik    22840 dub 17 23:09 Microsoft.Extensions.FileProviders.Abstractions.dll
-rwxr--r-- 1 tonik tonik    44816 dub 17 23:09 Microsoft.Extensions.FileProviders.Physical.dll
-rwxr--r-- 1 tonik tonik    46864 dub 17 23:09 Microsoft.Extensions.FileSystemGlobbing.dll
-rwxr--r-- 1 tonik tonik    53520 dub 17 23:09 Microsoft.Extensions.Hosting.Abstractions.dll
-rwxr--r-- 1 tonik tonik    71440 dub 17 23:09 Microsoft.Extensions.Hosting.dll
-rwxr--r-- 1 tonik tonik    24848 dub 17 23:09 Microsoft.Extensions.Hosting.Systemd.dll
-rwxr--r-- 1 tonik tonik    92432 dub 17 23:09 Microsoft.Extensions.Http.dll
-rwxr--r-- 1 tonik tonik    33032 dub 17 23:09 Microsoft.Extensions.Http.Polly.dll
-rwxr--r-- 1 tonik tonik    66320 dub 17 23:09 Microsoft.Extensions.Logging.Abstractions.dll
-rwxr--r-- 1 tonik tonik    27920 dub 17 23:09 Microsoft.Extensions.Logging.Configuration.dll
-rwxr--r-- 1 tonik tonik    75528 dub 17 23:09 Microsoft.Extensions.Logging.Console.dll
-rwxr--r-- 1 tonik tonik    20280 dub 17 23:09 Microsoft.Extensions.Logging.Debug.dll
-rwxr--r-- 1 tonik tonik    50960 dub 17 23:09 Microsoft.Extensions.Logging.dll
-rwxr--r-- 1 tonik tonik    25360 dub 17 23:09 Microsoft.Extensions.Logging.EventLog.dll
-rwxr--r-- 1 tonik tonik    35088 dub 17 23:09 Microsoft.Extensions.Logging.EventSource.dll
-rwxr--r-- 1 tonik tonik    22280 dub 17 23:09 Microsoft.Extensions.Options.ConfigurationExtensions.dll
-rwxr--r-- 1 tonik tonik    64824 dub 17 23:09 Microsoft.Extensions.Options.dll
-rwxr--r-- 1 tonik tonik    44344 dub 17 23:09 Microsoft.Extensions.Primitives.dll
-rwxr--r-- 1 tonik tonik  1218832 dub 17 23:09 Microsoft.VisualBasic.Core.dll
-rwxr--r-- 1 tonik tonik    17672 dub 17 23:09 Microsoft.VisualBasic.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 Microsoft.Win32.Primitives.dll
-rwxr--r-- 1 tonik tonik    33032 dub 17 23:09 Microsoft.Win32.Registry.dll
-rwxr--r-- 1 tonik tonik    59696 dub 17 23:09 mscorlib.dll
-rwxr--r-- 1 tonik tonik   101136 dub 17 23:09 netstandard.dll
-rwxr-xr-x 1 tonik tonik    72568 dub 17 23:09 ParentalControl.Client
-rw-r--r-- 1 tonik tonik    70173 dub 17 23:09 ParentalControl.Client.deps.json
-rw-r--r-- 1 tonik tonik    42496 dub 17 23:09 ParentalControl.Client.dll
-rw-r--r-- 1 tonik tonik    21992 dub 17 23:09 ParentalControl.Client.pdb
-rw-r--r-- 1 tonik tonik      426 dub 17 23:09 ParentalControl.Client.runtimeconfig.json
-rw-r--r-- 1 tonik tonik    40448 dub 17 23:09 ParentalControl.Shared.dll
-rw-r--r-- 1 tonik tonik    17244 dub 17 23:09 ParentalControl.Shared.pdb
-rwxr--r-- 1 tonik tonik   287984 dub 17 23:09 Polly.dll
-rwxr--r-- 1 tonik tonik     6144 dub 17 23:09 Polly.Extensions.Http.dll
-rwxr--r-- 1 tonik tonik   153088 dub 17 23:09 Serilog.dll
-rwxr--r-- 1 tonik tonik    29696 dub 17 23:09 Serilog.Extensions.Hosting.dll
-rwxr--r-- 1 tonik tonik    29696 dub 17 23:09 Serilog.Extensions.Logging.dll
-rwxr--r-- 1 tonik tonik    30720 dub 17 23:09 Serilog.Sinks.File.dll
-rwxr--r-- 1 tonik tonik     5120 dub 17 23:09 SQLitePCLRaw.batteries_v2.dll
-rwxr--r-- 1 tonik tonik    50688 dub 17 23:09 SQLitePCLRaw.core.dll
-rwxr--r-- 1 tonik tonik    35840 dub 17 23:09 SQLitePCLRaw.provider.e_sqlite3.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.AppContext.dll
-rwxr--r-- 1 tonik tonik    15664 dub 17 23:09 System.Buffers.dll
-rwxr--r-- 1 tonik tonik   255792 dub 17 23:09 System.Collections.Concurrent.dll
-rwxr--r-- 1 tonik tonik   252680 dub 17 23:09 System.Collections.dll
-rwxr--r-- 1 tonik tonik   755984 dub 17 23:09 System.Collections.Immutable.dll
-rwxr--r-- 1 tonik tonik    93968 dub 17 23:09 System.Collections.NonGeneric.dll
-rwxr--r-- 1 tonik tonik    93968 dub 17 23:09 System.Collections.Specialized.dll
-rwxr--r-- 1 tonik tonik   192776 dub 17 23:09 System.ComponentModel.Annotations.dll
-rwxr--r-- 1 tonik tonik    17200 dub 17 23:09 System.ComponentModel.DataAnnotations.dll
-rwxr--r-- 1 tonik tonik    17672 dub 17 23:09 System.ComponentModel.dll
-rwxr--r-- 1 tonik tonik    36624 dub 17 23:09 System.ComponentModel.EventBasedAsync.dll
-rwxr--r-- 1 tonik tonik    71440 dub 17 23:09 System.ComponentModel.Primitives.dll
-rwxr--r-- 1 tonik tonik   748304 dub 17 23:09 System.ComponentModel.TypeConverter.dll
-rwxr--r-- 1 tonik tonik    19728 dub 17 23:09 System.Configuration.dll
-rwxr--r-- 1 tonik tonik   200456 dub 17 23:09 System.Console.dll
-rwxr--r-- 1 tonik tonik    23816 dub 17 23:09 System.Core.dll
-rwxr--r-- 1 tonik tonik  2899208 dub 17 23:09 System.Data.Common.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Data.DataSetExtensions.dll
-rwxr--r-- 1 tonik tonik    25400 dub 17 23:09 System.Data.dll
-rwxr--r-- 1 tonik tonik    16656 dub 17 23:09 System.Diagnostics.Contracts.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Diagnostics.Debug.dll
-rwxr--r-- 1 tonik tonik   192304 dub 17 23:09 System.Diagnostics.DiagnosticSource.dll
-rwxr--r-- 1 tonik tonik    52496 dub 17 23:09 System.Diagnostics.EventLog.dll
-rwxr--r-- 1 tonik tonik    43280 dub 17 23:09 System.Diagnostics.FileVersionInfo.dll
-rwxr--r-- 1 tonik tonik   273168 dub 17 23:09 System.Diagnostics.Process.dll
-rwxr--r-- 1 tonik tonik    30984 dub 17 23:09 System.Diagnostics.StackTrace.dll
-rwxr--r-- 1 tonik tonik    60176 dub 17 23:09 System.Diagnostics.TextWriterTraceListener.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.Diagnostics.Tools.dll
-rwxr--r-- 1 tonik tonik   134920 dub 17 23:09 System.Diagnostics.TraceSource.dll
-rwxr--r-- 1 tonik tonik    16688 dub 17 23:09 System.Diagnostics.Tracing.dll
-rwxr--r-- 1 tonik tonik    50480 dub 17 23:09 System.dll
-rwxr--r-- 1 tonik tonik    20744 dub 17 23:09 System.Drawing.dll
-rwxr--r-- 1 tonik tonik   125200 dub 17 23:09 System.Drawing.Primitives.dll
-rwxr--r-- 1 tonik tonik    16656 dub 17 23:09 System.Dynamic.Runtime.dll
-rwxr--r-- 1 tonik tonik   230664 dub 17 23:09 System.Formats.Asn1.dll
-rwxr--r-- 1 tonik tonik   277808 dub 17 23:09 System.Formats.Tar.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Globalization.Calendars.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Globalization.dll
-rwxr--r-- 1 tonik tonik    15664 dub 17 23:09 System.Globalization.Extensions.dll
-rwxr--r-- 1 tonik tonik    72496 dub 17 23:09 System.IO.Compression.Brotli.dll
-rwxr--r-- 1 tonik tonik   259848 dub 17 23:09 System.IO.Compression.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.IO.Compression.FileSystem.dll
-rwxr--r-- 1 tonik tonik    55608 dub 17 23:09 System.IO.Compression.ZipFile.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.IO.dll
-rwxr--r-- 1 tonik tonik    32056 dub 17 23:09 System.IO.FileSystem.AccessControl.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.IO.FileSystem.dll
-rwxr--r-- 1 tonik tonik    78600 dub 17 23:09 System.IO.FileSystem.DriveInfo.dll
-rwxr--r-- 1 tonik tonik    15672 dub 17 23:09 System.IO.FileSystem.Primitives.dll
-rwxr--r-- 1 tonik tonik   102192 dub 17 23:09 System.IO.FileSystem.Watcher.dll
-rwxr--r-- 1 tonik tonik    77072 dub 17 23:09 System.IO.IsolatedStorage.dll
-rwxr--r-- 1 tonik tonik    80136 dub 17 23:09 System.IO.MemoryMappedFiles.dll
-rwxr--r-- 1 tonik tonik    77624 dub 17 23:09 System.IO.Pipelines.dll
-rwxr--r-- 1 tonik tonik    23816 dub 17 23:09 System.IO.Pipes.AccessControl.dll
-rwxr--r-- 1 tonik tonik   127240 dub 17 23:09 System.IO.Pipes.dll
-rwxr--r-- 1 tonik tonik    15664 dub 17 23:09 System.IO.UnmanagedMemoryStream.dll
-rwxr--r-- 1 tonik tonik   530184 dub 17 23:09 System.Linq.dll
-rwxr--r-- 1 tonik tonik  3766536 dub 17 23:09 System.Linq.Expressions.dll
-rwxr--r-- 1 tonik tonik   798472 dub 17 23:09 System.Linq.Parallel.dll
-rwxr--r-- 1 tonik tonik   168248 dub 17 23:09 System.Linq.Queryable.dll
-rwxr--r-- 1 tonik tonik   147768 dub 17 23:09 System.Memory.dll
-rwxr--r-- 1 tonik tonik    17712 dub 17 23:09 System.Net.dll
-rwxr--r-- 1 tonik tonik  1710352 dub 17 23:09 System.Net.Http.dll
-rwxr--r-- 1 tonik tonik   120080 dub 17 23:09 System.Net.Http.Json.dll
-rwxr--r-- 1 tonik tonik   296712 dub 17 23:09 System.Net.HttpListener.dll
-rwxr--r-- 1 tonik tonik   424208 dub 17 23:09 System.Net.Mail.dll
-rwxr--r-- 1 tonik tonik    84744 dub 17 23:09 System.Net.NameResolution.dll
-rwxr--r-- 1 tonik tonik   170800 dub 17 23:09 System.Net.NetworkInformation.dll
-rwxr--r-- 1 tonik tonik   101136 dub 17 23:09 System.Net.Ping.dll
-rwxr--r-- 1 tonik tonik   229640 dub 17 23:09 System.Net.Primitives.dll
-rwxr--r-- 1 tonik tonik   281360 dub 17 23:09 System.Net.Quic.dll
-rwxr--r-- 1 tonik tonik   341256 dub 17 23:09 System.Net.Requests.dll
-rwxr--r-- 1 tonik tonik   804112 dub 17 23:09 System.Net.Security.dll
-rwxr--r-- 1 tonik tonik    35592 dub 17 23:09 System.Net.ServicePoint.dll
-rwxr--r-- 1 tonik tonik   593672 dub 17 23:09 System.Net.Sockets.dll
-rwxr--r-- 1 tonik tonik   163640 dub 17 23:09 System.Net.WebClient.dll
-rwxr--r-- 1 tonik tonik    57608 dub 17 23:09 System.Net.WebHeaderCollection.dll
-rwxr--r-- 1 tonik tonik    33584 dub 17 23:09 System.Net.WebProxy.dll
-rwxr--r-- 1 tonik tonik    90384 dub 17 23:09 System.Net.WebSockets.Client.dll
-rwxr--r-- 1 tonik tonik   180496 dub 17 23:09 System.Net.WebSockets.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.Numerics.dll
-rwxr--r-- 1 tonik tonik    16184 dub 17 23:09 System.Numerics.Vectors.dll
-rwxr--r-- 1 tonik tonik    68880 dub 17 23:09 System.ObjectModel.dll
-rwxr--r-- 1 tonik tonik 12784392 dub 17 23:09 System.Private.CoreLib.dll
-rwxr--r-- 1 tonik tonik  2098448 dub 17 23:09 System.Private.DataContractSerialization.dll
-rwxr--r-- 1 tonik tonik   249136 dub 17 23:09 System.Private.Uri.dll
-rwxr--r-- 1 tonik tonik  8133896 dub 17 23:09 System.Private.Xml.dll
-rwxr--r-- 1 tonik tonik   400136 dub 17 23:09 System.Private.Xml.Linq.dll
-rwxr--r-- 1 tonik tonik    65808 dub 17 23:09 System.Reflection.DispatchProxy.dll
-rwxr--r-- 1 tonik tonik    16656 dub 17 23:09 System.Reflection.dll
-rwxr--r-- 1 tonik tonik   119088 dub 17 23:09 System.Reflection.Emit.dll
-rwxr--r-- 1 tonik tonik    16184 dub 17 23:09 System.Reflection.Emit.ILGeneration.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Reflection.Emit.Lightweight.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Reflection.Extensions.dll
-rwxr--r-- 1 tonik tonik  1098000 dub 17 23:09 System.Reflection.Metadata.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Reflection.Primitives.dll
-rwxr--r-- 1 tonik tonik    32016 dub 17 23:09 System.Reflection.TypeExtensions.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.Resources.Reader.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Resources.ResourceManager.dll
-rwxr--r-- 1 tonik tonik    43272 dub 17 23:09 System.Resources.Writer.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Runtime.CompilerServices.Unsafe.dll
-rwxr--r-- 1 tonik tonik    19768 dub 17 23:09 System.Runtime.CompilerServices.VisualC.dll
-rwxr--r-- 1 tonik tonik    43824 dub 17 23:09 System.Runtime.dll
-rwxr--r-- 1 tonik tonik    18184 dub 17 23:09 System.Runtime.Extensions.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Runtime.Handles.dll
-rwxr--r-- 1 tonik tonik    86328 dub 17 23:09 System.Runtime.InteropServices.dll
-rwxr--r-- 1 tonik tonik    39176 dub 17 23:09 System.Runtime.InteropServices.JavaScript.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Runtime.InteropServices.RuntimeInformation.dll
-rwxr--r-- 1 tonik tonik    17160 dub 17 23:09 System.Runtime.Intrinsics.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Runtime.Loader.dll
-rwxr--r-- 1 tonik tonik   306960 dub 17 23:09 System.Runtime.Numerics.dll
-rwxr--r-- 1 tonik tonik    17200 dub 17 23:09 System.Runtime.Serialization.dll
-rwxr--r-- 1 tonik tonik   305936 dub 17 23:09 System.Runtime.Serialization.Formatters.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Runtime.Serialization.Json.dll
-rwxr--r-- 1 tonik tonik    28976 dub 17 23:09 System.Runtime.Serialization.Primitives.dll
-rwxr--r-- 1 tonik tonik    17160 dub 17 23:09 System.Runtime.Serialization.Xml.dll
-rwxr--r-- 1 tonik tonik    58640 dub 17 23:09 System.Security.AccessControl.dll
-rwxr--r-- 1 tonik tonik    90416 dub 17 23:09 System.Security.Claims.dll
-rwxr--r-- 1 tonik tonik    17712 dub 17 23:09 System.Security.Cryptography.Algorithms.dll
-rwxr--r-- 1 tonik tonik    16648 dub 17 23:09 System.Security.Cryptography.Cng.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Security.Cryptography.Csp.dll
-rwxr--r-- 1 tonik tonik  2193680 dub 17 23:09 System.Security.Cryptography.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Security.Cryptography.Encoding.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Security.Cryptography.OpenSsl.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Security.Cryptography.Primitives.dll
-rwxr--r-- 1 tonik tonik    17208 dub 17 23:09 System.Security.Cryptography.X509Certificates.dll
-rwxr--r-- 1 tonik tonik    18696 dub 17 23:09 System.Security.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.Security.Principal.dll
-rwxr--r-- 1 tonik tonik    37640 dub 17 23:09 System.Security.Principal.Windows.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Security.SecureString.dll
-rwxr--r-- 1 tonik tonik    17160 dub 17 23:09 System.ServiceModel.Web.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.ServiceProcess.dll
-rwxr--r-- 1 tonik tonik   852792 dub 17 23:09 System.Text.Encoding.CodePages.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Text.Encoding.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Text.Encoding.Extensions.dll
-rwxr--r-- 1 tonik tonik    66312 dub 17 23:09 System.Text.Encodings.Web.dll
-rwxr--r-- 1 tonik tonik   677136 dub 17 23:09 System.Text.Json.dll
-rwxr--r-- 1 tonik tonik  1022776 dub 17 23:09 System.Text.RegularExpressions.dll
-rwxr--r-- 1 tonik tonik   122128 dub 17 23:09 System.Threading.Channels.dll
-rwxr--r-- 1 tonik tonik    74040 dub 17 23:09 System.Threading.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Threading.Overlapped.dll
-rwxr--r-- 1 tonik tonik   489744 dub 17 23:09 System.Threading.Tasks.Dataflow.dll
-rwxr--r-- 1 tonik tonik    17168 dub 17 23:09 System.Threading.Tasks.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Threading.Tasks.Extensions.dll
-rwxr--r-- 1 tonik tonik   124728 dub 17 23:09 System.Threading.Tasks.Parallel.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Threading.Thread.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Threading.ThreadPool.dll
-rwxr--r-- 1 tonik tonik    15664 dub 17 23:09 System.Threading.Timer.dll
-rwxr--r-- 1 tonik tonik    16656 dub 17 23:09 System.Transactions.dll
-rwxr--r-- 1 tonik tonik   365320 dub 17 23:09 System.Transactions.Local.dll
-rwxr--r-- 1 tonik tonik    15632 dub 17 23:09 System.ValueTuple.dll
-rwxr--r-- 1 tonik tonik    15624 dub 17 23:09 System.Web.dll
-rwxr--r-- 1 tonik tonik    49968 dub 17 23:09 System.Web.HttpUtility.dll
-rwxr--r-- 1 tonik tonik    16136 dub 17 23:09 System.Windows.dll
-rwxr--r-- 1 tonik tonik    23864 dub 17 23:09 System.Xml.dll
-rwxr--r-- 1 tonik tonik    16184 dub 17 23:09 System.Xml.Linq.dll
-rwxr--r-- 1 tonik tonik    22288 dub 17 23:09 System.Xml.ReaderWriter.dll
-rwxr--r-- 1 tonik tonik    16656 dub 17 23:09 System.Xml.Serialization.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Xml.XDocument.dll
-rwxr--r-- 1 tonik tonik    16176 dub 17 23:09 System.Xml.XmlDocument.dll
-rwxr--r-- 1 tonik tonik    18184 dub 17 23:09 System.Xml.XmlSerializer.dll
-rwxr--r-- 1 tonik tonik    16144 dub 17 23:09 System.Xml.XPath.dll
-rwxr--r-- 1 tonik tonik    17672 dub 17 23:09 System.Xml.XPath.XDocument.dll
-rwxr--r-- 1 tonik tonik    16648 dub 17 23:09 WindowsBase.dll
filip@filip-ThinkPad-T540p:~$ sudo /opt/parental-control/ParentalControl.Client
Unhandled exception. System.InvalidOperationException: ServerUrl is not configured
   at Program.<>c.<<Main>$>b__0_0(HostBuilderContext context, IServiceCollection services) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Program.cs:line 26
   at Microsoft.Extensions.Hosting.HostBuilder.InitializeServiceProvider()
   at Microsoft.Extensions.Hosting.HostBuilder.Build()
   at Program.<Main>$(String[] args) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Program.cs:line 18
   at Program.<Main>$(String[] args) in /home/runner/work/timekeeper-net/timekeeper-net/src/ParentalControl.Client/Program.cs:line 49
   at Program.<Main>(String[] args)
Aborted
filip@filip-ThinkPad-T540p:~$ cat /opt/parental-control/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ParentalControl": {
    "ServerUrl": "http://10.0.0.55:8081",
    "TickIntervalSeconds": 60
  }
}
filip@filip-ThinkPad-T540p:~$ sudo ls -la /var/log/parental-control/
total 8
drwxr-xr-x 1 root root     36 dub 17 22:57 .
drwxrwxr-x 1 root syslog 2866 dub 17 22:57 ..
-rw-r--r-- 1 root root   5008 dub 17 23:16 client20260417.log
