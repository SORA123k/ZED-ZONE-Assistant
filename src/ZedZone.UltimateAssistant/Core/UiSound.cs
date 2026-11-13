using System;
using System.IO;
using UnityEngine;

namespace ZedZone.UltimateAssistant.Core
{
    /// <summary>
    /// 功能开关音效（本地 wav 文件）。
    /// 启用：codex-notification.wav；关闭：codex-classic.wav。
    /// 自行解析 PCM16 WAV，避免依赖 UnityWebRequest/协程与网络。
    /// </summary>
    internal static class UiSound
    {
        internal const string OnPath = @"D:\AI-Salvage\outputs\2026-10-09__youd\codex-notification.wav";
        internal const string OffPath = @"D:\AI-Salvage\outputs\2026-10-09__youd\codex-classic.wav";

        private static AudioSource _src;
        private static AudioClip _on, _off;
        private static bool _inited;

        internal static void Init(AudioSource src)
        {
            if (_inited) return;
            _inited = true;
            _src = src;
            try { _on = LoadWav(OnPath); } catch (Exception e) { Log.Warn("[sound] 启用音效加载失败: " + e.GetType().Name); }
            try { _off = LoadWav(OffPath); } catch (Exception e) { Log.Warn("[sound] 关闭音效加载失败: " + e.GetType().Name); }
            Log.Info($"[sound] 音效就绪 on={_on != null} off={_off != null}");
        }

        internal static void PlayToggle(bool enabled)
        {
            try
            {
                if (_src == null) return;
                var clip = enabled ? _on : _off;
                if (clip == null) return;
                _src.PlayOneShot(clip, 0.7f);
            }
            catch { }
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
            if (bits != 16) return null;

            int sampleCount = dataLen / 2;
            float[] data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                data[i] = BitConverter.ToInt16(b, dataOff + i * 2) / 32768f;

            var clip = AudioClip.Create("zedua_ui", sampleCount / Math.Max(1, channels), channels, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
