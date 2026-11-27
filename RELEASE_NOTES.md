# Sigil v0.8.1

Read-only API access, and a metrics endpoint that survives a restart.

This release closes the gap that a deployment without its own database exporter kept
running into: the `/api` endpoints had no supported way in for a script, and the
metrics that exporter provided were missing from `/metrics`. It also fixes three
defects that made pages crash and events go missing.

**8 commits · 1366 → 1469 tests · no new runtime dependency.**

---

## API tokens

### Read-only tokens for the API endpoints

The API endpoints accepted only the UI cookie, so a script had no supported way in
and a deployment kept its own database exporter beside Sigil.

A user creates a token in **Settings → API Tokens** and receives the secret once.
Only the SHA-256 hash is stored, the lookup compares it in constant time, and a
revoked or expired token is rejected. The token is presented as a bearer credential
and authenticates `GET` and `HEAD` requests under `/api` only, so it can never
authorize a state-changing request. The UI cookie keeps working unchanged.

A token carries the read access of its owner, so a script sees what its creator sees,
minus the admin role.

### A token never reaches the secrets

A token carries every role of its owner, so a token made by an administrator could
have read the ingestion key of every project, the webhook URLs of the alert channels
and the metrics token in the application configuration. A token is long lived and
lives in scripts and environment files, so it leaks far more easily than a session.

- The admin role is no longer granted to a token, so the endpoints an administrator
  sees stay out of reach.
- The endpoints that return a secret are marked cookie only: the project detail, the
  alert channels, the issue tracker configurations, the source code providers, the
  project repositories and the application configuration. A bearer token is refused
  there and the session keeps working.
- The project list stays reachable for a script, but without the ingestion keys,
  which are the secret it used to carry.

---

## Metrics

### The missing ingestion and release signals

A deployment that dropped its own database exporter lost the signals that exporter
provided. These are restored, together with the identifiers needed to keep a series
stable.

- `sigil_digestion_oldest_pending_seconds`: the age of the oldest envelope still
  awaiting digestion, which is what shows a stalled ingestion pipeline. The backlog
  depth alone stays low while the queue stops moving.
- Envelopes Sigil does not process, such as `client_report` and `transaction`, are
  marked discarded at ingestion and at digestion, so they leave the queue instead of
  inflating the backlog forever.
- `sigil_release_info` and `sigil_release_first_seen_timestamp_seconds`: the newest
  release of every project and when it was first seen.
- `sigil_events` gains environment and status labels, with the environment values
  capped per project and the remainder reported as `other`.
- `sigil_new_issues`: issues first seen inside each reported window.
- `project_id` sits next to `project` on every per-project family, so a renamed
  project keeps its series.
- `sigil_last_ingestion_timestamp_seconds` is seeded from persisted data, so it no
  longer reads as 56 years old after a restart.

### A series that does not disappear

`sigil_events` reports all four windows for every combination seen in the last day,
including the quiet ones with a zero, so a series does not vanish while it is still
observed. The new `sigil_project_events` family carries the project level grid and
reports every project, so a silence rule can still fire after a day of quiet.

### Discarded envelopes are counted, not measured

`sigil_envelopes_discarded_total` is now a counter that only rises, fed by the process
local counter store. The number of discarded envelopes still held in the database is
reported as the `sigil_envelopes_discarded` gauge, because retention removes those rows
and a counter that falls reads as a reset.

`Events.ReceivedAt` is indexed, so the persisted ingestion instant no longer costs a
sequential scan of the whole table on every scrape.

### Metrics access from the interface

The metrics token and the CIDR allow-list are editable from **Settings → Metrics
Access** in the admin area: a masked token with a random generator, copy and clear,
and the allow-list with client-side validation that rejects unparsable ranges instead
of letting the server drop them silently. Status badges mirror the endpoint state and
the Prometheus scrape snippet is shown inline.

---

## Reliability

### Query string parameters bind as raw strings

`SupplyParameterFromQuery` only binds string, bool, numeric, `Guid`, `DateTime`,
`DateOnly` and `TimeOnly` types. The issue list, issue detail, issue events and
release pages bound enum and `DateTimeOffset` parameters, which the binder rejects
before it reads the query string, so the pages crashed with "Querystring values cannot
be parsed as type ..." even with an empty URL.

The raw string is now bound and the typed name kept as a property that parses on read
and formats on write, so every existing read and write site is unchanged. The enum
helper rejects values that are not defined members, so `?level=42` no longer parses to
a severity. A reflection test over the client assembly fails when any page binds a
type the binder cannot handle.

### Events with a partial runtime context are parsed

Sentry's runtime context is optional, the Unity SDK sends a name with no version, and
`extra` carries free-form values. Reading them with `GetProperty` and `GetString`
threw on every such payload, so the whole envelope was marked failed and the event
never became an issue.

The runtime context is now looked up by key, name and version are read through a
tolerant helper that returns null instead of throwing, a runtime without a version is
kept, and `extra` and user data values are rendered as text whatever their JSON type.

### Envelope payloads with a declared length

An envelope item header may declare the byte length of its payload, and that payload
may contain line feeds. The payload is now skipped by its declared length, and an
envelope that cannot be read exactly is left alone rather than marked unsupported.

---

## Upgrading

The Docker image is published as `ghcr.io/alaa13212/sigil:0.8.1` and `:latest`.

Two migrations are included, `AddApiTokens` and `AddIndexOnEventsReceivedAt`. They
are applied by the setup wizard or by the maintenance migration endpoint, exactly as
before.

Existing installations need no configuration change. The metrics endpoint stays
disabled until you set `metrics_token` or `metrics_allowed_cidrs`. A scraper that
reads `sigil_envelopes_discarded_total` now reads a counter, and the number of rows
still held in the database moves to the `sigil_envelopes_discarded` gauge.
