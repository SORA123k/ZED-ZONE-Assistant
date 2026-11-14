#requires -Version 7.0
<#
.SYNOPSIS
    一条命令发布新版本：改版本号 → 构建 → 提交 → 打 tag → 推送 → 创建 GitHub Release（附 DLL）。

.EXAMPLE
    pwsh tools/release.ps1 -Version 1.0.1

.EXAMPLE
    pwsh tools/release.ps1 -Version 1.1.0 -Notes "新增 XXX 功能"

.NOTES
    - 需要：git、gh（已登录）、dotnet 6+、已安装游戏与 BepInEx（interop 作为编译引用）
    - 脚本不修改游戏文件；只在游戏运行时拒绝安装（本脚本默认不安装）
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Notes = "",
    [string]$Repo = "SORA123k/ZED-ZONE-Assistant",
    [switch]$SkipPush,
    [switch]$Draft
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host "==> 检查工作区" -ForegroundColor Cyan
    $dirty = git status --porcelain
    if ($dirty) {
        throw "工作区不干净，请先提交或 stash：`n$dirty"
    }

    Write-Host "==> 更新版本号 -> $Version" -ForegroundColor Cyan
    $plugin = Join-Path $root "src\ZedZone.UltimateAssistant\Plugin.cs"
    $content = Get-Content $plugin -Raw
    if ($content -notmatch 'PluginVersion = "[0-9]+\.[0-9]+\.[0-9]+"') {
        throw "未在 Plugin.cs 中找到 PluginVersion"
    }
    $content = [regex]::Replace($content, 'PluginVersion = "[0-9]+\.[0-9]+\.[0-9]+"', "PluginVersion = `"$Version`"")
    Set-Content -Path $plugin -Value $content -Encoding UTF8

    Write-Host "==> 构建" -ForegroundColor Cyan
    $proj = Join-Path $root "src\ZedZone.UltimateAssistant"
    Push-Location $proj
    try {
        $buildLog = & dotnet build -c Release -nologo --no-incremental 2>&1
        $buildLog | Select-String -Pattern "警告|错误|已成功|warning|error" | Select-Object -Last 6 | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) { throw "构建失败（exit=$LASTEXITCODE）" }
        if (($buildLog | Select-String -Pattern "错误|error CS").Count -gt 0) { throw "构建存在错误" }
    }
    finally { Pop-Location }

    $dll = Join-Path $proj "bin\ZedZone.UltimateAssistant.dll"
    if (-not (Test-Path $dll)) { throw "未找到构建产物：$dll" }
    $size = (Get-Item $dll).Length
    $md5 = (Get-FileHash $dll -Algorithm MD5).Hash
    Write-Host ("    DLL: {0}  ({1} 字节, MD5 {2})" -f $dll, $size, $md5) -ForegroundColor Green

    Write-Host "==> 提交" -ForegroundColor Cyan
    git add -A
    git -c user.name="release-bot" -c user.email="release-bot@local" commit -m "release v$Version" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "提交失败" }

    Write-Host "==> 打 tag v$Version" -ForegroundColor Cyan
    git tag -a "v$Version" -m "release v$Version"

    if (-not $SkipPush) {
        Write-Host "==> 推送 main + tag" -ForegroundColor Cyan
        git push origin HEAD
        git push origin "v$Version"
    }

    Write-Host "==> 创建 GitHub Release" -ForegroundColor Cyan
    $title = "v$Version — ZED ZONE Ultimate Assistant"
    $body = if ($Notes) { $Notes } else { "release v$Version" }
    $body += "`n`n---`nDLL: ``ZedZone.UltimateAssistant.dll`` ($size 字节, MD5 ``$md5``)"

    $notesFile = Join-Path $env:TEMP "zedua_release_$Version.md"
    Set-Content -Path $notesFile -Value $body -Encoding UTF8

    $args = @(
        "release", "create", "v$Version", $dll,
        "--repo", $Repo,
        "--title", $title,
        "--notes-file", $notesFile
    )
    if ($Draft) { $args += "--draft" }

    & gh @args
    if ($LASTEXITCODE -ne 0) { throw "gh release create 失败" }

    Write-Host "==> 完成" -ForegroundColor Green
    Write-Host "    Release: https://github.com/$Repo/releases/tag/v$Version"
    Write-Host "    MD5    : $md5"
}
finally {
    Pop-Location
}
