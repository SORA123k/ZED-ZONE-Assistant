# Contributing (CONTRIBUTING · English)

Thanks for helping maintain this project!

   

## 1. Ways to contribute

| Way | Description |
|   |   |
| Open an issue | Bug reports, feature requests, compatibility reports |
| Open a PR | Fixes, features, UI improvements, translations, docs |
| Translate | UI strings live in `src/.../UI/Localization.cs` (zhCN / en / zhTW) |
| Write docs | Install guides, FAQ, reverse engineering notes |
| Test | Report "feature X works/does not work on version Y" in an issue |

   

## 2. Becoming a maintainer (write access)

1. Land 1–2 PRs (bug fix / feature / docs all count);
2. Say in the issue or PR that you would like to maintain;
3. The owner adds you to **Collaborators** (Settings → Collaborators → Add people).

Maintainers can push to `main` (PRs still preferred), review/merge others' PRs, and run
`tools/release.ps1`.

   

## 3. Development flow

```bash
# 1. fork & clone
git clone https://github.com/<you>/ZED ZONE Assistant.git
cd ZED ZONE Assistant

# 2. branch
git checkout  b feat/your feature

# 3. build (needs BepInEx 6 IL2CPP and one game launch first)
$env:ZEDZONE_GAME_ROOT = "D:\SteamLibrary\steamapps\common\ZED ZONE"
pwsh tools/build.ps1  SkipInstall     # must be 0 warnings / 0 errors

# 4. verify in game, then commit
git commit  m "feat: what changed and why"
git push origin feat/your feature
```

Then open a PR on GitHub.

   

## 4. Code style

  C# / .NET 6, `LangVersion latest`, `Nullable disable`;
  **Every feature needs its own toggle**; turning it off must unpatch immediately, no background thread;
  Every patch body starts with `try/catch` and falls through to vanilla behaviour on error;
  Local player only (compare `Pointer` after `PlayerAccessor.Resolve()`);
  Forbidden: touching global `GameConfigs`, changing save structures, affecting NPCs/other characters,
  writing guessed field offsets;
- **There is no online gate**: `GateOnline` was removed;
  missing it gets a PR rejected;
  Logging via `Log.Info/Warn/Error`; high frequency logs must be rate limited (`WarnOnce` / `ErrorOnce`);
  New UI text must be added in all three languages via `L.T("key")`;
  New config keys go into `docs/CONFIG.md`; patch changes go into `docs/API_PATCH_MAPPING.md`;
  Using an AI assistant? Read [AGENTS.md](AGENTS.md) first.

Full architecture notes and the feature checklist: [DEVELOPMENT_EN.md](DEVELOPMENT_EN.md),
documentation map: [docs/README.md](docs/README.md).

   

## 5. PR checklist

  [ ] `pwsh tools/build.ps1  SkipInstall` → **0 warnings, 0 errors**
  [ ] `dotnet run   project tests/ZedUaOfflineTests/ZedUaOfflineTests.csproj  c Release` passes
  [ ] `python3 tools/ci/check_links.py` passes (if docs changed)
  [ ] `python3 tools/ci/check_localization.py` passes (if UI text changed)
- **There is no online gate**: `GateOnline` was removed;
  [ ] New feature: `docs/CONFIG.md` updated; patch changes: `docs/API_PATCH_MAPPING.md` updated
  [ ] Verified in game (or clearly stated as compile only)
  [ ] Existing features still work (durability / ammo / protection / ESP / menu / config)
  [ ] No `bin/`, `obj/`, `Diagnostics/*.log`, backup DLLs, or `research/` in the diff
  [ ] Version numbers consistent (edit `Directory.Build.props` only; `Plugin.cs` is synced by the script)
  [ ] `README.md` / `README_EN.md` feature tables updated (if applicable)
  [ ] `CHANGELOG.md` updated (a `## vX.Y.Z` section is required for releases)

PR description should contain:

1. What changed and why;
2. How it was tested (or why not);
3. Affected game versions (current baseline: 1.0.17).

   

## 6. Issue templates

Two templates are provided:

  **Bug report**: game version, BepInEx version, relevant `LogOutput.log` lines, reproduction steps;
  **Feature request**: use case, expected behaviour, willingness to implement it yourself.

> Please search existing issues first. Never paste tokens, account data or full personal paths.

   

## 7. Release process (maintainers)

```powershell
pwsh tools/release.ps1  Version 1.0.2
```

The script:

1. Validates the version format and a clean working tree;
2. **Requires a `## vX.Y.Z` section in `CHANGELOG.md`** (aborts otherwise);
3. Updates `Directory.Build.props` (Version / AssemblyVersion / FileVersion) **and**
   `Plugin.cs`'s `PluginVersion` (CI checks they match);
4. Builds Release and asserts the output `FileVersion`;
5. Computes SHA256 / MD5;
6. Commits `release vX.Y.Z`, tags, pushes `main` + tag;
7. `gh release create` with the CHANGELOG section + checksums + install notes.

Write the CHANGELOG entry (and state what was verified in game) before releasing.

   

## 8. Code of conduct

  Respect all contributors; critique the code, not the person;
  No requests for multiplayer cheating / anti cheat bypass features;
  Do not submit third party code or assets without attribution and a compatible license;
  Do not submit decompiled game code, game binaries or runtime logs
  (`research/` and `Diagnostics/` are already in `.gitignore`).

