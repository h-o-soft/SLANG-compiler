using System.Diagnostics;
using System.Text.Json;
using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Parsing;
using SLANGCompiler.HuBasic.Web;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// hubasic2sl のファイルの命令 (x1native_hufs。段階 1b の順ファイルと、段階 2 のランダム): 構文・解析・環境の選び方・出力・組み立て。
/// 本物の Hu-BASIC と同じ動きかは、エミュレータで本物と並べて確かめた (docs/HUFS.md)
/// </summary>
public class HuBasicFileTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static HuLine Parse(string line, out HuDiagnosticBag diag)
    {
        diag = new HuDiagnosticBag();
        var parsed = HuParser.ParseSingleLine(line, diag);
        Assert.NotNull(parsed);
        return parsed!;
    }

    /// <summary>ファイルの命令をひととおり使う小さなプログラム (自作)</summary>
    private const string FileProgram = """
        10 OPEN "O",#1,"A.DAT":PRINT #1,"X";1,2;TAB(10):PRINT #1,USING "##";3:WRITE #1,1,"B",2.5:CLOSE #1
        20 OPEN "I",#1,"A.DAT":LINE INPUT #1,A$:INPUT #1,N,B$,F:PRINT EOF(1):CLOSE
        30 OPEN "A",#2,"A.DAT":PRINT #2,"Y":CLOSE:KILL "B.DAT":NAME "A.DAT" AS "C.DAT"
        40 MAXFILES 3:OPEN "O",#3,"D.DAT":PRINT #3,"Z":CLOSE
        50 END
        """;

    /// <summary>ランダムの命令をひととおり使う小さなプログラム (自作)</summary>
    private const string RandomProgram = """
        10 OPEN "R",#1,"R.DAT":FIELD #1,4 AS A$,6 AS B$:FIELD 1,10 AS C$
        20 LSET A$="X":RSET B$=STR$(12):PUT #1,0:PUT 1:GET #1,0:GET #1
        30 PRINT LOC(1);LOF(1);ASC(MIRROR$(C$)):A$="Y":SWAP B$,D$:MID$(C$,2,1)="Z":LSET E$="W"
        40 CLOSE
        50 END
        """;

    // ------------------------------------------------------------ 構文

    [Fact]
    public void Parser_FileStatements()
    {
        var line = Parse("10 OPEN \"O\",#1,\"A.DAT\":CLOSE #1,2:CLOSE:KILL \"A\":NAME A$ AS \"B\":MAXFILES 3", out var diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var open = Assert.IsType<HuOpen>(line.Statements[0]);
        Assert.Equal("O", Assert.IsType<HuStr>(open.Mode).Text);
        Assert.Equal(1, Assert.IsType<HuNum>(open.Number).Value);
        Assert.Equal(2, Assert.IsType<HuClose>(line.Statements[1]).Numbers.Count);
        Assert.Empty(Assert.IsType<HuClose>(line.Statements[2]).Numbers);
        Assert.IsType<HuKill>(line.Statements[3]);
        var name = Assert.IsType<HuNameAs>(line.Statements[4]);
        Assert.Equal("A$", Assert.IsType<HuVar>(name.Old).Name);
        Assert.IsType<HuMaxFiles>(line.Statements[5]);

        line = Parse("10 INPUT #1,A,B$:LINE INPUT #N,C$:WRITE #1,A,\"X\":WRITE#2,B:WRITE#(N),C:MAXFILES (N+1)", out diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var inp = Assert.IsType<HuInputFile>(line.Statements[0]);
        Assert.False(inp.Line);
        Assert.Equal(2, inp.Targets.Count);
        Assert.True(Assert.IsType<HuInputFile>(line.Statements[1]).Line);
        Assert.Equal(2, Assert.IsType<HuWrite>(line.Statements[2]).Items.Count);
        Assert.Equal(2, Assert.IsType<HuNum>(Assert.IsType<HuWrite>(line.Statements[3]).Number).Value);
        Assert.IsType<HuVar>(Assert.IsType<HuWrite>(line.Statements[4]).Number);
        Assert.IsType<HuBinary>(Assert.IsType<HuMaxFiles>(line.Statements[5]).Count);
    }

    /// <summary>
    /// PRINT#0 (数の字句の 0) は、いままでどおり字句 1 つだけを番号にする (PRINT#0+1 は画面に +1)。
    /// ほかの番号は式 (本物と同じく PRINT #N+1 は N+1 番)
    /// </summary>
    [Fact]
    public void Parser_PrintChannel()
    {
        var p = Assert.IsType<HuPrint>(Parse("10 PRINT#0+1", out var diag).Statements[0]);
        Assert.False(diag.HasErrors);
        Assert.Equal(0, p.Channel);
        Assert.Null(p.File);
        Assert.IsType<HuUnary>(Assert.IsType<HuPrintExpr>(p.Items[0]).Expr);

        p = Assert.IsType<HuPrint>(Parse("10 PRINT #N+1,\"A\"", out diag).Statements[0]);
        Assert.False(diag.HasErrors);
        Assert.Null(p.Channel);
        Assert.IsType<HuBinary>(p.File);
        Assert.Single(p.Items);
    }

    /// <summary>MAXFILES・WRITE#・AS・DEVI$ は予約語にしない。いまの構文で代入として読めるものは、いままでどおり代入</summary>
    [Theory]
    [InlineData("10 MAXFILES=2")]
    [InlineData("10 MAXFILES(1)=7")]
    [InlineData("10 WRITE#=3")]
    [InlineData("10 WRITE#(1)=7")]
    [InlineData("10 DEVI$=\"A\"")]
    [InlineData("10 AS=4")]
    [InlineData("10 MAXFILESX=1")]
    public void Parser_FileWordsStayAssignments(string text)
    {
        var line = Parse(text, out var diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        Assert.IsType<HuAssign>(Assert.Single(line.Statements));
    }

    [Fact]
    public void Parser_RandomStatements()
    {
        var line = Parse("10 FIELD #1,4 AS A$,N AS B$:LSET A$=\"X\":RSET B$=C$:GET #1,2:PUT 1:GET #N", out var diag);
        Assert.False(diag.HasErrors, string.Join("; ", diag.Items.Select(d => d.Message)));
        var field = Assert.IsType<HuField>(line.Statements[0]);
        Assert.Equal(2, field.Items.Count);
        Assert.Equal("B$", Assert.IsType<HuVar>(field.Items[1].Var).Name);
        Assert.IsType<HuVar>(field.Items[1].Width);
        Assert.False(Assert.IsType<HuLset>(line.Statements[1]).Right);
        Assert.True(Assert.IsType<HuLset>(line.Statements[2]).Right);
        var get = Assert.IsType<HuGetPut>(line.Statements[3]);
        Assert.False(get.Put);
        Assert.NotNull(get.Record);
        var put = Assert.IsType<HuGetPut>(line.Statements[4]);
        Assert.True(put.Put);
        Assert.Null(put.Record);
        Assert.Null(Assert.IsType<HuGetPut>(line.Statements[5]).Record);
    }

    [Theory]
    [InlineData("10 LINE INPUT A$", "HB0301")]      // キーボードからの LINE INPUT は未対応
    [InlineData("10 FIELD #1,4 A$", "HB0203")]      // AS がない
    [InlineData("10 FIELD #1", "HB0203")]
    [InlineData("10 GET (0,0)-(7,7),A", "HB0301")]  // かっこで始まる GET・PUT (グラフィック) はいままでどおり未対応
    [InlineData("10 WRITE #1", "HB0301")]           // 項目のない WRITE#
    [InlineData("10 NAME \"A\" TO \"B\"", "HB0203")]
    [InlineData("10 MAXFILES", "HB0203")]
    public void Parser_FileErrors(string text, string code)
    {
        var diag = new HuDiagnosticBag();
        HuParser.ParseSingleLine(text, diag);
        Assert.Contains(diag.Items, d => d.Code == code);
    }

    // ------------------------------------------------------------ 解析

    [Fact]
    public void Analyzer_FileUseAndCapacity()
    {
        var diag = new HuDiagnosticBag();
        var prog = HuTranspiler.Analyze(FileProgram, diag);
        Assert.NotNull(prog);
        Assert.True(prog!.UsesFiles);
        Assert.Equal(3, prog.FileCapacity);

        // PRINT#0 はファイルを使うことに数えない
        diag = new HuDiagnosticBag();
        prog = HuTranspiler.Analyze("10 PRINT#0,\"A\"\n20 MAXFILES=3", diag);
        Assert.False(prog!.UsesFiles);
        Assert.Equal(0, prog.FileCapacity);

        // 番号か MAXFILES に式があれば 15 本 (warning)
        diag = new HuDiagnosticBag();
        prog = HuTranspiler.Analyze("10 N=1:OPEN \"O\",#N,\"A\"", diag);
        Assert.Equal(15, prog!.FileCapacity);
        Assert.Contains(diag.Items, d => d.Code == "HB0707");
    }

    /// <summary>WRITE #0・PRINT #(0) は画面への生の出力 (本物の WRITE #0 も画面に出す。実測)。番号の 0 はエラーにしない</summary>
    [Theory]
    [InlineData("10 WRITE #0,1")]
    [InlineData("10 PRINT #(0),1")]
    [InlineData("10 N=0:WRITE #N,1")]
    public void Analyzer_ZeroChannelIsScreen(string text)
    {
        var diag = new HuDiagnosticBag();
        Assert.NotNull(HuTranspiler.Analyze(text, diag));
        Assert.DoesNotContain(diag.Items, d => d.Code == "HB0316");
    }

    [Theory]
    [InlineData("10 OPEN \"O\",#16,\"A\"", "HB0316")]
    [InlineData("10 CLOSE #0", "HB0316")]
    [InlineData("10 INPUT #0,A", "HB0316")]
    [InlineData("10 MAXFILES 16", "HB0316")]
    [InlineData("10 DIM A$(2):FIELD #1,4 AS A$(1)", "HB0301")]   // FIELD の配列の要素は未対応
    [InlineData("10 FIELD #1,4 AS A", "HB0404")]
    [InlineData("10 LSET A=\"X\"", "HB0404")]
    [InlineData("10 RSET A$=1", "HB0404")]
    [InlineData("10 GET #16", "HB0316")]
    [InlineData("10 PRINT LOF(0)", "HB0316")]
    [InlineData("10 LINE INPUT #1,A", "HB0404")]
    [InlineData("10 OPEN 1,#1,\"A\"", "HB0404")]
    public void Analyzer_FileErrors(string text, string code)
    {
        var diag = new HuDiagnosticBag();
        HuTranspiler.Analyze(text, diag);
        Assert.Contains(diag.Items, d => d.Code == code);
    }

    [Fact]
    public void Analyzer_RandomBuffersAndFieldVars()
    {
        var diag = new HuDiagnosticBag();
        var prog = HuTranspiler.Analyze(RandomProgram, diag)!;
        Assert.True(prog.UsesFiles);
        Assert.True(prog.UsesRandom);
        Assert.Equal(1, prog.RecordBuffers);
        // FIELD の変数と、それと SWAP する変数 (D$)。LSET だけの変数 (E$) は入らない
        Assert.Equal(new[] { "A$", "B$", "C$", "D$" }, prog.FieldVars.OrderBy(kv => kv.Value).Select(kv => kv.Key));

        // 順ファイルだけならランダムは使わない
        prog = HuTranspiler.Analyze(FileProgram, new HuDiagnosticBag())!;
        Assert.False(prog.UsesRandom);
        Assert.Equal(0, prog.RecordBuffers);

        // 定数の番号の最大だけ緩衝を取る。式のモードの OPEN の番号も数える。番号が式なら、ファイルの緩衝と同じ本数
        prog = HuTranspiler.Analyze("10 MAXFILES 3:OPEN M$,#3,\"A\":OPEN \"O\",#2,\"B\"", new HuDiagnosticBag())!;
        Assert.True(prog.UsesRandom);
        Assert.Equal(3, prog.RecordBuffers);
        prog = HuTranspiler.Analyze("10 N=2:GET #N", new HuDiagnosticBag())!;
        Assert.Equal(15, prog.RecordBuffers);
    }

    // ------------------------------------------------------------ 環境と出力

    [Fact]
    public void Transpile_FileProgramTargetsHufs()
    {
        var r = HuTranspiler.Transpile(FileProgram);
        Assert.NotNull(r.Output);
        Assert.Equal("x1native_hufs", r.RequiredEnv);
        var sl = r.Output!;
        Assert.Contains("// env: x1native_hufs", sl);
        Assert.Contains("BAS_FABI_1();", sl);
        Assert.Contains("BAS_FINIT(3);", sl);
        Assert.Contains("FSETBUF(2, H_FB3);", sl);
        Assert.Contains("ARRAY BYTE H_FB3[271];", sl);
        foreach (var call in new[] { "BAS_FOPEN(", "BAS_FOUT(1)", "BAS_FOUT0()", "BAS_FWSTR(", "BAS_FWFLT(", "BAS_FIN(1)", "BAS_FLINE()",
                     "BAS_FINN()", "BAS_FINS()", "BAS_FEOF(1)", "BAS_FCLOSE(1)", "BAS_FCLOSEA()", "BAS_FKILL(", "BAS_FNAME(", "BAS_FMAXF(3)" })
            Assert.Contains(call, sl);

        // テープ (x1native) と x1native_slfs ではエラー。x1native_hufs はよい
        foreach (var env in new[] { "x1native", "x1native_slfs" })
        {
            var bad = HuTranspiler.Transpile(FileProgram, new HuTranspileOptions(Env: env));
            Assert.Null(bad.Output);
            Assert.Contains(bad.Diagnostics.Items, d => d.Code == "HB0315");
        }
        Assert.NotNull(HuTranspiler.Transpile(FileProgram, new HuTranspileOptions(Env: "x1native_hufs")).Output);
    }

    [Fact]
    public void Transpile_RandomProgram()
    {
        var r = HuTranspiler.Transpile(RandomProgram);
        Assert.Equal("x1native_hufs", r.RequiredEnv);
        var sl = r.Output!;
        Assert.Contains("ARRAY BYTE H_RB[255];", sl);
        Assert.Contains("ARRAY BYTE H_RFT[19];", sl);
        Assert.Contains("BAS_RINIT(H_RB, H_RFT, 1025);", sl);
        foreach (var call in new[] { "BAS_FOPENR(", "BAS_RFLD(1, 255);", "BAS_RFAS(0, S_A, 4);", "BAS_RFAS(2, S_C, 10);",
                     "BAS_RLSET(S_A, K_", "BAS_RGP(1, 0, 3);", "BAS_RGP(1, 0, 2);", "BAS_RGP(1, 0, 1);", "BAS_RGP(1, 0, 0);",
                     "BAS_FLOC(1)", "BAS_FLOF(1)", "BAS_RUNB(0);", "BAS_RSWAP(769, S_B, S_D);", "BAS_RSYNC(2);", "BAS_RMIRV(S_C, 2)" })
            Assert.Contains(call, sl);
        // RSET B$ は表の項目 1 (+1 して 2 倍、RSET で +1)。表にない E$ は 0 (+1 で RSET でない)
        Assert.Contains("BAS_RLSET(S_B, ", sl);
        Assert.Matches(@"BAS_RLSET\(S_B, [^;]*, 5\);", sl);
        Assert.Matches(@"BAS_RLSET\(S_E, K_\d+, 0\);", sl);
        // ふつうの代入は、右辺を計算して入れてから結びつきを外す (右辺の MIRROR$ が緩衝に届くように)
        Assert.Matches(@"BAS_SSET\(S_A, K_\d+\);\s+BAS_RUNB\(0\);", sl);

        // 順ファイルだけのプログラムには、ランダムのものは出ない (段階 1b と同じ出力)
        var seq = HuTranspiler.Transpile(FileProgram).Output!;
        Assert.DoesNotContain("BAS_R", seq);
        Assert.DoesNotContain("BAS_FOPENR", seq);
        Assert.DoesNotContain("H_RB", seq);
    }

    /// <summary>ファイルを使わないプログラムは、--env x1native_hufs のときだけ BAS_FINIT を呼ぶ (ほかは出力を変えない)</summary>
    [Fact]
    public void Transpile_NonFileProgram()
    {
        const string prog = "10 PRINT#0,\"A\":PRINT 1\n20 END\n";
        var plain = HuTranspiler.Transpile(prog);
        Assert.Null(plain.RequiredEnv);
        Assert.DoesNotContain("BAS_F", plain.Output!);
        Assert.DoesNotContain("// env:", plain.Output!);
        Assert.Equal(plain.Output, HuTranspiler.Transpile(prog, new HuTranspileOptions(Env: "x1native_slfs")).Output);
        var hufs = HuTranspiler.Transpile(prog, new HuTranspileOptions(Env: "x1native_hufs")).Output!;
        Assert.Contains("BAS_FABI_1();", hufs);
        Assert.Contains("BAS_FINIT(2);", hufs);
        Assert.DoesNotContain("FSETBUF", hufs);
    }

    [Fact]
    public void Web_RequiredEnv()
    {
        var file = JsonDocument.Parse(HuWebApi.Transpile(System.Text.Encoding.ASCII.GetBytes(FileProgram), "F.BAS", "{}")).RootElement;
        Assert.True(file.GetProperty("ok").GetBoolean());
        Assert.Equal("x1native_hufs", file.GetProperty("requiredEnv").GetString());
        var plain = JsonDocument.Parse(HuWebApi.Transpile("10 PRINT 1\n"u8.ToArray(), "P.BAS", "{}")).RootElement;
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("requiredEnv").ValueKind);
    }

    private static int RunCli(params string[] args)
    {
        var main = typeof(SLANGCompiler.HuBasic.CLI.Program).GetMethod("Main",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)main.Invoke(null, new object[] { args })!;
    }

    /// <summary>--emit disk は、ファイルの命令を使うなら x1native_hufs、使わなければ x1native_slfs で slangbuild を呼ぶ</summary>
    [Fact]
    public void Cli_EmitDiskChoosesEnv()
    {
        if (OperatingSystem.IsWindows()) return;
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var argsFile = Path.Combine(dir, "args.txt");
            var fake = Path.Combine(dir, "fake.sh");
            File.WriteAllText(fake, "#!/bin/sh\nprintf '%s\\n' \"$@\" > '" + argsFile + "'\nfor a; do p=$a; done\n" +
                                    "printf x > \"$p.bin\"; echo '  2000  __WORKEND__ EQU $' > \"$p.LST\"; : > \"$p.d88\"\n");
            File.SetUnixFileMode(fake, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var files = Path.Combine(dir, "F.bas");
            File.WriteAllText(files, FileProgram);
            var plain = Path.Combine(dir, "P.bas");
            File.WriteAllText(plain, "10 PRINT 1\n");
            string Env() => File.ReadAllLines(argsFile)[3];

            Assert.Equal(0, RunCli(files, "--emit", "disk", "--slangbuild", fake));
            Assert.Equal("x1native_hufs", Env());
            Assert.Equal(0, RunCli(plain, "--emit", "disk", "--slangbuild", fake));
            Assert.Equal("x1native_slfs", Env());
            Assert.Equal(0, RunCli(plain, "--emit", "disk", "--env", "x1native_hufs", "--slangbuild", fake));
            Assert.Equal("x1native_hufs", Env());
            // テープ・x1native_slfs を指したディスクでは、変換で止まる
            File.Delete(argsFile);
            Assert.Equal(1, RunCli(files, "--emit", "tape", "--slangbuild", fake));
            Assert.Equal(1, RunCli(files, "--emit", "disk", "--env", "x1native_slfs", "--slangbuild", fake));
            Assert.False(File.Exists(argsFile));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    // ------------------------------------------------------------ 組み立て

    private static (int Code, string Output) RunSlangbuild(string root, string dir, string sl, string asmPath, string outBase, string env)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["SLANG_HOME"] = root;
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        foreach (var a in new[] { "run", "--project", Path.Combine(root, "src", "SLANGCompiler.Build", "SLANGCompiler.Build.csproj"),
                     "-c", "Release", "--", sl, "-E", env, "--asm", asmPath, "--keep-asm", "-o", outBase })
            psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        var so = proc.StandardOutput.ReadToEndAsync();
        var se = proc.StandardError.ReadToEndAsync();
        Assert.True(proc.WaitForExit(180_000), "slangbuild timed out");
        return (proc.ExitCode, so.Result + "\n" + se.Result);
    }

    /// <summary>
    /// ファイルを使う出力は x1native_hufs で組み立てまで通る (libx1_basic の #if exists HUFS の中が使う記号が、BAS_FINIT から
    /// すべてたどれる)。BAS_FINIT を呼ばない出力 (--env なしで変換した、ファイルを使わないもの) を x1native_hufs で組み立てると、
    /// 黙って壊れたものはできずにアセンブルで止まる (設計 4.9)
    /// </summary>
    [SkippableFact]
    public void Build_FileProgramAssemblesForHufs()
    {
        var root = RepoRoot();
        var asmPath = Path.Combine(root, "tools", OperatingSystem.IsWindows() ? "AILZ80ASM.exe" : "AILZ80ASM");
        Skip.IfNot(File.Exists(asmPath), "AILZ80ASM がない (make setup-tools で取得)");
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var sl = Path.Combine(dir, "F.SL");
            File.WriteAllText(sl, HuTranspiler.Transpile(FileProgram).Output!);
            var (code, output) = RunSlangbuild(root, dir, sl, asmPath, Path.Combine(dir, "F"), "x1native_hufs");
            Assert.True(code == 0, output);
            Assert.Contains("BAS_FENDHOOK", File.ReadAllText(Path.Combine(dir, "F.sym")));

            // ランダム (段階 2)
            var slr = Path.Combine(dir, "R.SL");
            File.WriteAllText(slr, HuTranspiler.Transpile(RandomProgram).Output!);
            (code, output) = RunSlangbuild(root, dir, slr, asmPath, Path.Combine(dir, "R"), "x1native_hufs");
            Assert.True(code == 0, output);
            Assert.Contains("BAS_RGP", File.ReadAllText(Path.Combine(dir, "R.sym")));

            const string plain = "10 PRINT \"A\";1,2;TAB(5);\"B\"\n20 END\n";
            var sl2 = Path.Combine(dir, "P.SL");
            File.WriteAllText(sl2, HuTranspiler.Transpile(plain, new HuTranspileOptions(Env: "x1native_hufs")).Output!);
            (code, output) = RunSlangbuild(root, dir, sl2, asmPath, Path.Combine(dir, "P"), "x1native_hufs");
            Assert.True(code == 0, output);

            var sl3 = Path.Combine(dir, "Q.SL");
            File.WriteAllText(sl3, HuTranspiler.Transpile(plain).Output!);
            (code, output) = RunSlangbuild(root, dir, sl3, asmPath, Path.Combine(dir, "Q"), "x1native_hufs");
            Assert.NotEqual(0, code);
            Assert.Contains("BAS_FOUTN", output);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
