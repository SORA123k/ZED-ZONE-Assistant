# Development guide (DEVELOPMENT · English)

For people extending the mod. Stack: **BepInEx 6 (IL2CPP) + HarmonyX + Il2CppInterop**.

> If you use an AI coding assistant, read [AGENTS.md](AGENTS.md) first — it lists the hard rules
> that get a PR rejected.
> Documentation map: [docs/README.md](docs/README.md).

---

## 1. Environment

| Tool | Notes |
|---|---|
| .NET SDK 6.0+ (tests target 8.0) | `dotnet --version` |
| ZED ZONE 1.0.17 | its `BepInEx/interop/` assemblies are compile references |
| BepInEx 6 IL2CPP | generated on first game launch (or via the Workshop loader's `install.cmd`) |
| Optional: Ghidra / ILSpy | for native reverse engineering |

The game path is **not hard-coded**. Resolution order:

1. `-p:GameRoot=...`
2. environment variable `ZEDZONE_GAME_ROOT`

```powershell
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"
```

If neither is set, the build fails with a readable message (`CheckGameRefs` target) instead of
dozens of "type not found" errors.

`Directory.Build.props` derives `GameRoot` / `BepInExCore` / `InteropDir` and is also the single
source of truth for the version (see §7).

---

## 2. Build & install

```powershell
# compile only
pwsh tools/build.ps1 -SkipInstall

# compile + copy to <Game>\Mods\ZedZone.UltimateAssistant\
pwsh tools/build.ps1
```

The script auto-detects the game via the Steam registry + `libraryfolders.vdf` (multiple libraries,
non-default drives). Pass `-GameDir` if it fails. Or use dotnet directly:

```bash
dotnet build src/ZedZone.UltimateAssistant -c Release -p:GameRoot="E:\...\ZED ZONE"
```

Output: `src/ZedZone.UltimateAssistant/bin/ZedZone.UltimateAssistant.dll`

---

## 3. Architecture

```
Plugin.cs
 ├ ModConfig.Bind / BindFeatures        configuration
 ├ ModuleRegistry                       read-only info modules
 ├ CheckGameVersion                     game-version warning
 ├ Features.SyncFromConfig              install/uninstall patches per toggle
 └ AssistantPanel                       MonoBehaviour host
      ├ Update: hotkeys / input guard / throttled read / online self-check / service ticks
      └ OnGUI : DynamicIsland → ZombieEsp → ClickGui
```

### 3.1 Feature lifecycle (the pattern every feature follows)

1. Declare a `ConfigEntry<bool>` in `Core/ModConfig.cs`;
2. Declare `XxxOn` + `SetXxx(bool)` in `Core/Features.cs`;
3. The first line of `SetXxx` calls `GateOnline(...)` — **refuse to enable when online** (fail-closed);
4. `Features.Apply()` installs/uninstalls the matching Harmony patches;
5. The UI card calls `ModConfig.Xxx.Value = v; Features.SetXxx(v);`
6. `HotkeyManager` registers the hotkey + sound.

Turning a feature off unpatches immediately; no background loop is left behind.
**Skipping step 3 gets a PR rejected.**

> Note: `GateOnline` deliberately does **not** rewrite the config file. Detection can, in rare
> cases, fall back to "online" on error; rewriting config would silently wipe user preferences.
> Only the runtime flag is cleared, and `SyncFromConfig` restores it in single-player.

### 3.2 Patch rules

- Prefer **public managed APIs** (interop signatures) over raw memory writes;
- Only affect the **local player**: resolve via `PlayerAccessor.Resolve()` and compare `Pointer`;
- Wrap every patch body in `try/catch` and fall through to the original logic on error;
- Observation-only patches use postfix and rate-limited logging (`Log.WarnOnce` / `Log.ErrorOnce`);
- **Never** touch global `GameConfigs`, save structures, or other characters/NPCs;
- Guessing field offsets / writing unknown memory is forbidden — evidence only
  (`docs/internal/diagnostics/`).

### 3.3 IL2CPP gotchas

- `Update()` may be scheduled more than once per frame → dedupe with `Time.frameCount`;
- Accessing a destroyed IL2CPP object throws `Handle is not initialized` → null-check + `try/catch`;
- **Never cache object references** (`PlayerAccessor` re-resolves each read) — death / respawn /
  scene change invalidates them;
- Getter-only properties cannot be assigned (e.g. `BasicRangedWeapon.durabilityFloat`) — find the
  backing field or change approach.

### 3.4 New-feature checklist

- [ ] Config entry in `ModConfig` (own key, **default off**) + documented in `docs/CONFIG.md`
- [ ] `Features` toggle + `GateOnline` + install/uninstall logic
- [ ] Patch affects local player only, unpatches when toggled off + row in `docs/API_PATCH_MAPPING.md`
- [ ] UI card in `UI/ClickGui.cs`, three languages in `UI/Localization.cs`
- [ ] Hotkey registered in `HotkeyManager`
- [ ] `pwsh tools/build.ps1 -SkipInstall` → 0 warnings, 0 errors
- [ ] Offline tests pass + `python3 tools/ci/check_links.py` and `check_localization.py` pass
- [ ] Verified in-game (compiling is not verification)
- [ ] `README.md` feature table and `CHANGELOG.md` updated

---

## 4. Directory layout

| Path | Content |
|---|---|
| `src/ZedZone.UltimateAssistant/` | plugin source |
| `src/.../Core/` | logic (no Unity UI dependency) |
| `src/.../Patch/` | Harmony patches |
| `src/.../UI/` | IMGUI menu |
| `docs/` | documentation (map in `docs/README.md`) |
| `docs/reference/` | game internals research |
| `docs/internal/` | historical phase reports / diagnostics / raw evidence (archive) |
| `tests/ZedUaOfflineTests/` | offline tests (console assertion runner) |
| `tools/` | `build.ps1`, `release.ps1`, `ci/` |
| `.github/workflows/ci.yml` | CI: offline tests + version consistency + doc links + localization |

---

## 5. Tests

```powershell
dotnet run --project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj -c Release
```

> It is `dotnet run`, **not** `dotnet test`: the test project is a console assertion runner
> (`<OutputType>Exe</OutputType>` with its own `Check()` helper), so `dotnet test` would not run
> `Program.Main`.

Offline tests cover engine-independent pure logic only (`PlayerSnapshot`, `RangeSelection`, ...),
linked directly from `src/` so **no game installation is required**.

**In-game verification cannot be automated**: the mod mutates live game state. Every feature needs
manual testing. In a PR, state which features you verified in-game and which are compile-only.
The regression checklist is in [docs/COMPATIBILITY.md](docs/COMPATIBILITY.md) §6.

Static checks (run locally and in CI):

```powershell
python3 tools/ci/check_links.py          # markdown relative links
python3 tools/ci/check_localization.py   # UI text table vs. usage
bash tools/ci/check-version.sh           # version in props / Plugin.cs / CHANGELOG
```

---

## 6. Reverse engineering (optional)

`docs/internal/diagnostics/GUN_DURABILITY_NATIVE_REVERSE_ENGINEERING.md` walks through a full case:

1. At runtime, get `MethodInfo*` → `methodPointer` → RVA via IL2CPP metadata;
2. Import `GameAssembly.dll` into Ghidra headless and decompile at that RVA;
3. Cross-check `[reg+0xNN]` offsets against metadata field offsets;
4. Conclude `durabilityFloat = weaponData.properties[0] / weaponAttr.durabilityMax`.

Script: `docs/internal/diagnostics/ghidra/DumpDurabilityGetter.cs`.

---

## 7. Release process

```powershell
pwsh tools/release.ps1 -Version 1.0.2
```

The script:

1. Validates the version format and a clean working tree;
2. **Requires a `## vX.Y.Z` section in `CHANGELOG.md`** (aborts otherwise; `-AllowNoChangelog` overrides);
3. Updates **both** `Directory.Build.props` (Version / AssemblyVersion / FileVersion) and
   `Plugin.cs`'s `PluginVersion` — CI also checks they match;
4. Builds Release and asserts the assembly `FileVersion` equals the release version;
5. Computes SHA256 + MD5;
6. Commits, tags, pushes `main` + tag;
7. `gh release create` with the CHANGELOG section + checksums + install instructions.

| Flag | Purpose |
|---|---|
| `-Notes "..."` | extra release text |
| `-Draft` | create a draft release |
| `-SkipPush` | commit + tag locally only |
| `-AllowNoChangelog` | skip the CHANGELOG gate (not recommended) |
| `-GameDir "..."` | game path (defaults to `ZEDZONE_GAME_ROOT`) |

---

## 8. Commit conventions

- One feature/fix per PR;
- Commit messages: what changed + why (Chinese or English both fine);
- **Do not commit** `bin/`, `obj/`, `Diagnostics/*.log`, backup DLLs or `research/`
  (decompiled output / evidence) — `.gitignore` covers them;
- Run the three CI checks locally before pushing;
- PR description: build result (0 warnings/0 errors) + in-game verification notes.
