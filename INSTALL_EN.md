# Installation (INSTALL · English)

This mod needs **BepInEx 6 (IL2CPP)** as a mod loader. The steps below assume you own ZED ZONE on Steam (AppID `1211600`).

- Never installed a mod before → see [GUIDE_EN.md](GUIDE_EN.md) (step-by-step).
- Config reference → [docs/CONFIG.md](docs/CONFIG.md) (Chinese, key names are language-neutral).
- Game version / adaptation → [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) (Chinese).

---

## 0. Back up your save (strongly recommended)

```
%USERPROFILE%\AppData\LocalLow\LevenLiu\ZEDZONE\GameData
```

Copy the whole `GameData` folder somewhere safe.

---

## 1. Check your game version

- Supported: **ZED ZONE 1.0.17** (Unity 2023.1.18f1 / IL2CPP x64)
- Steam → right-click the game → Properties → Installed Files → version

Other versions may work, but IL2CPP signatures can change. The plugin logs a "version mismatch"
warning at startup; handling steps are in [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md).

---

## 2. Install BepInEx 6 (IL2CPP)

> The mod does **not** bundle BepInEx. Pick one of the two paths.

### Option A (recommended for beginners): Steam Workshop loader

> Workshop item: [ZED ZONE 代码模组加载器 (BepInEx IL2CPP / DLL Mods)](https://steamcommunity.com/sharedfiles/filedetails/?id=3834614258) by **star fox**
> **The steps below are summarized from that Workshop page.** If anything differs, the Workshop page wins.

1. Open the link → **Subscribe** (~36 MB, Steam downloads it).
2. Open `...\steamapps\workshop\content\1211600\3834614258\`.
3. Run **`install.cmd`**. It locates your game and installs BepInEx + the loader
   (fully offline; run as administrator if the game is under `Program Files`).
4. Launch the game **through Steam** (do not double-click `ZEDZONE.exe`).
5. The main menu shows this line in the top-left corner when it works:

   ```
   ZED MODS: N code mod(s) loaded
   ```

The loader scans DLLs under `<Game>\Mods\` (one folder per mod), DLLs from Workshop items and
`.zedmod` archives (extracted to `BepInEx\ZedModsCache`), and
`LocalLow\LevenLiu\ZEDZONE\Mods\Workspace`. It also drops a `ZedModKit\` folder (templates for
writing your own DLL mods — **not needed for this mod**).

- Uninstall the loader: run `uninstall.cmd` from the same Workshop folder (removes BepInEx / Doorstop /
  .NET runtime, keeps your `Mods` data).
- Windows x64 only; BepInEx 6.0.0-be.793 is bundled (LGPL-2.1).
- Loader config: `BepInEx\config\zedzone.zedmodsloader.cfg`.
- After a game update: delete `BepInEx\interop` to let it rebuild, or re-run `install.cmd`.

### Option B: manual BepInEx

1. Download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.*` from the BepInEx releases (BleedingEdge).
2. Extract it into the game root so the layout becomes:

```
<Game>\
  BepInEx\
    core\
    interop\
  winhttp.dll
  doorstop_config.ini
  .doorstop_version
  ZEDZONE.exe
```

3. Launch the game **once** and quit. This generates the IL2CPP assemblies under `BepInEx/interop/`
   (this mod needs them at compile time).
   - If `interop` is empty, BepInEx is not loading — check `winhttp.dll` and `doorstop_config.ini`.

---

## 3. Install this mod

### 3.1 Download

From **Releases**:

```
ZedZone.UltimateAssistant.dll
```

### 3.2 Verify (optional, recommended)

The release notes list a SHA256:

```powershell
Get-FileHash .\ZedZone.UltimateAssistant.dll -Algorithm SHA256
```

If it does not match, do not install it.

### 3.3 Place the DLL

Recommended (modular):

```
<Game>\Mods\ZedZone.UltimateAssistant\ZedZone.UltimateAssistant.dll
```

Or the classic plugins folder:

```
<Game>\BepInEx\plugins\ZedZone.UltimateAssistant.dll
```

> Pick **one** location only — two copies means the mod loads twice.
> With Option A, `Mods\` is exactly what the Workshop loader scans.

### 3.4 Launch

Launch the game → load a save → press **INSERT**.

---

## 4. Verify the install

Open:

```
<Game>\BepInEx\LogOutput.log
```

Search for `Ultimate Assistant`:

```
[Info   :ZED ZONE Ultimate Assistant] ZED ZONE Ultimate Assistant v1.0.1 loading
[Info   :ZED ZONE Ultimate Assistant] detected game version: 1.0.17
[Info   :ZED ZONE Ultimate Assistant] [patches] 启动时已挂载补丁数 = 0（功能开启后按需增加）
[Info   :ZED ZONE Ultimate Assistant] [ui] ClickGUI 已就绪（INSERT 开关，当前关闭）
```

`patches = 0` is expected: patches are only applied while the matching feature is enabled.
If the menu opens with `INSERT`, you are done.

---

## 5. Configuration

Generated on first run:

```
<Game>\BepInEx\config\zedzone.ultimateassistant.cfg
```

Common entries:

- Menu hotkey `[UI] ToggleKey` (default `Insert`; `Insert` always works)
- Extra menu keys `[Menu] MenuKeys` (comma-separated KeyCode names)
- Language `[Menu] Language`: `zhCN` / `en` / `zhTW`
- Theme `[Menu] Theme`: `Dark` / `BlackGold` / `BlueTech` / `Purple` / `Teal` / `Light` / `RedBlack`

**Full reference (69 config entries + 16 hotkeys): [docs/CONFIG.md](docs/CONFIG.md).**

> Menu changes apply immediately; hand edits require a game restart.

---

## 6. Uninstall

Delete:

```
<Game>\Mods\ZedZone.UltimateAssistant\
<Game>\BepInEx\config\zedzone.ultimateassistant.cfg
```

Loader: Option A → `uninstall.cmd`; Option B → delete `winhttp.dll`, `doorstop_config.ini`,
`.doorstop_version` and `BepInEx\`.

---

## 7. Troubleshooting

  Symptom   Check  
 --- --- 
  `INSERT` does nothing   Is there a loading line in `LogOutput.log`? Is the DLL in only one place? Is BepInEx active?  
  No `ZED MODS: N code mod(s) loaded` in the main menu   Workshop loader not installed: re-run `install.cmd` (as admin if needed) and launch through Steam  
  Menu opens but the character keeps moving   Settings → "Block game input while menu is open" should be on  
  No mouse cursor   The mod forces the cursor visible; if it still fails, open an issue with the log  
  Game crashes on launch   Most likely BepInEx or game version mismatch; remove the mod first to confirm the game boots  
  A feature does nothing   Is its toggle on? Some features only work in a loaded save (not the main menu)  
| Toggles refuse to stay on in an online session | There is no online gate in this build; check config/hotkey conflicts |
  Save file looks wrong   Restore your backup immediately; report with `LogOutput.log`  

---

## 8. Reporting issues

Include:

1. Game version (Steam → Properties)
2. BepInEx version (or Workshop loader version)
3. The relevant part of `BepInEx/LogOutput.log`
4. Reproduction steps
5. Other installed mods (conflicts are common)

Security issues (save corruption, offline gating bypass, tampered release) → private channel in
[SECURITY.md](SECURITY.md).

