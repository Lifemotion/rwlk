# rwlk

CLI for [rwlk.net](https://rwlk.net) — short links and file sharing.

## Install

Download a binary from [Releases](../../releases) and put it in your PATH.

Or install as a dotnet tool:

```bash
dotnet tool install --global rwlk
```

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

## Configuration

| Variable | Default | Description |
|---|---|---|
| `RWLK_SERVER` | `https://rwlk.net` | Server URL |
