# PhieuFlow

A three-service form platform — a form-builder UI, a form-filler UI, and a hub that owns
the database — used to demonstrate synchronous CRUD alongside a single deliberate
asynchronous boundary (form submission over RabbitMQ). See [`docs/adr/`](docs/adr/) for
the architectural decisions.

## Running locally

```
dotnet run --project src/PhieuFlow.AppHost
```

Aspire orchestrates SQL Server, the migration/seed services, the hub, and the
form-builder as local containers (ADR 0004). Docker must be running.

## Testing

End-to-end tests live in [`tests/PhieuFlow.Tests.E2E`](tests/PhieuFlow.Tests.E2E) and
drive a real browser against the full Aspire topology with Playwright (ADR 0006).

Prerequisites:

- **Docker** running — the harness starts a SQL Server container plus the app services.
- **Chromium** for Playwright — the test fixture installs it automatically on first run
  (`playwright install chromium`). To do it by hand:
  `pwsh tests/PhieuFlow.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`.

Commands:

```
# builder + versioning coverage (the specs that pass today)
dotnet test tests/PhieuFlow.Tests.E2E --filter "Category!=Future"

# everything, including the skipped specs for not-yet-built features
dotnet test tests/PhieuFlow.Tests.E2E
```

Specs for features that do not exist yet (form-filler + async submission, ADR 0001/0006;
staleness handling, ADR 0002; service auth, ADR 0005) are written in full but marked
`[Fact(Skip = "…")]` with `[Trait("Category", "Future")]`. Un-skip a spec when its
feature lands. See the [test project README](tests/PhieuFlow.Tests.E2E/README.md).

Test naming follows the convention in [`CLAUDE.md`](CLAUDE.md#testing-conventions):
`Test<Operation>_When_<condition>_Should_<outcome>`.
