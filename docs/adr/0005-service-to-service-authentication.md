# ADR 0005: Service-to-service authentication for the Hub REST API

## Status
Accepted

## Context
The form-builder and form-filler services call the hub's REST API
synchronously (see ADR 0001). The hub needs to authenticate these callers —
confirm a request genuinely comes from the form-builder or form-filler, not
an arbitrary client — before processing it.

The system runs locally via Aspire with no Azure integration; Kubernetes/AKS
is a possible future deployment target (see ADR 0004), not a current
dependency. This rules out Azure-specific mechanisms (e.g. Microsoft Entra ID
Workload Identity federation, which depends on AKS's federated identity
credential support) for the current scope, but the chosen approach should
not foreclose adopting one later if the project is deployed to AKS.

Options considered:

1. **API keys** — simplest to implement. No standard expiry, no rotation
   story, no per-call claims (can't express "form-builder can write, form-filler
   can only read submissions" without inventing a scheme). Adequate for a
   throwaway prototype, not for something meant to demonstrate authorization
   design.
2. **mTLS** — strong, transport-level identity. Requires a service mesh
   (e.g. Istio, Linkerd) to manage certificates at any reasonable scale.
   Disproportionate infrastructure for two calling services in a local-dev
   Aspire setup.
3. **Hand-rolled JWT with a shared signing key** — services mint their own
   tokens using a symmetric key shared via Aspire parameter resources; hub
   validates against the same key. Minimal infrastructure, but not a real
   OIDC flow — no token issuance service, no standard claims/scope model to
   demonstrate.
4. **OAuth2 client credentials flow against a local identity provider** —
   each service is a registered OAuth2 client, authenticates with the IdP to
   get a JWT, calls the hub with it as a bearer token. Hub validates via
   standard OIDC metadata (issuer, audience, expiry) and checks role/scope
   claims for authorization.

## Decision
Use **OAuth2 client credentials flow**, with **Keycloak** run as an
additional Aspire-orchestrated resource (`Aspire.Hosting.Keycloak`) as the
local identity provider.

- Form-builder and form-filler are registered as OAuth2 clients in a realm
  configuration checked into the repo (imported on Keycloak startup), not
  configured by hand through the admin UI, so the setup is reproducible.
- Each service authenticates with client credentials to obtain a JWT, then
  presents it as a bearer token on calls to the hub.
- The hub validates tokens using standard ASP.NET Core JWT bearer
  middleware against Keycloak's OIDC metadata (issuer, audience, expiry).
- Authorization is claim-based: role/scope claims on the token (e.g.
  `forms:write`, `forms:read`, `submissions:write`) determine what each
  caller may do, checked by the hub — not inferred from "this is a valid
  token, therefore it's trusted for everything."

The hub's validation logic is written against standard OIDC concepts, not
against Keycloak specifically. If the project is later deployed to AKS, the
issuer can be swapped to Microsoft Entra ID via configuration — changing the
authority/metadata endpoint and re-registering the app clients — without
changing the hub's validation code path.

## Consequences
- Requires running Keycloak locally as part of the Aspire AppHost, adding a
  resource beyond RabbitMQ and SQL. Reasonable overhead for what it
  demonstrates: a real client-credentials OAuth2 flow rather than a
  simplified stand-in.
- The realm/client configuration needs to be exported and version-controlled
  so a fresh clone of the repo can stand up an identical local IdP — this is
  a setup cost worth documenting in the README, not just the ADR.
- This ADR covers service-to-service trust only: the hub knows which
  *service* is calling, not which *end user* is behind that call. End-user
  identity and per-user form ownership, if needed, is a separate, layered
  concern and explicitly out of scope here.
- mTLS and Entra ID Workload Identity were considered stronger long-term
  options but deferred: mTLS due to disproportionate service-mesh overhead
  for two callers, Entra ID Workload Identity because it depends on an AKS
  deployment that doesn't currently exist. Both remain candidates to revisit
  if the project's scope grows.
