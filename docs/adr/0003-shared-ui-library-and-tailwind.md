# ADR 0003: Shared Blazor UI library and Tailwind build strategy

## Status
Accepted

## Context
The form-builder and form-filler are both Blazor UI services and should
share components for visual consistency, styled with Tailwind. Two questions
need deciding: how the shared component library is distributed, and how
Tailwind's JIT scanning covers both consuming projects.

Distribution options: a Razor Class Library (RCL) referenced by project, or
an RCL packaged and published as NuGet. With only two internal consumers in
the same repository and no external audience, packaging as NuGet adds
versioning and publishing overhead with no corresponding benefit.

Tailwind generates CSS by scanning source files for class names in use. If
the `content` config only scans the shared library's `.razor` files, classes
used directly in the builder's or filler's own markup won't be generated.
Two ways to handle it:
1. One Tailwind build at the solution root, with `content` globbing across
   the shared library and both app projects, emitting a single CSS file both
   apps reference as a static asset.
2. Each app runs its own Tailwind build, with `content` covering itself plus
   the shared library, producing duplicated config and two CSS outputs.

## Decision
- Shared components live in a Razor Class Library, referenced via
  `ProjectReference` from both apps — not published as a NuGet package.
- Tailwind runs as a single build at the solution root, with `content`
  globbing the shared library and both app projects, emitting one CSS
  output both apps consume.
- Blazor's scoped CSS isolation (`.razor.css`) is used alongside Tailwind
  where structural, component-local styling is needed; the two don't
  conflict.

## Consequences
- The shared library's build config has some awareness of its consumers
  (via the root-level Tailwind content globs), a minor inversion of
  dependency direction. Acceptable given the small, known, fixed set of
  consumers — this is a deliberate simplicity-over-purity tradeoff, not an
  oversight.
- Adding a third consumer later would mean extending the root `content`
  glob, not restructuring the build.
- If the consumer set were external or unknown in advance, option 2
  (per-app builds) or NuGet packaging would be the better default — this
  decision is scoped to the current two-consumer reality.
