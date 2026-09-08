# ADR 0011: Client-side pre-publish validation in the FormBuilder

Date: 2026-09-08

## Status

Accepted

## Context

`FormPublishValidator` — the rules a form must pass to be published (title present, every
question has text, choice questions have distinct non-empty options, min ≤ max, and so on) —
lived in `PhieuFlow.Hub` and ran only inside `POST /forms/{id}/publish`. ADR 0002 and the
validator's own class remark made this deliberate: *"the builder does not re-implement these
rules client-side; it renders whatever issues the Hub hangs on the returned tree."*

The cost is that every Publish attempt on an invalid form is a full HTTP round-trip (flush the
autosave, POST, get a 422 with the annotated tree) before the pre-publish dialog can even open.
Meanwhile the submission side just gained the opposite, symmetric arrangement (ADR 0010): one
`SubmissionAnswersValidator` in `PhieuFlow.Hub.Contracts`, run by the FormFiller before it
publishes and by the Hub consumer as the authority.

## Decision

- **Move `FormPublishValidator` (with its `IFormPublishValidator` interface) into
  `PhieuFlow.Hub.Contracts.Validation`.** Its only dependencies are the `FormDto` tree and
  `ValidationField` / `ValidationIssueDto`, all already in that project, which stays
  dependency-free. The Hub keeps registering `IFormPublishValidator` and injecting it into the
  publish endpoint exactly as before.

- **The FormBuilder runs it locally when Publish is pressed**, in
  `FormEditorSession.PublishAsync`, after the "no title" check and before the autosave flush.
  It maps the live edit tree to `FormDto`, calls `Validate`, and on any issue annotates the
  live tree (`FormEditMapper.ApplyIssues`), builds the dialog rows (`PrePublishRow.From`), and
  returns `PublishOutcomeKind.NeedsFixes` with a synthesised `PublishResultDto` — **no
  `POST /forms/{id}/publish`, no flush**. The pre-publish dialog and inline highlights render
  identically to the 422 path.

- **The Hub still validates on the actual publish.** When the local gate is clean, publish
  proceeds through the unchanged flush + `POST` + `NeedsFixes`/`Published` handling. A race
  (another session's save landing between the local check and the POST) or any client/Hub
  drift is caught server-side and flows through the same dialog. The client gate is a
  fast path, not a replacement.

- **Trigger is the Publish click only.** No continuous re-validation as the form is edited;
  the existing per-node "clear this node's issues on edit" (`IHasIssues.Edit`) and the
  inline min/max hints (`MinMaxValidation`) are unchanged.

## Consequences

- An invalid form never leaves the browser on Publish; the dialog opens instantly.
- The publish rules can no longer drift between client and server — one implementation, like
  `SubmissionAnswersValidator`. The cost is a class with light logic living in the otherwise
  DTO-only `PhieuFlow.Hub.Contracts` (already true since ADR 0010).
- The E2E publish-gate flow is behaviourally the same (dialog, jump links, fix-and-retry);
  only the hidden `POST` on the invalid path disappears. No E2E assertion depended on it.
- `FormPublishGateTests` (integration) is untouched — the Hub is still the authority over
  HTTP.
- `FormEditorSessionTests`' publishable-form fixture had to gain a real question, since the
  local gate now flags the previous "one empty page" tree before the fake Hub is reached.
