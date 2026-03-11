# Codex Cost Tracker

[English version](./README.md) | [Русская версия](./README.ru.md)

适用于 Windows 的轻量 WinUI 3 桌面应用，可按聊天跟踪 Codex/Codex App 成本，并近实时显示预估使用费用。

## 功能说明

- 监控 `~/.codex/sessions` 和 `~/.codex/archived_sessions`
- 从官方 OpenRouter Models API 拉取定价数据
- 基于 token 使用记录估算每个会话和总支出
- 会话文件变化时自动刷新
- 可在界面中手动刷新聊天与价格

## 系统要求

- Windows 10 1809 或更高版本
- 本地构建需要 .NET 9 SDK
- 同步 OpenRouter 定价需要联网

## 快速开始

运行已发布应用：

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1
```

启动前强制重新发布：

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## 从源码构建

构建项目：

```powershell
dotnet build .\CodexSpendMonitor\CodexSpendMonitor.csproj
```

发布 self-contained 可执行文件：

```powershell
dotnet publish .\CodexSpendMonitor\CodexSpendMonitor.csproj -c Release -o .\dist\CodexSpendMonitor
```

若需获得可直接运行的本地 `dist/` 目录，建议使用启动脚本，因为它还会将 WinUI 运行所需的生成资源同步到发布输出：

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## 仓库结构

- `CodexSpendMonitor/` - WinUI 3 应用源码
- `Start-CodexSpendPopout.ps1` - 本地发布并启动的便捷脚本
- `dist/` - 生成的发布输出（已被 Git 忽略）
- `dumps/` 和 `*.log` - 本地诊断文件（已被 Git 忽略）

## 费用估算方式

应用会读取会话 `.jsonl` 文件，并结合以下字段：

- `input_tokens`
- `cached_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`

以及以下来源的定价：

- [OpenRouter Models API](https://openrouter.ai/api/v1/models)

如果会话模型无法匹配到 OpenRouter 的价格条目，聊天仍会在界面中显示，但价格会标记为未匹配。

## 会话目录发现

默认监控目录：

- `~/.codex/sessions`
- `~/.codex/archived_sessions`

若某台机器上的 Codex 将会话存储在其他位置，可通过环境变量覆盖目录发现：

- `CODEX_HOME` - 作为包含 `sessions/` 与 `archived_sessions/` 的父目录
- `CODEX_SESSIONS_DIR` - 活跃会话目录的显式路径
- `CODEX_ARCHIVED_SESSIONS_DIR` - 归档会话目录的显式路径
