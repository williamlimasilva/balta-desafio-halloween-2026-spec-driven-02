# T-01 Project Foundation

## Dependency rationale

- `Microsoft.EntityFrameworkCore.Sqlite` provides the EF Core provider required by the constitution and the persistent storage requirement RF-08. The test project references the same provider to use SQLite in integration tests rather than a different database engine.
- `Microsoft.AspNetCore.Mvc.Testing` supplies `WebApplicationFactory` and the in-process test host used by `ApiFactory`.
- `xunit` is the test framework required by the constitution; `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` enable .NET test discovery and execution.

## Configuration contract

The API's production SQLite connection string is named `DefaultConnection`. Each integration test factory uses a temporary SQLite database file, deleted when the factory is disposed.