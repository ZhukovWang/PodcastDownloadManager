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
| `PodcastDownloadManager/OutputTools.cs` | red user-facing errors on a real console; plain text for redirected/custom output |
| `PodcastDownloadManager/Podcast/Podcast.cs` | feed parsing, new-release detection, download-list entries |
| `PodcastDownloadManager/Podcast/Opml.cs` | the library file (`podcasts.opml`), library-wide operations |
| `PodcastDownloadManager/FileMetadata/AudioMetadata.cs` | writes/consumes the `.metadata` sidecar, tags audio with TagLib# |
| `PodcastDownloadManager/ProgramConfiguration.cs` | config JSON plus every runtime path (all static) |
| `PodcastDownloadManager/ExitCode.cs` | the `0`/`1` exit codes returned by commands |
| `PodcastDownloadManager.Tests/` | xunit v3 suite (see Testing) |

Dependencies: `NFlags` 1.8.0 (CLI), `NLog.Extensions.Logging` 1.6.4 (logging), `TagLibSharp` 2.2.0 (tags).

## Build, run, test

```powershell
dotnet build                      # solution: app + tests
dotnet run --project PodcastDownloadManager.Tests   # the whole test suite
dotnet run --project PodcastDownloadManager -- list # run the CLI from source
```

The app targets `net10.0`, `AssemblyName` is `pdlm`. Its displayed version is
`git describe --tags --always --dirty`, injected as `AssemblyInformationalVersion` by the
`GenerateGitVersion` target; `AssemblyVersion`/`FileVersion` stay `1.0.0.0` for compatibility. A
non-git source tree falls back to `1.0.0.0`. Tests are xunit v3 on Microsoft.Testing.Platform:
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
| `config.json` | `DownloadPodcastPath`, `DownloadProgram` (`Aria2`/`IDM`), `DownloadProgramPathName`, `FeedRequestTimeoutSeconds`, `FeedDownloadMaxAttempts` |
| `podcasts.opml` | the library: OPML 1.0, `<outline text="<name>" xmlUrl="<feed url>" />` |
| `PodcastsFile/<name>.xml` | cached feed. **Its last-write time is the "last update" watermark** |
| `PodcastsFile/NewPodcast.xml` | scratch file used while reading the title of a newly added feed |
| `NewlyReleaseInfo.txt` | download list for `upgrade`; deleted only after the downloader exits `0` |
| `<DownloadPodcastPath>/PodcastDownload.txt` | download list for `download` |
| `<DownloadPodcastPath>/<audio>.metadata` | 7-line sidecar consumed by `autometadata` |
| `logs/<date>_Log.log` | NLog output (`nlog.config`) |

The podcast name is the channel title with spaces removed and invalid filename characters stripped. It is
both the opml key and the cache filename, so **renaming a show in the feed orphans its cache entry**.
`CreateConfig()` also creates the default `DownloadPodcastPath` directory, so a first-run `update` can
write metadata sidecars before any downloader runs.

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
| `show <name> [--refresh]` | prints channel + newest item; reuses the cached feed unless `--refresh`; a failed `--refresh` is a failure, not stale success |
| `update` | **the main workhorse**: refreshes every feed, prints and queues new releases |
| `upgrade` | refreshes feeds if the list is missing/older than a day, feeds the list to aria2/IDM |
| `download [<date>] [-d dir] [-s]` | builds `PodcastDownload.txt` for everything after `d` and downloads it |
| `download list <name>` / `download select <name> <1;2>` | per-podcast release listing / selection; a selection that matches nothing fails |
| `autometadata` | turns sidecars into audio tags |
| `config -p/-dp/-dpp/-ft/-fr` | writes `config.json` |

`update` → new items → `NewlyReleaseInfo.txt` + `.metadata` sidecars → `upgrade` → audio files →
`autometadata` → tags. That is the intended loop; each stage is independent and re-runnable.

### Exit codes

Every `Execute` returns `ExitCode.Success` (`0`) or `ExitCode.Failure` (`1`); `Program.Main` returns
the value from `Cli.Run`. NFlags itself returns `255` for an unknown command or bad arguments. A
partial failure (one broken feed, one missing audio file, a non-zero downloader exit) is still a
failure, but work that succeeded is kept: the download list for the healthy feeds, the tags already
written, and (for `upgrade`) the pending list, which is only deleted when the downloader exits `0`.

## Feed parsing rules (Podcast.cs)

- RSS only: `channel/title`, `channel/item`, `pubDate`, `enclosure@url`. The `itunes` namespace is used
  for `itunes:author` and `itunes:image@href`; author falls back to `author`, then the channel title
  for `update`, and to the literal `None` for `download`/`download select`.
- The feed is **streamed** with `XmlReader`: only `channel/title`, `channel/description` and item
  fields are kept in `ParsedFeed`; each `<item>` subtree is materialized as an `XElement`, converted,
  and discarded. Do not go back to loading a whole `XmlDocument`, and do not hold `XmlNode` objects.
- `pubDate` is parsed as `DateTimeOffset` with `AssumeUniversal`, so feeds without an offset do not depend
  on the machine time zone. **All comparisons are UTC.** The watermark is the cache file's
  `GetLastWriteTimeUtc()`, read *before* the download (the download replaces the file and resets its mtime).
- An item without a readable `pubDate`, or without an `enclosure@url`, is skipped and logged. One broken
  feed never aborts the rest of the library: `CollectNewlyReleases()` returns a `FeedUpdateResult` with
  `Success`/`Error` instead of throwing.
- A feed without a `<channel>` is an error, never an empty success. `CollectNewlyReleases` puts the
  message in `FeedUpdateResult.Error`; `GetPodcastDetail`, `BuildPodcastDownloadFile`,
  `GetPodcastAllReleaseDetail` and `DownloadSelectIndexRelease` call `EnsureChannel` and throw
  `InvalidDataException`, which `Opml` turns into a failed command.
- `download` parses its `yyyyMMdd` argument as local midnight and converts it to UTC
  (`DateTime.SpecifyKind(..., Local).ToUniversalTime()`), so the cutoff matches the UTC publication
  dates. Do not compare raw local ticks with UTC dates.
- `ParsedFeed` is cached on the `Podcast` instance and invalidated after a successful download, so a
  command parses each feed at most once. `DownloadFailed` is set by `TryDownloadFeed` and is what lets
  `show --refresh` report failure even when an old cache still exists.
- `update` fetches feeds concurrently (SemaphoreSlim, at most 8 in flight). Results are printed and
  written serially afterwards, because `NewlyReleaseInfo.txt` is a single shared file. `download`
  uses the same concurrency helper to prefetch feeds that have no cache before building its list.
- XML is read with `DtdProcessing.Prohibit` and a null resolver.
- `XmlReader.ReadSubtree()` leaves the parent reader on the element's end tag; the outer `Read()` then
  advances. Preserve that pattern when editing `ParseFeed()`.

## Testing

- `PodcastDownloadManager.Tests` — 77 tests, no network: feeds are served by `Support/FakeFeedServer.cs`
  (a `TcpListener` on loopback), and `Support/TestWorkspace.cs` points every `ProgramConfiguration` path
  at a fresh temp directory per test.
- `ExitCodeTests` drives `Program.Main` end to end and asserts the process-level result; keep it in sync
  with any new failure path in a command.
- `UpgradeCommandTests` pins the UTC-safe staleness check and uses a tiny shell script as a fake
  downloader to verify that a non-zero exit keeps `NewlyReleaseInfo.txt` and a zero exit deletes it.
  Windows has no shell-script equivalent `Process.Start` runs directly, so those two tests return early
  there; Ubuntu CI covers them.
- `ConfigurationTests` covers old config files without the feed download fields; `DownloadCommandTests`
  pins the UTC date parsing; `OpmlTests` covers failed refresh, missing channel and no-match selection.
- `ProgramConfiguration.UseDirectory()` exists only for the tests (`InternalsVisibleTo`). Do not call it
  from product code. `ProgramConfiguration` paths are static, which is why
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]` is required: parallel test classes
  overwrite each other's workspace (three tests fail without it). Tests that mutate
  `DownloadConfigurations.FeedRequestTimeoutSeconds` / `FeedDownloadMaxAttempts` rely on the same rule.
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
8. **Exit codes are part of the CLI contract.** `Bootstrap.Run` returns the `int` from the command's
   `SetExecute` callback, and `Program.Main` returns that value. Every command error path must return
   `ExitCode.Failure`; NFlags returns `255` for an unknown command or bad arguments. `ExitCodeTests`
   calls `Program.Main` directly, so it exercises the real propagation.
9. **New config fields rely on property initializers.** Old `config.json` files contain only the three
   original fields; `FeedRequestTimeoutSeconds` and `FeedDownloadMaxAttempts` get their defaults from
   the property initializers in `DownloadConfiguration`. Keep those initializers, or deserializing an
   old file leaves the values at `0`. `DownloadHelper` also clamps them to the min/max constants.
10. **Feed parsing is streaming now.** `Podcast.ParseFeed()` uses `XmlReader` and materializes one
    `XElement` per `<item>`; it deliberately does not build an `XmlDocument`. `ReadSubtree()` leaves
    the parent reader on the element's end tag, so the outer `Read()` advances to the next node. If you
    change the loop, run the whole `PodcastTests`/`OpmlTests` suite: item ordering and the
    "newest first" index used by `download select` depend on it.
11. **A `Low` mandatory integrity label on the checkout breaks the tests on Windows.** If the repository
    folder (and therefore `bin/`) carries `Mandatory Label\Low Mandatory Level` — check with
    `icacls <repo> | findstr /i Mandatory` — every process started from the build output runs at Low
    integrity. A Low process cannot write to `%TEMP%` or connect to the Medium-integrity named pipe that
    `dotnet test` creates, so every test fails in `TestWorkspace..ctor` with
    `UnauthorizedAccessException`, and `dotnet test` reports an MTP "Handshake failure"
    (`NamedPipeClientStream.TryConnect`). It is a property of the **files**, not of the terminal/tool, so
    it reproduces in any shell and via Task Scheduler; copying the built exe to a normal folder or
    pointing `TEMP` inside the repo makes the tests pass. Fix with
    `icacls <repo> /setintegritylevel (OI)(CI)Medium /T`, then rebuild; after that `dotnet test` reports
    77/77 passing. A sandbox may also leave a `Everyone:(CI)(DENY)(S,DC)` ACE and an unresolvable
    `S-1-4-...` grant on the folder; those are harmless but can be removed with
    `icacls <repo> /remove:d Everyone /T` (the synthetic SID has to be removed through .NET).
12. **`ConfigCommand` registers options without defaults on purpose.** `GetOption<string>` returns
    `null` for an omitted option, so only the values the user actually supplied are validated. Do not
    add `RegisterOption(..., defaultValue)` back: then `pdlm config -ft 60` would also revalidate an
    untouched but invalid path or downloader and fail.
13. **The `download` cutoff date is local midnight converted to UTC.**
    `DownloadCommand.TryParseDate` uses `DateTime.SpecifyKind(..., Local).ToUniversalTime()` before
    comparing with the UTC `pubDate` values. Any new date input must do the same.

## Remaining work worth doing

- Split the library from the CLI, then add a GUI (the long-standing roadmap).
- The streaming parser still keeps every item's fields in `ParsedFeed.Items`. For very large feeds,
  filter items by date while parsing (in `update`/`download`) and keep only matching items.
- Extend the fake-downloader `UpgradeCommandTests` to Windows (a small compiled helper instead of a
  shell script); Ubuntu CI already covers the keep/delete behaviour.
- Consider Atom feed support if it is ever needed; the parser is RSS 2.0 only.

## Style

The codebase is older-style C# (block namespaces, explicit types, no nullable annotations) with a few
newer files. Match the file you are editing. Log through `Logger.Log`, print user output through
`IOutput`, and keep the existing message wording unless a test is updated with it. Print
user-facing errors through `output.WriteError(...)` (`OutputTools`); it colors them red only on a
real console, so redirected output and tests stay plain text.
