# 构建并安装 ZED ZONE Ultimate Assistant
#
# 用法（在仓库根目录）：
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1
#   powershell -ExecutionPolicy Bypass -File tools\build.ps1 -GameDir "D:\Games\ZED ZONE"
#
# 前置：.NET SDK 8（本机在 <dotnet>）。游戏必须先关闭，否则目标 DLL 被占用。

param(
    [string]$GameDir = "<游戏目录>",
    [string]$DotnetDir = "<dotnet>",
    [string]$NuGetDir  = "<nuget-cache>",
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $repo "src\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.csproj"
if (-not (Test-Path $proj)) { throw "project not found: $proj" }

$dotnet = Join-Path $DotnetDir "dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet"; Write-Host "warning: using dotnet from PATH" -ForegroundColor Yellow }

if ((Get-Process ZEDZONE -ErrorAction SilentlyContinue) -and -not $SkipInstall) {
    Write-Host "ZED ZONE is running - close the game first (the DLL is locked)." -ForegroundColor Red
    exit 1
}

$env:DOTNET_ROOT = $DotnetDir
$env:NUGET_PACKAGES = $NuGetDir
$env:DOTNET_CLI_HOME = Join-Path (Split-Path -Parent $DotnetDir) "dotnet-home"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Host "==> building" -ForegroundColor Cyan
& $dotnet build $proj -c Release -v q -nologo "-p:GameRoot=$GameDir\"
if ($LASTEXITCODE -ne 0) { throw "build failed" }

$dll = Join-Path $repo "src\ZedZone.UltimateAssistant\bin\ZedZone.UltimateAssistant.dll"
if (-not (Test-Path $dll)) { throw "output not found: $dll" }

if ($SkipInstall) { Write-Host "built: $dll" -ForegroundColor Green; exit 0 }

$dest = Join-Path $GameDir "Mods\ZedZone.UltimateAssistant"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $dll (Join-Path $dest "ZedZone.UltimateAssistant.dll") -Force

Write-Host "installed -> $dest\ZedZone.UltimateAssistant.dll" -ForegroundColor Green
Write-Host ""
Write-Host "Launch through Steam (steam://rungameid/1211600). In game: Insert = 面板开关，Esc = 关闭" -ForegroundColor Cyan
Write-Host "Log: $GameDir\BepInEx\LogOutput.log"
