# Podcast Download Manager（pdlm）

[English](README.md) | **简体中文**

[![.NET Core](https://github.com/ZhukovWang/PodcastDownloadManager/workflows/.NET%20Core/badge.svg)](https://github.com/ZhukovWang/PodcastDownloadManager/actions/workflows/dotnet-core.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4.svg)](https://dotnet.microsoft.com/)

`pdlm` 是一个命令行播客管理器。它用 [OPML](https://opml.org/) 文件维护节目库，刷新节目的
RSS 源，并把新发布的单集转换成 [aria2](https://aria2.github.io/) 或
[Internet Download Manager（IDM）](https://www.internetdownloadmanager.com/) 的下载任务；音频下载完成后，
还可以把单集信息和封面写入音频文件。

`pdlm` 本身不下载音频。实际传输由 aria2 或 IDM 完成，因此断点续传、连接数、限速和重试策略都
沿用你正在使用的下载器的配置。

## 目录

- [功能](#功能)
- [环境要求](#环境要求)
- [构建](#构建)
- [快速开始](#快速开始)
- [工作原理](#工作原理)
- [命令](#命令)
- [配置](#配置)
- [文件与日志](#文件与日志)
- [RSS 源支持与命名](#rss-源支持与命名)
- [开发](#开发)
- [路线图](#路线图)
- [许可证](#许可证)

## 功能

- **简单的节目库。** 在终端里添加、删除、列出和查看播客。节目库就是一个普通的 OPML 文件，
  其他播客工具也能读取。
- **增量更新。** 每个源都会被缓存，`update` 只把上次刷新之后发布的单集加入队列，不会重复下载
  整个历史目录。
- **并行刷新。** 所有源会同时刷新（最多 8 个），所以节目很多时，总耗时接近最慢的那个源，而不是
  所有源耗时之和。
- **两种下载方式。** 把任务列表交给 aria2 或 IDM；也可以加 `--simple-file`，只输出纯 URL 列表，
  交给任意其他下载工具。
- **按日期补下载和按集挑选。** 可以下载某个日期之后的所有单集，也可以列出某个节目的单集后按序号
  挑选。
- **自动写入标签。** 借助 [TagLib#](https://github.com/mono/taglib-sharp)，把入队时收集的单集信息
  和封面写入音频文件。
- **稳健的源下载。** 临时性 HTTP 错误会自动重试，支持重定向、gzip 和 brotli；传输失败不会破坏
  已有的缓存。超时和重试次数都可以配置。
- **脚本友好的结果。** 命令成功时返回 `0`；输入非法，或任一源、下载器、标签写入操作失败时返回
  非零退出码。

## 环境要求

- 构建需要 [.NET 10](https://dotnet.microsoft.com/) SDK；运行已构建的版本需要 .NET 10 运行时。
- 一个可写目录用于保存 `pdlm` 的状态。状态文件放在可执行文件旁边（见
  [文件与日志](#文件与日志)），因此不要放在 `Program Files` 这类只读位置。
- 可选：[aria2](https://aria2.github.io/)（`aria2c`）或
  [IDM](https://www.internetdownloadmanager.com/)，用于自动下载。IDM 仅支持 Windows；aria2 可在
  Windows、macOS 和 Linux 上使用。
- 可选：`autometadata` 需要 TagLib# 支持的音频格式（MP3、M4A/AAC、FLAC、OGG 等）。

## 构建

```bash
git clone https://github.com/ZhukovWang/PodcastDownloadManager.git
cd PodcastDownloadManager
dotnet build --configuration Release
```

程序集名称是 `pdlm`，构建产物为：

```
PodcastDownloadManager/bin/Release/net10.0/pdlm       # Windows 下是 pdlm.exe
```

可以直接运行产物，也可以在开发时从源码运行：

```bash
dotnet run --project PodcastDownloadManager -- --help
```

## 快速开始

```bash
# 1. 指向一个已存在的下载目录，并选择下载器。
pdlm config --download-path "D:\Podcasts" --download-program Aria2 --download-program-path aria2c

# 2. 添加节目。pdlm 会读取一次源以获取频道标题。
pdlm add https://example.com/feed.xml

# 3. 查看节目库。
pdlm list

# 4. 刷新所有源，把上次刷新之后发布的单集加入队列。
pdlm update

# 5. 下载 update 排入队列的内容。
pdlm upgrade

# 6. 把标签和封面写入已下载的文件。
pdlm autometadata
```

第一次使用前需要知道的几点：

- `pdlm` 把频道标题作为节目名，并去掉其中的空格和非法文件名字符：`My Great Show` 会变成
  `MyGreatShow`。`show`、`remove`、`download list` 和 `download select` 都要使用这个名字，
  `pdlm list` 会显示出来。
- `add` 之后的第一次 `pdlm update` 只会建立源缓存。此时还没有上一次刷新的时间作为比较基准，
  所以不会报告新单集；之后再发布的单集才会在后续运行中加入队列。
- 想补下以前的单集，可以用 `pdlm download <date>`（例如 `pdlm download 20240101`），也可以先
  列出某个节目的单集，再按序号选择。
- `pdlm config --download-path` 只接受已经存在的目录，需要的话请先创建；`pdlm download` 的
  `--dir` 目录不存在时会自动创建。

## 工作原理

```
add ────────► podcasts.opml                        节目库
                   │
update ────────────┴──► PodcastsFile/<name>.xml    源缓存（文件修改时间 = 更新基准）
                   │
                   ├──► NewlyReleaseInfo.txt       给 upgrade 用的下载列表
                   └──► <下载目录>/*.metadata       待写入的元数据附属文件
                             │
upgrade ─────────────────────┴──► aria2 / IDM ──► 音频文件
                                                       │
autometadata ──────────────────────────────────────────┴──► 标签 + 封面
```

每个阶段都是独立的，可以反复执行。`update` 发现新单集时会立即打印，某个源出错也不会中断整个
节目库的更新。`upgrade` 使用 `update` 生成的下载列表；如果列表不存在或超过一天，它会先刷新所有
源。不过先显式运行 `update` 仍然更方便检查将要下载的内容。

## 命令

全局选项：`-h` / `--help`，`-v` / `--version`。每次执行命令前会先打印构建版本号；
`-v` / `--version` 只输出版本号本身。版本号来自 `git describe --tags --always --dirty`，编译时
注入；不在 git 仓库中时回退为 `1.0.0.0`。

| 命令 | 说明 |
| --- | --- |
| `pdlm add <url>` | 通过 RSS 源地址添加播客。 |
| `pdlm remove <name>` | 从节目库中删除播客。 |
| `pdlm list` | 列出节目库中的播客。 |
| `pdlm show <name>` | 显示某个播客及其最新单集。 |
| `pdlm update` | 刷新所有源，把新单集加入队列。 |
| `pdlm upgrade` | 下载 `update` 排入队列的单集；列表过期时会先刷新源。 |
| `pdlm download [<date>]` | 为指定日期之后的单集生成下载列表并下载。 |
| `pdlm download list <name>` | 列出某个播客的全部单集及从 1 开始的序号。 |
| `pdlm download select <name> <indexes>` | 下载某个播客中选中的单集。 |
| `pdlm autometadata` | 把标签和封面写入已下载的音频文件。 |
| `pdlm config` | 设置并校验配置项。 |

### `pdlm add <url>`

读取 RSS 源，从频道标题生成节目名，并把节目写入 `podcasts.opml`。URL 必须是 HTTP 或 HTTPS。
如果源无法读取，则不会添加节目。

```bash
pdlm add https://example.com/feed.xml
```

### `pdlm remove <name>`

从 `podcasts.opml` 中删除节目。源缓存和已下载的单集不会被删除，所以删除节目不会清理磁盘上的
文件。

```bash
pdlm remove MyGreatShow
```

### `pdlm list`

每个节目输出一行：`* Name: <name>  Url: <feed url>`。

```bash
pdlm list
```

### `pdlm show <name>`

显示频道标题、描述和最新一集。默认使用缓存的源；加 `--refresh` / `-r` 会先下载源。没有缓存时
会自动下载。如果请求的刷新失败，命令会以失败状态退出，而不会把旧缓存当作最新内容继续显示。

```bash
pdlm show MyGreatShow
pdlm show MyGreatShow --refresh
```

### `pdlm update`

最核心的命令。并行刷新所有源，打印新发现的单集，把它们追加到 `upgrade` 使用的下载列表，并写入
元数据附属文件。某个源失败时会报告并跳过，节目库的其余部分照常更新。

```bash
pdlm update
```

某个节目如果没有源缓存，第一次更新只会建立缓存。缓存文件的最后修改时间是「上次更新」的基准，
因此修改或删除 `PodcastsFile/<name>.xml` 会改变哪些单集算作新单集。

### `pdlm upgrade`

把待处理的下载列表交给 aria2 或 IDM，然后删除列表。如果列表不存在或超过一天，会先刷新所有源并
生成新列表。下载器以非零状态退出时会保留列表，方便重试。

```bash
pdlm upgrade
```

如果希望在交给下载器之前检查新发现的单集，仍然可以先运行 `pdlm update`。

### `pdlm download [<date>]`

从缓存的源中找出 `<date>` 之后发布的单集，生成下载列表写入
`<下载目录>/PodcastDownload.txt`，然后启动配置的下载器。日期格式为 `yyyyMMdd`，默认
`20210101`。`<date>` 按本机时区的零点解释，再转换为 UTC，与发布日期的比较基准一致。

```bash
pdlm download 20240101
pdlm download 20240101 --dir "D:\Podcasts\Archive"
pdlm download 20240101 --simple-file
```

- `-d`、`--dir <dir>`：下载目录。默认使用配置中的 `DownloadPodcastPath`；目录不存在时会自动
  创建。
- `-s`、`--simple-file`：每行只写一个 URL，不启动下载器，方便交给其他下载工具。此模式不会写入
  元数据附属文件。
- 没有缓存的源会在生成列表前并行下载；已有缓存的源不会在这个命令中刷新。如果需要包含最新单集，
  请先运行 `pdlm update`。
- 无法下载或解析的源会报告并跳过，命令以失败状态退出；列表中仍会包含成功的节目。

### `pdlm download list <name>`

按源中的顺序列出某个节目的所有单集，序号从 1 开始：

```bash
$ pdlm download list MyGreatShow
* Episode one - 2024-01-02 10:00:00
* Episode two - 2023-12-20 09:30:00
```

### `pdlm download select <name> <indexes>`

下载选中的单集。序号从 1 开始，与 `download list` 的输出一致，多个序号用分号分隔：

```bash
pdlm download select MyGreatShow 1;3;5
```

下载列表写入 `<下载目录>/PodcastDownload.txt`，并交给配置的下载器。选中的单集会写入元数据附属
文件。如果没有任何序号匹配到可下载的单集，命令会以失败状态退出，并且不会启动下载器。

### `pdlm autometadata`

扫描配置的下载目录中的 `*.metadata` 附属文件。对每个能找到对应音频文件的附属文件，写入以下
标签，然后删除附属文件：

| 标签 | 值 |
| --- | --- |
| Title（标题） | 单集标题 |
| Artist / Album artist / Composer（艺术家等） | 源中的作者（依次取 `author`、`itunes:author`；由 `update` 入队时缺失则回退到频道标题） |
| Album（专辑） | 频道标题 |
| Genre（类型） | `Podcast` |
| Date tagged（标记日期） | 发布日期 |
| Comment（备注） | 单集描述，已去除 HTML 标签 |
| 封面 | 单集级 `itunes:image`，存在时下载并嵌入 |

```bash
pdlm autometadata
```

如果音频文件不存在，会报告并保留附属文件，等下载完成后再处理。任一附属文件无法应用时，命令会以
失败状态退出。

### `pdlm config`

设置并校验配置项。输入非法时会打印错误并保留原值；配置文件仍会以最终结果保存。

| 选项 | 别名 | 含义 |
| --- | --- | --- |
| `--download-path <dir>` | `-p` | 单集保存目录，必须是已存在的目录。 |
| `--download-program <name>` | `-dp` | 外部下载器，只能是 `Aria2` 或 `IDM`（区分大小写）。 |
| `--download-program-path <path>` | `-dpp` | 下载器可执行文件路径，或能在 `PATH` 中找到的名称。Windows 下缺少 `.exe` 时会自动补上。 |
| `--feed-timeout <seconds>` | `-ft` | 单次源下载的超时时间，1 到 600 秒。 |
| `--feed-retries <attempts>` | `-fr` | 源下载的总尝试次数（包含第一次），1 到 10 次。 |

```bash
pdlm config -p "D:\Podcasts"
pdlm config -dp Aria2 -dpp aria2c
pdlm config -dp IDM -dpp "C:\Program Files (x86)\Internet Download Manager\IDMan.exe"
pdlm config -ft 60 -fr 4
```

### 退出码

| 退出码 | 含义 |
| --- | --- |
| `0` | 所有请求的操作都成功完成。 |
| `1` | 输入非法，或至少一个源、下载器、标签写入操作失败。已经成功完成的工作仍会保留在磁盘上。 |
| 其他 | 命令行参数解析器报告的错误；任何非零值都应视为失败。 |

`add`、`remove`、`show` 和 `config` 通过这种方式报告输入和源错误。`show` 在 `--refresh` 无法
下载源时也会失败。`update` 在任一源失败时返回 `1`，即使其他源更新成功。`download` 仍会为成功的
节目生成列表，然后在该源失败或下载器非零退出时返回 `1`。`download select` 在没有任何序号匹配时
返回 `1`。`autometadata` 在任一附属文件无法应用时返回 `1`。

在交互式终端中，面向用户的错误会以红色输出；输出被重定向时保持纯文本，不写入颜色转义字符。

## 配置

配置文件是 `config.json`，位于可执行文件所在目录，首次运行时自动创建：

```json
{
  "DownloadPodcastPath": "D:\\Podcasts",
  "DownloadProgram": "Aria2",
  "DownloadProgramPathName": "aria2c",
  "FeedRequestTimeoutSeconds": 30,
  "FeedDownloadMaxAttempts": 3
}
```

| 字段 | 默认值 | 说明 |
| --- | --- | --- |
| `DownloadPodcastPath` | `<可执行文件目录>/Download` | `update` 写入元数据附属文件的位置，也是下载保存位置。 |
| `DownloadProgram` | `Aria2` | `Aria2` 或 `IDM`，区分大小写。 |
| `DownloadProgramPathName` | `aria2c` | 完整路径，或能在 `PATH` 中找到的名称。 |
| `FeedRequestTimeoutSeconds` | `30` | 单次源下载的超时时间，单位为秒（1-600）。 |
| `FeedDownloadMaxAttempts` | `3` | 源下载的总尝试次数，包含第一次（1-10）。 |

旧版本写出的配置文件没有这两个源下载字段，此时会使用上面的默认值。

`pdlm config` 每次都会写入全部配置值。没有传入的选项会保留当前值，并且不会重新校验，所以
`pdlm config -ft 60` 不会因为无关的已保存路径或下载器不存在而失败；显式传入的选项会正常校验。

默认的 `Download` 目录会在首次运行时自动创建。如果你自己把 `DownloadPodcastPath` 指向其他目录，
使用 `pdlm config -p` 时该目录必须已经存在。

aria2 以 `aria2c -i <列表>` 启动。IDM 会先为每个单集执行一条
`/a /d ... /p ... /f ...` 命令，再以 `/s` 启动。连接数、代理、限速等设置请留在下载器自己的
配置中。

## 文件与日志

所有状态都保存在 `pdlm` 可执行文件所在目录。从源码用 `dotnet run` 运行时，该目录是项目的
`bin/<Configuration>/net10.0`。

| 路径 | 内容 |
| --- | --- |
| `config.json` | 配置，由 `pdlm config` 管理。 |
| `podcasts.opml` | 节目库：每个节目一个 `<outline text="<名称>" xmlUrl="<源地址>" />`。 |
| `PodcastsFile/<name>.xml` | 缓存的源。其最后修改时间是更新基准。 |
| `PodcastsFile/NewPodcast.xml` | 添加新节目时读取标题用的临时文件。 |
| `NewlyReleaseInfo.txt` | `upgrade` 待处理的下载列表；下载器成功后删除，失败时保留。 |
| `<下载目录>/PodcastDownload.txt` | `download` 和 `download select` 生成的下载列表。 |
| `<下载目录>/<单集>.metadata` | 七行的元数据附属文件，供 `autometadata` 使用。 |
| `logs/<date>_Log.log` | NLog 日志，每天一个文件。 |

节目名同时是 `podcasts.opml` 中的键和缓存文件名。源中的频道标题改了，不会自动重命名节目库中
已有的条目；手动改过的名字也找不到旧的缓存文件。请用 `pdlm list` 查看准确的名称。

源会先下载到临时的 `.part` 文件，传输成功后才移动到目标位置，因此传输失败会保留原来的缓存。
手动清理状态目录时请留意这一点。

## RSS 源支持与命名

- 仅支持 RSS 2.0，不支持 Atom。
- 从源中读取：`channel/title`、`channel/description`、`item/title`、`item/pubDate`、
  `item/enclosure@url`、`item/author`、`item/description`、`itunes:author` 和
  `itunes:image@href`。
- 无法解析 `pubDate` 或没有 `enclosure` URL 的单集会跳过并记录日志。
- 日期按 UTC 比较。没有时区信息的源日期会当作 UTC 处理。
- 单集文件名为 `<节目名> - <yyyy_MM_dd> - <单集标题>.<扩展名>`，非法文件名字符会被去掉。源 URL
  中包含 `.m4a` 时扩展名为 `.m4a`，否则为 `.mp3`。
- 描述在写入元数据前会去除 HTML 标签。
- 源采用流式解析，只保留程序需要的字段，因此整份 XML 不会常驻内存；每条命令最多解析一次源。

## 开发

项目结构：

| 路径 | 内容 |
| --- | --- |
| `PodcastDownloadManager/Program.cs` | 入口：创建状态目录、加载配置、交给 NFlags。 |
| `PodcastDownloadManager/RootCommand.cs` | 注册九个命令。 |
| `PodcastDownloadManager/Commands/` | 每个命令一个文件。 |
| `PodcastDownloadManager/DownloadHelper.cs` | 唯一的 HTTP 客户端：下载源、重试、原子替换。 |
| `PodcastDownloadManager/DownloadTools.cs` | 启动 aria2 或 IDM 并转发输出。 |
| `PodcastDownloadManager/Podcast/Podcast.cs` | 源解析、新单集判断、下载列表条目。 |
| `PodcastDownloadManager/Podcast/Opml.cs` | OPML 节目库和库级操作。 |
| `PodcastDownloadManager/FileMetadata/AudioMetadata.cs` | 元数据附属文件和 TagLib# 标签写入。 |
| `PodcastDownloadManager.Tests/` | xunit v3 测试套件。 |

在仓库根目录构建和测试：

```bash
dotnet build
dotnet run --project PodcastDownloadManager.Tests
```

测试套件包含 77 个 xunit v3 测试，运行在
[Microsoft.Testing.Platform](https://learn.microsoft.com/dotnet/core/testing/unit-testing-platform-intro)
上。测试项目会构建成可自行运行测试的可执行文件，因此
`dotnet run --project PodcastDownloadManager.Tests` 就是一次完整测试，不需要测试宿主。测试不会
访问网络：源由回环地址上的 HTTP 服务器提供，每个测试使用自己的临时目录。

如果 SDK 能与测试进程建立命名管道，`dotnet test` 也可以驱动同一个可执行文件。在禁止该握手的
机器上，请使用 `dotnet run --project PodcastDownloadManager.Tests`；这是环境限制，不是测试失败。

CI（`.github/workflows/dotnet-core.yml`）会在 Ubuntu 和 Windows 上还原依赖、构建 Release 并
运行测试可执行文件。

实现细节、数据格式和容易踩的坑见 [AGENT.md](AGENT.md)。

## 路线图

- 把节目库与命令行界面拆开，方便其他前端复用。
- 增加 GUI。

源下载超时与重试策略、所有命令中的并行源获取、单次解析以及按命令返回退出码都已经实现。

## 许可证

[MIT](LICENSE) © 2020 ZhukovWang
