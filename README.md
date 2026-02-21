# Sigil (سجل)

Self-hosted error monitoring platform. An open developer-friendly alternative to Sentry.

Sigil ingests events from **Sentry-compatible SDKs**, groups errors into issues using intelligent fingerprinting, and provides a web interface for debugging and triage while giving you full control over your data and infrastructure.

Built with .NET 10, PostgreSQL, and Blazor.

---

## Features

### Event Ingestion & Processing

- **Sentry-compatible ingestion API**: accepts envelopes from any Sentry SDK
- **Message normalization**: Customized regex rules to strip dynamic values (UUIDs, IPs, numbers, hashes) for consistent issue grouping
- **Multi-level LRU caching**: Projects, Releases, Issues, Tags, EventUsers cached to reduce a database load

### Issue Grouping

- **Fingerprint-based grouping**: events are deduplicated and grouped into issues by fingerprint (exception type + message + stack trace)
- **Occurrence tracking**: first seen, last seen, and occurrence count per issue
- **Issue Merge**: allows for merging issues with different fingerprints into a single issue

### Authentication & Access Control

- **Email/password authentication** cookie-based sessions for the UI
- **Passkey Authentication**: Login with a one-time passkey or with a **YubiKey**

### Web UI

**Issue Management**
- Issue list with filtering by status (open/resolved/ignored), severity (fatal/error/warning/info/debug), and free-text search
- Sort by last seen, first seen, occurrence count, or priority

**Issue Detail**
- Representative event with inline stack trace viewer (in-app frame highlighting)
- Event timeline with navigation between events
- Breadcrumbs viewer: vertical timeline showing the trail of actions before the error, relative timestamps, expandable data
- Status actions: resolve, ignore, reopen
- Priority selector
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
- Project settings page: view/copy DSN and DSN, rotate API key
- Home dashboard: project cards showing open issue count, recent event count, and last event time

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

### Deployment

- **Docker Compose**: One step setup for Sigil server + PostgreSQL database


---

## Quick Start

```bash
git clone https://github.com/AliDev-ir/Sigil.git
cd sigil

# Copy and edit environment config
cp .env.example .env

# Start Sigil + PostgreSQL
docker compose up -d
```

Open **http://localhost:8080** and complete the setup wizard to create your admin account and first project.

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

---

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 17+

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
Sigil.Server → Sigil.Infrastructure → Sigil.Application → Sigil.Domain
```

| Layer | Responsibility |
|---|---|
| **Domain** | Entities, enums, interfaces, value objects |
| **Application** | Business logic, service interfaces, enrichers |
| **Infrastructure** | EF Core/PostgreSQL, parsing, caching, background workers |
| **Server** | ASP.NET Core host, Blazor UI, API controllers |
| **Server.Client** | Blazor WebAssembly client components |

---

## License

See [LICENSE](LICENSE) for details.
