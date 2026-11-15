using System;
using System.IO;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 功能开关提示音。
    ///
    /// 说明（重要）：
    ///  * 之前的 wav 文件位于 `D:\AI-Salvage\outputs\2026-10-09__youd\`，该目录已被清理；
    ///  * 已核对 GitHub 仓库（SORA123k/ZED-ZONE-Assistant）全部 72 个提交与 v1.0.0~v1.0.3 标签：
    ///    **仓库里从未包含任何 wav/mp3/ogg**，因此无法从 GitHub 找回；
    ///  * 所以本模块不再内置/伪造音频，改为"用户放置文件 + 多路径查找"，
    ///    找不到时只在日志里说明一次，不发出假声音。
    ///
    /// 查找顺序：
    ///   1) 配置项 `[Sound] OnPath` / `OffPath`（绝对路径或相对游戏目录）
    ///   2) <游戏目录>\BepInEx\plugins\ZedZone.UltimateAssistant\sounds\
    ///   3) <游戏目录>\Mods\ZedZone.UltimateAssistant\sounds\
    ///   4) <游戏目录>\BepInEx\sounds\
    /// 文件名：on.wav / off.wav（也接受 codex-notification.wav / codex-classic.wav）
    /// </summary>
    internal static class UiSound
    {
        private static AudioSource _src;
        private static AudioClip _on;
        private static AudioClip _off;
        private static bool _inited;
        private static int _playLog;
        private static bool _warnedMissing;

        internal static void Init()
        {
            if (_inited) return;
            _inited = true;

            try
            {
                var go = new GameObject("SORA_UiSound");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _src = go.AddComponent<AudioSource>();
                _src.playOnAwake = false;
                _src.spatialBlend = 0f;
                _src.volume = 1f;
                _src.loop = false;
                _src.ignoreListenerPause = true;
                _src.ignoreListenerVolume = true;
            }
            catch (Exception e) { Log.Warn("[sound] AudioSource 创建失败: " + e.GetType().Name); }

            Reload();
        }

        /// <summary>重新查找并加载音频（配置改变后可手动调用）。</summary>
        internal static void Reload()
        {
            _on = TryLoad(ModConfig.SoundOnPath?.Value, "on.wav", "codex-notification.wav", "启用音效");
            _off = TryLoad(ModConfig.SoundOffPath?.Value, "off.wav", "codex-classic.wav", "关闭音效");
            Log.Info($"[sound] 就绪 source={_src != null} on={_on != null} off={_off != null}");
            if (_on == null && _off == null && !_warnedMissing)
            {
                _warnedMissing = true;
                Log.Warn("[sound] 未找到提示音文件。请把 on.wav / off.wav 放入 "
                    + "BepInEx\\plugins\\ZedZone.UltimateAssistant\\sounds\\ 或 Mods\\ZedZone.UltimateAssistant\\sounds\\，"
                    + "或在配置 [Sound] OnPath / OffPath 填写绝对路径。"
                    + "（GitHub 仓库中从未包含音频文件，无法自动恢复）");
            }
        }

        private static AudioClip TryLoad(string configured, string nameA, string nameB, string label)
        {
            try
            {
                foreach (var path in Candidates(configured, nameA, nameB))
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    var clip = LoadWav(path);
                    if (clip == null) { Log.Warn($"[sound] {label} 解析失败: {path}"); continue; }
                    Log.Info($"[sound] {label} 已加载: {path} len={clip.length:0.###}s");
                    return clip;
                }
            }
            catch (Exception e) { Log.Warn($"[sound] {label} 加载异常: {e.GetType().Name}"); }
            return null;
        }
        private static System.Collections.Generic.List<string> Candidates(string configured, string a, string b)
        {
            var list = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(configured)) list.Add(configured);

            string gameRoot = null;
            try { gameRoot = BepInEx.Paths.GameRootPath; } catch { }

            if (!string.IsNullOrEmpty(configured) && !Path.IsPathRooted(configured) && !string.IsNullOrEmpty(gameRoot))
                list.Add(Path.Combine(gameRoot, configured));

            if (!string.IsNullOrEmpty(gameRoot))
            {
                var dirs = new[]
                {
                    Path.Combine(gameRoot, "BepInEx", "plugins", "ZedZone.UltimateAssistant", "sounds"),
                    Path.Combine(gameRoot, "Mods", "ZedZone.UltimateAssistant", "sounds"),
                    Path.Combine(gameRoot, "BepInEx", "sounds"),
                };
                foreach (var d in dirs)
                {
                    list.Add(Path.Combine(d, a));
                    list.Add(Path.Combine(d, b));
                }
            }
            return list;
        }
        internal static void PlayToggle(bool enabled)
        {
            try
            {
                var clip = enabled ? _on : _off;
                if (clip == null) { if (!_warnedMissing) Reload(); clip = enabled ? _on : _off; }
                if (clip == null) return;   // 没有文件就安静，不伪造声音

                var cam = Camera.main;
                if (cam != null)
                {
                    try
                    {
                        AudioSource.PlayClipAtPoint(clip, cam.transform.position, 1f);
                        if (_playLog < 5) { _playLog++; Log.Info($"[sound] 播放 {(enabled ? "on" : "off")} len={clip.length:0.###}"); }
                        return;
                    }
                    catch { }
                }

                if (_src != null)
                {
                    _src.PlayOneShot(clip, 1f);
                    if (_playLog < 5) { _playLog++; Log.Info($"[sound] PlayOneShot {(enabled ? "on" : "off")} len={clip.length:0.###}"); }
                }
            }
            catch (Exception e) { Log.WarnOnce("sound.play", "[sound] 播放失败: " + e.GetType().Name); }
        }

        private static AudioClip LoadWav(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44) return null;
            if (b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;

            int channels = BitConverter.ToInt16(b, 22);
            int rate = BitConverter.ToInt32(b, 24);
            int bits = BitConverter.ToInt16(b, 34);
            if (channels <= 0 || rate <= 0 || (bits != 16 && bits != 8)) return null;

            int dataStart = 12;
            while (dataStart + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, dataStart, 4);
                int size = BitConverter.ToInt32(b, dataStart + 4);
                if (id == "data") { dataStart += 8; break; }
                dataStart += 8 + size + (size & 1);
            }
            if (dataStart >= b.Length) return null;

            int bytesPerSample = bits / 8;
            int sampleCount = (b.Length - dataStart) / bytesPerSample;
            if (sampleCount <= 0) return null;

            var data = new float[sampleCount / channels];
            for (int i = 0, s = 0; s < data.Length; i += bytesPerSample * channels, s++)
            {
                float v;
                if (bits == 16) v = BitConverter.ToInt16(b, dataStart + i) / 32768f;
                else v = (b[dataStart + i] - 128) / 128f;
                for (int c = 1; c < channels; c++)
                {
                    int idx = dataStart + i + c * bytesPerSample;
                    if (idx + bytesPerSample <= b.Length)
                    {
                        float v2 = bits == 16 ? BitConverter.ToInt16(b, idx) / 32768f : (b[idx] - 128) / 128f;
                        v = (v + v2) * 0.5f;
                    }
                }
                data[s] = v;
            }

            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(path), data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}


