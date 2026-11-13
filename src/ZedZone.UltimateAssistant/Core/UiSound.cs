using System;
using System.IO;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 功能开关音效（本地 wav）。
    /// 启用：codex-notification.wav；关闭：codex-classic.wav。
    ///
    /// 采用独立 GameObject + AudioSource（ignoreListenerPause），避免游戏暂停/菜单暂停时静音；
    /// WAV 为自行解析 PCM16，不依赖网络与协程。
    /// </summary>
    internal static class UiSound
    {
        internal const string OnPath = @"D:\AI-Salvage\outputs\2026-10-09__youd\codex-notification.wav";
        internal const string OffPath = @"D:\AI-Salvage\outputs\2026-10-09__youd\codex-classic.wav";

        private static AudioSource _src;
        private static AudioClip _on, _off;
        private static bool _inited;
        private static int _playLog;

        internal static void Init(AudioSource ignored = null)
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

            _on = TryLoad(OnPath, "启用音效");
            _off = TryLoad(OffPath, "关闭音效");
            Log.Info($"[sound] 就绪 source={_src != null} on={_on != null} off={_off != null}");
        }

        private static AudioClip TryLoad(string path, string label)
        {
            try
            {
                var clip = LoadWav(path);
                if (clip == null) { Log.Warn($"[sound] {label} 解析失败: {path}"); return null; }
                Log.Info($"[sound] {label} 已加载: len={clip.length:0.###}s ch={clip.channels} hz={clip.frequency}");
                return clip;
            }
            catch (Exception e)
            {
                Log.Warn($"[sound] {label} 加载异常: {e.GetType().Name}: {e.Message}");
                return null;
            }
        }

        internal static void PlayToggle(bool enabled)
        {
            try
            {
                if (_src == null) { Log.WarnOnce("sound.nosrc", "[sound] 无 AudioSource，无法播放"); return; }
                var clip = enabled ? _on : _off;
                if (clip == null)
                {
                    // 懒加载重试（Init 早于音频系统就绪时可能失败）
                    if (enabled) _on = TryLoad(OnPath, "启用音效(重试)");
                    else _off = TryLoad(OffPath, "关闭音效(重试)");
                    clip = enabled ? _on : _off;
                    if (clip == null) { Log.WarnOnce("sound.noclip", "[sound] 音效 clip 为空"); return; }
                }
                _src.PlayOneShot(clip, 1f);
                if (_playLog < 3) { _playLog++; Log.Info($"[sound] 播放 {(enabled ? "启用" : "关闭")} 音效"); }
            }
            catch (Exception e) { Log.WarnOnce("sound.play", "[sound] 播放失败: " + e.GetType().Name); }
        }

        private static AudioClip LoadWav(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44) return null;
            if (b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return null;

            int pos = 12;
            int channels = 1, sampleRate = 44100, bits = 16;
            int dataOff = -1, dataLen = 0;

            while (pos + 8 <= b.Length)
            {
                string id = "" + (char)b[pos] + (char)b[pos + 1] + (char)b[pos + 2] + (char)b[pos + 3];
                int size = BitConverter.ToInt32(b, pos + 4);
                int body = pos + 8;

                if (id == "fmt ")
                {
                    channels = BitConverter.ToInt16(b, body + 2);
                    sampleRate = BitConverter.ToInt32(b, body + 4);
                    bits = BitConverter.ToInt16(b, body + 14);
                }
                else if (id == "data")
                {
                    dataOff = body;
                    dataLen = Math.Min(size, b.Length - body);
                    break;
                }
                pos = body + size + (size & 1);
            }

            if (dataOff < 0 || dataLen <= 0) return null;
            if (bits != 16) { Log.Warn("[sound] 仅支持 16-bit PCM wav"); return null; }
            if (channels <= 0) channels = 1;
            if (sampleRate <= 0) sampleRate = 44100;

            int totalSamples = dataLen / 2;
            int frames = totalSamples / channels;
            if (frames <= 0) return null;

            float[] data = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
                data[i] = BitConverter.ToInt16(b, dataOff + i * 2) / 32768f;

            var clip = AudioClip.Create("zedua_ui", frames, channels, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

