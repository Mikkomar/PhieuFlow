# PhieuFlow.Tests.E2E

End-to-end tests: Playwright (.NET) driving a real browser against the full application
topology started by `Aspire.Hosting.Testing` (ADR 0006).

## Layout

| Path | Purpose |
| --- | --- |
| `Infrastructure/AppHostFixture.cs` | Starts the AppHost graph (SQL, Keycloak, migrations, seed, hub, form-builder) once for the assembly; launches one headless Chromium. Also mints client-credentials tokens for the hub-assertion helpers. |
| `Infrastructure/E2ECollection.cs` | xUnit collection binding the fixture (`[Collection("e2e")]`). |
| `Infrastructure/E2ETestBase.cs` | Per-test browser context + page, a Playwright trace per test under `bin/**/traces/`, and shared hub-assertion helpers. |
| `Infrastructure/FormBuilderPage.cs` | Page object over `FormBuilder.razor` (role/label/placeholder selectors). |
| `Infrastructure/PlaywrightInstaller.cs` | Installs the Chromium build in-process on first run. |
| `Builder/` | Runnable — building a form through the UI, asserted over the hub REST API. |
| `Versioning/` | ADR 0007 — publish locks a version; the next edit forks a new draft. |
| `Submission/` | ADR 0001/0002/0006 — build → fill → submit → assert persisted. **Skipped.** |
| `Auth/` | ADR 0005 — hub rejects unauthenticated / wrong-scope callers, via a real Keycloak client-credentials token. |

## Running

Requires Docker (SQL Server + Keycloak containers plus the app services). First run
installs Chromium automatically and pulls the Keycloak image.

```
dotnet test                              # all specs; future ones report as skipped
dotnet test --filter "Category!=Future"  # only what passes today (CI default)
```

A Playwright trace is written to `bin/<config>/net10.0/traces/<test>.zip` for every test;
open it with `playwright show-trace <file>`.

## Un-skipping a future spec

Specs for unbuilt features carry `[Fact(Skip = "<blocker> — ADR NNNN")]` and
`[Trait("Category", "Future")]`. When the feature ships:

1. Delete the `Skip` argument (keep `[Fact]`).
2. Remove `[Trait("Category", "Future")]`.
3. Replace any placeholder in the body — `Fixture.FormFillerBaseUrl`, the
   `/forms/{id}/submissions` calls — with the real endpoint/flow.
4. Add the new resource to `AppHost.cs` so the fixture starts it.

## Conventions

- Assertions use **AwesomeAssertions** (`actual.Should()....`), never xUnit `Assert.*`.
  Playwright's `Assertions.Expect(locator)` web-first assertions are kept as-is.
- Test names: `Test<Operation>_When_<condition>_Should_<outcome>`, keywords (`_When_`,
  `_With_`, `_Without_`, `_Should_`) fenced by underscores.

See `CLAUDE.md`.
