# Sigil v0.9.0

Six ways to see what Sigil already knows, and to act on it.

This release is about reading and acting. The dashboard gained a time
context, so a project card says what happened lately and not only how
much has ever happened. A project charts its own event activity. The
issue list sorts by the columns it shows, and can be triaged in bulk.
The keyboard can drive the issue list. The settings pages state the
retention policy that is actually enforced instead of one that was
never true.

**9 commits · 1484 → 1569 tests · no new runtime dependency.**

---

## The dashboard

### A project list with a time context

Each project card states the lifetime totals it always did and, next to
them, the issues and the events inside a trailing window. The window
follows the range the activity chart is showing, so the two cannot
disagree, and the range lives in the query string, so a view can be
shared.

An activity chart across the visible projects and a sparkline per project
answer the question the totals could not: is this project quiet, ramping
up or failing? A quiet project no longer looks like a busy one with a
small number.

Each card also says how many open issues you have not seen since you
last opened that project's issue list. It reuses the unseen issue count
the navigation already computes, so it needs no new storage, and it says
exactly what it measures.

### Only the projects you can see

The dashboard listed every project on the instance. On a multi-team
deployment that leaks, and it would put a time series next to a project
you have no access to.

The list is now the projects whose team you belong to, and an
administrator keeps the full view. A non-administrator therefore stops
seeing a project that has no team, which they could not open anyway.
Creating a project is offered to administrators only, because a project
created without a team would be invisible to the person who created it.

The projects are now ordered by the most recent activity first, with the
projects that have never had an event last. The order used to be
ascending by the newest event, which put the quietest project at the
top.

A failed request no longer reads as "No projects yet". The page says the
request failed and offers to retry, and a single project no longer
renders an empty page while its redirect is in flight.

---

## The issue list

### The headers sort the list

The issue list sorted through a dropdown while its column headers were
static text, so the two most common triage orders, severity and
occurrences, needed a detour through a menu. The server already sorted
by both, and the event table already had clickable headers.

The headers are now buttons that sort, and the header is a shared
component with the event table, so the two cannot drift. The sort
direction travels in the URL as a separate key that is omitted when it
matches the default, so existing links keep working. The sort chip names
the direction, so the order is visible without reading the header.

While a free text search is active the server ranks by relevance and
ignores the selected column, so the headers are disabled there and say
why, instead of appearing to do nothing.

### A batch of issues, one action

Triage is the core loop, and the issue list had no way to do it in bulk.
Resolving fifty duplicates was fifty round trips, one per issue, through
a menu on each row.

The list now has a checkbox column and a toolbar that appears with a
selection. Resolve, ignore, ignore with future events, bookmark, assign
and unassign all apply to the ticked issues in one request.

A batch is rejected as a whole when any issue is missing from the
project, so a partial write cannot happen. The status transition is
lifted out of the single-issue path and is called by both, so the
resolve fields, the ignore filter and the merge set propagation cannot
drift. The bookmark action sets the state instead of flipping it, so
clicking bookmark twice leaves every selected issue bookmarked.

---

## Events

### A project charts its own event activity

The project page showed counts but never showed when they happened. The
only activity chart in the product was scoped to a single issue, so
there was no way to see whether a project was quiet, ramping up or
spiking without opening issues one at a time.

The events page now carries a project scoped chart. It reads the
pre-aggregated hourly buckets that digestion already writes, joined
through the issue to its project, so it costs one grouped query instead
of a scan over the raw event table.

The window is trailing rather than calendar aligned, which is what makes
the twenty four hour preset mean the last twenty four hours instead of
today since midnight. The calendar aligned window is kept for the issue
and release histograms, whose day buckets are still written on day
boundaries.

---

## Keyboard

### The issue list can be driven from the keyboard

The issue list could only be worked with the mouse, so a triage pass over
a long list was a long series of clicks.

`j` and `k` move a focus through the rows, `r` resolves the focused
issue and reports it in a toast with an undo, and `?` opens an overlay
that lists the shortcuts. The `g` prefix moves between the sections of a
project: `g i`, `g r`, `g e` and `g d`. Those are project scoped, so they
do nothing outside a project, and the overlay greys them out there
instead of listing them as if they worked.

What a key means is decided in C# and tested like any other code. The
browser script only filters what it knows better than the server does: a
key typed into a text field, an auto-repeat, and an open modal.

The event table and the release list were left alone. The layer is a
target registry, so a page joins it by registering itself for as long as
it is on screen.

The search palette resolves an event reference now, so `event:42` opens
the event. It also gains the project releases and project events pages,
so every section the `g` prefix can reach is reachable from the palette
too.

---

## Settings

### The retention policy that is actually enforced

The project limits page showed the global event cap as 1,000,000 while
the retention worker enforces 250,000, and it read that default from a
literal on the client, so the number drifted the moment the server
default changed.

Worse, the page fetched the global configuration through the admin-only
endpoint with no error handling. The page is reachable by any project
admin or owner, so every non-admin project manager saw an unhandled
exception instead of the limits.

The policy is now resolved on the server, where the same expression the
worker uses decides which override applies, and exposed through a project
scoped endpoint. The page states the effective policy in plain language,
and the worker logs the baseline and the per project policy.

---

## Fixed

### The "All" button filtered by open issues

The status filter had an "All" button that did not mean all. It set
the status to "all", but the status parameter was only written to the
URL when it resolved to a single status, so the "all" was dropped and
the page fell back to the open default. The button now shows every
issue, whatever its status.

---

## Upgrading

There are no migrations in this release. The Docker image is published as
`ghcr.io/alaa13212/sigil:0.9.0` and `:latest`.

One behaviour change to be aware of: the dashboard lists only the
projects you can see. A non-administrator no longer sees a project that
has no team, and creating a project is offered to administrators only.
Existing installations need no configuration change.
