# Sigil v0.8.2

The event listing shows the real failure, and a nested query parameter no longer crashes
navigation.

**1 commit · 1469 → 1484 tests · no new runtime dependency.**

---

## Reliability

### The event listing shows the real failure

Razor binds a bare attribute value as a string literal when the component parameter is a
string, so `Error="_error"` handed the event table the text "_error" instead of the
message of the failure. The alert box was drawn on every listing, a permanent
placeholder named nothing, and the failure that actually occurred was hidden behind it.

The value is bound as an expression now, so the box carries the failure that occurred
and stays away when the listing loaded.

### Navigation no longer crashes on a nested query parameter

A nested component may not declare `[SupplyParameterFromQuery]`: the provider adds it to
the set it enumerates when the location changes, and the navigation then fails with
`InvalidOperation_EnumFailedVersion`
([dotnet/aspnetcore#57746](https://github.com/dotnet/aspnetcore/issues/57746)). The
events tab of an issue and the event panel both declared the listing query keys, so
moving between the events of an issue took the page down.

Both take their values as ordinary component parameters now, and the issue detail page
binds the listing query keys and passes them down. Only pages bind the query string.

### One place builds the listing filters

The listing filters were assembled in three components, each of which parsed the query
string its own way. `EventQueryParamsFactory` builds them in one place now: a missing or
malformed value falls back to the default, which is the newest events first, on the first
page, without a severity, a release or a date bound.

A reflection test over the client assembly fails when a component that is not a page
declares a query parameter, and a test over the razor files fails when a member name is
handed to a string parameter without an `@`.

---

## Upgrading

The Docker image is published as `ghcr.io/alaa13212/sigil:0.8.2` and `:latest`.

No migration is included, and existing installations need no configuration change.
