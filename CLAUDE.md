# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`rwlk` is a single-file .NET 10 CLI (top-level statements in `Program.cs`) that talks to the rwlk.net HTTP API for URL shortening and file sharing. It ships as a Native AOT self-contained binary and as a dotnet global tool.

## Commands

- Run locally: `dotnet run -- <args>`
- Debug build: `dotnet build`
- Publish AOT binary for a target: `dotnet publish -c Release -r <win-x64|linux-x64|linux-arm64>`
- Pack + install as global tool: `dotnet pack && dotnet tool install -g --add-source bin/Release rwlk`
- Point at a self-hosted server during dev: set `RWLK_SERVER` (default `https://rwlk.net`)

There is no test project — the repo contains only `Program.cs`, `rwlk.csproj`, publish profiles, and the release workflow. Releases are cut by pushing a `v*` tag (see `.github/workflows/release.yml`), which builds the AOT matrix and attaches archives to a GitHub Release.

## Architecture

### Smart single-command dispatch

The CLI has no subcommands for the core actions — `Program.cs` inspects the shape of the argument and picks the operation:

- `http(s)://…` → `Shorten` (POST `/api/links`)
- `^\d{3,6}$` → `OpenSlug` (GET `/<slug>`, follows redirect or saves file from `Content-Disposition`)
- existing file path → `UploadAnonymous` (POST `/api/links`)
- `^[a-z0-9]{12}$` (`IsFileKey`) → `DownloadByKey` (GET `/api/file/<key>`)
- two args `<key> <file>` → `UploadByKey` (POST `/api/file/<key>`)

When adding a new input shape, keep the detection predicates mutually exclusive and update `PrintUsage` to match. The regexes are the contract the user relies on.

### `--share` (file manager integration) mode

`install` / `uninstall` register a "Share with rwlk" entry that re-invokes the binary with `--share <path>`. In share mode the core dispatch runs normally, then the response body is piped to the platform clipboard and (on Linux) a `notify-send` toast, and on Windows the console pauses for a keypress so the user can read it. Anything new that returns a result should set the `result` variable so share mode can pick it up.

Platform specifics:
- Windows: registry under `HKCU\Software\Classes\*\shell\rwlk` (no admin needed).
- Linux: auto-detects Nautilus / Dolphin / Nemo and drops a script or `.desktop` / `.nemo_action` file under `~/.local/share/...`. `uninstall` mirrors the same paths.
- Clipboard fallback chain on Linux: `wl-copy` → `xclip` → `xsel`; silently no-ops if none are present.

### Native AOT constraints

`PublishAot=true` and `OptimizationPreference=Size` are set in `rwlk.csproj`. This means:
- No runtime reflection / dynamic code gen — avoid `System.Text.Json` source-gen-less paths, `Activator.CreateInstance` on open generics, etc. The current code parses no JSON (responses are printed raw); keep it that way or add a JSON source-generator context if parsing is needed.
- Trim warnings are build failures in practice — fix them rather than suppressing.
- `Microsoft.Win32.Registry` usage is Windows-only and must stay guarded by `OperatingSystem.IsWindows()`.

### Version and release

`<Version>` in `rwlk.csproj` is the source of truth for both the NuGet tool package and the displayed version. Bump it in the same commit as the tag you push.
