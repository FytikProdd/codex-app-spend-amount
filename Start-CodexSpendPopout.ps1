param(
    [switch]$Build
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "CodexSpendMonitor\CodexSpendMonitor.csproj"
$publishDir = Join-Path $PSScriptRoot "dist\CodexSpendMonitor"
$exePath = Join-Path $publishDir "CodexSpendMonitor.exe"

if ($Build -or -not (Test-Path $exePath)) {
    dotnet publish $project -c Release -o $publishDir
}

Start-Process -FilePath $exePath
