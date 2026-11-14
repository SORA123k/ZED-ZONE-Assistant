#!/usr/bin/env bash
# 校验版本号在三个地方一致：
#   1. Directory.Build.props  <Version>
#   2. src/ZedZone.UltimateAssistant/Plugin.cs  PluginVersion
#   3. CHANGELOG.md 最新的 `## vX.Y.Z` 小节
set -euo pipefail

props="Directory.Build.props"
plugin="src/ZedZone.UltimateAssistant/Plugin.cs"
changelog="CHANGELOG.md"

fail=0

v_props=$(grep -oP '(?<=<Version>)[^<]+' "$props" | head -n1 || true)
v_plugin=$(grep -oP '(?<=PluginVersion = ")[0-9]+\.[0-9]+\.[0-9]+' "$plugin" | head -n1 || true)
v_change=$(grep -oP '(?<=^## v)[0-9]+\.[0-9]+\.[0-9]+' "$changelog" | head -n1 || true)

echo "Directory.Build.props : ${v_props:-<缺失>}"
echo "Plugin.cs             : ${v_plugin:-<缺失>}"
echo "CHANGELOG 最新小节     : ${v_change:-<缺失>}"

if [ -z "$v_props" ]; then echo "::error::Directory.Build.props 里没有 <Version>"; fail=1; fi
if [ -z "$v_plugin" ]; then echo "::error::Plugin.cs 里没有 PluginVersion"; fail=1; fi
if [ -z "$v_change" ]; then echo "::error::CHANGELOG.md 里没有 '## vX.Y.Z' 小节"; fail=1; fi

if [ -n "$v_props" ] && [ -n "$v_plugin" ] && [ "$v_props" != "$v_plugin" ]; then
  echo "::error::版本号不一致：props=$v_props plugin=$v_plugin（应同时由 tools/release.ps1 修改）"
  fail=1
fi

if [ -n "$v_props" ] && [ -n "$v_change" ] && [ "$v_props" != "$v_change" ]; then
  echo "::error::版本号不一致：props=$v_props CHANGELOG=$v_change（发布前请补写更新日志）"
  fail=1
fi

if [ "$fail" -ne 0 ]; then exit 1; fi
echo "OK: 版本号一致（$v_props）"
