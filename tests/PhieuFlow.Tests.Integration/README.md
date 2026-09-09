# PhieuFlow.Tests.Integration

Two tiers in one project. `dotnet test tests/PhieuFlow.Tests.Integration` runs
both.

## `integration-sql` — endpoints and persistence against real SQL Server

These tests drive the form-management endpoints over HTTP. Examples:
`FormCreateTests`, `FormSaveTests`, `FormPublishGateTests`. The Hub runs in
process. A real SQL Server database backs it. `MigrationService` builds the
schema with the production migration chain. A test auth scheme (`TestAuthHandler`)
always authorizes. This tier tests the endpoints and what they persist, not
token validation.

| Path | Purpose |
| --- | --- |
| `../PhieuFlow.Tests.IntegrationAppHost` | A minimal Aspire topology: one SQL Server container and the `MigrationService` worker. Nothing else. |
| `Infrastructure/SqlServerFixture.cs` | Starts that topology one time for the collection with `Aspire.Hosting.Testing`. Waits for `migrations` to finish. Exposes the connection string and the in-process Hub. Turns on `TestServer.PreserveExecutionContext` so the per-test `TransactionScope` flows into the pipeline. |
| `Infrastructure/IntegrationWebApplicationFactory.cs` | Hosts the Hub in process. Pins `HubDbContext` to one shared `SqlConnection` so a per-test `TransactionScope` does not need MSDTC. Swaps in `TestAuthHandler`. |
| `Infrastructure/IntegrationCollection.cs` | `[Collection("integration-sql")]`. One serial collection shares the fixture. |
| `Infrastructure/IntegrationTestBase.cs` | Wraps each test in a `TransactionScope` that disposes without `Complete()`. Every write rolls back. The next test starts from the migrated but empty schema. |
| `Infrastructure/TestForms.cs` | Shared `FormDto` builders. The all-question-types form drives every mapper, reconcile, and clone branch. |
| `Infrastructure/SubmissionSeed.cs` | Writes a `FormSubmission` row directly through `HubDbContext`. A fast shortcut for a test that only needs a form to have a submission. |
| `MigrationModelTests.cs` | Checks that the migrations define the schema, not `EnsureCreated`, and that they still match the model. |
| `TransactionIsolationTests.cs` | Checks that the rollback happens. |

Coverage-gap classes for the provider-dependent logic that unit tests cannot
reach:

| Class | Covers |
| --- | --- |
| `FormListTests` | `GET /forms` and `GetBatchAsync`: the Guid cursor (SQL Server `uniqueidentifier` order), the `take` range, the `PageCount` and `QuestionCount` aggregates, the latest-published pointer, and the `HasSubmissions` flag. |
| `FormQuestionTypesTests` | Every `QuestionDto` subtype persisted and read back: `QuestionMapper` both ways, the TPH discriminator, `decimal(18,4)`, and `DateOnly`. `Checkbox` and `CheckBoxGroup` are otherwise never persisted. |
| `FormReconcileTests` | `ReconcilePages`, `ReconcileQuestions`, `ReconcileOptions`, and `UpdateQuestionFields` under the real EF change tracker. Also the question-type-change guard. |
| `FormDeleteTests` | The `FormSubmission` `Restrict` foreign key. A delete that would destroy submission data returns 409. |
| `SubmissionConsumeTests` | `SubmissionMessageHandler` against the real database. This is the persistence half of the RabbitMQ consumer, with no broker. Covers typed-answer mapping, inbox dedup, and the poison classifications. |
| `SubmissionListTests` | `GET /forms/{id}/submissions`: a keyset-paged batch of persisted submissions. Answers are display strings. Option ids resolve to labels. |

One test asserts an **HTTP 500** on purpose
(`FormReconcileTests.TestSaveAsync_When_AQuestionChangesType_Should_Return500`).
A question that changes type makes `ReconcileQuestions` throw, and the Hub
returns a bare 500. It should return a 400 or 409. The `// TODO` on the test
says so.

The ack and nack transport wiring for the consumer has no automated test. A
manual check covers it. The full async submission flow is in the E2E suite.

**Needs Docker** for the SQL Server container. The first run pulls
`mcr.microsoft.com/mssql/server`.

## `integration-auth` — the auth pipeline

`HubAuthorizationTests` runs the Hub in process with the real JWT bearer
middleware rebound for offline validation (`HubAuthWebApplicationFactory` and
`TestJwt`) and in-memory SQLite. It asserts 401, 403, and 200 on the auth
boundary. It covers negatives that a real identity provider cannot mint cheaply:
an expired token, a garbage token, an unknown signing key, a wrong audience, and
a bad scope claim. **No Docker.**

```
dotnet test tests/PhieuFlow.Tests.Integration --filter "FullyQualifiedName~HubAuthorizationTests"
```

## Conventions

- Assertions use **AwesomeAssertions** (`actual.Should()....`). Do not use xUnit
  `Assert.*`.
- Test names use the pattern `Test<Operation>_When_<condition>_Should_<outcome>`.
  Underscores fence the keywords. See `CLAUDE.md`.
