# Codex Cost Tracker

[English version](./README.md) | [Русская версия](./README.ru.md)

Aplicación de escritorio compacta de WinUI 3 para Windows que realiza un seguimiento de los costos de Codex/Codex App por chat y muestra costos estimados de uso casi en tiempo real.

## Qué hace

- Supervisa `~/.codex/sessions` y `~/.codex/archived_sessions`
- Obtiene datos de precios desde la API oficial de OpenRouter Models
- Estima el gasto por conversación y el gasto total a partir de registros de uso de tokens
- Se actualiza automáticamente cuando cambian los archivos de sesión
- Permite actualizar manualmente chats y precios desde la interfaz

## Requisitos

- Windows 10 versión 1809 o superior
- SDK de .NET 9 para compilaciones locales
- Acceso a Internet para sincronizar precios de OpenRouter

## Inicio rápido

Ejecutar la aplicación publicada:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1
```

Forzar una publicación nueva antes de iniciar:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## Compilar desde el código fuente

Compilar el proyecto:

```powershell
dotnet build .\CodexSpendMonitor\CodexSpendMonitor.csproj
```

Publicar un ejecutable self-contained:

```powershell
dotnet publish .\CodexSpendMonitor\CodexSpendMonitor.csproj -c Release -o .\dist\CodexSpendMonitor
```

Para obtener una carpeta local `dist/` ejecutable, se recomienda usar el script de inicio porque también sincroniza los recursos generados de WinUI necesarios en la salida de publicación:

```powershell
powershell -ExecutionPolicy Bypass -File .\Start-CodexSpendPopout.ps1 -Build
```

## Estructura del repositorio

- `CodexSpendMonitor/` - código fuente de la aplicación WinUI 3
- `Start-CodexSpendPopout.ps1` - script práctico para publicar y ejecutar localmente
- `dist/` - salida de publicación generada, ignorada por Git
- `dumps/` y `*.log` - diagnósticos locales, ignorados por Git

## Cómo se estima el gasto

La aplicación lee archivos de sesión `.jsonl` y combina:

- `input_tokens`
- `cached_input_tokens`
- `output_tokens`
- `reasoning_output_tokens`

con precios obtenidos de:

- [OpenRouter Models API](https://openrouter.ai/api/v1/models)

Si no se puede asociar el modelo de una sesión a una entrada de precio de OpenRouter, el chat igualmente se muestra en la interfaz, pero el precio aparece como no coincidente.

## Detección de carpetas de sesión

Por defecto, la aplicación vigila:

- `~/.codex/sessions`
- `~/.codex/archived_sessions`

Si Codex guarda las sesiones en otro lugar en una máquina específica, puedes sobrescribir la detección de rutas con variables de entorno:

- `CODEX_HOME` - se trata como la carpeta padre que contiene `sessions/` y `archived_sessions/`
- `CODEX_SESSIONS_DIR` - ruta explícita a la carpeta de sesiones activas
- `CODEX_ARCHIVED_SESSIONS_DIR` - ruta explícita a la carpeta de sesiones archivadas
