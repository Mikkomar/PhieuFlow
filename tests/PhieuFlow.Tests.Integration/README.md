# PhieuFlow.Tests.Integration

Two tiers, one project. Both run under `dotnet test tests/PhieuFlow.Tests.Integration`.

## `integration-sql` — endpoints + persistence against real SQL Server

The form-management endpoints (`FormCreateTests`, `FormSaveTests`, `FormPublishGateTests`,
…) driven over HTTP against a hub hosted in-process, backed by a **real SQL Server
database built by the production migration chain**. Authentication is replaced with a
scheme that always authorizes (`TestAuthHandler`) — this tier tests the endpoints and what
they persist, not token validation.

| Path | Purpose |
| --- | --- |
| `../PhieuFlow.Tests.IntegrationAppHost` | Minimal Aspire topology: a SQL Server container + the `MigrationService` worker, nothing else. |
| `Infrastructure/SqlServerFixture.cs` | Starts that topology once for the collection via `Aspire.Hosting.Testing`, waits for `migrations` to finish, exposes the connection string and the in-process hub. |
| `Infrastructure/IntegrationWebApplicationFactory.cs` | Hosts the hub in-process. Re-pins `HubDbContext` to one shared `SqlConnection` (so a per-test `TransactionScope` never needs MSDTC) and swaps in `TestAuthHandler`. |
| `Infrastructure/IntegrationCollection.cs` | `[Collection("integration-sql")]` — one serial collection sharing the fixture. |
| `Infrastructure/IntegrationTestBase.cs` | Wraps each test in a `TransactionScope` disposed without `Complete()`, so every write rolls back and the next test starts from the migrated-but-empty schema. |
| `MigrationModelTests.cs` | Guards that migrations (not `EnsureCreated`) define the schema and still match the model. |
| `TransactionIsolationTests.cs` | Guards that the rollback actually happens. |

**Needs Docker** (SQL Server container). First run pulls `mcr.microsoft.com/mssql/server`.

## `integration-auth` — the auth pipeline

`HubAuthorizationTests` — the hub in-process with the real JWT bearer rebound offline
(`HubAuthWebApplicationFactory` + `TestJwt`) and in-memory SQLite. Asserts 401/403/200 on
the auth boundary, including negatives a real IdP can't cheaply mint (expired, garbage,
unknown key, wrong audience, `scp`-claim). **No Docker.**

```
dotnet test tests/PhieuFlow.Tests.Integration --filter "FullyQualifiedName~HubAuthorizationTests"
```

## Conventions

- Assertions use **AwesomeAssertions** (`actual.Should()....`), never xUnit `Assert.*`.
- Test names: `Test<Operation>_When_<condition>_Should_<outcome>`, keywords fenced by
  underscores. See `CLAUDE.md`.
