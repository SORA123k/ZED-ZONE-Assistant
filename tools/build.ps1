#requires -Version 7.0
<#
.SYNOPSIS
    构建并（可选）安装 ZED ZONE Assistant。

.DESCRIPTION
    游戏目录查找顺序：
      1. -GameDir 参数
      2. 环境变量 ZEDZONE_GAME_ROOT
      3. Steam 注册表 + libraryfolders.vdf 自动扫描（含非默认库目录）

.EXAMPLE
    pwsh tools/build.ps1

.EXAMPLE
    pwsh tools/build.ps1 -GameDir "E:\SteamLibrary\steamapps\common\ZED ZONE"

.EXAMPLE
    pwsh tools/build.ps1 -SkipInstall     # 只编译，不复制到游戏目录
#>
param(
    [string]$GameDir = $env:ZEDZONE_GAME_ROOT,
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $repo "src\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.csproj"
if (-not (Test-Path $proj)) { throw "项目文件不存在: $proj" }

function Resolve-ZedZoneGame {
    param([string]$Explicit)

    $candidates = [System.Collections.Generic.List[string]]::new()
    if ($Explicit) { $candidates.Add($Explicit) }
    if ($env:ZEDZONE_GAME_ROOT) { $candidates.Add($env:ZEDZONE_GAME_ROOT) }

    # Steam 安装根目录
    $steam = $null
    foreach ($key in @("HKCU:\Software\Valve\Steam", "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam", "HKLM:\SOFTWARE\Valve\Steam")) {
        try {
            $item = Get-ItemProperty -Path $key -ErrorAction Stop
            $p = $item.SteamPath
            if (-not $p) { $p = $item.InstallPath }
            if ($p -and (Test-Path $p)) { $steam = $p; break }
        } catch { }
    }
    if (-not $steam) { $steam = "C:\Program Files (x86)\Steam" }

    $libraries = @($steam)
    $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) {
        foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libraries += ($m.Groups[1].Value -replace '\\\\', '\')
        }
    }
    foreach ($lib in $libraries) {
        # 库目录可能指向已拔出的盘（F: 之类），Join-Path/Test-Path 都会抛，逐条跳过
        try { $candidates.Add((Join-Path $lib "steamapps\common\ZED ZONE")) } catch { }
    }

    foreach ($c in $candidates) {
        if (-not $c) { continue }
        try {
            if (Test-Path (Join-Path $c "ZEDZONE.exe")) { return (Resolve-Path $c).Path }
        } catch { }
    }
    return $null
}

$resolved = Resolve-ZedZoneGame -Explicit $GameDir
if (-not $resolved) {
    throw "找不到 ZED ZONE。请用 -GameDir 指定，或设置环境变量 ZEDZONE_GAME_ROOT，或确认 Steam 已安装该游戏。"
}
Write-Host "==> game: $resolved" -ForegroundColor Cyan

$interop = Join-Path $resolved "BepInEx\interop\Assembly-CSharp.dll"
if (-not (Test-Path $interop)) {
    throw "缺少 $interop —— 请先安装 BepInEx 6 (IL2CPP) 并启动过一次游戏（见 INSTALL.md）。"
}

if ((Get-Process ZEDZONE -ErrorAction SilentlyContinue) -and -not $SkipInstall) {
    throw "ZED ZONE 正在运行，DLL 被占用。请先关闭游戏。"
}

$dotnet = "dotnet"
if ($env:DOTNET_ROOT) {
    $candidate = Join-Path $env:DOTNET_ROOT "dotnet.exe"
    if (Test-Path $candidate) { $dotnet = $candidate }
} elseif (Test-Path "<dotnet>\dotnet.exe") {
    # 本仓库作者的机器布局；其他机器走 PATH
    $candidate = "<dotnet>\dotnet.exe"
    if (Test-Path $candidate) { $dotnet = $candidate }
}

Write-Host "==> building" -ForegroundColor Cyan
& $dotnet build $proj -c Release -v q -nologo "-p:GameRoot=$resolved\"
if ($LASTEXITCODE -ne 0) { throw "构建失败（exit=$LASTEXITCODE）" }

$dll = Join-Path $repo "src\ZedZone.UltimateAssistant\bin\ZedZone.UltimateAssistant.dll"
if (-not (Test-Path $dll)) { throw "构建产物不存在: $dll" }

$ver = (Get-Item $dll).VersionInfo.FileVersion
$sha = (Get-FileHash $dll -Algorithm SHA256).Hash
Write-Host ("    DLL: v{0}  {1:N0} 字节  SHA256 {2}" -f $ver, (Get-Item $dll).Length, $sha) -ForegroundColor Green

if ($SkipInstall) {
    Write-Host "==> 已跳过安装（-SkipInstall）" -ForegroundColor Yellow
    exit 0
}

$dest = Join-Path $resolved "Mods\ZedZone.UltimateAssistant"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $dll (Join-Path $dest "ZedZone.UltimateAssistant.dll") -Force

Write-Host "==> installed -> $dest\ZedZone.UltimateAssistant.dll" -ForegroundColor Green
Write-Host ""
Write-Host "通过 Steam 启动（steam://rungameid/1211600），不要直接双击 ZEDZONE.exe" -ForegroundColor Cyan
Write-Host "游戏内按 Insert 打开菜单；日志在 $resolved\BepInEx\LogOutput.log"
