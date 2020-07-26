# Podcast Download Manager

![.NET Core](https://github.com/ZhukovWang/PodcastDownloadManager/workflows/.NET%20Core/badge.svg)

Podcast Download Manager is a commandline podcast manager and provides commands for managing and downloading podcast.

Allow add, remove, list, show, update and download podcast.

## Build

Use [.Net Core](https://dotnet.microsoft.com/) to build.

```bash
$ cd PodcastDownloadManager
$ dotnet build
```

## Usage

### prepare

Install [.Net Core runtime](https://dotnet.microsoft.com/) and [aria2](https://aria2.github.io/).

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

### download

Download the podcast newly release before a specific date. Use [aria2](https://aria2.github.io/).

I know this is stupid, so when this have a gui, will change to a list can check download items.

```bash
$ pdlm download 20200701
```

## LICENSE

MIT
