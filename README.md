# PhieuFlow

A three-service form platform — a form-builder UI, a form-filler UI, and a hub that owns
the database — used to demonstrate synchronous CRUD alongside a single deliberate
asynchronous boundary (form submission over RabbitMQ). See [`docs/adr/`](docs/adr/) for
the architectural decisions.

## Prerequisites

- **.NET SDK 10.0** or later — every project targets `net10.0` and the AppHost builds
  with `Aspire.AppHost.Sdk` 13.5.2. Aspire is restored as ordinary NuGet packages, so
  the standalone `aspire` CLI is *not* required (`AspireUseCliBundle` is off in
  [`PhieuFlow.AppHost.csproj`](src/PhieuFlow.AppHost/PhieuFlow.AppHost.csproj)).
- **Docker Engine — running, and with BuildKit / buildx.** Aspire 13.5 builds a
  container-network-tunnel proxy image with `docker build --progress=…`, which the
  legacy builder rejects (`unknown flag: --progress`). Docker Desktop and Docker CE
  (`docker-buildx-plugin`) already bundle it; Ubuntu's `docker.io` package does not —
  install it separately:

  ```
  sudo apt-get install docker-buildx      # Ubuntu/Debian universe
  docker buildx version                    # verify — prints v0.x
  ```

  Your user must be able to reach the daemon (member of the `docker` group, or rootless
  Docker). SQL Server and Keycloak run as containers, so leave ~4 GB RAM for them.
- **ASP.NET Core HTTPS dev certificate** — trust it once with
  `dotnet dev-certs https --trust`, or start the AppHost with `dotnet watch --trust`.
- **Node.js 20+ and npm** — the form-builder's CSS is built with Tailwind CSS v4. Run
  `npm install` once. `dotnet build` automatically runs the Tailwind build before
  building the form-builder; `npm run watch:css` is available for live CSS updates
  while working on the UI. Output lands in
  `src/PhieuFlow.FormBuilder/wwwroot/css/tailwind.generated.css`.
- **For the end-to-end tests only** — a headless **Chromium** for Playwright. The test
  fixture installs it on first run (and pulls the Keycloak image). A by-hand install
  needs **PowerShell** (`pwsh`):
  `pwsh tests/PhieuFlow.Tests.E2E/bin/Debug/net10.0/playwright.ps1 install chromium`.

## Running locally

```
dotnet run --project src/PhieuFlow.AppHost
```

Aspire orchestrates SQL Server, Keycloak, the migration/seed services, the hub, and the
form-builder as local containers (ADR 0004). Docker — with buildx — must be running; see
[Prerequisites](#prerequisites).

## Identity provider

Service-to-service calls to the hub are authenticated with an OAuth2 client-credentials
flow against Keycloak (ADR 0005). The AppHost runs Keycloak as a container and imports
the realm from
[`src/PhieuFlow.AppHost/realms/phieuflow-realm.json`](src/PhieuFlow.AppHost/realms/phieuflow-realm.json)
on startup — a fresh clone stands up an identical local IdP with no manual admin-UI
steps. The realm defines:

- realm `phieuflow`;
- client `form-builder` (confidential, service accounts / client-credentials grant), dev
  secret `form-builder-dev-secret`;
- scopes `forms:read` (default), `forms:write` / `submissions:write` (optional), and an
  audience mapper adding `phieuflow-hub` to the token.

Aspire assigns Keycloak's URL at runtime and injects it into the hub and form-builder as
`Keycloak__Authority` (issuer, OIDC metadata and the URL every caller fetches tokens from
are all the same string). The hub validates tokens with standard ASP.NET Core JWT bearer
middleware (`Keycloak:Authority` / `Keycloak:Audience`) and authorises per scope claim.
Take Keycloak's URL from the Aspire dashboard and get a token by hand:

```
curl -sk -X POST <keycloak-url>/realms/phieuflow/protocol/openid-connect/token \
  -d grant_type=client_credentials \
  -d client_id=form-builder -d client_secret=form-builder-dev-secret \
  -d 'scope=forms:read forms:write'
```

The committed secret, `sslRequired=none`, `RequireHttpsMetadata=false` and the
accept-any-certificate backchannel toggle are local-dev only; a real deployment sets a
fixed `Keycloak:Authority` with a trusted certificate and its own secret.

## Testing

Fast integration tests for the hub's authorization live in
[`tests/PhieuFlow.Tests.Integration`](tests/PhieuFlow.Tests.Integration) — they host the
hub in-process with an offline-validated JWT and an in-memory SQLite database, so they
need no Docker (`dotnet test tests/PhieuFlow.Tests.Integration`).

End-to-end tests live in [`tests/PhieuFlow.Tests.E2E`](tests/PhieuFlow.Tests.E2E) and
drive a real browser against the full Aspire topology with Playwright (ADR 0006).

Prerequisites:

- **Docker** running — the harness starts SQL Server and Keycloak containers plus the
  app services.
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
staleness handling, ADR 0002) are written in full but marked `[Fact(Skip = "…")]` with
`[Trait("Category", "Future")]`. Un-skip a spec when its feature lands. See the
[test project README](tests/PhieuFlow.Tests.E2E/README.md).

Test naming follows the convention in [`CLAUDE.md`](CLAUDE.md#testing-conventions):
`Test<Operation>_When_<condition>_Should_<outcome>`.
