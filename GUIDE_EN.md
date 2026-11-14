# Beginner's guide (GUIDE · English)

> For players who have never installed a mod. Follow it top to bottom; no programming needed.
> Just want the short version? See [INSTALL_EN.md](INSTALL_EN.md).

---

## Step 0 — Back up your save (1 minute, important)

Press `Win + R` and paste:

```
%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE
```

Copy the **`GameData`** folder inside it to your desktop (or anywhere safe).

> If something goes wrong later, copy it back. Do this before installing any mod.

---

## Step 1 — Check the game version

1. Steam → Library → right-click **ZED ZONE** → **Properties** → **Installed Files**;
2. The version should be **1.0.17**.

> Other versions may still work, but internal interfaces can change. The plugin logs a
> "version mismatch" warning when they do.

---

## Step 2 — Find the game folder

Steam → right-click the game → **Manage** → **Browse local files**.

You should see something like:

```
...\steamapps\common\ZED ZONE\
    ZEDZONE.exe
    GameAssembly.dll
    ZEDZONE_Data\
```

**Remember this path** — every "game folder" below means this one.

---

## Step 3 — Install a mod loader (BepInEx)

The mod does **not** include BepInEx. There are two ways; pick **one**:

### Option A (recommended): Steam Workshop, one click

> Workshop page: [ZED ZONE 代码模组加载器 (BepInEx IL2CPP / DLL Mods)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258) by **star fox**
> **These steps are summarized from that Workshop page.** If anything differs, the page wins.

1. Open the link and click **Subscribe** (~36 MB, Steam downloads it).
2. Open the local folder:
   ```
   ...\steamapps\workshop\content\1211600\3834614258\
   ```
3. Double-click **`install.cmd`**.
   - It finds the game folder by itself and installs BepInEx + the loader;
   - Fully offline; if the game is under `Program Files`, run it as administrator.
4. Launch the game **through Steam** (not `ZEDZONE.exe` directly).
5. On the main menu, the top-left corner should show:

   ```
   ZED MODS: N code mod(s) loaded
   ```

   That means the loader works (`N` is the number of code mods it found; 0 is fine if you have none yet).

The loader scans `Mods\` in the game folder (one folder per mod — this mod goes there), Workshop DLLs
and `.zedmod` archives, plus `LocalLow\LevenLiu\ZEDZONE\Mods\Workspace`.

It also adds a `ZedModKit\` folder (templates/build script for writing your own DLL mods) — you do
**not** need it for this mod.

**Uninstalling Option A**: run `uninstall.cmd` in the same Workshop folder — it removes
BepInEx / Doorstop / the .NET runtime and leaves your `Mods` data alone.

> Windows x64 only (Unity 2023.1.18f1 / IL2CPP). BepInEx 6.0.0-be.793 is bundled (LGPL-2.1).
> Loader config: `BepInEx\config\zedzone.zedmodsloader.cfg`; log: `BepInEx\LogOutput.log`.
> After a game update: delete `BepInEx\interop` to rebuild, or re-run `install.cmd`.

### Option B: manual BepInEx 6 (IL2CPP)

#### 3.1 Download

1. Open `https://github.com/BepInEx/BepInEx/releases`
2. Find the **BleedingEdge** build (`BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.xxx.zip`);
3. Download the **IL2CPP x64** one — not the Mono build.

#### 3.2 Extract into the game folder

Extract everything into the game root (next to `ZEDZONE.exe`). The result should look like:

```
...\ZED ZONE\
    ZEDZONE.exe
    GameAssembly.dll
    winhttp.dll            ← required
    doorstop_config.ini    ← required
    .doorstop_version
    BepInEx\
        core\
        interop\
        plugins\
```

#### 3.3 Launch the game once

1. Start the game from Steam;
2. Quit when you reach the main menu.

This lets BepInEx generate `BepInEx\interop\` (every DLL mod depends on it).

**Checkpoint**: `...\ZED ZONE\BepInEx\interop\` should contain many `.dll` files (e.g. `Assembly-CSharp.dll`).
If it is empty, BepInEx is not loading — see Troubleshooting.

---

## Step 4 — Download this mod

1. Open the repository **Releases** page;
2. Download the latest **`ZedZone.UltimateAssistant.dll`** (a single file);
3. Optionally verify the SHA256 listed in the release notes:

   ```powershell
   Get-FileHash .\ZedZone.UltimateAssistant.dll -Algorithm SHA256
   ```

---

## Step 5 — Place the DLL

Create this folder in the game directory:

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\
```

Put the DLL inside it:

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

> `...\ZED ZONE\BepInEx\plugins\` also works.
> **Pick one** — two copies means the mod loads twice.
> With the Workshop loader (Option A), `Mods\` is exactly where it looks.

---

## Step 6 — Launch and verify

1. Start the game from Steam;
2. Load your save (not the main menu);
3. Press **`INSERT`**.

The menu appears → success.

If nothing happens, open:

```
...\ZED ZONE\BepInEx\LogOutput.log
```

Search for `Ultimate Assistant` — you should see:

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.1 loading
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

---

## Step 7 — Basic usage

| Action | Description |
|---|---|
| `INSERT` | Open / close the menu |
| Left click a card | Toggle that feature |
| Small button on the card's right edge | Bind a hotkey for the feature (`ESC` clears it) |
| Drag the title bar | Move the menu |
| Mouse wheel | Scroll the page |
| Settings → Theme | Change theme / font size |
| Settings → Language | 简体中文 / English / 繁體中文 |

> Toggling plays a sound and shows a pill notification at the top of the screen.

**Important**: `INSERT` always opens the menu, even if you bind other keys.
Full config reference: [docs/CONFIG.md](docs/CONFIG.md).

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `INSERT` does nothing | Mod not loaded | Look for `Ultimate Assistant v1.0.1 loading` in `LogOutput.log` |
| No trace of the mod in the log | DLL in the wrong place | Path must be `Mods\ZedZone.UltimateAssistant\` or `BepInEx\plugins\` |
| No `ZED MODS: N code mod(s) loaded` | Workshop loader not installed | Re-run `install.cmd` (admin if needed), launch through Steam |
| `Failed to load` in the log | Wrong BepInEx build | Use **BepInEx 6 IL2CPP win-x64** |
| `BepInEx\interop` is empty | BepInEx not active | Check `winhttp.dll` / `doorstop_config.ini`; or delete interop and re-run `install.cmd` |
| Game crashes on start | Conflict / version mismatch | Remove this mod first to confirm the game boots, then bisect other mods |
| Menu opens but the character moves | Setting disabled | Settings → General → enable "Block game input while menu is open" |
| No mouse cursor | Rare | The mod forces the cursor; if it still fails, open an issue with the log |
| Feature has no effect | Toggle off / not in a save | Enable it and be in a loaded save (not the main menu) |
| Log says "version mismatch" | Game was updated | See [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md), or roll back the game version on Steam |

---

## Uninstall

Delete these two, the game itself is untouched:

```
...\ZED ZONE\Mods\ZedZone.UltimateAssistant\        (whole folder)
...\ZED ZONE\BepInEx\config\zedzone.ultimateassistant.cfg   (optional; deleting resets settings)
```

Also remove the loader:

- Option A: run `uninstall.cmd` in the Workshop folder;
- Option B: delete `winhttp.dll`, `doorstop_config.ini`, `.doorstop_version` and `BepInEx\` from the game root.

---

## Safety notes

- This mod is **single-player / offline only**: when an online session is detected, modification
  features and ESP refuse to enable and are turned off automatically;
- Do **not** take that as permission to try it online — server-side judgement is out of the mod's control;
- Back up your save first (Step 0);
- Antivirus software may flag `winhttp.dll` (a BepInEx file) — that is normal for mod loaders;
- Be careful with "Auto Supply / force-finish construction" if you have not backed up: it can change quest flow.

---

## About this project

This project is a **vibe coding** product: most of the code is written by AI, with a human providing
requirements, testing and review. Bugs and AI-specific oversights are possible.

If you hit one, open an issue with the log — kind feedback is always welcome, and pointing out a
problem is the biggest help to this project.
