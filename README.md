# Sigil (سجل)

Self-hosted error monitoring platform. An open developer-friendly alternative to Sentry.

Sigil ingests events from **Sentry-compatible SDKs**, groups errors into issues using intelligent fingerprinting, and provides a web interface for debugging and triage while giving you full control over your data and infrastructure.

Built with .NET 10, PostgreSQL, and Blazor.

---

## Quick Start
Run the following line in your terminal and then follow the on-screen instructions:
```bash
curl -fsSL https://raw.githubusercontent.com/alaa13212/Sigil/refs/heads/master/deploy/install.sh | bash
```

Open **http://localhost:8080** and complete the setup wizard to create your admin account and first project.

---

## Features

### Event Ingestion & Processing

- **Sentry-compatible ingestion API**: accepts envelopes from any Sentry SDK
- **Message normalization**: Customized regex rules to strip dynamic values (UUIDs, IPs, numbers, hashes) for consistent issue grouping
- **Multi-level LRU caching**: Projects, Releases, Issues, Tags, EventUsers cached to reduce a database load
- **Two-phase pipeline**: envelopes are buffered and written in batches, then digested asynchronously into issues, buckets and tag aggregates
- **Crash-safe**: raw envelopes are retained and can be replayed through re-ingestion if processing fails
- **Failure visibility**: a digestion monitor lists events that failed to process, grouped by stage

### Issue Grouping

- **Fingerprint-based grouping**: events are deduplicated and grouped into issues by fingerprint (exception type + message + stack trace)
- **Occurrence tracking**: first seen, last seen, and occurrence count per issue
- **Issue Merge**: allows for merging issues with different fingerprints into a single issue
- **Merge sets**: a group of issues tracked as one aggregate, kept fresh during ingestion and merge operations
- **Hourly event buckets** power the activity sparklines on issue and release views

### Triage & Workflow

- Issue status: open / resolved / ignored, with reopen
- Priority and assignee per issue
- Activity log recording status changes, assignments and merges
- **Recommendations engine**: background analyzers surface project-level setup problems (see below)
- **Shared links**: give external collaborators read-only access to a single issue via a token URL, with an expiry
- **Issue tracker integration**: create and optionally two-way sync linked tickets

### Alerting

- **Channels**: Slack and generic webhook
- **Four trigger types**:
  - `NewIssue` — first occurrence of a new issue
  - `IssueRegression` — a resolved issue starts receiving events again
  - `ThresholdExceeded` — an issue exceeds N events within a time window
  - `NewHighSeverity` — a new issue at or above a minimum severity
- **Minimum severity** gate and **cooldown period** per rule, so a flapping issue does not spam a channel
- **Delivery history**: every attempt is recorded with its delivery status and any error message, viewable from the admin area

### Data Hygiene

All four are per-project, rule-based, and applied during digestion.

- **Normalization rules** — ordered regex `pattern` → `replacement` pairs applied to event messages before fingerprinting. This is what keeps a UUID or a memory address in a message from splitting one issue into a thousand.
- **Inbound filters** — field/operator/value conditions that **reject** unwanted events at the door (for example dropping health-check or debug traffic). Operators: equals, contains, starts-with, ends-with, regex.
- **Stack trace filters** — conditions that drop frames or whole events out of the stack trace, so noisy framework internals stop dominating a group.
- **Auto-tag rules** — when a condition matches, apply a tag automatically, so common slices stay filterable without asking every team to remember to tag.

### Source Code & Debugging

- **Source code integration**: connect GitHub, GitLab or Bitbucket, link repositories to a project, and read the offending source snippet inline in the stack trace viewer
- **Source maps**: upload and resolve minified stack frames back to original files, lines and columns
- **In-app frame highlighting** in the stack trace viewer
- **Breadcrumbs viewer**: vertical timeline of the actions leading up to the error, with relative timestamps and expandable data
- **Event timeline** with navigation between occurrences
- **User and context sections**, plus a raw JSON viewer with copy and download

### Authentication & Access Control

- **Email/password authentication** with cookie-based sessions for the UI
- **Passkey Authentication**: login with a one-time passkey or with a **YubiKey**
- **Teams and projects**: group projects under a team and grant per-user roles (`Owner`, `Admin`, `Member`)
- **Project-scoped authorization** enforced on every mutating API route
- **Invite-only onboarding**: an admin creates an invite link per user, and the recipient activates
  their own account from it. There is no open self-registration.

### Reliability Limits

- **Ingestion rate limits**: a sliding-window limiter with a global cap, a default per-project cap, and a per-project override. Over-limit traffic is rejected rather than allowed to degrade the database.
- **Retention**: a background worker enforces a maximum event age and a maximum event count, with global defaults and per-project overrides. Failed raw envelopes have their own, shorter, retention window.
- **Prometheus metrics**: a `/metrics` endpoint exports issue counts, ingestion counters, digestion backlog, alert deliveries and build info, gated by a dedicated token or a CIDR allow-list.

### Web UI

**Issue Management**
- Issue list with filtering by status (open/resolved/ignored), severity (fatal/error/warning/info/debug), and free-text search
- `key:value` search grammar over tags, alongside free text
- Sort by last seen, first seen, occurrence count, or priority
- Activity sparkline per issue row

**Issue Detail**
- Representative event with inline stack trace viewer (in-app frame highlighting)
- Event timeline with navigation between events
- Breadcrumbs viewer: vertical timeline showing the trail of actions before the error, relative timestamps, expandable data
- Status actions: resolve, ignore, reopen
- Priority selector and assignment
- Activity log: status changes, assignments
- Tag display grouped by key with occurrence frequency

**Event Detail**
- Full stack trace viewer with in-app frame highlighting
- Breadcrumbs timeline
- Tags table (key/value)
- User info
- Context section
- Raw JSON viewer copy to clipboard and download

**Project & Navigation**
- Project selector in sidebar: supports multiple projects
- Project settings, split into focused pages for alerts, normalization, inbound filters, auto-tags, stack trace filters, source code, source maps, issue tracker integrations, re-ingestion, limits and recommendations
- DSN display and copy, API key rotation
- Releases list and release detail with per-issue activity
- Command palette (`Ctrl+K`) for issues, releases and tags
- Home dashboard: project cards showing open issue count, recent event count, and last event time

### Project Recommendations

A background analyzer periodically inspects each project and raises actionable recommendations, surfaced on the project's recommendations page. Current analyzers cover:

- No alerts configured
- Missing `environment` tag
- Missing user context
- Anonymous users everywhere
- High-cardinality tags
- High volume with no grouping in place
- Log-level-only events (everything sharing one fingerprint)
- Low event diversity
- No in-app frames
- No semver releases
- Release never changes

Each recommendation carries a severity, a description, an action link, and can be dismissed.

### First-Time Setup Wizard

- Auto-detected on first run
- Step-by-step flow:
  1. Welcome screen
  2. Database connection check and migration runner
  3. App configuration (host URL)
  4. Admin account creation
  5. First team and project creation with platform selection
  6. Integration guide with DSN and SDK code snippets
- Setup route is disabled after completion

---

## SDK Integration

Sigil accepts events from any Sentry SDK. After creating a project, copy its DSN from the project settings page or the setup wizard.

## Configuration

Configuration via environment variables (see `.env.example`):

| Variable            | Default              | Description                  |
|---------------------|----------------------|------------------------------|
| `POSTGRES_USER`     | `sigil`              | PostgreSQL username          |
| `POSTGRES_PASSWORD` | `sigil_dev_password` | PostgreSQL password          |
| `POSTGRES_DB`       | `sigil`              | Database name                |
| `SIGIL_PORT`        | `8080`               | Host port to expose Sigil on |

Batch worker tuning in `appsettings.json`:

```json
{
  "BatchWorkers": {
    "EventIngestion": {
      "BatchSize": 50,
      "Cap": 1000,
      "FlushTimeout": "00:00:02"
    }
  }
}
```

Runtime limits are stored in the database rather than in configuration files, so they are editable
from the admin settings page and overridable per project. Defaults shown are what a fresh install uses:

| Setting | Global default | Per-project override | Description |
|---|---|---|---|
| `rate_limit_global_limit` | 50,000 | — | Global ingestion cap per window |
| `rate_limit_default_project_limit` | 50,000 | `rate_limit_max_events_per_window` | Per-project ingestion cap per window |
| `rate_limit_window_seconds` | 60 | — | Rate limit window |
| `retention_default_max_age_days` | 90 | `retention_max_age_days` | Maximum event age |
| `retention_default_max_events` | 250,000 | `retention_max_event_count` | Maximum events retained per project |
| `retention_check_interval_minutes` | 60 | — | Retention sweep interval |
| `retention_failed_envelope_max_age_days` | 7 | — | Retention for failed raw envelopes |

### Operational Endpoints

| Endpoint | Purpose |
|---|---|
| `/health` | Health check, mapped for container orchestrators |
| `/metrics` | Prometheus metrics, token- or CIDR-gated (see [Metrics](#metrics)) |
| `/api/*` | REST API consumed by the Blazor client and available for scripting |

### Metrics

`GET /metrics` returns metrics in the Prometheus text exposition format. Metrics cover issue
counts, event volumes, ingestion health, digestion backlog, alert delivery outcomes and build
information:

| Metric | Type | Labels | Meaning |
|---|---|---|---|
| `sigil_issues` | gauge | `project`, `status`, `level` | Current issue count |
| `sigil_events_total` | counter | `project`, `level` | Events received in the last 24 hours |
| `sigil_events_ingested_total` | counter | — | Events accepted into the ingestion pipeline since process start |
| `sigil_events_dropped_total` | counter | — | Events discarded during digestion since process start |
| `sigil_digestion_backlog` | gauge | — | Envelopes awaiting digestion |
| `sigil_alert_deliveries_total` | counter | `channel`, `status` | Alert deliveries recorded |
| `sigil_last_ingestion_timestamp_seconds` | gauge | — | Unix time of the last ingested event, for staleness alerts |
| `sigil_build_info` | gauge | `version` | Running build version, always `1` |

Label cardinality is bounded by design: only project names and enum values are ever used as
labels. Issue identifiers, fingerprints and tag values are never exported.

#### Access control

The endpoint is closed by default. It does **not** use UI sessions, so a scraper never needs a
cookie. Configure at least one of these two database-backed settings, editable from the admin
settings page:

| Setting | Purpose |
|---|---|
| `metrics_token` | Shared secret. Send it as `Authorization: Bearer <token>` or `?token=<token>`. Compared in constant time. |
| `metrics_allowed_cidrs` | Comma-separated CIDR ranges allowed to scrape without a token, e.g. `10.0.0.0/8,192.168.0.0/16`. |

With neither configured, `/metrics` returns `403` — an unauthenticated scrape is rejected rather
than served. A presented token that does not match returns `403`; a request with no token at all
returns `401`.

#### Prometheus scrape config

```yaml
scrape_configs:
  - job_name: sigil
    metrics_path: /metrics
    scheme: https
    static_configs:
      - targets: ['sigil.example.com']
    authorization:
      # Sent as 'Authorization: Bearer <token>'
      credentials: <metrics_token>
      type: Bearer
```

When Sigil runs behind a reverse proxy, the CIDR allow-list matches against the forwarded
client address (`X-Forwarded-For`) rather than the proxy's own address.

---

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 17+
- Docker — the Infrastructure test suite runs against a real PostgreSQL container via Testcontainers

### Build & Run

```bash
# Build
dotnet build

# Run
dotnet watch --project src/Sigil.Server/Sigil.Server.csproj
```

Make sure Tailwinds is running

```bash
src/Sigil.Server/Tools/tailwindcss -i src/Sigil.Server/Styles/input.css -o src/Sigil.Server/wwwroot/css/site.css --watch
```

### Tests

```bash
dotnet test Sigil.slnx
```

`Sigil.Domain.Tests` and `Sigil.Application.Tests` are pure unit tests and run anywhere.
`Sigil.Infrastructure.Tests` needs Docker, because it exercises EF Core against a real PostgreSQL
container rather than an in-memory substitute.

### CI

`.github/workflows/ci.yml` runs on every pull request and on pushes to `master`, in two jobs: a fast
job that builds and runs the Domain and Application suites, and a slower job that runs the
Infrastructure suite with Docker available.

### Database Migrations

```bash
# Create a new migration
dotnet ef migrations add <Name> \
  --project src/Sigil.Infrastructure \
  --startup-project src/Sigil.Server

# Apply migrations
dotnet ef database update \
  --project src/Sigil.Infrastructure \
  --startup-project src/Sigil.Server
```

---

## Architecture

```
Sigil.Server ─┬─> Sigil.Infrastructure ─> Sigil.Application ─> Sigil.Domain
              └─> Sigil.Server.Client ───> Sigil.Application
```

| Layer | Responsibility |
|---|---|
| **Domain** | Entities, enums, interfaces, value objects |
| **Application** | Business logic, service interfaces, enrichers |
| **Infrastructure** | EF Core/PostgreSQL, parsing, caching, background workers |
| **Server** | ASP.NET Core host, Blazor UI, API controllers |
| **Server.Client** | Blazor UI components, typed HTTP client services |

The UI is a Blazor Web App. `Sigil.Server` hosts the app and the API; `Sigil.Server.Client` holds the
components and the typed client services that talk to the API over HTTP. **No component talks to
`HttpClient` directly** — each feature exposes an interface in `Sigil.Application`, implemented in
`Sigil.Infrastructure` for the server side and again in `Sigil.Server.Client` as a thin HTTP wrapper
over the matching controller. That is what lets the same interface be served from a local database in
tests and over the wire in the browser.

---

## License

See [LICENSE](LICENSE) for details.
