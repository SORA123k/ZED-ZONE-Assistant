#!/usr/bin/env python3
"""检查所有 Markdown 里的相对链接是否指向真实存在的文件/目录。

只校验仓库内部的相对路径（跳过 http(s)、mailto、纯锚点）。
用途：防止改名/移动文档后留下 404 链接（本项目历史上就出现过
Release 说明指向改名前的仓库地址这种问题）。
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
LINK_RE = re.compile(r'!?\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')
SKIP_PREFIX = ("http://", "https://", "mailto:", "#", "steam://")

errors = []
checked = 0

for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in {".git", "bin", "obj", "node_modules"}]
    for name in filenames:
        if not name.endswith(".md"):
            continue
        md = os.path.join(dirpath, name)
        with open(md, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        for m in LINK_RE.finditer(text):
            target = m.group(1).strip()
            if target.lower().startswith(SKIP_PREFIX):
                continue
            target = target.split("#", 1)[0].strip()
            if not target:
                continue
            target = target.replace("%20", " ")
            resolved = os.path.normpath(os.path.join(dirpath, target))
            checked += 1
            if not os.path.exists(resolved):
                rel = os.path.relpath(md, ROOT).replace("\\", "/")
                errors.append(f"{rel}: 链接目标不存在 -> {target}")

print(f"checked {checked} relative links in markdown")
if errors:
    for e in errors:
        print("::error::" + e)
    sys.exit(1)
print("OK: 所有相对链接都存在")
