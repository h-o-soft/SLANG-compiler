using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// LSX-Dodgers 系の env (lsx・msxlsx・x1) の FDELETE・FRENAME・FFIRST・FNEXT。
/// 4 つを使うプログラムが組み立てられること、使わないプログラムには作業の FCB と DTA が入らないことを確かめる
/// </summary>
public class LsxFileDirOpsTests : IDisposable
{
    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public LsxFileDirOpsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_lsxdir_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private const string UsesAll = @"ARRAY BYTE ENT[31];
VAR R;
MAIN()
BEGIN
    R = FDELETE(""OLD.TXT"");
    R = FRENAME(""A.TXT"", ""B:C.TXT"");
    R = FFIRST(ENT);
    WHILE R == 0 DO BEGIN
        R = FNEXT(ENT);
    END;
END;
";

    private const string UsesOpenOnly = @"VAR R;
MAIN()
BEGIN
    R = FOPEN(0, ""A.TXT"", 0);
    FCLOSE(0);
END;
";

    private (int Code, string Output) Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            WorkingDirectory = _repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var so = proc.StandardOutput.ReadToEndAsync();
        var se = proc.StandardError.ReadToEndAsync();
        Assert.True(proc.WaitForExit(180_000), $"{file} timed out");
        return (proc.ExitCode, so.Result + "\n" + se.Result);
    }

    /// <summary>slangc で ASM にし、AILZ80ASM で組む。ASM の中身を返す</summary>
    private string Build(string env, string name, string source)
    {
        var asmTool = Path.Combine(_repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmTool), "AILZ80ASM がない (make setup-tools で取得)");
        var sl = Path.Combine(_dir, $"{name}_{env}.SL");
        var asm = Path.ChangeExtension(sl, ".ASM");
        var bin = Path.ChangeExtension(sl, ".bin");
        // msxlsx は ROM に置く番地をプログラムが決める (0 なら置かない)
        File.WriteAllText(sl, (env == "msxlsx" ? "CONST ASM MSXROMADR = 0;\n" : "") + source);
        var (code, output) = Run(TestTools.SlangcPath, "-E", env, sl, "-o", asm);
        Assert.True(code == 0, output);
        (code, output) = Run(asmTool, "-i", asm, "-bin", bin, "-f");
        Assert.True(code == 0 && File.Exists(bin), output);
        Assert.DoesNotMatch(new Regex(@"\sE\d{4}:"), output);
        return File.ReadAllText(asm);
    }

    [SkippableTheory]
    [InlineData("lsx")]
    [InlineData("msxlsx")]
    [InlineData("x1")]
    public void AllFour_Assemble(string env)
    {
        var asm = Build(env, "ALL", UsesAll);
        foreach (var label in new[] { "FDELETE", "FRENAME", "FFIRST", "FNEXT", "LSXXFIND", "PPATH" })
            Assert.Matches(new Regex($@"(?m)^{label}:?\s*$"), asm);
        Assert.Contains("LSXTFCB", asm);
        Assert.Contains("LSXDTA", asm);
    }

    [SkippableTheory]
    [InlineData("lsx")]
    [InlineData("msxlsx")]
    [InlineData("x1")]
    public void OtherFileFunctions_DoNotBringTheNewWork(string env)
    {
        var asm = Build(env, "OPEN", UsesOpenOnly);
        Assert.DoesNotContain("LSXTFCB", asm);
        Assert.DoesNotContain("LSXDTA", asm);
        Assert.DoesNotContain("LSXXFIND", asm);
    }
}
