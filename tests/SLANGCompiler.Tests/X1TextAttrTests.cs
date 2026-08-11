using System.Text.RegularExpressions;
using SLANGCompiler.Runtime;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// X1 系 4 env (x1 / sosx1 / x1native / x1native_slfs) に公開する現在セル用テキスト
/// 属性設定関数 `TATTR` の契約テスト。
///
/// TATTR は「呼出時点のテキストカーソル位置のセルの attribute VRAM を 1 バイトだけ
/// 書く」非状態関数で、以下が契約 (= xmil-web 側実装とのパリティ契約):
///
/// - 入力 HL、有効域は $0000-$00FF のみ。低位バイトはマスクせずそのまま書く
/// - 戻り HL=1 成功 / HL=0 検証失敗 (検証失敗時は I/O を一切行わない)
/// - 成功時は canonical attribute port ($2000 + offset) へ **ちょうど 1 回** OUT
/// - text ($30xx) / kanji ($38xx) には書かない、IN もしない
/// - カーソルを進めない・書き戻さない
/// - DI / EI を実行しない (割込状態不変)
///
/// 実 Z80 の挙動は本テストでは検証できないため (エミュレータ非搭載)、ここでは
/// ランタイム asm のメタデータと命令列の構造を pin する。
/// </summary>
public class X1TextAttrTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "runtime", "env", "x1.env")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string RuntimePath(string asmName)
        => Path.Combine(RepoRoot(), "runtime", asmName);

    private static string EnvPath(string envName)
        => Path.Combine(RepoRoot(), "runtime", "env", envName + ".env");

    /// <summary>指定 asm から TATTR の RuntimeFunction を取り出す</summary>
    private static RuntimeFunction LoadTattr(string asmName)
    {
        var path = RuntimePath(asmName);
        Assert.True(File.Exists(path), $"{asmName} が存在しない");
        var functions = RuntimeParser.Parse(File.ReadAllText(path), path);
        var tattr = functions.SingleOrDefault(f => f.Name == "TATTR");
        Assert.True(tattr != null, $"{asmName} に @name TATTR が 1 個だけ存在するべき");
        return tattr!;
    }

    /// <summary>コメント行と空行を除いた実命令行だけを返す</summary>
    private static string[] CodeLines(RuntimeFunction f)
        => f.Code.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith(";"))
            .Select(l => l.Split(';')[0].TrimEnd())   // 行末コメントを落とす
            .Where(l => l.Length > 0)
            .ToArray();

    /// <summary>TATTR を実装している 3 ファイルと、それを使う env</summary>
    public static TheoryData<string> TattrAsmFiles => new()
    {
        "libx1_print.asm",        // x1 (LSX-Dodgers)
        "libx1native_print.asm",  // x1native / x1native_slfs
        "libsos_pcg.asm",         // sosx1
    };

    [Theory]
    [MemberData(nameof(TattrAsmFiles))]
    public void Tattr_HasExpectedMetadata(string asmName)
    {
        var tattr = LoadTattr(asmName);

        // 引数 1 個 (= 属性値のみ、座標は取らない)
        Assert.Equal(1, tattr.ParamCount);
        // overlay 時にメイン側へ集約可能 (= 既存 print 系公開 API と同じ)
        Assert.Equal(RuntimeResidency.Shared, tattr.Residency);
        // 依存が宣言されている (= 未宣言だと link 時に work / helper が解決できない)
        Assert.NotEmpty(tattr.Dependencies);
    }

    [Theory]
    [MemberData(nameof(TattrAsmFiles))]
    public void Tattr_WritesExactlyOneAttributeByte(string asmName)
    {
        var lines = CodeLines(LoadTattr(asmName));

        // canonical attribute port への OUT がちょうど 1 回
        var outs = lines.Where(l => Regex.IsMatch(l, @"^OUT\s*\(\s*C\s*\)", RegexOptions.IgnoreCase)).ToArray();
        Assert.Single(outs);

        // port は $2000 + offset。offset 上位バイトは 0-7 なので bit5 を立てて作る。
        // ($2800 ミラーや $30xx text / $38xx kanji を使っていないことの pin)
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^SET\s+5\s*,\s*B$", RegexOptions.IgnoreCase));
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"\$28|\$30|\$38|02800H|03000H|03800H", RegexOptions.IgnoreCase));
    }

    [Theory]
    [MemberData(nameof(TattrAsmFiles))]
    public void Tattr_DoesNotReadPortsOrChangeInterruptState(string asmName)
    {
        var lines = CodeLines(LoadTattr(asmName));

        // IN を使わない (= 読み出しは行わない)
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^IN\s", RegexOptions.IgnoreCase));
        // 単一 OUT なので critical section 不要。割込状態は完全に不変。
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^(DI|EI)$", RegexOptions.IgnoreCase));
    }

    [Theory]
    [MemberData(nameof(TattrAsmFiles))]
    public void Tattr_DoesNotMoveOrStoreCursor(string asmName)
    {
        var lines = CodeLines(LoadTattr(asmName));

        // カーソルワークへの書き戻しをしない (= 読むだけ、進めない)
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^LD\s*\(\s*(_TXADR|sXYADR)", RegexOptions.IgnoreCase));
        // S-OS のカーソル設定 (sLOC) も呼ばない
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"\bsLOC\b", RegexOptions.IgnoreCase));
    }

    [Theory]
    [MemberData(nameof(TattrAsmFiles))]
    public void Tattr_ValidatesHighByteBeforeAnyIo(string asmName)
    {
        var lines = CodeLines(LoadTattr(asmName));

        // 先頭 3 命令で H != 0 を弾く (= I/O どころか退避よりも前)
        Assert.Matches(@"^LD\s+A\s*,\s*H$", lines[0]);
        Assert.Matches(@"^OR\s+A$", lines[1]);
        Assert.Matches(@"^JR\s+NZ\s*,", lines[2]);

        // 表示範囲判定は 40 桁 = 1000 / 80 桁 = 2000 の二択
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+BC\s*,\s*1000$", RegexOptions.IgnoreCase));
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+BC\s*,\s*2000$", RegexOptions.IgnoreCase));
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^CP\s+41$", RegexOptions.IgnoreCase));

        // 属性の低位バイトをマスクしない (= AND 7 等の色マスク混入を検出)
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^AND\s+\d", RegexOptions.IgnoreCase));
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^AND\s+\$", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Tattr_X1UsesLsxCursorOffsetDirectly()
    {
        var lines = CodeLines(LoadTattr("libx1_print.asm"));

        // LSX と共有する _TXADR は論理 VRAM offset そのものなので変換不要
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+HL\s*,\s*\(_TXADR\)$", RegexOptions.IgnoreCase));
        // 幅は X1WORK の AT_WIDTH (= 初期値付きコード領域、WIDTH() 未呼出でも 80 で有効)
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+A\s*,\s*\(AT_WIDTH\)$", RegexOptions.IgnoreCase));
        Assert.Contains("sWORK", LoadTattr("libx1_print.asm").Dependencies);
        Assert.Contains("X1WORK", LoadTattr("libx1_print.asm").Dependencies);
    }

    [Fact]
    public void Tattr_X1NativeConvertsXyCursorThroughAtVrcalc()
    {
        var tattr = LoadTattr("libx1native_print.asm");
        var lines = CodeLines(tattr);

        // x1native のカーソルは (X,Y) なので AT_VRCALC で offset 化してから判定する
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+HL\s*,\s*\(sXYADR\)$", RegexOptions.IgnoreCase));
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^CALL\s+AT_VRCALC$", RegexOptions.IgnoreCase));
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+A\s*,\s*\(AT_WIDTH\)$", RegexOptions.IgnoreCase));
        Assert.Contains("AT_VRCALC", tattr.Dependencies);
        Assert.Contains("sWORK", tattr.Dependencies);

        // AT_VRCALC が DE を保存する性質を使い、属性を D に退避する (= stack 不使用)。
        // SP に一切触れないことを pin する。
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"^(PUSH|POP)\s", RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Tattr_Sosx1ReadsSosWidthNotAtWidth()
    {
        var tattr = LoadTattr("libsos_pcg.asm");
        var lines = CodeLines(tattr);

        // sosx1 の AT_WIDTH は SLANGINIT が値を入れた直後に __WORK__ ゼロクリアで
        // 潰されるため起動直後は 0。既存 PCGDEF / GETCGROM と同じく S-OS work の
        // sWIDTH ($1F5C) を読まなければならない。
        // (AT_WIDTH を読むと 80 桁時に offset 1000-1999 を誤って拒否する)
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^LD\s+A\s*,\s*\(sWIDTH\)$", RegexOptions.IgnoreCase));
        Assert.DoesNotContain(lines, l => Regex.IsMatch(l, @"\(AT_WIDTH\)", RegexOptions.IgnoreCase));

        // カーソルは S-OS 所有なので sCSR で取得する
        Assert.Contains(lines, l => Regex.IsMatch(l, @"^CALL\s+sCSR$", RegexOptions.IgnoreCase));
        Assert.Contains("SOSCALLS", tattr.Dependencies);
    }

    [Fact]
    public void Tattr_Sosx1BalancesStackOnBothPaths()
    {
        var lines = CodeLines(LoadTattr("libsos_pcg.asm"));

        // sCSR の破壊レジスタが不明なため属性を stack に退避する。
        // 成功パスと拒否パスの両方で POP されること (= PUSH 1 個 / POP 2 個。
        // 拒否パスは PUSH 前から来る TAT_NG と PUSH 後から来る TAT_NGPOP に分かれる)
        var pushes = lines.Count(l => Regex.IsMatch(l, @"^PUSH\s+HL$", RegexOptions.IgnoreCase));
        var pops = lines.Count(l => Regex.IsMatch(l, @"^POP\s+DE$", RegexOptions.IgnoreCase));
        Assert.Equal(1, pushes);
        Assert.Equal(2, pops);
    }

    [Fact]
    public void Tattr_IsNotAddedToSharedNonX1PrintRuntime()
    {
        // libsos_print.asm は sos / sosx1 / sosmz2500 で共有されている。
        // X1 の attribute VRAM 書込をここに置くと MZ-2500 や機種非依存 S-OS で
        // $2000 ポートへの無意味な OUT が発生するため、絶対に入れてはならない。
        var path = RuntimePath("libsos_print.asm");
        var functions = RuntimeParser.Parse(File.ReadAllText(path), path);
        Assert.DoesNotContain(functions, f => f.Name == "TATTR");
    }

    [Theory]
    [InlineData("x1", "libx1_print.asm")]
    [InlineData("sosx1", "libsos_pcg.asm")]
    [InlineData("x1native", "libx1native_print.asm")]
    [InlineData("x1native_slfs", "libx1native_print.asm")]
    public void Tattr_IsReachableFromEachTargetEnv(string envName, string providingAsm)
    {
        // 4 env それぞれが TATTR を実装した asm を libraries に含んでいること
        // (= 公開範囲の pin。env から外れると SLANG から呼べなくなる)
        var config = EnvironmentLoader.Load(EnvPath(envName));
        Assert.Contains(providingAsm, config.Libraries);
    }

    [Theory]
    [InlineData("sos")]
    [InlineData("sosmz2500")]
    [InlineData("lsx")]
    public void Tattr_IsNotExposedToNonX1Envs(string envName)
    {
        // X1 以外の env に TATTR を実装した asm が混ざっていないこと
        var config = EnvironmentLoader.Load(EnvPath(envName));
        Assert.DoesNotContain("libx1_print.asm", config.Libraries);
        Assert.DoesNotContain("libx1native_print.asm", config.Libraries);
        Assert.DoesNotContain("libsos_pcg.asm", config.Libraries);
    }
}
