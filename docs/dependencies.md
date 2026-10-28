# Dependency decisions

Verified against official sources on 2026-10-28 before implementation:

- .NET 10 LTS / WPF, `net10.0-windows`. Microsoft lists support through November 2028: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support . .NET 8 and 9 approach end of support in November 2026, so they were not selected. The local validation SDK is 10.0.403 / runtime 10.0.14. `global.json` accepts newer .NET 10 feature bands.
- Microsoft.Data.Sqlite 10.0.14, targeting .NET Standard 2.0 and compatible with .NET 10: https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.14 . It brings SQLite native binaries through SQLitePCLRaw. Parameterized commands and WAL support durable local state. SQLite usage guidance: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/ .
- Microsoft.Extensions.Hosting 10.0.0 provides explicit DI and structured Microsoft logging: https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.0 . Logging is debug-only and has no network sink.
- Microsoft.NET.Test.Sdk 17.14.1, xUnit 2.9.3 and Visual Studio runner 3.1.1 provide Windows integration tests. https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/17.14.1 , https://www.nuget.org/packages/xunit/2.9.3 , https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.1 .

WPF framework and accessibility reference: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/ . Windows handle information and rename contracts: https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle and https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info . File sharing: https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew .

No third-party theme, reflection-based registration, telemetry or content service is used. The Windows-only infrastructure intentionally uses native file handles; the Core project contains no WPF references.
