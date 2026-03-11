# Codex Spend Popout

Compact WinUI 3 desktop app that watches the local Codex session folders, matches conversations to OpenRouter pricing, and shows an estimated spend per chat in near real time.

## What it does

- Monitors `~/.codex/sessions` and `~/.codex/archived_sessions`
- Pulls pricing data from the official OpenRouter models API
- Estimates per-conversation and total spend from token usage records
- Refreshes automatically when session files change
- Lets you manually refresh chats and prices from the UI

## Requirements

- Windows 10 version 1809 or newer
- .NET 9 SDK for local builds
- Internet access for OpenRouter pricing sync

## Quick start

Run the published app:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1
```

Force a fresh publish before launch:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## Build from source

Build the project:

```powershell
dotnet build .\CodexSpendMonitor\CodexSpendMonitor.csproj
```

Publish a self-contained executable:

```powershell
dotnet publish .\CodexSpendMonitor\CodexSpendMonitor.csproj -c Release -o .\dist\CodexSpendMonitor
```

## Repository layout

- `CodexSpendMonitor/` - WinUI 3 application source
- `Start-CodexSpendPopout.ps1` - convenience launcher for local publish + run
- `dist/` - generated publish output, ignored by Git
- `dumps/` and `*.log` - local diagnostics, ignored by Git

## How spend is estimated

The app reads session `.jsonl` files and combines:

- `input_tokens`
- `cached_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`

with pricing returned by:

- [OpenRouter Models API](https://openrouter.ai/api/v1/models)

If a session model cannot be matched to an OpenRouter price entry, the chat is still shown in the UI but the cost remains unmatched.

## GitHub-ready notes

- Build artifacts and local diagnostics are excluded through `.gitignore`
- The repository is safe to upload without `bin/`, `obj/`, `dist/`, `dumps/`, or local log files
- No secrets are stored in the project by default; the app reads local Codex session data from the current Windows user profile
