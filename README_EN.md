# ZED ZONE Assistant (SORA Assistant)

<p align="right"><a href="README.md">简体中文</a> | <b>English</b></p>

[![CI](https://github.com/SORA123k/ZED-ZONE-Assistant/actions/workflows/ci.yml/badge.svg)](https://github.com/SORA123k/ZED-ZONE-Assistant/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/SORA123k/ZED-ZONE-Assistant)](https://github.com/SORA123k/ZED-ZONE-Assistant/releases)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A **single-player, offline-only** utility mod for **ZED ZONE** (Steam AppID `1211600`), built on **BepInEx 6 (IL2CPP)** + **HarmonyX**. It ships an in-game ClickGUI-style menu (default hotkey `INSERT`) with durability, ammo, protection, movement and visual helpers.

> **Single-player / offline use only.** When an online session is detected, **every state-modifying feature** (ammo, durability, overheat, jam, HP/stamina/hunger/thirst, auto supply, movement) plus **ESP** is refused and forcibly turned off (fail-closed). Do not use it in multiplayer.

---

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Build from source](#build-from-source)
- [Project layout](#project-layout)
- [Known limitations](#known-limitations)
- [Contributing](#contributing)
- [License](#license)

---

## Features

The menu has four categories: **Combat / Player / Visuals / Settings**. Every feature has its own toggle, its own config entry and an optional hotkey.

### Combat

| Feature | Description |
|---|---|
| Infinite Ammo | Shooting does not consume reserve ammo (local player only) |
| No Reload | Fire with an empty magazine; `R` still reloads natively |
| No Jam | Prevents weapon jams and clears the existing `MalfunctionFlag` |
| Tool / Melee Durability | Tools, melee weapons and gear never lose durability |
| Gun Durability | Keeps gun durability at max (writes `ItemData.properties[0]`) |
| No Overheat | Your weapon heat is always 0 (NPCs unaffected) |
| Auto Supply | Crafting/repair treat materials as sufficient; can force-finish the island boat |

### Player

| Feature | Description |
|---|---|
| No HP Loss | Taking damage does not reduce HP |
| No Stamina Loss | Sprinting/jumping costs no stamina |
| No Hunger Loss | Hunger does not drop (eating still works) |
| No Thirst Loss | Thirst does not drop (drinking still works) |
| Always Sprint | Auto-sprint when not firing; firing cancels sprint to avoid "cannot shoot while sprinting" |
| Movement Speed | 1.0x – 20.0x, also applies to rolling |
| One-click restore (buttons) | HP / Energy / Hunger / Thirst — one click, one restore |

### Visuals

| Feature | Description |
|---|---|
| Zombie ESP | Marks loaded zombies (name / distance / box, off-screen edge indicators) |
| Full Bright | Brightens night scenes via URP 2D global light + ambient light; no time/weather/AI changes |

### Settings

- **Theme**: Dark / BlackGold / BlueTech / Purple / Teal / Light / RedBlack
- **Font size**: 12–24 px, applied live
- **Language**: 简体中文 / English / 繁體中文, switches instantly
- UI scale, animations, block game input while the menu is open, show limb status
- **Hotkeys**: menu key supports multiple bindings (`Insert` is always active); each feature can bind one key
- **Sounds**: on/off notification sounds for feature toggles
- **Dynamic Island**: top-screen pill showing "feature X enabled/disabled" in real time

---

## Requirements

| Item | Requirement |
|---|---|
| Game | ZED ZONE **1.0.17** (Steam AppID `1211600`) |
| Engine | Unity 2023.1.18f1 / IL2CPP **x64** |
| Mod loader | **BepInEx 6** (IL2CPP). Easiest: the [Steam Workshop code-mod loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258) (one-click install). Manual: BepInEx BleedingEdge 6.0.0-be.* |
| Runtime | .NET 6.0 (bundled with BepInEx) |
| OS | Windows 10/11 x64 |

After a game update the `Assembly-CSharp` IL2CPP signatures may change. The plugin logs a
"version mismatch" warning; see [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) (Chinese).

---

## Installation

### Step 1 — get a mod loader (BepInEx 6)

**A. Steam Workshop (recommended for beginners)**

[ZED ZONE 代码模组加载器 (BepInEx IL2CPP / DLL Mods)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258)

1. Subscribe (Steam downloads it, ~36 MB).
2. Open `...\steamapps\workshop\content\1211600\3834614258\` and run **`install.cmd`**
   (fully offline; run as administrator if the game is under `Program Files`).
   The script locates the game and installs BepInEx + the loader automatically.
3. Launch the game **through Steam**.
4. The main menu shows `ZED MODS: N code mod(s) loaded` when the loader is active.

This loader scans `Mods\<name>\*.dll` in the game folder (that's where this mod goes), DLLs from
Workshop items / `.zedmod` archives, and `LocalLow\LevenLiu\ZEDZONE\Mods\Workspace`.
Uninstall with `uninstall.cmd` in the same folder. BepInEx 6.0.0-be.793 is bundled (LGPL-2.1).

> The loader and the steps above are provided by the Workshop author **star fox**;
> they are summarized from the Workshop page. If anything differs, the Workshop page wins.

**B. Manual BepInEx 6 (IL2CPP BleedingEdge)**

Download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.*` from the BepInEx releases, extract it into the
game root, then launch the game once so `BepInEx\interop\` is generated.

### Step 2 — install this mod

1. Download `ZedZone.UltimateAssistant.dll` from **Releases** (optionally verify the SHA256 in the release notes).
2. Copy it to:
   ```
   <Game>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
   ```
   (`BepInEx\plugins\` also works — pick **one** location only.)
3. Launch the game, load a save, press **INSERT**.

### Verify

Check `<Game>\BepInEx\LogOutput.log` for:

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.1 loading
[Info   :ZED ZONE Ultimate Assistant] detected game version: 1.0.17
[Info   :ZED ZONE Ultimate Assistant] [patches] 启动时已挂载补丁数 = 0（功能开启后按需增加）
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

`patches = 0` is expected: patches are only applied while their feature is enabled.

Detailed guides: [INSTALL_EN.md](INSTALL_EN.md) and [GUIDE_EN.md](GUIDE_EN.md) (beginner walkthrough). Chinese versions: [INSTALL.md](INSTALL.md), [GUIDE.md](GUIDE.md).

---

## Usage

| Action | Description |
|---|---|
| `INSERT` | Open / close the menu (always available, cannot be unbound) |
| Left click | Toggle a feature by clicking the card or the switch |
| Drag the title bar | Move the menu (position is saved) |
| Mouse wheel | Scroll long pages |
| Feature hotkey | Button on the right side of each card: click, then press a key; `ESC` clears |
| Menu multi-bind | Settings → Controls → menu keys: bind extra keys besides `Insert` |

While the menu is open the mod blocks character input, disables the game's UI raycasters (prevents click-through) and forces the cursor visible. Closing the menu restores everything.

---

## Configuration

```
<Game>\BepInEx\config\zedzone.ultimateassistant.cfg
```

- Changes made in the menu apply immediately and are written back to the file.
- Edits made by hand require a game restart.
- Defaults: **all state-modifying features off**, all diagnostics off.
- The complete reference (69 config entries + 16 hotkeys) is in [docs/CONFIG.md](docs/CONFIG.md) (Chinese, but key names and defaults are language-neutral).

---

## Build from source

Requirements: .NET SDK 6.0+, ZED ZONE + BepInEx 6 installed (interop assemblies are compile references).

Point the build at your game folder (pick one):

```powershell
# 1) environment variable
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"

# 2) MSBuild property
dotnet build src/ZedZone.UltimateAssistant -c Release -p:GameRoot="D:\...\ZED ZONE"

# 3) helper script (auto-detects Steam libraries, then installs)
pwsh tools/build.ps1            # build + copy to Mods\
pwsh tools/build.ps1 -SkipInstall
```

Output: `src/ZedZone.UltimateAssistant/bin/ZedZone.UltimateAssistant.dll`

Tests and checks:

```powershell
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release
python3 tools/ci/check_links.py
```

`tests/ZedUaOfflineTests` is a console assertion runner (not xUnit), hence `dotnet run` rather than `dotnet test`.

---

## Project layout

```
Directory.Build.props         single source of truth for the version
ZedZone.Assistant.sln         solution (plugin + tests)
src/ZedZone.UltimateAssistant/
  Plugin.cs                   entry point
  Core/                       logic (config, features, gate, durability, ESP, services)
  Patch/                      Harmony patches
  UI/                         IMGUI menu (ClickGui, themes, island, input guard)
tests/                        offline tests (console runner)
tools/                        build.ps1, release.ps1, ci checks
docs/                         documentation map: docs/README.md
  CONFIG.md                   full config reference
  COMPATIBILITY.md            game-version adaptation
  reference/                  game internals research
  internal/                   historical phase reports (archive, don't trust blindly)
.github/                      CI, issue/PR templates
```

---

## Known limitations

- **Offline single-player only**: state-modifying features and ESP refuse to enable and are turned off
  when an online session is detected (including sessions detected after the fact). This only governs the
  mod's own behaviour — it cannot control what a multiplayer server decides.
- Tied to game **1.0.17** interop signatures. A mismatch only produces a warning (not a hard block);
  adaptation steps: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).
- Auto Supply handles normal crafting/building; the island escape boat uses a separate path and is handled by "force finish construction", which may affect quest flow — back up your save.
- Full Bright relies on URP 2D global light and ambient light; a custom darkening method would reduce the effect.
- Gun durability is written into `ItemData.properties[0]`, which is persisted in the save.
- The codebase is AI-written (see disclaimer); edge cases are not exhaustively covered.

---

## Disclaimer

### How this project was built (please read)

This project is a **vibe coding** product: most of the code was written by AI, with a human providing requirements, testing and review.

That means:

- Code style, comments and structure may not be "engineering grade", and AI-specific mistakes are possible;
- Real in-game testing does not cover every edge case;
- Some implementations favor "it works" over "the optimal design".

So if you find a bug, a logic flaw, a crash, or anything that bothers you, **please point it out kindly**.
Open an issue with the log and reproduction steps — no need to be formal, and no one will blame you. Reporting a problem is itself the biggest help to this project.

PRs that fix these AI-left pitfalls are equally welcome.

---

### Scope of the reverse-engineering notes

`docs/internal/diagnostics/` contains **read-only** analysis of the game's native binary
(disassembly fragments and the logic derived from them). It exists only to avoid writing the wrong
fields into a single-player save. The repository does not ship game assets, `GameAssembly.dll`,
`global-metadata.dat` or runtime logs. Do not use these notes in online sessions or redistribute
them; if you are a rights holder and object to them, contact us via [SECURITY.md](SECURITY.md) and
they will be removed. Details: [docs/internal/README.md](docs/internal/README.md) (Chinese).

---

## Contributing

Contributions are welcome — code, translations, docs, bug reports.

- Documentation map: [docs/README.md](docs/README.md) (read this first)
- Architecture, patch conventions, new-feature checklist: [DEVELOPMENT_EN.md](DEVELOPMENT_EN.md) (Chinese: [DEVELOPMENT.md](DEVELOPMENT.md))
- Workflow, code style, PR checklist: [CONTRIBUTING_EN.md](CONTRIBUTING_EN.md) (Chinese: [CONTRIBUTING.md](CONTRIBUTING.md))
- Rules for AI coding assistants: [AGENTS.md](AGENTS.md)
- Config reference: [docs/CONFIG.md](docs/CONFIG.md) · Game compatibility: [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md)
- Security issues: [SECURITY.md](SECURITY.md)

If you want to become a maintainer (write access), open an issue or contact the owner — see CONTRIBUTING.md.

---

## License

[MIT](LICENSE) © 2026 SORA123k
