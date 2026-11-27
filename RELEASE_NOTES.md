# Sigil v0.8.0

Search, filtering and observability — the largest release since v0.7.0.

This release lands Phase 9 in full, the remaining Phase 8 integrations, and the
operational groundwork (CI, README, Prometheus export) that makes the product
self-hostable with confidence.

**22 commits · 924 → 1366 tests · no new runtime dependency.**

---

## Filtering and search

### Issue date-range filtering

Issue lists can now be scoped to a time window. Filtering runs on `Issue.LastSeen`,
because an issue is a grouping bucket rather than an event — "issues active in this
window" is the meaningful query, and it matches the existing default sort.

A shared `DateRangePicker` component provides 24h / 7d / 30d / 90d presets and a
custom range, and is reused by the issue list, the event list and the chart range
selector. Ranges are transmitted as UTC instants, so a browser offset never shifts
the window.

### Event search and filtering

The largest gap in the product, now closed. Events were previously unfilterable —
finding one specific event in an issue with thousands of occurrences meant clicking
"next" repeatedly.

Events can now be filtered by date range, severity, release, logger, platform, user,
free text, tag filters, and exact SDK event id, within an issue or across a whole
project. The search grammar is shared with the issue list, so `level:error tag:env:prod`
works in both places.

Supporting work: new indexes for project-wide and level-filtered event browse, a
project-wide event list page, and a shared event table component.

### Filter state lives in the URL

Every filter — status, severity, sort, date range, search, bookmarked — is now part of
the URL. Combinations are shareable, bookmarkable and survive a refresh.

### Active filter chips and a wider grammar

Each active filter renders as a dismissible chip. Dismissing one reparses the query
rather than editing the string, so free text and the other tokens survive. The grammar
now understands `release:`, `is:assigned`, `is:unassigned`, `is:bookmarked` and
`is:unviewed`, in addition to the existing `key:value` tag filters.

---

## Charts

### Interactive time series

Charts are now drawn from a `TimeSeries` model that carries its own window, instead of
a bare list of counts. That makes axis labels, range captions and per-bucket tooltips
possible.

- The chart always states the window it is showing (`Oct 1 – Oct 7, 2026 · 6-hour buckets`),
  read from the data itself, so it can never disagree with what is plotted.
- Every bucket carries a tooltip with its start, end and exact count.
- Bucket width adapts to the range — hourly, 6-hour, daily or weekly — and can be
  overridden. Weekly buckets align to ISO weeks, starting Monday, in UTC.
- Bar and line + area modes.
- Empty, single-point and all-zero states are distinct rather than a blank box.

Rendering is done in real pixel space, so the chart no longer stretches the way the old
sparkline did.

### Charts no longer lie about their range

The hardcoded "Last 14 days" labels are gone. Issue sidebar and release pages now expose
the range as a real selector.

---

## Prometheus metrics

`GET /metrics` exports metrics in Prometheus text exposition format.

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `sigil_issues` | gauge | `project`, `status`, `level` | Current issue count |
| `sigil_events` | gauge | `project`, `level`, `window` | Events received within the window (`5m`, `15m`, `1h`, `24h`) |
| `sigil_events_ingested_total` | counter | — | Events accepted into the pipeline since process start |
| `sigil_events_dropped_total` | counter | — | Events discarded during digestion since process start |
| `sigil_digestion_backlog` | gauge | — | Envelopes awaiting digestion |
| `sigil_alert_deliveries_total` | counter | `channel`, `status` | Alert deliveries recorded |
| `sigil_last_ingestion_timestamp_seconds` | gauge | — | Unix time of the last ingested event, for staleness alerts |
| `sigil_build_info` | gauge | `version` | Running build version |

**Access control.** The endpoint is closed by default. Set a metrics token or a CIDR
allow-list to enable it. A scraper never needs a UI session, and issue identifiers,
fingerprints and tag values are never exported.

`/metrics` reports sliding-window gauges rather than counters, so it must not be fed to
`rate()` or `increase()`. Alert on a threshold instead — for example
`sigil_events{level=~"fatal|error",window="5m"} > 0` for new errors in the last five
minutes.

No Prometheus library was added; the exposition format is hand-rolled in roughly a
hundred lines.

---

## Reliability

### Failed requests are no longer empty states

A failed request used to render as "No events" or "No issues found" — indistinguishable
from genuinely zero data, which is the most trust-destroying defect the UI had. Loading,
error-with-retry and empty are now distinct states on the issue list, issue sidebar and
release pages.

### Project-scoped authorization on mutating routes

Every mutating API route now resolves its project from the route and is authorized
against it, instead of relying on a generic authenticated check.

### Sign-in session survives reloads and browser restarts

Sessions no longer expire on a page reload or a browser restart.

### Tailwind build order

`tailwind.targets` had contradictory target hooks that made a **fresh clone unbuildable
on Linux and macOS** — the step that runs the Tailwind CLI fired before the step that
downloads it. It was masked on developer machines by a gitignored binary and by the
Dockerfile pre-downloading it in an earlier layer.

### Search palette: holding Shift no longer flickers

The double-Shift shortcut triggered on every auto-repeat keydown, so holding the key
toggled the palette open and shut rapidly. It now requires two discrete presses.

---

## Integrations

### Source maps

Source code can be linked to issues, and stack frames are deobfuscated against the
matching uploaded source map.

### Issue tracker integration

Issues can be pushed to an external tracker, and the external state is kept in sync.

### Per-project CORS origin whitelist

SDK ingestion now accepts a per-project list of allowed browser origins.

---

## Operations

### CI

A GitHub Actions workflow runs on every push and pull request: a fast job that builds
and tests Domain and Application, and a slower job that runs the Infrastructure tests
against a real PostgreSQL container. Previously nothing guarded the repository.

### README

The README documented roughly 40% of the product. It now covers alerting, data hygiene,
source code and source maps, issue tracker integration, merge sets, re-ingestion, shared
links, teams and permissions, retention, rate limits, recommendations and analyzers,
breadcrumbs, the event timeline and the metrics endpoint — including a Prometheus scrape
example and the real architecture, with `Sigil.Server.Client` shown as a distinct layer.

---

## Security

Dependency updates clear three published advisories:

- **CVE-2026-102870** — remote code execution in the Blazor WebAssembly debugger
- **CVE-2026-96230** — cross-site scripting in the Blazor WebAssembly development-server debug endpoint
- **CVE-2026-118747** — hash-flood denial of service in `System.Formats.Cbor`

The ASP.NET Core and Entity Framework packages move to 10.0.15.

---

## Upgrading

The Docker image is published as `ghcr.io/alaa13212/sigil:0.8.0` and `:latest`.

Existing installations need no configuration change. The metrics endpoint stays disabled
until you set `metrics_token` or `metrics_allowed_cidrs`. Existing deployments should
confirm that their ingestion CORS configuration still matches, since the per-project
allow-list is now enforced.
