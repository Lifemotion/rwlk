# rwlk

[![Website](https://img.shields.io/website?url=https%3A%2F%2Frwlk.net)](https://rwlk.net)

CLI for [rwlk.net](https://rwlk.net) -- short links and file sharing from the terminal.

Smart single-command interface: pass a URL to shorten it, a file to upload it, a slug to open it, or a file key to sync files.

## Features

- **Shorten URLs** -- get a short link in one command
- **Upload files** -- anonymous upload (100 KB, 30 days) or via file key (50 MB, permanent)
- **Download files** -- by slug or file key
- **Open links** -- resolve a short link and print the target URL
- **Self-hostable** -- point to your own server with `RWLK_SERVER`
- **Native AOT** -- fast startup, small self-contained binary

---

## Installation

**As a standalone binary** (no .NET required):

Download from [Releases](https://github.com/Lifemotion/rwlk/releases) -- Windows x64, Linux x64, Linux ARM64.

**As a dotnet tool** (requires .NET 10) -- installs globally, available from any directory:

Install .NET 10 if not already present:

- **Windows:** `winget install Microsoft.DotNet.SDK.10`
- **Linux:**

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> ~/.bashrc
source ~/.bashrc
```

Then clone and install:

```bash
git clone https://github.com/Lifemotion/rwlk.git
cd rwlk
dotnet pack
dotnet tool install -g --add-source bin/Release rwlk
```

---

## Usage

```
rwlk <url>              Shorten a URL
rwlk <file>             Upload a file (anonymous, max 100 KB, 30 days)
rwlk <slug>             Open a link / download a file by slug
rwlk <key>              Download a file by file key
rwlk <key> <file>       Upload a file by file key
```

The CLI detects what you mean automatically:
- starts with `http://` or `https://` → shorten
- existing file path → upload
- 3-6 digit number → open slug
- 12-char hex string → file key

### Examples

```bash
# Shorten a URL
$ rwlk https://example.com/very-long-page
{"slug":"482","url":"https://rwlk.net/482"}

# Upload a file anonymously
$ rwlk screenshot.png
{"slug":"849201","url":"https://rwlk.net/849201"}

# Open a short link (prints target URL)
$ rwlk 150
https://example.com/very-long-page

# Download a file by slug
$ rwlk 849201
Downloaded: screenshot.png

# Upload a file using a file key
$ rwlk 9fec5f2cbc68 backup.tar.gz
{"ok":true,"fileName":"backup.tar.gz","size":1048576}

# Download a file using a file key
$ rwlk 9fec5f2cbc68
Downloaded: backup.tar.gz
```

### File keys

File keys are persistent upload slots -- each key holds one file that can be overwritten. Useful for scripts that regularly update a file (backups, exports, reports).

Create file keys at [rwlk.net/Account](https://rwlk.net/Account) (requires login).

---

## Configuration

| Variable | Default | Description |
|---|---|---|
| `RWLK_SERVER` | `https://rwlk.net` | Server URL (for self-hosted instances) |

---

## API

The CLI uses the rwlk.net HTTP API. You can call it directly:

```bash
# Shorten a URL
curl -F "url=https://example.com" https://rwlk.net/api/links

# Shorten a URL (one-time link)
curl -F "url=https://example.com" -F "mode=onetime" https://rwlk.net/api/links

# Upload a file (anonymous)
curl -F "file=@photo.jpg" https://rwlk.net/api/links

# Upload via file key
curl -F "file=@data.csv" https://rwlk.net/api/file/<key>

# Download via file key
curl -O -J https://rwlk.net/api/file/<key>
```

Rate limit: **10 requests per minute** per IP.

---

## Building native AOT binaries

```bash
git clone https://github.com/Lifemotion/rwlk.git
cd rwlk
dotnet publish -c Release -r win-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r linux-arm64
```

## License

MIT
