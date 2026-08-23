# ADR 0004: Aspire for local orchestration, Kubernetes as deployment target

## Status
Accepted

## Context
The system needs a way to run all services (hub, form-builder, form-filler,
RabbitMQ, SQL) together locally, and a path to a container-orchestrated
deployment target for learning purposes. Aspire and Kubernetes solve
different problems and shouldn't be treated as interchangeable:

- Aspire's local orchestrator (`aspire run`) runs services as Docker
  containers on the developer's machine, with automatic service discovery,
  health checks, and OpenTelemetry wired in by default. It is explicitly a
  development-time tool, not a production runtime.
- Kubernetes is a production-grade container orchestration platform.
  Aspire is not a replacement for it and doesn't run a cluster locally.

The `Aspire.Hosting.Kubernetes` integration bridges the two: it lets the
AppHost's existing application model be published as Kubernetes deployment
artifacts (a Helm chart), and optionally deployed directly to whatever
cluster context is active.

An alternative — running a local Kubernetes cluster (kind/minikube) for full
local/prod parity — was considered and set aside for day-to-day development,
since Aspire's local loop iterates faster and doesn't require a cluster to
be running just to debug a service.

## Decision
Two-phase setup, using the same AppHost-defined application model for both:

1. **Local development**: `aspire run` orchestrates hub, form-builder,
   form-filler, RabbitMQ, and SQL as local containers. Integration tests run
   against this model with an ephemeral SQL container. This is the primary
   day-to-day loop.
2. **Deployment target**: each service resource calls
   `PublishAsKubernetesService` in the AppHost. `aspire publish` generates a
   Helm chart from the same topology; `aspire deploy` (or `helm install`
   manually) ships it to a cluster — kind/minikube first to validate the
   chart cheaply, AKS once confirmed.

## Consequences
- One application model (the AppHost) is the source of truth for both local
  topology and the generated Kubernetes artifacts — service references,
  RabbitMQ/SQL dependencies, and configuration don't need to be redefined
  per environment.
- Kubernetes-specific concerns (replica counts, resource limits, additional
  manifests such as ConfigMaps) are attached via `PublishAsKubernetesService`
  callbacks per resource, kept separate from the local-dev definition.
- Local dev never requires a running cluster; validating the Kubernetes path
  is a separate, occasional step rather than part of the everyday loop.
- The Kubernetes hosting integration is a newer, still-evolving part of
  Aspire — API surface (`AddContainerRegistry`, `PublishAsKubernetesService`)
  should be checked against current docs before implementation, not assumed
  stable long-term.
