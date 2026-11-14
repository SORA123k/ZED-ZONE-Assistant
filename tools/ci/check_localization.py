#!/usr/bin/env python3
"""静态检查 UI 本地化表（UI/Localization.cs）与文案使用点是否一致。

检查内容：
  1. 每条文案必须恰好有 3 个语言（简中 / English / 繁中），且都非空；
  2. UI 里用到的 key（L.T / Card / ActionCard / ToggleRow / Section）必须存在于表中；
  3. 表中未被任何地方使用的 key 只做提示（不算失败）。

用途：漏一个 key 时菜单会直接显示原始 key（如 "no_overheat_d"），
实机很难发现，这个脚本在 CI 里挡掉。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LOC = os.path.join(ROOT, "src", "ZedZone.UltimateAssistant", "UI", "Localization.cs")
UI_DIR = os.path.join(ROOT, "src", "ZedZone.UltimateAssistant", "UI")

ENTRY_RE = re.compile(r'\["([^"]+)"\]\s*=\s*new\[\]\s*\{(.*?)\}\s*,', re.S)
STR_RE = re.compile(r'"((?:[^"\\]|\\.)*)"')

errors = []
warnings = []

if not os.path.exists(LOC):
    print("::error::找不到 UI/Localization.cs")
    sys.exit(1)

with open(LOC, encoding="utf-8") as fh:
    src = fh.read()

table = {}
for m in ENTRY_RE.finditer(src):
    key, body = m.group(1), m.group(2)
    if key in table:
        errors.append(f"重复的 key：{key}")
    values = STR_RE.findall(body)
    if len(values) != 3:
        errors.append(f"{key}：有 {len(values)} 种语言（应为 3）")
    if any(v.strip() == "" for v in values):
        errors.append(f"{key}：存在空文案")
    table[key] = values

if not table:
    print("::error::没有解析到任何本地化条目（正则或文件结构变了？）")
    sys.exit(1)

# ---- 收集 UI 里用到的 key ----
used = set()
usage_re = re.compile(
    r'L\.T\("([^"]+)"\)'
    r'|(?<![A-Za-z])Card\("([^"]+)",\s*"([^"]+)"'
    r'|(?<![A-Za-z])ActionCard\("([^"]+)",\s*"([^"]+)"'
    r'|ToggleRow\("([^"]+)"'
    r'|Section\("([^"]+)"'
)

for name in sorted(os.listdir(UI_DIR)):
    if not name.endswith(".cs") or name == "Localization.cs":
        continue
    path = os.path.join(UI_DIR, name)
    with open(path, encoding="utf-8") as fh:
        text = fh.read()
    for m in usage_re.finditer(text):
        for g in m.groups():
            if g:
                used.add(g)

missing = sorted(k for k in used if k not in table)
if missing:
    for k in missing:
        errors.append(f"UI 用到但表里没有的 key：{k}")

unused = sorted(k for k in table if k not in used)
for k in unused:
    warnings.append(f"表里有但 UI 未使用：{k}")

print(f"本地化条目 {len(table)} 条｜UI 引用 {len(used)} 个 key")
if warnings:
    print(f"提示（不失败）：{len(warnings)} 条未使用 key")
    for w in warnings[:10]:
        print("  - " + w)
if errors:
    for e in errors:
        print("::error::" + e)
    sys.exit(1)
print("OK: 本地化表与引用一致")
