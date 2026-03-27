# rwlk

CLI for [rwlk.net](https://rwlk.net) — short links and file sharing.

## Install

**Recommended:** install as a [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) (requires .NET 10+ runtime):

```bash
dotnet tool install --global rwlk
```

Alternatively, download a self-contained binary from [Releases](../../releases) — no runtime needed.

## Usage

```
rwlk <url>                Shorten a URL
rwlk <slug>              Open a link / download a file by slug
rwlk <file>              Upload a file anonymously (max 100 KB, 30 days)
rwlk <key> <file>        Upload a file using a file key
rwlk <key>               Download a file using a file key
```

## Examples

```bash
# Shorten a URL
rwlk https://example.com/very-long-url

# Open a short link
rwlk 150

# Upload a file (anonymous, 30 days)
rwlk photo.jpg

# Upload/download with a file key (create keys at rwlk.net/Account)
rwlk abc123def456 backup.tar.gz
rwlk abc123def456
```

## API

You can use the API directly without the CLI:

```bash
# Shorten a URL
curl -F "url=https://example.com/long-url" https://rwlk.net/api/links

# Shorten a URL (one-time)
curl -F "url=https://example.com/long-url" -F "mode=onetime" https://rwlk.net/api/links

# Upload a file (anonymous, max 100 KB, 30 days)
curl -F "file=@photo.jpg" https://rwlk.net/api/links

# Upload a file using a file key
curl -F "file=@backup.tar.gz" https://rwlk.net/api/file/abc123def456

# Download a file using a file key
curl -O -J https://rwlk.net/api/file/abc123def456
```

## Configuration

| Variable | Default | Description |
|---|---|---|
| `RWLK_SERVER` | `https://rwlk.net` | Server URL |
