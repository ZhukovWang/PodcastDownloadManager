# AGENT.md

Notes for anyone (human or AI) working on this repository. Everything below was verified against the
code and by running it. Update it when behaviour changes.

## What this program actually is

`pdlm` is a console podcast manager. The single most important architectural fact:

**it does not download audio itself.** It parses RSS feeds, writes a download list in the format of an
external downloader (aria2c or IDM), and shells out to that program. Media bytes never pass through
C#. So throughput, resume, and multi-connection behaviour belong to aria2/IDM and their configuration,
not to this code. Performance work here is about feed fetching and parsing.

## Layout

| Path | Contents |
| --- | --- |
| `PodcastDownloadManager/Program.cs` | entry point: creates the state directory, loads config, hands off to NFlags |
| `PodcastDownloadManager/RootCommand.cs` | registers the 9 commands |
| `PodcastDownloadManager/Commands/` | one file per command; NFlags fluent config + `Execute` |
| `PodcastDownloadManager/DownloadHelper.cs` | the only HTTP client: feed download, retry, atomic replace |
| `PodcastDownloadManager/DownloadTools.cs` | starts aria2c / IDM and forwards their stdout to the console |
| `PodcastDownloadManager/Podcast/Podcast.cs` | feed parsing, new-release detection, download-list entries |
| `PodcastDownloadManager/Podcast/Opml.cs` | the library file (`podcasts.opml`), library-wide operations |
| `PodcastDownloadManager/FileMetadata/AudioMetadata.cs` | writes/consumes the `.metadata` sidecar, tags audio with TagLib# |
| `PodcastDownloadManager/ProgramConfiguration.cs` | config JSON plus every runtime path (all static) |
| `PodcastDownloadManager.Tests/` | xunit v3 suite (see Testing) |

Dependencies: `NFlags` 1.8.0 (CLI), `NLog.Extensions.Logging` 1.6.4 (logging), `TagLibSharp` 2.2.0 (tags).

## Build, run, test

```powershell
dotnet build                      # solution: app + tests
dotnet run --project PodcastDownloadManager.Tests   # the whole test suite
dotnet run --project PodcastDownloadManager -- list # run the CLI from source
```

The app targets `net10.0`, `AssemblyName` is `pdlm`. Tests are xunit v3 on Microsoft.Testing.Platform:
the test project is an executable that runs its own tests, so the command above is a complete test run.
`global.json` selects the MTP runner for `dotnet test` where the platform permits the runner/test
process handshake. On Windows that handshake fails outright if the checkout is `Low` integrity; see
gotcha 11.

CI (`.github/workflows/dotnet-core.yml`) restores, builds Release, then runs the test executable, on
ubuntu and windows.

## Runtime state

Everything lives next to the executable (`ProgramConfiguration.Local`, i.e. the app base directory):

| File | Meaning |
| --- | --- |
| `config.json` | `DownloadPodcastPath`, `DownloadProgram` (`Aria2`/`IDM`), `DownloadProgramPathName` |
| `podcasts.opml` | the library: OPML 1.0, `<outline text="<name>" xmlUrl="<feed url>" />` |
| `PodcastsFile/<name>.xml` | cached feed. **Its last-write time is the "last update" watermark** |
| `PodcastsFile/NewPodcast.xml` | scratch file used while reading the title of a newly added feed |
| `NewlyReleaseInfo.txt` | download list for `upgrade` |
| `<DownloadPodcastPath>/PodcastDownload.txt` | download list for `download` |
| `<DownloadPodcastPath>/<audio>.metadata` | 7-line sidecar consumed by `autometadata` |
| `logs/<date>_Log.log` | NLog output (`nlog.config`) |

The podcast name is the channel title with spaces removed and invalid filename characters stripped. It is
both the opml key and the cache filename, so **renaming a show in the feed orphans its cache entry**.

### Download list formats (a contract with the external tools)

aria2 (`-i` input file):

```
https://example.test/ep.mp3
	dir=C:\Downloads
	out=Show - 2026_11_08 - Episode title.mp3
```

IDM (one command line per release, then the tool is started with `/s`):

```
/a /d "https://example.test/ep.mp3" /p "C:\Downloads" /f "Show - 2026_11_08 - Episode title.mp3"
```

### Metadata sidecar format (a contract with `autometadata`)

Seven positional lines: title, artist, album, genre (`Podcast`), date, image url (or `null`), comment.
`autometadata` pairs `<name>.metadata` with `<name>` (any audio extension), downloads the cover, writes
the TagLib tags, then deletes the sidecar and the downloaded image. Changing the line order breaks
every sidecar written before the change.

## Command map and data flow

| Command | Effect |
| --- | --- |
| `add <url>` | reads the feed once for its title, appends an outline to `podcasts.opml` |
| `remove <name>` | drops the outline |
| `list` | prints name/url from the opml |
| `show <name> [--refresh]` | prints channel + newest item; reuses the cached feed unless `--refresh` |
| `update` | **the main workhorse**: refreshes every feed, prints and queues new releases |
| `upgrade` | feeds `NewlyReleaseInfo.txt` to aria2/IDM, then deletes it |
| `download [--date d] [-d dir] [-s]` | builds `PodcastDownload.txt` for everything after `d` and downloads it |
| `download list <name>` / `download select <name> <1;2>` | per-podcast release listing / selection |
| `autometadata` | turns sidecars into audio tags |
| `config -p/-dp/-dpp` | writes `config.json` |

`update` → new items → `NewlyReleaseInfo.txt` + `.metadata` sidecars → `upgrade` → audio files →
`autometadata` → tags. That is the intended loop; each stage is independent and re-runnable.

## Feed parsing rules (Podcast.cs)

- RSS only: `channel/title`, `channel/item`, `pubDate`, `enclosure@url`. The `itunes` namespace is used
  for `itunes:author` and `itunes:image@href`; author falls back to `author`, then the channel title.
- `pubDate` is parsed as `DateTimeOffset` with `AssumeUniversal`, so feeds without an offset do not depend
  on the machine time zone. **All comparisons are UTC.** The watermark is the cache file's
  `GetLastWriteTimeUtc()`, read *before* the download (the download replaces the file and resets its mtime).
- An item without a readable `pubDate`, or without an `enclosure@url`, is skipped and logged. One broken
  feed never aborts the rest of the library: `CollectNewlyReleases()` returns a `FeedUpdateResult` with
  `Success`/`Error` instead of throwing.
- `update` fetches feeds concurrently (SemaphoreSlim, at most 8 in flight). Results are printed and
  written serially afterwards, because `NewlyReleaseInfo.txt` is a single shared file.
- XML is read with `DtdProcessing.Prohibit` and a null resolver.

## Testing

- `PodcastDownloadManager.Tests` — 36 tests, no network: feeds are served by `Support/FakeFeedServer.cs`
  (a `TcpListener` on loopback), and `Support/TestWorkspace.cs` points every `ProgramConfiguration` path
  at a fresh temp directory per test.
- `ProgramConfiguration.UseDirectory()` exists only for the tests (`InternalsVisibleTo`). Do not call it
  from product code. `ProgramConfiguration` paths are static, which is why
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]` is required: parallel test classes
  overwrite each other's workspace (three tests fail without it).
- Assertions on user-facing output (update lines, opml contents, download list text) are intentional
  contract tests — changing output formats means changing tests.
- Tests that call APIs taking a `CancellationToken` must pass `TestContext.Current.CancellationToken`
  (xunit analyzer xUnit1051); pass it inline, a local variable does not satisfy the analyzer.

## Gotchas that will bite

1. **`NFlags.Environment` shadows `System.Environment`.** In any file with `using NFlags;`, write
   `System.Environment.ProcessorCount` explicitly, otherwise `CS0104`.
2. **`NFlags.IOutput` mirrors `TextWriter`** (29 members). Implementing it in a test double means
   forwarding all `Write*`/`WriteLine*` overloads; see `Support/TestWorkspace.cs`.
3. **`ProgramConfiguration` is static and mutated.** `DownloadFileName` is assigned by the `Opml`
   methods before the commands call `DownloadTools`. Order matters.
4. Download list and sidecar formats are external contracts (see above); do not "tidy" them.
5. `Podcast.GetValidName` uses `Path.GetInvalidFileNameChars()`, so the same title yields different
   filenames on Windows and Linux. Tests avoid titles with platform-specific characters.
6. `DownloadHelper.DownloadFileAsync` streams into `<target>.part` and only moves it into place when the
   transfer succeeded, so a failed download never destroys a usable cached feed. Keep that property.
7. `Assembly.Location` is empty under single-file publish; `ProgramConfiguration` falls back to
   `AppContext.BaseDirectory`. Do not reintroduce a bare `Path.GetDirectoryName(...)`.
8. `UpgradeCommand` compares `File.GetLastWriteTimeUtc(...)` with `DateTime.Now` (local) — a leftover
   time zone bug that makes its "needs update" window off by the UTC offset. Fixing it changes when
   `upgrade` refreshes first; it is not yet fixed.
9. `download` (the `--date` variant) and `download select` still fetch feeds serially, one podcast after
   another, so their first run costs one round trip per podcast. `update` was parallelised; these were not.
10. Feeds are parsed with `XmlDocument`, i.e. the whole document is loaded. Podcast feeds of several MB
    are common; a streaming or cached parse is the obvious next step if this ever shows up in profiles.
11. **A `Low` mandatory integrity label on the checkout breaks the tests on Windows.** If the repository
    folder (and therefore `bin/`) carries `Mandatory Label\Low Mandatory Level` — check with
    `icacls <repo> | findstr /i Mandatory` — every process started from the build output runs at Low
    integrity. A Low process cannot write to `%TEMP%` or connect to the Medium-integrity named pipe that
    `dotnet test` creates, so all 36 tests fail in `TestWorkspace..ctor` with
    `UnauthorizedAccessException`, and `dotnet test` reports an MTP "Handshake failure"
    (`NamedPipeClientStream.TryConnect`). It is a property of the **files**, not of the terminal/tool, so
    it reproduces in any shell and via Task Scheduler; copying the built exe to a normal folder or
    pointing `TEMP` inside the repo makes the tests pass. Fix with
    `icacls <repo> /setintegritylevel (OI)(CI)Medium /T`, then rebuild; after that `dotnet test` reports
    36/36 passing. A sandbox may also leave a `Everyone:(CI)(DENY)(S,DC)` ACE and an unresolvable
    `S-1-4-...` grant on the folder; those are harmless but can be removed with
    `icacls <repo> /remove:d Everyone /T` (the synthetic SID has to be removed through .NET).

## Remaining work worth doing

- Parallelise `download --date` / `download select` the same way `update` was (collect per podcast, write
  the list serially).
- Parse feeds once per command instead of re-loading the XML for every operation.
- Give `DownloadHelper` a configurable timeout/retry policy instead of constants.
- `upgrade`'s time zone bug (gotcha 8).

## Style

The codebase is older-style C# (block namespaces, explicit types, no nullable annotations) with a few
newer files. Match the file you are editing. Log through `Logger.Log`, print user output through
`IOutput`, and keep the existing message wording unless a test is updated with it.
