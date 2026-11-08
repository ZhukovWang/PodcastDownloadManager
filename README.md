# Podcast Download Manager

**English** | [简体中文](README.zh-CN.md)

[![.NET Core](https://github.com/ZhukovWang/PodcastDownloadManager/workflows/.NET%20Core/badge.svg)](https://github.com/ZhukovWang/PodcastDownloadManager/actions/workflows/dotnet-core.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

`pdlm` is a command-line podcast manager. It keeps a library of shows in an
[OPML](https://opml.org/) file, refreshes their RSS feeds, and turns new episodes into download
jobs for [aria2](https://aria2.github.io/) or
[Internet Download Manager (IDM)](https://www.internetdownloadmanager.com/). Once the audio has
arrived, it can write episode metadata and cover art into the files.

`pdlm` does not download audio itself. The transfer is performed by aria2 or IDM, so resume
support, connection count, speed limits and retries stay in the downloader you already use.

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Build](#build)
- [Quick start](#quick-start)
- [How it works](#how-it-works)
- [Commands](#commands)
- [Configuration](#configuration)
- [Files and logs](#files-and-logs)
- [Feed support and naming](#feed-support-and-naming)
- [Development](#development)
- [Roadmap](#roadmap)
- [License](#license)

## Features

- **A simple library.** Add, remove, list and inspect podcasts from the terminal. The library is a
  plain OPML file that other podcast tools can read.
- **Incremental updates.** Each feed is cached, and `update` only queues episodes published after
  the previous refresh instead of downloading the whole back catalog.
- **Parallel feed refresh.** All feeds are refreshed concurrently (up to 8 at a time), so a large
  library costs about as much time as its slowest feed, not the sum of all of them.
- **Two download modes.** Hand a download list to aria2 or IDM, or write a plain URL list for any
  other download tool with `--simple-file`.
- **Backfill and cherry-picking.** Download everything since a given date, or list a show's
  episodes and select individual ones by index.
- **Automatic tagging.** Episode metadata and cover art are written into the audio files with
  [TagLib#](https://github.com/mono/taglib-sharp), using the metadata collected when episodes were
  queued.
- **Resilient feed fetching.** Transient HTTP errors are retried, redirects are followed, gzip and
  brotli are decoded, and a failed transfer never replaces a usable cached feed. The timeout and
  retry count are configurable.
- **Script-friendly results.** Commands return `0` on success and a non-zero exit code when input is
  invalid or any feed, downloader or tagging operation fails.

## Requirements

- [.NET 10](https://dotnet.microsoft.com/) SDK to build; the .NET 10 runtime is enough to run a
  published build.
- A writable directory for `pdlm`'s state. Files are stored next to the executable (see
  [Files and logs](#files-and-logs)), so avoid read-only locations such as `Program Files`.
- Optional: [aria2](https://aria2.github.io/) (`aria2c`) or
  [IDM](https://www.internetdownloadmanager.com/) for automatic downloads. IDM is Windows-only;
  aria2 runs on Windows, macOS and Linux.
- Optional: for `autometadata`, an audio format supported by TagLib# (MP3, M4A/AAC, FLAC, OGG,
  and others).

## Build

```bash
git clone https://github.com/ZhukovWang/PodcastDownloadManager.git
cd PodcastDownloadManager
dotnet build --configuration Release
```

The assembly is named `pdlm`, so the build output is:

```
PodcastDownloadManager/bin/Release/net10.0/pdlm       # pdlm.exe on Windows
```

Run it directly, or run from source while developing:

```bash
dotnet run --project PodcastDownloadManager -- --help
```

## Quick start

```bash
# 1. Point pdlm at an existing directory and choose a downloader.
pdlm config --download-path "D:\Podcasts" --download-program Aria2 --download-program-path aria2c

# 2. Add a show. pdlm reads the feed once to learn the channel title.
pdlm add https://example.com/feed.xml

# 3. Check the library.
pdlm list

# 4. Refresh all feeds and queue episodes newer than the last refresh.
pdlm update

# 5. Download what update queued.
pdlm upgrade

# 6. Write tags and cover art into the downloaded files.
pdlm autometadata
```

Things worth knowing before the first run:

- `pdlm` uses the channel title as the show name, with spaces and invalid filename characters
  removed: `My Great Show` becomes `MyGreatShow`. Use that name with `show`, `remove`,
  `download list` and `download select`; `pdlm list` prints it.
- The first `pdlm update` after `add` only seeds the feed cache. There is no earlier refresh to
  compare against, so it reports nothing. Episodes published after that point are queued on later
  runs.
- To fetch older episodes, use `pdlm download <date>` (for example `pdlm download 20240101`), or
  list a show and select episodes by index.
- `pdlm config --download-path` only accepts a directory that already exists. Create it first if
  needed; `pdlm download` creates its own `--dir` directory when it is missing.

## How it works

```
add ────────► podcasts.opml                        the library
                   │
update ────────────┴──► PodcastsFile/<name>.xml    cached feed (mtime = watermark)
                   │
                   ├──► NewlyReleaseInfo.txt       download list for upgrade
                   └──► <download dir>/*.metadata  sidecars for tagging
                             │
upgrade ─────────────────────┴──► aria2 / IDM ──► audio files
                                                       │
autometadata ──────────────────────────────────────────┴──► tags + cover art
```

Every stage is independent and can be re-run. `update` prints the new releases as it finds them,
and a broken feed is reported without stopping the rest of the library. `upgrade` consumes the
download list that `update` produced; if the list is missing or older than a day, it refreshes the
feeds first, but running `update` explicitly keeps the two steps easy to inspect.

## Commands

Global options: `-h` / `--help`, `-v` / `--version`.

| Command | Description |
| --- | --- |
| `pdlm add <url>` | Add a podcast from its RSS feed URL. |
| `pdlm remove <name>` | Remove a podcast from the library. |
| `pdlm list` | List the podcasts in the library. |
| `pdlm show <name>` | Show a podcast and its newest release. |
| `pdlm update` | Refresh every feed and queue new episodes. |
| `pdlm upgrade` | Download the episodes queued by `update`, refreshing a stale list first. |
| `pdlm download [<date>]` | Build a download list for releases after `<date>` and download it. |
| `pdlm download list <name>` | List one podcast's releases with 1-based indexes. |
| `pdlm download select <name> <indexes>` | Download selected releases of one podcast. |
| `pdlm autometadata` | Write tags and cover art into downloaded audio files. |
| `pdlm config` | Set and validate configuration values. |

### `pdlm add <url>`

Reads the feed, derives the show name from the channel title, and stores the show in
`podcasts.opml`. The URL must use HTTP or HTTPS. If the feed cannot be read, no show is added.

```bash
pdlm add https://example.com/feed.xml
```

### `pdlm remove <name>`

Removes the show from `podcasts.opml`. The cached feed and already downloaded episodes are left
alone, so removing a show does not delete files from disk.

```bash
pdlm remove MyGreatShow
```

### `pdlm list`

Prints every show as `* Name: <name>  Url: <feed url>`.

```bash
pdlm list
```

### `pdlm show <name>`

Prints the channel title, description and newest release. The cached feed is used by default;
`--refresh` / `-r` downloads the feed first. When there is no cached feed, it is downloaded
automatically. If a requested refresh fails, the command exits with a failure code instead of
silently presenting the stale cache as current.

```bash
pdlm show MyGreatShow
pdlm show MyGreatShow --refresh
```

### `pdlm update`

The main workhorse. Refreshes every feed in parallel, prints newly found episodes, appends them to
the download list used by `upgrade`, and writes the metadata sidecars. A failing feed is reported
and skipped; the rest of the library still updates.

```bash
pdlm update
```

The first update of a show without a cached feed only fills the cache. The cache file's last write
time is the "last update" watermark, so touching or deleting `PodcastsFile/<name>.xml` changes
which episodes count as new.

### `pdlm upgrade`

Passes the pending download list to aria2 or IDM, then deletes the list. If the list is missing or
older than a day, all feeds are refreshed first to build a new one. When the downloader exits with
a non-zero code, the list is kept so the download can be retried.

```bash
pdlm upgrade
```

Running `pdlm update` first is still useful when you want to review newly found episodes before
handing anything to the downloader.

### `pdlm download [<date>]`

Builds a download list from the cached feeds for every episode published after `<date>` (format
`yyyyMMdd`, default `20210101`), writes it to `<download dir>/PodcastDownload.txt`, and starts the
configured downloader. `<date>` is treated as midnight in the machine's local time zone and converted
to UTC, the same clock used for publication dates.

```bash
pdlm download 20240101
pdlm download 20240101 --dir "D:\Podcasts\Archive"
pdlm download 20240101 --simple-file
```

- `-d`, `--dir <dir>`: download directory. Defaults to `DownloadPodcastPath` from the
  configuration; the directory is created if it does not exist.
- `-s`, `--simple-file`: write one URL per line and do not start a downloader. Use this to feed
  another download tool. Metadata sidecars are not written in this mode.
- Feeds with no cache are downloaded in parallel before the list is built. Cached feeds are not
  refreshed by this command, so run `pdlm update` first if you want the latest episodes included.
- A feed that cannot be downloaded or parsed is reported and skipped, and the command exits with a
  failure code; the list still contains the podcasts that worked.

### `pdlm download list <name>`

Lists every release of one show, numbered from 1 in feed order:

```bash
$ pdlm download list MyGreatShow
* Episode one - 2024-01-02 10:00:00
* Episode two - 2023-12-20 09:30:00
```

### `pdlm download select <name> <indexes>`

Downloads selected releases. Indexes are 1-based, as printed by `download list`, and are separated
by semicolons:

```bash
pdlm download select MyGreatShow 1;3;5
```

The download list is written to `<download dir>/PodcastDownload.txt` and passed to the configured
downloader. Metadata sidecars are written for the selected episodes. If none of the indexes match a
downloadable release, the command exits with a failure code and does not start the downloader.

### `pdlm autometadata`

Scans the configured download directory for `*.metadata` sidecars. For each sidecar whose audio
file exists, it writes these tags and then deletes the sidecar:

| Tag | Value |
| --- | --- |
| Title | Episode title |
| Artist / Album artist / Composer | Feed author (`author`, then `itunes:author`; episodes queued by `update` fall back to the channel title) |
| Album | Channel title |
| Genre | `Podcast` |
| Date tagged | Publication date |
| Comment | Episode description, with HTML tags stripped |
| Cover art | Item-level `itunes:image`, downloaded and embedded when present |

```bash
pdlm autometadata
```

If the audio file is missing, the sidecar is reported and kept, so it can be applied after the
download finishes. Any sidecar that cannot be applied makes the command exit with a failure code.

### `pdlm config`

Sets and validates configuration values. Invalid input prints an error and keeps the previous
value; the configuration file is still saved with the resulting settings.

| Option | Alias | Meaning |
| --- | --- | --- |
| `--download-path <dir>` | `-p` | Directory that episodes are saved to. Must already exist. |
| `--download-program <name>` | `-dp` | External downloader: exactly `Aria2` or `IDM`. |
| `--download-program-path <path>` | `-dpp` | Downloader executable, or a name found on `PATH`. On Windows, `.exe` is appended when missing. |
| `--feed-timeout <seconds>` | `-ft` | Timeout of one feed download attempt, from 1 to 600 seconds. |
| `--feed-retries <attempts>` | `-fr` | Total feed download attempts, including the first, from 1 to 10. |

```bash
pdlm config -p "D:\Podcasts"
pdlm config -dp Aria2 -dpp aria2c
pdlm config -dp IDM -dpp "C:\Program Files (x86)\Internet Download Manager\IDMan.exe"
pdlm config -ft 60 -fr 4
```

### Exit status

| Code | Meaning |
| --- | --- |
| `0` | Every requested operation succeeded. |
| `1` | The input was invalid, or at least one feed, downloader or tagging operation failed. Work that did succeed is kept on disk. |
| other | Argument parsing errors reported by the CLI framework; treat any non-zero value as failure. |

`add`, `remove`, `show` and `config` report input and feed errors this way. `show` also fails when
`--refresh` cannot download the feed. `update` returns `1` when any feed failed, even if other feeds
updated successfully. `download` still writes the list for the podcasts that worked, then returns
`1` if any feed failed or the downloader exited non-zero. `download select` returns `1` when no
selected index matches. `autometadata` returns `1` if any sidecar could not be applied.

## Configuration

The configuration is a JSON file named `config.json` in the same directory as the executable. It is
created automatically on the first run:

```json
{
  "DownloadPodcastPath": "D:\\Podcasts",
  "DownloadProgram": "Aria2",
  "DownloadProgramPathName": "aria2c",
  "FeedRequestTimeoutSeconds": 30,
  "FeedDownloadMaxAttempts": 3
}
```

| Field | Default | Notes |
| --- | --- | --- |
| `DownloadPodcastPath` | `<executable folder>/Download` | Where `update` writes metadata sidecars and where downloads are saved. |
| `DownloadProgram` | `Aria2` | `Aria2` or `IDM`, case-sensitive. |
| `DownloadProgramPathName` | `aria2c` | Full path, or a name that can be found on `PATH`. |
| `FeedRequestTimeoutSeconds` | `30` | Timeout for one feed attempt, in seconds (1-600). |
| `FeedDownloadMaxAttempts` | `3` | Total feed attempts, including the first (1-10). |

Configuration files written by older versions do not contain the two feed download fields; they
fall back to the defaults above.

`pdlm config` writes all configuration values each time. Options you do not pass keep their current
value and are not revalidated, so `pdlm config -ft 60` does not fail because an unrelated saved path
or downloader is missing. Options you do pass are validated.

The default `Download` directory is created on the first run. If you point `DownloadPodcastPath` at
another directory yourself, that directory must already exist when you run `pdlm config -p`.

Aria2 is started as `aria2c -i <list>`. IDM is given one `/a /d ... /p ... /f ...` command per
episode and then started with `/s`. Keep the downloader's own configuration (connections, proxy,
speed limit) in that program.

## Files and logs

All state lives in the directory containing the `pdlm` executable. When running from source with
`dotnet run`, that is the project's `bin/<Configuration>/net10.0` directory.

| Path | Contents |
| --- | --- |
| `config.json` | Configuration, managed by `pdlm config`. |
| `podcasts.opml` | The library: one `<outline text="<name>" xmlUrl="<feed url>" />` per show. |
| `PodcastsFile/<name>.xml` | Cached feed. Its last write time is the update watermark. |
| `PodcastsFile/NewPodcast.xml` | Temporary file used while reading the title of a newly added feed. |
| `NewlyReleaseInfo.txt` | Pending download list for `upgrade`; deleted after the downloader succeeds and kept when it fails. |
| `<download dir>/PodcastDownload.txt` | Download list built by `download` and `download select`. |
| `<download dir>/<episode>.metadata` | Seven-line metadata sidecar, consumed by `autometadata`. |
| `logs/<date>_Log.log` | NLog output, one file per day. |

The show name doubles as the key in `podcasts.opml` and as the cache filename. Renaming a show in
the feed does not rename what is already in the library, and a hand-edited name will not find the
old cache file. Use `pdlm list` to see the exact names.

Feeds are downloaded to a temporary `.part` file and only moved into place after the transfer
succeeds, so a failed transfer keeps the previous cache intact. Keep that property in mind if you
clean up the state directory by hand.

## Feed support and naming

- RSS 2.0 only; Atom feeds are not supported.
- Read from each feed: `channel/title`, `channel/description`, `item/title`, `item/pubDate`,
  `item/enclosure@url`, `item/author`, `item/description`, `itunes:author` and `itunes:image@href`.
- Items without a readable `pubDate` or without an `enclosure` URL are skipped and logged.
- Dates are compared in UTC. A feed date without a time zone offset is treated as UTC.
- Episode files are named `<show> - <yyyy_MM_dd> - <episode>.<ext>`, with invalid filename
  characters removed. The extension is `.m4a` when the enclosure URL contains `.m4a`; otherwise it
  is `.mp3`.
- HTML tags are stripped from descriptions before they are stored in metadata.
- Feeds are streamed and only the fields the program needs are kept, so the whole XML document is
  not held in memory. A feed is parsed at most once per command.

## Development

Project layout:

| Path | Contents |
| --- | --- |
| `PodcastDownloadManager/Program.cs` | Entry point: creates the state directory, loads configuration, starts NFlags. |
| `PodcastDownloadManager/RootCommand.cs` | Registers the nine commands. |
| `PodcastDownloadManager/Commands/` | One file per command. |
| `PodcastDownloadManager/DownloadHelper.cs` | The only HTTP client: feed download, retries, atomic replace. |
| `PodcastDownloadManager/DownloadTools.cs` | Starts aria2 or IDM and forwards their output. |
| `PodcastDownloadManager/Podcast/Podcast.cs` | Feed parsing, new-release detection, download-list entries. |
| `PodcastDownloadManager/Podcast/Opml.cs` | The OPML library and library-wide operations. |
| `PodcastDownloadManager/FileMetadata/AudioMetadata.cs` | Metadata sidecars and TagLib# tagging. |
| `PodcastDownloadManager.Tests/` | xunit v3 test suite. |

Build and test from the repository root:

```bash
dotnet build
dotnet run --project PodcastDownloadManager.Tests
```

The suite contains 76 xunit v3 tests on
[Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro).
The test project builds to an executable that runs its own tests, so running it directly is a
complete test run and needs no test host. The tests never reach the network: feeds are served by a
loopback HTTP server and every test uses its own temporary directory.

`dotnet test` drives the same executable when the SDK can open the named pipe to the test process.
On machines that block that handshake, use `dotnet run --project PodcastDownloadManager.Tests`
instead; the failure is an environment limitation, not a test failure.

CI (`.github/workflows/dotnet-core.yml`) restores, builds Release, and runs the test executable on
Ubuntu and Windows.

For implementation notes, data formats and non-obvious gotchas, see [AGENT.md](AGENT.md).

## Roadmap

- Separate the library from the command-line interface so it can be reused by other front ends.
- Add a GUI.

The feed timeout and retry policy, parallel feed fetching in every command, single-parse feed
handling and per-command exit codes are implemented.

## License

[MIT](LICENSE) © 2020 ZhukovWang
