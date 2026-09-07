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
| `Infrastructure/SqlServerFixture.cs` | Starts that topology once for the collection via `Aspire.Hosting.Testing`, waits for `migrations` to finish, exposes the connection string + the in-process hub. Turns on `TestServer.PreserveExecutionContext` so the per-test `TransactionScope` flows into the pipeline. |
| `Infrastructure/IntegrationWebApplicationFactory.cs` | Hosts the hub in-process. Re-pins `HubDbContext` to one shared `SqlConnection` (so a per-test `TransactionScope` never needs MSDTC) and swaps in `TestAuthHandler`. |
| `Infrastructure/IntegrationCollection.cs` | `[Collection("integration-sql")]` — one serial collection sharing the fixture. |
| `Infrastructure/IntegrationTestBase.cs` | Wraps each test in a `TransactionScope` disposed without `Complete()`, so every write rolls back and the next test starts from the migrated-but-empty schema. |
| `Infrastructure/TestForms.cs` | Shared `FormDto` builders — the all-question-types form that drives every mapper / reconcile / clone switch arm. |
| `MigrationModelTests.cs` | Guards that migrations (not `EnsureCreated`) define the schema and still match the model. |
| `TransactionIsolationTests.cs` | Guards that the rollback actually happens. |

Coverage-gap classes added for the provider-dependent logic that unit tests can't reach:

| Class | Covers |
| --- | --- |
| `FormListTests` | `GET /forms` / `GetBatchAsync` — the Guid cursor (SQL Server `uniqueidentifier` order), `take` range, `PageCount`/`QuestionCount` aggregates, the latest-published pointer. |
| `FormQuestionTypesTests` | every `QuestionDto` subtype persisted and read back (`QuestionMapper` both ways, TPH discriminator — `Checkbox`/`CheckBoxGroup` are otherwise never persisted — `decimal(18,4)`, `DateOnly`). |
| `FormReconcileTests` | `ReconcilePages`/`ReconcileQuestions`/`ReconcileOptions`/`UpdateQuestionFields` under the real EF change tracker; the question-type-change guard. |
| `FormDeleteTests` (`…HasASubmission…`) | the `FormSubmission` `Restrict` FK — a delete that would destroy response data. |

Two tests deliberately assert an **HTTP 500** (`FormReconcileTests.TestSaveAsync_When_AQuestionChangesType…`,
`FormDeleteTests.TestDelete_When_FormHasASubmission…`) — both are unhandled-exception paths
the Hub should translate to a 4xx; the `// TODO` on each says so.

The async submission boundary (RabbitMQ) is left to the E2E suite.

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
