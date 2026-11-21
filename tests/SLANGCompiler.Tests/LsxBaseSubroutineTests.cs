using System.Diagnostics;
using System.Text.RegularExpressions;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// liblsx_base・libmsxlsx_base の S-OS 風の下請け sMSG・sMSX・sNL・sPAUSE。
/// これらを ASM から呼ぶプログラムが、その env (lsx・msxlsx・x1・msx2・cpm) で組み立てられることを確かめる
/// </summary>
public class LsxBaseSubroutineTests : IDisposable
{
    private readonly string _repo = TestTools.RepoRoot();
    private readonly string _dir;

    public LsxBaseSubroutineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "slang_lsxsub_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    // sMSG は CR まで、sMSX は 0 まで出す。sPAUSE は CALL の後の DW に、ESC・Ctrl+C で飛ぶ先を置く
    private const string UsesAll = @"MAIN()
BEGIN
#ASM
  LD DE,SUBM1
  CALL sMSG
  CALL sNL
  LD DE,SUBM2
  CALL sMSX
  CALL sNL
  CALL sPAUSE
  DW SUBBRK
SUBBRK:
  RET
SUBM1: DB ""A"",0DH
SUBM2: DB ""B"",0
#END
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

    [SkippableTheory]
    [InlineData("lsx")]
    [InlineData("msxlsx")]
    [InlineData("x1")]
    [InlineData("msx2")]
    [InlineData("cpm")]
    public void MsgNlPause_Assemble(string env)
    {
        var asmTool = Path.Combine(_repo, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmTool), "AILZ80ASM がない (make setup-tools で取得)");
        var sl = Path.Combine(_dir, $"SUB_{env}.SL");
        var asmPath = Path.ChangeExtension(sl, ".ASM");
        var bin = Path.ChangeExtension(sl, ".bin");
        // msxlsx は ROM に置く番地をプログラムが決める (0 なら置かない)
        File.WriteAllText(sl, (env == "msxlsx" ? "CONST ASM MSXROMADR = 0;\n" : "") + UsesAll);
        var (code, output) = Run(TestTools.SlangcPath, "-E", env, sl, "-o", asmPath);
        Assert.True(code == 0, output);
        (code, output) = Run(asmTool, "-i", asmPath, "-bin", bin, "-f");
        Assert.True(code == 0 && File.Exists(bin), output);
        Assert.DoesNotMatch(new Regex(@"\sE\d{4}:"), output);

        var asm = File.ReadAllText(asmPath);
        // sNL は sPCLR を呼ぶので、sPCLR も入る
        foreach (var label in new[] { "sMSG", "sMSX", "sNL", "sPCLR", "sPAUSE", "sBRKEY" })
            Assert.Matches(new Regex($@"(?m)^{label}:?\s*$"), asm);
    }
}
