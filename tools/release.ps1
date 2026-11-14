#requires -Version 7.0
<#
.SYNOPSIS
    一条命令发布新版本：改版本号 → 构建 → 校验 → 提交 → 打 tag → 推送 → 创建 GitHub Release（附 DLL）。

.DESCRIPTION
    版本号同时写入两处，保持单一事实来源：
      * Directory.Build.props（Version / AssemblyVersion / FileVersion）
      * src/ZedZone.UltimateAssistant/Plugin.cs（PluginVersion，BepInEx 显示用）
    发布前要求 CHANGELOG.md 里已经存在 `## v<版本>` 小节，否则中止 —— 避免发出去的版本没有说明。

.EXAMPLE
    pwsh tools/release.ps1 -Version 1.0.1

.NOTES
    需要：git、gh（已登录）、dotnet 6+、已安装游戏与 BepInEx（interop 作为编译引用）
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Notes = "",
    [string]$Repo = "SORA123k/ZED-ZONE-Assistant",
    [string]$GameDir = $env:ZEDZONE_GAME_ROOT,
    [switch]$SkipPush,
    [switch]$Draft,
    [switch]$AllowNoChangelog
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host "==> 检查工作区" -ForegroundColor Cyan
    $dirty = git status --porcelain
    if ($dirty) { throw "工作区不干净，请先提交或 stash：`n$dirty" }

    # ---------- CHANGELOG ----------
    $changelog = Join-Path $root "CHANGELOG.md"
    if (-not (Test-Path $changelog)) { throw "缺少 CHANGELOG.md" }
    $cl = Get-Content $changelog -Raw
    if ($cl -notmatch ("(?m)^## v" + [regex]::Escape($Version) + "\b")) {
        if (-not $AllowNoChangelog) {
            throw "CHANGELOG.md 里没有 '## v$Version' 小节。请先补写更新日志（或加 -AllowNoChangelog 强制发布）。"
        }
        Write-Host "   警告：CHANGELOG 缺少 v$Version 小节（已按 -AllowNoChangelog 继续）" -ForegroundColor Yellow
    }

    # ---------- 版本号（两处同时改） ----------
    Write-Host "==> 更新版本号 -> $Version" -ForegroundColor Cyan

    $props = Join-Path $root "Directory.Build.props"
    $pt = Get-Content $props -Raw
    $pt = [regex]::Replace($pt, '<Version>[^<]+</Version>', "<Version>$Version</Version>")
    $pt = [regex]::Replace($pt, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>")
    $pt = [regex]::Replace($pt, '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$Version.0</FileVersion>")
    Set-Content -Path $props -Value $pt -Encoding UTF8 -NoNewline

    $plugin = Join-Path $root "src\ZedZone.UltimateAssistant\Plugin.cs"
    $content = Get-Content $plugin -Raw
    if ($content -notmatch 'PluginVersion = "[0-9]+\.[0-9]+\.[0-9]+"') {
        throw "未在 Plugin.cs 中找到 PluginVersion"
    }
    $content = [regex]::Replace($content, 'PluginVersion = "[0-9]+\.[0-9]+\.[0-9]+"', "PluginVersion = `"$Version`"")
    Set-Content -Path $plugin -Value $content -Encoding UTF8 -NoNewline

    # ---------- 构建 ----------
    Write-Host "==> 构建" -ForegroundColor Cyan
    $projDir = Join-Path $root "src\ZedZone.UltimateAssistant"
    Push-Location $projDir
    try {
        $buildArgs = @("build", "-c", "Release", "-nologo", "--no-incremental")
        if ($GameDir) { $buildArgs += "-p:GameRoot=$GameDir\" }
        $buildLog = & dotnet @buildArgs 2>&1
        $buildLog | Select-String -Pattern "警告|错误|已成功|warning|error" | Select-Object -Last 6 | ForEach-Object { Write-Host $_ }
        if ($LASTEXITCODE -ne 0) { throw "构建失败（exit=$LASTEXITCODE）" }
    }
    finally { Pop-Location }

    $dll = Join-Path $projDir "bin\ZedZone.UltimateAssistant.dll"
    if (-not (Test-Path $dll)) { throw "未找到构建产物：$dll" }

    $size = (Get-Item $dll).Length
    $md5 = (Get-FileHash $dll -Algorithm MD5).Hash
    $sha = (Get-FileHash $dll -Algorithm SHA256).Hash
    $fileVer = (Get-Item $dll).VersionInfo.FileVersion
    if ($fileVer -ne "$Version.0") { throw "程序集版本 $fileVer 与发布版本 $Version 不一致，版本号写入失败。" }
    Write-Host ("    DLL: {0}  ({1:N0} 字节)" -f $dll, $size) -ForegroundColor Green
    Write-Host ("    MD5    {0}" -f $md5)
    Write-Host ("    SHA256 {0}" -f $sha)

    # ---------- 提交 ----------
    Write-Host "==> 提交" -ForegroundColor Cyan
    git add -A
    git -c user.name="release-bot" -c user.email="release-bot@local" commit -m "release v$Version" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "提交失败（没有改动？）" }

    Write-Host "==> 打 tag v$Version" -ForegroundColor Cyan
    git tag -a "v$Version" -m "release v$Version"

    if (-not $SkipPush) {
        Write-Host "==> 推送 main + tag" -ForegroundColor Cyan
        git push origin HEAD
        git push origin "v$Version"
    }

    # ---------- Release ----------
    Write-Host "==> 创建 GitHub Release" -ForegroundColor Cyan
    $title = "v$Version — ZED ZONE Assistant"

    $body = ""
    if ($Notes) { $body += "$Notes`n`n" }
    $section = [regex]::Match($cl, "(?s)## v" + [regex]::Escape($Version) + "\b.*?(?=\n## |\z)")
    if ($section.Success) { $body += ($section.Value -replace '^## v[^\n]*\n', '') }

    $body += "`n`n---`n"
    $body += "**安装**：`ZedZone.UltimateAssistant.dll` → ``<游戏目录>\Mods\ZedZone.UltimateAssistant\``（或 ``BepInEx\plugins\``）`n`n"
    $body += "**校验**：`n`n"
    $body += "- SHA256: ``$sha`` `n- MD5: ``$md5`` `n- 大小: $size 字节`n"

    $notesFile = Join-Path $env:TEMP "zedua_release_$Version.md"
    Set-Content -Path $notesFile -Value $body -Encoding UTF8

    $ghArgs = @(
        "release", "create", "v$Version", $dll,
        "--repo", $Repo,
        "--title", $title,
        "--notes-file", $notesFile
    )
    if ($Draft) { $ghArgs += "--draft" }

    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create 失败" }

    Write-Host "==> 完成" -ForegroundColor Green
    Write-Host "    Release: https://github.com/$Repo/releases/tag/v$Version"
    Write-Host "    SHA256 : $sha"
}
finally {
    Pop-Location
}
