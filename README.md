# Podcast Download Manager

![.NET Core](https://github.com/ZhukovWang/PodcastDownloadManager/workflows/.NET%20Core/badge.svg)

Podcast Download Manager is a commandline podcast manager and provides commands for managing and downloading podcast.

Allow add, remove, list, show, update, upgrade and download podcast.

Support [Aria2](https://aria2.github.io/) and [Internet Download Manager](http://www.internetdownloadmanager.com/) automatic download.

## Build

Use [.Net Core](https://dotnet.microsoft.com/) to build.

```bash
$ cd PodcastDownloadManager
$ dotnet build
```

## Usage

### Prepare

Install [.Net Core runtime](https://dotnet.microsoft.com/).

Install [Aria2](https://aria2.github.io/) or [Internet Download Manager](http://www.internetdownloadmanager.com/) for automatic download.

### Typical use

```bash
# config
$ pdlm config --download-path "path\to\download"
$ pdlm config --download-program "Aria2"
$ pdlm config --download-program-path "path\to\download\program"

# add podcast
$ pdlm add http://path/to/podcast

# update
$ pdlm update
$ pdlm upgrade
$ pdlm autometadata
```

## Command

### add

Add a podcast through url.

```bash
$ pdlm add http://path/to/podcast
```

### remove

Remove a podcast through name.

```bash
$ pdlm remove podcast_name
```

### show

Display information about a podcast and newly release.

```bash
$ pdlm show podcast_name
```

### list

List added podcasts.

```bash
$ pdlm list
```

### update

Update the podcast newly release.

```bash
$ pdlm update
```

### upgrade

Download the podcast newly release since last update.

```bash
$ pdlm upgrade
```

### autometadata

Automatically add audio metadata to podcast file, when the podcast file is all successful download.

```bash
$ pdlm autometadata
```

### download

Download the podcast specific release.

```bash
$ pdlm download list PodcastName # list all the PodcastName release
1. title1
2. title2
...

$ pdlm download select PodcastName 1;2 # download the PodcastName release no.1 and no.2
```

### config

Set configuration values. Can set `DownloadPodcastPath`, `DownloadProgram`, `DownloadProgramPathName`.

`DownloadProgram` only support [Aria2](https://aria2.github.io/) and [Internet Download Manager](http://www.internetdownloadmanager.com/).

The config file can find in `PodcastDownloadManager\config.json`.

If set `DownloadProgram`, `DownloadProgramPathName` MUST be suitable.

```bash
# set DownloadPodcastPath
$ pdlm config --download-path "path\to\download"
# or
$ pdlm config -p "path\to\download"

# set DownloadProgram to Aria2
$ pdlm config --download-program "Aria2"
# or
$ pdlm config -dp "Aria2"

# set DownloadProgram to IDM
$ pdlm config --download-program "IDM"
# or
$ pdlm config -dp "IDM"

# set DownloadProgramPathName
$ pdlm config --download-program-path "path\to\download\program"
# or
$ pdlm config -dpp "path\to\download\program"
```

## Roadmap

- change project to c/c++ and separate command-line interface and library.
- add a GUI

## LICENSE

MIT
