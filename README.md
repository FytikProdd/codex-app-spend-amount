# Codex Cost Tracker

[Русская версия](./README.ru.md) | [Español](./README.es.md) | [简体中文](./README.zh-CN.md) | [हिन्दी](./README.hi.md)

Compact WinUI 3 desktop app for Windows that tracks Codex/Codex App costs per chat and shows estimated usage costs in near real time.

## What it does

- Monitors `~/.codex/sessions` and `~/.codex/archived_sessions`
- Pulls pricing data from the official OpenRouter Models API
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

For a runnable local `dist/` folder, prefer the launcher script because it also syncs required WinUI generated resources into the publish output:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
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

If a session model cannot be matched to an OpenRouter price entry, the chat is still shown in the UI but the price is marked as unmatched.

## Session folder discovery

By default the app watches:

- `~/.codex/sessions`
- `~/.codex/archived_sessions`

If Codex stores sessions somewhere else on a specific machine, you can override the discovery path with environment variables:

- `CODEX_HOME` - treated as the parent folder that contains `sessions/` and `archived_sessions/`
- `CODEX_SESSIONS_DIR` - explicit path to the live sessions folder
- `CODEX_ARCHIVED_SESSIONS_DIR` - explicit path to the archived sessions folder
