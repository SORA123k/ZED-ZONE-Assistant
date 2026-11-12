using System;
using ZedZone.UltimateAssistant.Core;

internal static class Program
{
    private static int _fail;
    private static int _pass;

    private static void Check(string name, bool ok, string detail = "")
    {
        if (ok) { _pass++; Console.WriteLine($"PASS  {name}"); }
        else { _fail++; Console.WriteLine($"FAIL  {name}   {detail}"); }
    }

    private static int Main()
    {
        Console.WriteLine("=== ZED ZONE Ultimate Assistant · Phase 2 offline tests ===\n");

        TestPercent();
        TestValueTextNoFakeData();
        TestSnapshotCounts();
        TestFailedFactory();
        TestSuspiciousStillUsable();
        TestRangeSelection();

        Console.WriteLine();
        Console.WriteLine($"passed={_pass} failed={_fail}");
        Console.WriteLine(_fail == 0 ? "ALL TESTS PASSED" : "TESTS FAILED");
        return _fail == 0 ? 0 : 1;
    }

    private static void TestPercent()
    {
        var v = new ReadValue { Key = "hp", LabelZh = "生命", Value = 50, Max = 100, HasMax = true, State = ReadState.Ok };
        Check("percent 50/100 = 0.5", Math.Abs(v.Percent01() - 0.5f) < 0.001f, v.Percent01().ToString());

        v.Value = 150;
        Check("percent clamped to 1", Math.Abs(v.Percent01() - 1f) < 0.001f);
        v.Value = -20;
        Check("percent clamped to 0", Math.Abs(v.Percent01() - 0f) < 0.001f);

        v.Value = 50; v.HasMax = false;
        Check("percent 0 when no max", Math.Abs(v.Percent01()) < 0.001f);

        v.HasMax = true; v.Max = 0;
        Check("percent 0 when max<=0", Math.Abs(v.Percent01()) < 0.001f);

        v.Max = 100; v.State = ReadState.Failed;
        Check("percent 0 when failed", Math.Abs(v.Percent01()) < 0.001f);
    }

    private static void TestValueTextNoFakeData()
    {
        var ok = new ReadValue { Value = 12.5f, Max = 100f, HasMax = true, State = ReadState.Ok };
        Check("ok value text has number", ok.ValueText().Contains("12.5"), ok.ValueText());

        var failed = new ReadValue { Key = "hunger", LabelZh = "饱食度", State = ReadState.Failed, Error = "NullReference" };
        string ft = failed.ValueText();
        Check("failed text says 读取失败", ft.Contains("读取失败"), ft);
        Check("failed text contains NO number", !System.Text.RegularExpressions.Regex.IsMatch(ft, @"\d"), ft);
        Check("failed is not usable", !failed.IsUsable);

        var np = ReadValue.NoPlayer_("energy", "精力");
        Check("noplayer text has no number", !System.Text.RegularExpressions.Regex.IsMatch(np.ValueText(), @"\d"), np.ValueText());
        Check("noplayer not usable", !np.IsUsable);
    }

    private static void TestSnapshotCounts()
    {
        var s = new PlayerSnapshot();
        s.Values.Add(new ReadValue { Key = "hp", State = ReadState.Ok });
        s.Values.Add(new ReadValue { Key = "energy", State = ReadState.Suspicious });
        s.Values.Add(new ReadValue { Key = "hunger", State = ReadState.Failed });
        s.Values.Add(new ReadValue { Key = "buffCount", State = ReadState.Ok });

        Check("OkCount counts usable (ok+suspicious) = 3", s.OkCount() == 3, s.OkCount().ToString());
        Check("FailCount = 1", s.FailCount() == 1, s.FailCount().ToString());
        Check("Get returns right element", s.Get("hunger") != null && s.Get("hunger").Key == "hunger");
        Check("Get unknown returns null", s.Get("nope") == null);
    }

    private static void TestFailedFactory()
    {
        var f = ReadValue.Failed_("thirst", "水分", "characterStatusData == null", "note");
        Check("failed factory state", f.State == ReadState.Failed);
        Check("failed factory keeps error", f.Error.Contains("characterStatusData"));
        Check("failed factory has no value", f.Value == 0f);
        Check("failed factory not usable", !f.IsUsable);
    }

    private static void TestSuspiciousStillUsable()
    {
        var v = new ReadValue { Value = -5f, Max = 100f, HasMax = true, State = ReadState.Suspicious };
        Check("suspicious is usable (shows real value)", v.IsUsable);
        Check("suspicious percent clamps to 0", Math.Abs(v.Percent01()) < 0.001f);
    }

    // ------------------------------------------------------------------
    // Phase 3A 严重 Bug 回归：最大值选源
    // 场景：maxhp=2000（真实）, maxHP=1000（陈旧字段）, current=2000
    // ------------------------------------------------------------------
    private static void TestRangeSelection()
    {
        System.Collections.Generic.List<RangeSelection.Candidate> C(params (string n, float v)[] xs)
        {
            var l = new System.Collections.Generic.List<RangeSelection.Candidate>();
            foreach (var x in xs) l.Add(new RangeSelection.Candidate(x.n, x.v));
            return l;
        }

        // 1) 首选 >= 当前值 → Ok
        var r = RangeSelection.Select(C(("maxhp", 2000f), ("FullHP", 2000f), ("maxHP", 1000f)),
                                     1500f, out var ch, out var why);
        Check("primary ok when >= current", r == RangeSelection.Result.Ok, why);
        Check("primary ok picks maxhp=2000", ch.Name == "maxhp" && Math.Abs(ch.Value - 2000f) < 0.01f);

        // 2) ★ 本次 Bug：首选 1000 < 当前 2000，但 FullHP=2000 可覆盖 → 回退
        r = RangeSelection.Select(C(("maxHP", 1000f), ("FullHP", 2000f)),
                                  2000f, out ch, out why);
        Check("BUG: fallback used when primary < current", r == RangeSelection.Result.FallbackUsed, why);
        Check("BUG: fallback picks FullHP=2000", ch.Name == "FullHP" && Math.Abs(ch.Value - 2000f) < 0.01f);

        // 3) 所有候选都 < 当前值 → Unreliable（必须拒绝，绝不截断）
        r = RangeSelection.Select(C(("maxHP", 1000f)), 2000f, out ch, out why);
        Check("unreliable when all candidates < current", r == RangeSelection.Result.Unreliable, why);

        // 4) 全无效（0 / 负数 / NaN / Inf） → Unreliable
        r = RangeSelection.Select(C(("a", 0f), ("b", -5f), ("c", float.NaN), ("d", float.PositiveInfinity)),
                                  10f, out ch, out why);
        Check("unreliable when all invalid", r == RangeSelection.Result.Unreliable, why);

        // 5) 首选无效但第二个有效 → 用第二个
        r = RangeSelection.Select(C(("bad", 0f), ("good", 120f)), 50f, out ch, out why);
        Check("skips invalid primary", r == RangeSelection.Result.Ok && ch.Name == "good", why);

        // 6) 空候选 → Unreliable
        r = RangeSelection.Select(new System.Collections.Generic.List<RangeSelection.Candidate>(), 1f, out ch, out why);
        Check("empty candidates -> unreliable", r == RangeSelection.Result.Unreliable);

        // 7) 边界：当前值正好等于上限 → Ok，不应判为不可靠
        r = RangeSelection.Select(C(("maxhp", 100f)), 100f, out ch, out why);
        Check("current == max is ok (no false unreliable)", r == RangeSelection.Result.Ok, why);

        // 8) 边界：当前值略高于上限（0.02，在容差内）→ Ok
        r = RangeSelection.Select(C(("maxhp", 100f)), 100.02f, out ch, out why);
        Check("current slightly above max within eps is ok", r == RangeSelection.Result.Ok, why);
    }
}
