# ADR 0006: End-to-end testing with Playwright via Aspire.Hosting.Testing

## Status
Accepted

## Context
Beyond the integration tests already planned (Aspire-orchestrated ephemeral
SQL container, per ADR-adjacent test setup), the project needs end-to-end
tests that exercise real user flows across all three services — building a
form, filling it out, and submitting it through the async RabbitMQ boundary
(ADR 0001) — driven through an actual browser against the real UI, not
through direct API calls.

Selenium was the initial candidate, based on prior familiarity. Playwright
was considered as an alternative, given first-party .NET bindings and a
documented integration pattern with Aspire specifically.

Comparison points:

- **Test harness fit**: `Aspire.Hosting.Testing`'s `DistributedApplicationTestingBuilder`
  spins up the full AppHost topology (all services, RabbitMQ, SQL,
  Keycloak) in the background and exposes real, running service URLs to the
  test. Playwright has an established pattern for consuming this directly —
  driving the browser against the URLs Aspire hands back once resources
  report healthy. Selenium can be wired into the same harness, but without
  the same first-party documentation and community precedent.
- **Waiting semantics**: the app's interactions (add/reorder/delete
  questions, async submission with a RabbitMQ round-trip before the UI can
  confirm receipt) involve a lot of "wait for this state" scenarios.
  Playwright auto-waits on actionability by default; Selenium generally
  needs explicit `WebDriverWait` calls hand-written per interaction.
- **Debuggability**: Playwright's trace viewer records a step-through
  timeline (DOM, network, console) per test run, useful for diagnosing
  failures in a multi-service async flow where "hub hadn't finished
  validating" and "RabbitMQ hadn't delivered yet" are both plausible
  failure modes that look similar from the outside.
- **Ecosystem fit**: `Microsoft.Playwright` keeps E2E tests in the same
  language and test project structure as the integration tests, rather than
  introducing a separate toolchain.

## Decision
Use **Playwright (.NET bindings)** for end-to-end tests, run against a full
application topology started via `Aspire.Hosting.Testing`'s
`DistributedApplicationTestingBuilder`.

- E2E tests live alongside the integration tests, using the same
  `DistributedApplicationTestingBuilder` fixture pattern, extended to also
  launch Playwright browser contexts against the form-builder and
  form-filler URLs Aspire returns once those resources are healthy.
- The primary E2E scenario is the full submission flow: build a form in one
  browser context, fill and submit it in another, assert the hub persisted
  the submission correctly — proving the async boundary works end to end,
  not just that each service passes its own tests in isolation.
- Selenium was not selected. Prior familiarity with it was real but didn't
  outweigh Playwright's tighter fit with the Aspire testing harness and its
  auto-waiting behavior, which removes a category of flaky-test boilerplate
  this app's async flows would otherwise require.

## Consequences
- Adds a new dependency (`Microsoft.Playwright`) and a one-time browser
  binary install step (`playwright install`) to the dev/CI setup, documented
  in the README.
- E2E tests are slower and heavier than the integration tests (real browser,
  full topology) and are scoped to a small number of high-value flows —
  primarily the submission path — rather than exhaustively covering every UI
  interaction. Component-level and unit-level coverage remain the right tool
  for testing individual builder/filler interactions in isolation.
- Because these tests share the same Aspire test harness as the integration
  tests, CI only needs to stand up the application topology once per test
  run category, keeping the added cost proportional to what's gained.
- The Selenium-vs-Playwright decision is intentionally documented rather
  than left implicit, since prior tool familiarity was a real factor
  weighed against architectural fit — worth being explicit that fit won.
