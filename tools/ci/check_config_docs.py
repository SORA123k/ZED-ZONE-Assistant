#!/usr/bin/env python3
"""检查 docs/CONFIG.md 是否覆盖了全部配置项。

数据来源（真值）：
  * src/ZedZone.UltimateAssistant/Core/ModConfig.cs      cfg.Bind("<section>", "<key>", ...)
  * src/ZedZone.UltimateAssistant/Core/HotkeyManager.cs  Add(cfg, "<id>", ...)

检查内容：
  1. 每个 Bind 的 key 都在 CONFIG.md 里以 `key` 形式出现；
  2. 每个快捷键 id 都在 CONFIG.md 里出现；
  3. CONFIG.md 里不应出现代码中不存在的 key（防止改名后文档残留）。

历史教训：README 里的配置段曾经只列了 25/69 项，用户改配置全靠猜。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODCONFIG = os.path.join(ROOT, "src", "ZedZone.UltimateAssistant", "Core", "ModConfig.cs")
HOTKEYS = os.path.join(ROOT, "src", "ZedZone.UltimateAssistant", "Core", "HotkeyManager.cs")
DOC = os.path.join(ROOT, "docs", "CONFIG.md")

# 表头/段名等非 key 的词
NOT_A_KEY = {
    "General", "UI", "Read", "Restore", "Modules", "Features", "Esp", "Menu",
    "Diagnostics", "Hotkeys",
}

for path in (MODCONFIG, HOTKEYS, DOC):
    if not os.path.exists(path):
        print(f"::error::缺少文件 {path}")
        sys.exit(1)

src = open(MODCONFIG, encoding="utf-8").read()
binds = re.findall(r'cfg\.Bind\(\s*"([^"]+)"\s*,\s*"([^"]+)"', src)

hsrc = open(HOTKEYS, encoding="utf-8").read()
hots = re.findall(r'Add\(cfg,\s*"([^"]+)"', hsrc)

doc = open(DOC, encoding="utf-8").read()
doc_keys = set(re.findall(r'\|\s*`([A-Za-z0-9_]+)`\s*\|', doc))

errors = []

for section, key in binds:
    if key not in doc_keys:
        errors.append(f"CONFIG.md 缺少配置项 {section}/{key}")

for key in hots:
    if key not in doc_keys:
        errors.append(f"CONFIG.md 缺少快捷键 [Hotkeys]/{key}")

code_keys = {k for _, k in binds} | set(hots)
for key in sorted(doc_keys):
    if key not in code_keys and key not in NOT_A_KEY:
        errors.append(f"CONFIG.md 里有代码中不存在的 key：{key}")

print(f"代码：{len(binds)} 个配置项 + {len(hots)} 个快捷键；文档记录 {len(doc_keys)} 个 key")

if errors:
    for e in errors:
        print("::error::" + e)
    sys.exit(1)

print("OK: CONFIG.md 与代码一致")
