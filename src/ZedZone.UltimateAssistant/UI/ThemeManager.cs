using System;
using UnityEngine;
using ZedZone.UltimateAssistant.Core;

namespace ZedZone.UltimateAssistant.UI
{
    /// <summary>菜单主题（7 套完整配色，统一定义，控件不硬编码颜色）。</summary>
    internal struct UiPalette
    {
        internal Color Window, Header, Sidebar, Card, CardHover, CardActive;
        internal Color Text, SubText, Accent, AccentText, Line;
        internal Color Track, Knob, ScrollTrack, ScrollThumb, Good, Warn, Bad;
    }

    internal static class ThemeManager
    {
        internal static readonly string[] Keys =
        {
            "Dark", "BlackGold", "BlueTech", "Purple", "Teal", "Light", "RedBlack"
        };

        private static readonly string[] _names =
        {
            "默认深色", "黑金", "蓝色科技", "紫色", "青绿", "白色浅色", "红黑"
        };

        private static string _current;
        private static UiPalette _p;

        internal static string Current => _current;
        internal static UiPalette P => _p;

        internal static string Display(string key)
        {
            int i = Array.IndexOf(Keys, key);
            return i >= 0 ? _names[i] : _names[0];
        }

        private static string _lastCfgKey;

        internal static void Init()
        {
            string k = "Dark";
            try { k = ModConfig.MenuTheme?.Value ?? "Dark"; } catch { }
            if (k == _lastCfgKey && _current == k) return;   // 每帧调用只做一次字符串比较
            _lastCfgKey = k;
            Set(k);
        }

        internal static void Set(string key)
        {
            if (Array.IndexOf(Keys, key) < 0) key = "Dark";
            _current = key;
            _p = Build(key);
            try { ModConfig.MenuTheme.Value = key; } catch { }
        }

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);
        private static Color Hex(string hex, float a = 1f)
        {
            try
            {
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                return new Color(r / 255f, g / 255f, b / 255f, a);
            }
            catch { return new Color(0.5f, 0.5f, 0.5f, a); }
        }

        private static UiPalette Build(string key)
        {
            var p = new UiPalette();
            switch (key)
            {
                case "BlackGold":
                    p.Window = Hex("#0B0B0C", 0.98f); p.Header = Hex("#131316");
                    p.Sidebar = Hex("#0E0E11"); p.Card = Hex("#1A1A1F");
                    p.CardHover = Hex("#232329"); p.CardActive = Hex("#2B2B33");
                    p.Text = Hex("#F2E6C8"); p.SubText = Hex("#A89873");
                    p.Accent = Hex("#D4AF37"); p.AccentText = Hex("#0B0B0C");
                    p.Line = Hex("#FFFFFF", 0.07f);
                    p.Track = Hex("#2A2A30"); p.Knob = Hex("#F2E6C8");
                    p.ScrollTrack = Hex("#141418"); p.ScrollThumb = Hex("#D4AF37", 0.6f);
                    p.Good = Hex("#7BD88F"); p.Warn = Hex("#E7C15A"); p.Bad = Hex("#EF6B6B");
                    break;
                case "BlueTech":
                    p.Window = Hex("#0A1420", 0.98f); p.Header = Hex("#0E1B2B");
                    p.Sidebar = Hex("#0B1725"); p.Card = Hex("#13273D");
                    p.CardHover = Hex("#1A3553"); p.CardActive = Hex("#204066");
                    p.Text = Hex("#E6F4FF"); p.SubText = Hex("#7FA8C9");
                    p.Accent = Hex("#36C2FF"); p.AccentText = Hex("#04121C");
                    p.Line = Hex("#36C2FF", 0.16f);
                    p.Track = Hex("#1B3350"); p.Knob = Hex("#E6F4FF");
                    p.ScrollTrack = Hex("#0E1B2B"); p.ScrollThumb = Hex("#36C2FF", 0.65f);
                    p.Good = Hex("#5BE3A6"); p.Warn = Hex("#FFD166"); p.Bad = Hex("#FF6B81");
                    break;
                case "Purple":
                    p.Window = Hex("#100B1C", 0.98f); p.Header = Hex("#160F26");
                    p.Sidebar = Hex("#120C1F"); p.Card = Hex("#1D1430");
                    p.CardHover = Hex("#271A40"); p.CardActive = Hex("#312050");
                    p.Text = Hex("#F1E9FF"); p.SubText = Hex("#A08CC4");
                    p.Accent = Hex("#A46BFF"); p.AccentText = Hex("#0E0818");
                    p.Line = Hex("#A46BFF", 0.18f);
                    p.Track = Hex("#2A1D45"); p.Knob = Hex("#F1E9FF");
                    p.ScrollTrack = Hex("#160F26"); p.ScrollThumb = Hex("#A46BFF", 0.6f);
                    p.Good = Hex("#74E0A0"); p.Warn = Hex("#E9C36B"); p.Bad = Hex("#F16A78");
                    break;
                case "Teal":
                    p.Window = Hex("#07161A", 0.98f); p.Header = Hex("#0A1E24");
                    p.Sidebar = Hex("#081A1F"); p.Card = Hex("#0F2A31");
                    p.CardHover = Hex("#153640"); p.CardActive = Hex("#1B4450");
                    p.Text = Hex("#E3FBF7"); p.SubText = Hex("#85B8B3");
                    p.Accent = Hex("#2FD9C0"); p.AccentText = Hex("#03110F");
                    p.Line = Hex("#2FD9C0", 0.16f);
                    p.Track = Hex("#16363D"); p.Knob = Hex("#E3FBF7");
                    p.ScrollTrack = Hex("#0A1E24"); p.ScrollThumb = Hex("#2FD9C0", 0.6f);
                    p.Good = Hex("#63E6A8"); p.Warn = Hex("#E7D06B"); p.Bad = Hex("#EF7272");
                    break;
                case "Light":
                    p.Window = Hex("#F5F6F8", 0.98f); p.Header = Hex("#FFFFFF");
                    p.Sidebar = Hex("#ECEEF2"); p.Card = Hex("#FFFFFF");
                    p.CardHover = Hex("#F0F3F8"); p.CardActive = Hex("#E3E9F2");
                    p.Text = Hex("#1A1D23"); p.SubText = Hex("#5C6470");
                    p.Accent = Hex("#2E6FE8"); p.AccentText = Hex("#FFFFFF");
                    p.Line = Hex("#000000", 0.10f);
                    p.Track = Hex("#D6DBE3"); p.Knob = Hex("#2E6FE8");
                    p.ScrollTrack = Hex("#E3E6EB"); p.ScrollThumb = Hex("#9AA3B0");
                    p.Good = Hex("#1F8A4C"); p.Warn = Hex("#B57B12"); p.Bad = Hex("#C0392B");
                    break;
                case "RedBlack":
                    p.Window = Hex("#12090B", 0.98f); p.Header = Hex("#1A0D10");
                    p.Sidebar = Hex("#150A0C"); p.Card = Hex("#241114");
                    p.CardHover = Hex("#30161A"); p.CardActive = Hex("#3D1C21");
                    p.Text = Hex("#FFE8E8"); p.SubText = Hex("#C08A8A");
                    p.Accent = Hex("#FF4D5E"); p.AccentText = Hex("#14070A");
                    p.Line = Hex("#FF4D5E", 0.18f);
                    p.Track = Hex("#33171B"); p.Knob = Hex("#FFE8E8");
                    p.ScrollTrack = Hex("#1A0D10"); p.ScrollThumb = Hex("#FF4D5E", 0.6f);
                    p.Good = Hex("#7ED9A0"); p.Warn = Hex("#E9C36B"); p.Bad = Hex("#FF6B6B");
                    break;
                default: // Dark
                    p.Window = Hex("#0E1014", 0.98f); p.Header = Hex("#151821");
                    p.Sidebar = Hex("#11141A"); p.Card = Hex("#1A1F2B");
                    p.CardHover = Hex("#232A39"); p.CardActive = Hex("#2C3547");
                    p.Text = Hex("#EDF0F5"); p.SubText = Hex("#98A1B3");
                    p.Accent = Hex("#5B8CFF"); p.AccentText = Hex("#080B12");
                    p.Line = Hex("#FFFFFF", 0.07f);
                    p.Track = Hex("#262D3B"); p.Knob = Hex("#EDF0F5");
                    p.ScrollTrack = Hex("#151821"); p.ScrollThumb = Hex("#5B8CFF", 0.6f);
                    p.Good = Hex("#5AD18A"); p.Warn = Hex("#E8C467"); p.Bad = Hex("#EF6B6B");
                    break;
            }
            return p;
        }
    }
}

