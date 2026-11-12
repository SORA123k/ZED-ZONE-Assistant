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
}
