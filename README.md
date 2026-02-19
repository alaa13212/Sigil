# Sigil (سجل)

Self-hosted error monitoring backend — an open, developer-friendly alternative to Sentry.

Sigil ingests events from **Sentry-compatible SDKs**, groups errors into issues using intelligent fingerprinting, and provides a web interface for debugging and triage — while giving you full control over your data and infrastructure.

Built with .NET 10, PostgreSQL, and Blazor.

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

## SDK Integration

Sigil is compatible with any Sentry SDK. Point your SDK's DSN at your Sigil instance:

```
http://<API_KEY>@<SIGIL_HOST>/api/<PROJECT_ID>/envelope
```

### .NET
```csharp
SentrySdk.Init(options =>
{
    options.Dsn = "http://<API_KEY>@localhost:8080/api/1/envelope";
});
```

### JavaScript
```javascript
Sentry.init({
  dsn: "http://<API_KEY>@localhost:8080/api/1/envelope",
});
```

### Python
```python
sentry_sdk.init(dsn="http://<API_KEY>@localhost:8080/api/1/envelope")
```

You can find your project's API key in the Sigil web UI after creating a project.

## Configuration

Configuration is done through environment variables (see `.env.example`):

| Variable | Default | Description |
|---|---|---|
| `POSTGRES_USER` | `sigil` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `sigil_dev_password` | PostgreSQL password |
| `POSTGRES_DB` | `sigil` | Database name |
| `SIGIL_PORT` | `8080` | Host port to expose Sigil on |

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL 17+

### Build & Run

```bash
# Build
dotnet build

# Run
dotnet run --project src/Sigil.Server/Sigil.Server.csproj
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

## License

See [LICENSE](LICENSE) for details.
