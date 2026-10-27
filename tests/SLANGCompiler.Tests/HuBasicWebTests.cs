using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Text;
using SLANGCompiler.HuBasic.Web;
using SLANGCompiler.Runtime;
using Xunit;
using SkippableFactAttribute = Xunit.SkippableFactAttribute;

namespace SLANGCompiler.Tests;

/// <summary>
/// hubasic2sl をブラウザ (wasm) から呼ぶ口と、CLI と共有する大きさの検査のテスト。
/// </summary>
public class HuBasicWebTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1native.env")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hubasic2sl_web_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>CLI を同じプロセスで動かし、終了コード・stdout・stderr を返す</summary>
    private static (int Code, string Out, string Err) RunCliCaptured(params string[] args)
    {
        var main = typeof(SLANGCompiler.HuBasic.CLI.Program).GetMethod("Main",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        var so = new StringWriter();
        var se = new StringWriter();
        Console.SetOut(so);
        Console.SetError(se);
        try
        {
            var code = (int)main.Invoke(null, new object[] { args })!;
            return (code, so.ToString(), se.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
    }

    /// <summary>
    /// --check-bin の文・出る先・終了コード。bin の側と作業領域の側のそれぞれで、終わりが $FFE0 なら収まり、$FFE1 なら HB0312
    /// </summary>
    [Theory]
    [InlineData(0xEFE0, "2000", 0, "hubasic2sl: $1000-$FFDF に収まります (bin $FFE0、作業領域 $2000、残り 0 バイト)")]
    [InlineData(0xEFE1, "2000", 1, "hubasic2sl: error HB0312: プログラムの終わり $FFE1 (bin $FFE1、作業領域 $2000) が 予約領域 $FFE0 を越えています (1 バイト超過)")]
    [InlineData(16, "FFE0", 0, "hubasic2sl: $1000-$FFDF に収まります (bin $1010、作業領域 $FFE0、残り 0 バイト)")]
    [InlineData(16, "FFE1", 1, "hubasic2sl: error HB0312: プログラムの終わり $FFE1 (bin $1010、作業領域 $FFE1) が 予約領域 $FFE0 を越えています (1 バイト超過)")]
    public void Cli_CheckBinFixedText(int binLength, string workEnd, int expectedCode, string expectedLine)
    {
        var dir = NewTempDir();
        try
        {
            var bin = Path.Combine(dir, "A.bin");
            File.WriteAllBytes(bin, new byte[binLength]);
            File.WriteAllText(Path.Combine(dir, "A.LST"), $"       {workEnd}                     __WORKEND__ EQU $\n");
            var (code, so, se) = RunCliCaptured("--check-bin", bin);
            Assert.Equal(expectedCode, code);
            var (hit, other) = expectedCode == 0 ? (so, se) : (se, so);
            Assert.Equal(expectedLine + Environment.NewLine, hit);
            Assert.Equal("", other);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>ファイルに書いて CLI と同じ道 (TranspileFile) で変換した結果</summary>
    private static HuTranspileResult ViaFile(byte[] bytes, string name, string encoding, HuTranspileOptions opt)
    {
        var dir = NewTempDir();
        try
        {
            var path = Path.Combine(dir, name);
            File.WriteAllBytes(path, bytes);
            return HuTranspiler.TranspileFile(path, encoding, opt with { SourceName = name });
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static readonly byte[] SjisKana =
        Encoding.ASCII.GetBytes("9 REM\r\n10 PRINT \"").Concat(new byte[] { 0xB1, 0xB2, 0xDD }).Concat(Encoding.ASCII.GetBytes("\"\r\n")).ToArray();

    public static IEnumerable<object[]> SameAsFileCases()
    {
        foreach (var f in new[] { "HELLO.BAS", "GRAPH.BAS", "MUSIC.BAS" })
        {
            var bytes = File.ReadAllBytes(Path.Combine(RepoRoot(), "examples", "hubasic", f));
            yield return new object[] { f, bytes, "{}", "auto", new HuTranspileOptions() };
            yield return new object[] { f, bytes, "{\"wait\":\"line:480\",\"stringLen\":63,\"noLineNumbers\":true}", "auto",
                new HuTranspileOptions(Wait: SLANGCompiler.HuBasic.Lowering.HuWaitMode.Line, WaitArg: 480, StringLen: 63, LineNumbers: false) };
            yield return new object[] { f, bytes, "{\"wait\":\"frame:2\",\"assumeDefint\":true,\"emitLineComments\":true,\"dataBudget\":20000}", "auto",
                new HuTranspileOptions(Wait: SLANGCompiler.HuBasic.Lowering.HuWaitMode.Frame, WaitArg: 2, AssumeDefint: true, LineComments: true, DataBudget: 20000) };
        }
        yield return new object[] { "SJIS.BAS", SjisKana, "{\"inputEncoding\":\"sjis\"}", "sjis", new HuTranspileOptions() };
        // UTF-8 として読めないので、auto は X1 の文字コードとして読む (warning HB0110)
        yield return new object[] { "X1.BAS", SjisKana, "", "auto", new HuTranspileOptions() };
        yield return new object[] { "X1.BAS", SjisKana, "{\"inputEncoding\":\"x1\"}", "x1", new HuTranspileOptions() };
        // utf8 を指定すると error HB0111
        yield return new object[] { "U8.BAS", SjisKana, "{\"inputEncoding\":\"utf8\"}", "utf8", new HuTranspileOptions() };
        // ない行への GOTO (error) と、何もしない命令 (warning)
        yield return new object[] { "ERR.BAS", Encoding.UTF8.GetBytes("10 KBUF ON\n20 GOTO 99\n"), "{}", "auto", new HuTranspileOptions() };
    }

    /// <summary>Transpile の SL・診断が、同じ入力・同じオプションの CLI の道と同じ</summary>
    [Theory]
    [MemberData(nameof(SameAsFileCases))]
    public void Web_TranspileSameAsFile(string name, byte[] bytes, string optionsJson, string encoding, HuTranspileOptions opt)
    {
        var web = Parse(HuWebApi.Transpile(bytes, name, optionsJson));
        var reference = ViaFile(bytes, name, encoding, opt);
        Assert.Equal(reference.Output != null, web.GetProperty("ok").GetBoolean());
        Assert.Equal(reference.Output, web.GetProperty("sl").ValueKind == JsonValueKind.Null ? null : web.GetProperty("sl").GetString());
        var diags = web.GetProperty("diagnostics").EnumerateArray().ToList();
        Assert.Equal(reference.Diagnostics.Items.Count, diags.Count);
        for (int i = 0; i < diags.Count; i++)
        {
            var d = reference.Diagnostics.Items[i];
            Assert.Equal(d.Severity == HuSeverity.Error ? "error" : "warning", diags[i].GetProperty("severity").GetString());
            Assert.Equal(d.Code, diags[i].GetProperty("code").GetString());
            Assert.Equal(d.Message, diags[i].GetProperty("message").GetString());
            Assert.Equal(d.SourceLine, diags[i].GetProperty("sourceLine").GetInt32());
            Assert.Equal(d.Column, diags[i].GetProperty("column").GetInt32());
            Assert.Equal(d.BasicLine, diags[i].GetProperty("basicLine").GetInt32());
            Assert.Equal(d.Format(name), diags[i].GetProperty("text").GetString());
        }
        if (name == "ERR.BAS")
        {
            Assert.Contains(diags, d => d.GetProperty("severity").GetString() == "error");
            Assert.Contains(diags, d => d.GetProperty("severity").GetString() == "warning");
        }
    }

    /// <summary>diskMainName は SL の頭の `// disk-main-name:` と同じ。名前が無ければ空</summary>
    [Fact]
    public void Web_DiskMainName()
    {
        var bytes = Encoding.ASCII.GetBytes("10 PRINT 1\n");
        var web = Parse(HuWebApi.Transpile(bytes, "dir/MyGame.bas", "{}"));
        Assert.Equal("MyGame", web.GetProperty("diskMainName").GetString());
        Assert.Contains("// disk-main-name: MyGame\n", web.GetProperty("sl").GetString());
        Assert.Contains("// source: MyGame.bas\n", web.GetProperty("sl").GetString());
        Assert.Equal("", Parse(HuWebApi.Transpile(bytes, "", "{}")).GetProperty("diskMainName").GetString());
    }

    /// <summary>wait は CLI と同じ読み方: N が読めないか 0 以下なら 1、3 つ目からは見ない</summary>
    [Theory]
    [InlineData("{\"wait\":\"line:0\"}", "{\"wait\":\"line:1\"}")]
    [InlineData("{\"wait\":\"line:abc\"}", "{\"wait\":\"line:1\"}")]
    [InlineData("{\"wait\":\"line\"}", "{\"wait\":\"line:1\"}")]
    [InlineData("{\"wait\":\"line:2:extra\"}", "{\"wait\":\"line:2\"}")]
    [InlineData("{\"wait\":\"frame:-3\"}", "{\"wait\":\"frame:1\"}")]
    public void Web_WaitLikeCli(string options, string same)
    {
        var bytes = Encoding.ASCII.GetBytes("10 FOR I=1 TO 3:PRINT I:NEXT\n20 GOTO 10\n");
        var a = Parse(HuWebApi.Transpile(bytes, "W.BAS", options)).GetProperty("sl").GetString();
        var b = Parse(HuWebApi.Transpile(bytes, "W.BAS", same)).GetProperty("sl").GetString();
        Assert.NotNull(a);
        Assert.Equal(b, a);
        // line は待ちの分だけ出力が変わる (frame はこのプログラムでは変わらない)
        if (same.Contains("line"))
            Assert.NotEqual(Parse(HuWebApi.Transpile(bytes, "W.BAS", "{}")).GetProperty("sl").GetString(), a);
    }

    /// <summary>CLI にある誤りは CLI と同じ文 (stderr の 1 行)。変換はしない</summary>
    [Theory]
    [InlineData("{\"inputEncoding\":\"euc\"}", "--input-encoding", "euc")]
    [InlineData("{\"wait\":\"sometimes\"}", "--wait", "sometimes")]
    [InlineData("{\"stringLen\":0}", "--string-len", "0")]
    [InlineData("{\"stringLen\":256}", "--string-len", "256")]
    [InlineData("{\"dataBudget\":0}", "--data-budget", "0")]
    [InlineData("{\"noSuchOption\":1}", "--no-such-option", null)]
    public void Web_OptionErrorsLikeCli(string options, string flag, string? value)
    {
        var web = Parse(HuWebApi.Transpile(Encoding.ASCII.GetBytes("10 PRINT 1\n"), "A.BAS", options));
        Assert.False(web.GetProperty("ok").GetBoolean());
        Assert.False(web.TryGetProperty("sl", out _));
        var error = web.GetProperty("error").GetString()!;
        var dir = NewTempDir();
        try
        {
            var bas = Path.Combine(dir, "A.BAS");
            File.WriteAllText(bas, "10 PRINT 1\n");
            var args = value == null ? new[] { bas, flag } : new[] { bas, flag, value };
            var (code, so, se) = RunCliCaptured(args);
            Assert.Equal(2, code);
            Assert.Equal("", so);
            if (flag == "--no-such-option")
                Assert.Equal("hubasic2sl: 不明なオプション noSuchOption", error);
            else
                Assert.Equal(se.TrimEnd('\r', '\n'), error);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    /// <summary>JSON の誤り・型の違いは ok:false (例外にしない)。空文字列は {} と同じ</summary>
    [Theory]
    [InlineData("{", "オプションの JSON を読めません")]
    [InlineData("null", "object で書いてください")]
    [InlineData("[1]", "object で書いてください")]
    [InlineData("{\"noLineNumbers\":1}", "noLineNumbers は true か false")]
    [InlineData("{\"inputEncoding\":5}", "--input-encoding は")]
    [InlineData("{\"wait\":3}", "--wait は")]
    [InlineData("{\"stringLen\":\"63\"}", "--string-len には")]
    [InlineData("{\"stringLen\":1.5}", "--string-len には")]
    public void Web_JsonErrors(string options, string contains)
    {
        var web = Parse(HuWebApi.Transpile(Encoding.ASCII.GetBytes("10 PRINT 1\n"), "A.BAS", options));
        Assert.False(web.GetProperty("ok").GetBoolean());
        Assert.Contains(contains, web.GetProperty("error").GetString());
    }

    [Fact]
    public void Web_EmptyOptionsSameAsObject()
    {
        var bytes = Encoding.ASCII.GetBytes("10 PRINT 1\n");
        Assert.Equal(HuWebApi.Transpile(bytes, "A.BAS", "{}"), HuWebApi.Transpile(bytes, "A.BAS", ""));
    }

    /// <summary>CheckLayout の文は CLI の --check-bin と同じ。入力の誤りは ok:false</summary>
    [Theory]
    [InlineData(0xEFE0, 0x2000, true, "hubasic2sl: $1000-$FFDF に収まります (bin $FFE0、作業領域 $2000、残り 0 バイト)")]
    [InlineData(0xEFE1, 0x2000, false, "hubasic2sl: error HB0312: プログラムの終わり $FFE1 (bin $FFE1、作業領域 $2000) が 予約領域 $FFE0 を越えています (1 バイト超過)")]
    [InlineData(16, 0xFFE0, true, "hubasic2sl: $1000-$FFDF に収まります (bin $1010、作業領域 $FFE0、残り 0 バイト)")]
    [InlineData(16, 0xFFE1, false, "hubasic2sl: error HB0312: プログラムの終わり $FFE1 (bin $1010、作業領域 $FFE1) が 予約領域 $FFE0 を越えています (1 バイト超過)")]
    public void Web_CheckLayoutLikeCli(int binLength, int workEnd, bool ok, string message)
    {
        var web = Parse(HuWebApi.CheckLayout(binLength, workEnd));
        Assert.Equal(ok, web.GetProperty("ok").GetBoolean());
        Assert.Equal(message, web.GetProperty("message").GetString());
        Assert.Equal(0x1000, web.GetProperty("loadAddress").GetInt32());
        Assert.Equal(0xFFE0, web.GetProperty("reservedStart").GetInt32());
    }

    [Theory]
    [InlineData(-1, 0x2000, "bin の大きさが負です")]
    [InlineData(16, 0x0FFF, "配置先 $1000 より前です")]
    [InlineData(16, -1, "配置先 $1000 より前です")]
    public void Web_CheckLayoutRejectsBadInput(int binLength, int workEnd, string contains)
    {
        var web = Parse(HuWebApi.CheckLayout(binLength, workEnd));
        Assert.False(web.GetProperty("ok").GetBoolean());
        Assert.Contains(contains, web.GetProperty("error").GetString());
        Assert.False(web.TryGetProperty("message", out _));
    }

    [Fact]
    public void Web_Version()
    {
        var web = Parse(HuWebApi.Version());
        var info = web.GetProperty("informational").GetString()!;
        Assert.Equal("0.1.0-alpha", web.GetProperty("version").GetString());
        Assert.StartsWith("0.1.0-alpha", info);
    }

    /// <summary>FromBytes と FromFile は同じ行と診断 (auto・utf8・sjis・x1)</summary>
    [Theory]
    [InlineData("auto")]
    [InlineData("utf8")]
    [InlineData("sjis")]
    [InlineData("x1")]
    public void Source_FromBytesSameAsFromFile(string encoding)
    {
        foreach (var bytes in new[] { SjisKana, Encoding.UTF8.GetBytes("10 PRINT \"あ\"\n20 END\n") })
        {
            var dir = NewTempDir();
            try
            {
                var path = Path.Combine(dir, "A.BAS");
                File.WriteAllBytes(path, bytes);
                var d1 = new HuDiagnosticBag();
                var d2 = new HuDiagnosticBag();
                var a = HuSource.FromFile(path, encoding, d1);
                var b = HuSource.FromBytes(bytes, encoding, d2);
                Assert.Equal(a.Lines.Select(l => l.ToString()), b.Lines.Select(l => l.ToString()));
                Assert.Equal(d1.Items, d2.Items);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }
    }

    /// <summary>wasm の入口の版は hubasic2sl と同じ</summary>
    [Fact]
    public void Wasm_VersionSameAsCli()
    {
        string Ver(string rel)
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), rel));
            var m = System.Text.RegularExpressions.Regex.Match(text, "<Version>(.*?)</Version>");
            Assert.True(m.Success, rel);
            return m.Groups[1].Value;
        }
        Assert.Equal(Ver("src/SLANGCompiler.HuBasic/SLANGCompiler.HuBasic.csproj"),
                     Ver("src/SLANGCompiler.HuBasic.Wasm/SLANGCompiler.HuBasic.Wasm.csproj"));
    }

    /// <summary>manifest の 5 つの項目は EnvironmentLoader と同じ値 (libraries は順番も)</summary>
    [SkippableFact]
    public void Manifest_SameAsEnvironmentLoader()
    {
        var root = RepoRoot();
        var psi = new ProcessStartInfo("python3")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in new[] { "-I", Path.Combine(root, "scripts", "hubasic-wasm-manifest.py"), root, "abc", "false", "0.1.0-alpha" })
            psi.ArgumentList.Add(a);
        Process? proc;
        try { proc = Process.Start(psi); }
        catch (System.ComponentModel.Win32Exception) { proc = null; }
        Skip.If(proc == null, "python3 がない");
        using (proc)
        {
            var so = proc!.StandardOutput.ReadToEndAsync();
            var se = proc.StandardError.ReadToEndAsync();
            Assert.True(proc.WaitForExit(60_000));
            Assert.True(proc.ExitCode == 0, se.Result);
            var m = Parse(so.Result);
            Assert.Equal(0x1000, m.GetProperty("loadAddress").GetInt32());
            Assert.Equal(0xFFE0, m.GetProperty("reservedStart").GetInt32());
            foreach (var name in new[] { "x1native", "x1native_slfs" })
            {
                var cfg = EnvironmentLoader.Load(Path.Combine(root, "runtime", "env", name + ".env"));
                var e = m.GetProperty("envs").GetProperty(name);
                Assert.Equal(cfg.EnvType, e.GetProperty("envType").GetInt32());
                Assert.Equal(cfg.OsType, e.GetProperty("osType").GetInt32());
                Assert.Equal(cfg.DefaultOrg, e.GetProperty("defaultOrg").GetInt32());
                Assert.Equal(cfg.Libraries, e.GetProperty("libraries").EnumerateArray().Select(x => x.GetString()!).ToList());
                var defines = e.GetProperty("defines").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());
                Assert.Equal((cfg.Defines ?? new Dictionary<string, int>()).OrderBy(kv => kv.Key), defines.OrderBy(kv => kv.Key));
            }
        }
    }
}
