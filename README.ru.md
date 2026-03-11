# Codex Spend Popout

[English version](./README.md)

Компактное WinUI 3 приложение для Windows, которое следит за локальными папками сессий Codex, сопоставляет чаты с тарифами OpenRouter и показывает примерную стоимость по каждому диалогу почти в реальном времени.

## Что делает приложение

- Отслеживает `~/.codex/sessions` и `~/.codex/archived_sessions`
- Загружает цены из официального OpenRouter Models API
- Считает ориентировочную стоимость по каждому чату и общую сумму на основе токенов
- Автоматически обновляется при изменении файлов сессий
- Позволяет вручную обновить чаты и цены прямо из интерфейса

## Требования

- Windows 10 версии 1809 или новее
- .NET 9 SDK для локальной сборки
- Доступ в интернет для синхронизации тарифов OpenRouter

## Быстрый старт

Запуск опубликованной версии:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1
```

Принудительно пересобрать и затем запустить:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## Сборка из исходников

Сборка проекта:

```powershell
dotnet build .\CodexSpendMonitor\CodexSpendMonitor.csproj
```

Публикация self-contained сборки:

```powershell
dotnet publish .\CodexSpendMonitor\CodexSpendMonitor.csproj -c Release -o .\dist\CodexSpendMonitor
```

## Структура репозитория

- `CodexSpendMonitor/` - исходный код WinUI 3 приложения
- `Start-CodexSpendPopout.ps1` - удобный скрипт для публикации и запуска
- `dist/` - сгенерированный результат публикации, исключён из Git
- `dumps/` и `*.log` - локальная диагностика, исключены из Git

## Как считается стоимость

Приложение читает `.jsonl` файлы сессий и использует:

- `input_tokens`
- `cached_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`

вместе с ценами из:

- [OpenRouter Models API](https://openrouter.ai/api/v1/models)

Если модель из сессии не удалось сопоставить с записью OpenRouter, чат всё равно отображается в интерфейсе, но цена для него помечается как неподтянутая.

## Поиск папок сессий

По умолчанию приложение отслеживает:

- `~/.codex/sessions`
- `~/.codex/archived_sessions`

Если на конкретном компьютере Codex хранит сессии в другом месте, путь можно переопределить через переменные окружения:

- `CODEX_HOME` - родительская папка, внутри которой лежат `sessions/` и `archived_sessions/`
- `CODEX_SESSIONS_DIR` - явный путь к папке активных сессий
- `CODEX_ARCHIVED_SESSIONS_DIR` - явный путь к папке архивных сессий
