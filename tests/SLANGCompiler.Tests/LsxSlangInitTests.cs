using System.Diagnostics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// LSX-Dodgers・MSX-DOS 系の env (liblsx_base・libmsxlsx_base) の SLANGINIT。
/// ウォームブートの飛び先の控え (WBOOTBK) は WORK の中にあるので、WORK を 0 にした後で控えること
/// (前で控えると 0 に戻り、STOP() が 0001H に 0 を書いて OS に戻れない)
/// </summary>
public class LsxSlangInitTests : IDisposable
{
    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public LsxSlangInitTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_lsxinit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private const string UsesStop = @"MAIN()
BEGIN
  STOP();
END;
";

    [Theory]
    [InlineData("lsx")]
    [InlineData("msxlsx")]
    [InlineData("x1")]
    [InlineData("msx2")]
    [InlineData("cpm")]
    public void WarmBootVector_IsSavedAfterWorkClear(string env)
    {
        var sl = Path.Combine(_dir, $"STOP_{env}.SL");
        var asmPath = Path.ChangeExtension(sl, ".ASM");
        File.WriteAllText(sl, (env == "msxlsx" ? "CONST ASM MSXROMADR = 0;\n" : "") + UsesStop);
        var psi = new ProcessStartInfo(TestTools.SlangcPath)
        {
            WorkingDirectory = _repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "-E", env, sl, "-o", asmPath }) psi.ArgumentList.Add(a);
        using (var proc = Process.Start(psi)!)
        {
            var so = proc.StandardOutput.ReadToEndAsync();
            var se = proc.StandardError.ReadToEndAsync();
            Assert.True(proc.WaitForExit(180_000), "slangc timed out");
            Assert.True(proc.ExitCode == 0, so.Result + "\n" + se.Result);
        }

        var lines = File.ReadAllLines(asmPath).Select(l => l.Trim()).ToList();
        var clear = lines.FindIndex(l => l == "LD BC,__WORKEND__-__WORK__-1");
        Assert.True(clear >= 0, "WORK を 0 にする所が無い");
        var ldir = lines.FindIndex(clear, l => l == "LDIR");
        Assert.True(ldir > clear, "WORK を 0 にする LDIR が無い");
        var saves = lines.Select((l, i) => (l, i)).Where(x => x.l == "LD (WBOOTBK),HL").Select(x => x.i).ToList();
        Assert.NotEmpty(saves);
        // SLANGINIT の控えは 1 つで、LDIR の後。その直前で ($0001) を読む
        var initSave = saves.Min();
        Assert.True(initSave > ldir, $"WBOOTBK を WORK を 0 にする前 ({initSave + 1} 行目) で控えている");
        Assert.Equal("LD HL,($0001)", lines[initSave - 1]);
        Assert.Contains("LD HL,(WBOOTBK)", lines);
    }
}
