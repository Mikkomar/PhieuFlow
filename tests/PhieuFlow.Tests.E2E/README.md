# PhieuFlow.Tests.E2E

End-to-end tests. Playwright for .NET drives a real browser against the full
application topology. `Aspire.Hosting.Testing` starts the topology (ADR 0006).

## Layout

| Path | Purpose |
| --- | --- |
| `Infrastructure/AppHostFixture.cs` | Starts the full AppHost graph one time for the assembly: SQL Server, RabbitMQ, Keycloak, the migration and seed services, the Hub, the form-builder, and the form-filler. Starts one headless Chromium. Also requests client-credentials tokens for the hub-assertion helpers, for both the form-builder client and the form-filler client. |
| `Infrastructure/E2ECollection.cs` | The xUnit collection that binds the fixture (`[Collection("e2e")]`). |
| `Infrastructure/E2ETestBase.cs` | A browser context and page for each test. One Playwright trace for each test under `bin/**/traces/`. Shared hub-assertion helpers. |
| `Infrastructure/FormBuilderPage.cs` | Page object over `FormBuilder.razor`. Uses role, label, and placeholder selectors. |
| `Infrastructure/PlaywrightInstaller.cs` | Installs the Chromium build in process on the first run. |
| `Builder/` | Builds a form through the UI. Asserts the result over the hub REST API. |
| `Versioning/` | ADR 0007. Publishing locks a version. The next edit forks a new draft. |
| `Submission/` | ADR 0001, 0002, 0010. Build, fill, submit, then assert the persisted submission. Also covers client-side answer validation and version staleness. |
| `Auth/` | ADR 0005. The Hub rejects an unauthenticated caller and a wrong-scope caller. Uses a real Keycloak client-credentials token. |

## Running

Docker must run first. The fixture starts SQL Server, RabbitMQ, and Keycloak
containers, and the application services. The first run installs Chromium and
pulls the Keycloak image.

```
dotnet test
```

The suite has no skipped test today.

A Playwright trace is written to `bin/<config>/net10.0/traces/<test>.zip` for
each test. Open it with `playwright show-trace <file>`.

## Future-feature tests

`CLAUDE.md` defines a convention for a test that depends on a feature that does
not exist yet. The test carries `[Fact(Skip = "<blocker> — ADR NNNN")]` and
`[Trait("Category", "Future")]`. CI runs `dotnet test --filter "Category!=Future"`.
No test uses this convention today.

When you write the real test for a feature that ships:

1. Add the new resource to `AppHost.cs` if it is not there yet.
2. Make the fixture wait for the new resource to report healthy.
3. Replace any placeholder in the test body with the real endpoint or flow.

## Conventions

- Assertions use **AwesomeAssertions** (`actual.Should()....`). Do not use xUnit
  `Assert.*`. Playwright web-first assertions (`Assertions.Expect(locator)`) stay
  as they are.
- Test names use the pattern `Test<Operation>_When_<condition>_Should_<outcome>`.
  Underscores fence the keywords `_When_`, `_With_`, `_Without_`, and `_Should_`.

See `CLAUDE.md`.
