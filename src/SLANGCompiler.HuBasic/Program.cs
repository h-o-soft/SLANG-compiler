using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Lowering;

namespace SLANGCompiler.HuBasic.CLI;

class Program
{
    const string Version = "0.1.0-alpha";

    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }
        if (args.Contains("--version"))
        {
            Console.WriteLine($"hubasic2sl {Version}");
            return 0;
        }

        string? input = null, output = null, checkBin = null, checkLst = null;
        string? env = null, emit = null, diskMainName = null, tapeName = null, slangbuild = null;
        bool wav = false;
        string encoding = "auto";
        var opt = new HuTranspileOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? NextArg() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "-o":
                    output = NextArg();
                    break;
                case "--env":
                    env = NextArg();
                    if (env != "x1native" && env != "x1native_slfs")
                        return Fail($"--env は x1native か x1native_slfs です: {env}");
                    break;
                case "--wait":
                    var w = NextArg() ?? "";
                    var parts = w.Split(':');
                    int n = parts.Length > 1 && int.TryParse(parts[1], out var v) && v > 0 ? v : 1;
                    opt = parts[0] switch
                    {
                        "none" => opt with { Wait = HuWaitMode.None },
                        "line" => opt with { Wait = HuWaitMode.Line, WaitArg = n },
                        "frame" => opt with { Wait = HuWaitMode.Frame, WaitArg = n },
                        _ => opt,
                    };
                    if (parts[0] is not ("none" or "line" or "frame"))
                        return Fail($"--wait は none / line[:N] / frame[:N] です: {w}");
                    break;
                case "--input-encoding":
                    encoding = NextArg() ?? "";
                    if (encoding is not ("auto" or "utf8" or "sjis" or "x1"))
                        return Fail($"--input-encoding は auto・utf8・sjis・x1 のどれかです: {encoding}");
                    break;
                case "--emit-line-comments":
                    opt = opt with { LineComments = true };
                    break;
                case "--assume-defint":
                    opt = opt with { AssumeDefint = true };
                    break;
                case "--no-line-numbers":
                    opt = opt with { LineNumbers = false };
                    break;
                case "--string-len":
                    if (!int.TryParse(NextArg(), out var len) || len < 1 || len > HuAnalyzer.MaxStringLen)
                        return Fail($"--string-len には文字列 1 本の長さの上限 (1〜{HuAnalyzer.MaxStringLen}) を指定してください");
                    opt = opt with { StringLen = len };
                    break;
                case "--data-budget":
                    if (!int.TryParse(NextArg(), out var budget) || budget <= 0)
                        return Fail("--data-budget にはバイト数を指定してください");
                    opt = opt with { DataBudget = budget };
                    break;
                case "--check-bin":
                    checkBin = NextArg();
                    break;
                case "--lst":
                    checkLst = NextArg();
                    break;
                case "--emit":
                    emit = NextArg();
                    if (emit is not ("disk" or "tape"))
                        return Fail($"--emit は disk か tape です: {emit}");
                    break;
                case "--disk-main-name":
                    diskMainName = NextArg() ?? "";
                    if (diskMainName.Length == 0) return Fail("--disk-main-name には IPL の起動名を指定してください");
                    break;
                case "--tape-name":
                    tapeName = NextArg() ?? "";
                    if (tapeName.Length == 0) return Fail("--tape-name にはテープの名前を指定してください");
                    break;
                case "--wav":
                    wav = true;
                    break;
                case "--slangbuild":
                    slangbuild = NextArg() ?? "";
                    if (slangbuild.Length == 0) return Fail("--slangbuild には slangbuild の実行ファイルを指定してください");
                    break;
                default:
                    if (a.StartsWith('-')) return Fail($"不明なオプション {a}");
                    input = a;
                    break;
            }
        }

        if (emit == null)
        {
            if (diskMainName != null) return Fail("--disk-main-name は --emit disk と一緒に書いてください");
            if (tapeName != null || wav) return Fail("--tape-name と --wav は --emit tape と一緒に書いてください");
            if (slangbuild != null) return Fail("--slangbuild は --emit disk か --emit tape と一緒に書いてください");
        }
        else
        {
            if (checkBin != null) return Fail("--emit と --check-bin は一緒に書けません");
            if (emit == "disk" && (tapeName != null || wav)) return Fail("--tape-name と --wav は --emit tape のときだけ書けます");
            if (emit == "tape" && diskMainName != null) return Fail("--disk-main-name は --emit disk のときだけ書けます");
            var envForEmit = emit == "disk" ? "x1native_slfs" : "x1native";
            if (env != null && env != envForEmit) return Fail($"--emit {emit} は --env {envForEmit} です: {env}");
            env = envForEmit;
        }

        if (checkBin != null) return CheckBin(checkBin, checkLst);
        if (input == null) return Fail("入力ファイルを指定してください");
        if (!File.Exists(input)) return Fail($"入力ファイルがありません: {input}");

        if (emit != null) return Emit(input, output, encoding, opt, emit, env!, diskMainName, tapeName, wav, slangbuild);

        opt = opt with { SourceName = Path.GetFileName(input) };
        var result = HuTranspiler.TranspileFile(input, encoding, opt);
        foreach (var d in result.Diagnostics.Items)
            Console.Error.WriteLine(d.Format(Path.GetFileName(input)));
        if (result.Output == null) return 1;

        output ??= Path.ChangeExtension(input, ".SL");
        File.WriteAllText(output, result.Output);
        return 0;
    }

    /// <summary>
    /// --emit disk|tape: 変換 → slangbuild → 大きさの検査。名前の元 (拡張子なし) は -o か入力から決め、
    /// SLANG は &lt;元&gt;.SL、イメージは &lt;元&gt;.d88 か &lt;元&gt;.tap (--wav なら &lt;元&gt;.wav も)。
    /// 止まったら、どの段で止まったかを最後の 1 行で言い (終了コード 1)、イメージを残さない
    /// </summary>
    static int Emit(string input, string? output, string encoding, HuTranspileOptions opt, string emit, string env,
                    string? diskMainName, string? tapeName, bool wav, string? slangbuildPath)
    {
        var imageExt = emit == "disk" ? ".d88" : ".tap";
        var outExt = output != null ? Path.GetExtension(output).ToLowerInvariant() : "";
        if (outExt is ".d88" or ".tap" && outExt != imageExt)
            return Fail($"--emit {emit} の出力は {imageExt} です: -o {output}");
        var prefix = Path.GetFullPath(Path.ChangeExtension(output ?? input, null));
        var sl = prefix + ".SL";
        var image = prefix + imageExt;
        var images = wav ? new[] { image, prefix + ".wav" } : new[] { image };
        var inputPath = Path.GetFullPath(input);
        // 作るファイルと、slangbuild が途中で作って消すファイル (Driver の outputBase + .ASM・.sym・.inc・.pass1.sym、SLFS の .assets.inc)
        var made = images.Concat(new[] { ".SL", ".bin", ".LST", ".ASM", ".sym", ".inc", ".pass1.sym", ".assets.inc" }.Select(e => prefix + e));
        foreach (var f in made)
            if (string.Equals(f, inputPath, StringComparison.OrdinalIgnoreCase))
                return Fail($"入力 {input} と、作るファイル {Path.GetFileName(f)} が同じです (-o で別の名前にしてください)");

        // 前の回のイメージが残っていると、途中で止まってもできたように見えるので先に消す
        var deleteError = DeleteImages(images, out _);
        if (deleteError != null) return Stop($"変換の前で止まりました: {deleteError}");

        HuTranspileResult result;
        try
        {
            result = HuTranspiler.TranspileFile(input, encoding, opt with { SourceName = Path.GetFileName(input) });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Stop($"変換で止まりました: {input} を読めません ({e.Message})");
        }
        foreach (var d in result.Diagnostics.Items)
            Console.Error.WriteLine(d.Format(Path.GetFileName(input)));
        if (result.Output == null)
            return Stop("変換で止まりました (上の error を直してください)。組み立てはしていません");
        try
        {
            File.WriteAllText(sl, result.Output);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Stop($"変換で止まりました: {sl} に書けません ({e.Message})");
        }

        var tool = new SlangbuildRunner().Resolve(slangbuildPath, out var tried);
        if (tool == null)
            return Stop($"組み立てで止まりました: slangbuild が見つかりません (探した所: {tried})。--slangbuild <path> で指定できます");
        var args = new List<string> { "--emit", emit, "-E", env };
        if (diskMainName != null) args.AddRange(new[] { "--disk-main-name", diskMainName });
        if (tapeName != null) args.AddRange(new[] { "--tape-name", tapeName });
        if (wav) args.Add("--wav");
        args.AddRange(new[] { sl, "-o", prefix });
        int code;
        try
        {
            code = SlangbuildRunner.Run(tool, args);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return Stop($"組み立てで止まりました: {tool.FileName} を動かせません ({e.Message})");
        }
        if (code != 0)
            return StopAndDelete($"組み立て (slangbuild) で止まりました (終了コード {code}。上の slangbuild: の行を見てください)", images);
        var missing = images.FirstOrDefault(f => !File.Exists(f));
        if (missing != null)
            return StopAndDelete($"組み立て (slangbuild) で止まりました ({Path.GetFileName(missing)} ができていません)", images);

        int check;
        try
        {
            check = CheckBin(prefix + ".bin", prefix + ".LST");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"hubasic2sl: {prefix}.LST を読めません ({e.Message})");
            check = 2;
        }
        if (check != 0) return StopAndDelete("大きさの検査で止まりました", images);
        Console.WriteLine($"hubasic2sl: {image} を作りました");
        return 0;
    }

    /// <summary>あるイメージを消す。消せなければその訳 (消せたら null)。消したものの名前を deleted に</summary>
    static string? DeleteImages(IEnumerable<string> images, out List<string> deleted)
    {
        deleted = new List<string>();
        foreach (var f in images)
        {
            if (!File.Exists(f)) continue;
            try
            {
                File.Delete(f);
                deleted.Add(Path.GetFileName(f));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return $"{f} を消せませんでした ({e.Message})";
            }
        }
        return null;
    }

    /// <summary>止まったときに、できかけのイメージを消してから、止まった段を言う</summary>
    static int StopAndDelete(string message, IEnumerable<string> images)
    {
        var error = DeleteImages(images, out var deleted);
        if (error != null) return Stop($"{message}。{error}");
        return Stop(deleted.Count > 0 ? $"{message}。{string.Join("・", deleted)} は消しました" : message);
    }

    static int Stop(string message)
    {
        Console.Error.WriteLine($"hubasic2sl: {message}");
        return 1;
    }

    /// <summary>
    /// 組み立て後のプログラムが、配置先から予約領域の手前に収まるか。
    /// bin の大きさだけでなく、ファイルに含まれない作業領域 (SLANG の __WORK__ から __WORKEND__ まで。
    /// 起動時に 0 で埋められる) の終わりも見る。__WORKEND__ はアセンブラの LST (既定は bin と同じ名前の .LST) から読む。
    /// </summary>
    static int CheckBin(string path, string? lstPath)
    {
        if (!File.Exists(path)) return Fail($"bin がありません: {path}");
        long binLength = new FileInfo(path).Length;
        lstPath ??= Path.ChangeExtension(path, ".LST");
        if (!File.Exists(lstPath))
            return Fail($"LST がありません: {lstPath} (--lst で指定してください。作業領域の終わりを確かめるのに要ります)");
        var workEnd = ReadWorkEnd(lstPath);
        if (workEnd == null)
            return Fail($"{lstPath} に __WORKEND__ が見つかりません");
        var (fits, message) = HuLayout.Check(binLength, workEnd.Value);
        if (!fits)
        {
            Console.Error.WriteLine(message);
            return 1;
        }
        Console.WriteLine(message);
        return 0;
    }

    /// <summary>AILZ80ASM の LST の「<番地>  __WORKEND__ EQU ...」の行から番地を読む</summary>
    internal static long? ReadWorkEnd(string lstPath)
    {
        var re = new System.Text.RegularExpressions.Regex(@"^\s*(?:[0-9A-F]{6}\s+)?([0-9A-F]{4})\s+__WORKEND__\s+EQU\b");
        foreach (var line in File.ReadLines(lstPath))
        {
            var m = re.Match(line);
            if (m.Success) return Convert.ToInt64(m.Groups[1].Value, 16);
        }
        return null;
    }

    static int Fail(string message)
    {
        Console.Error.WriteLine($"hubasic2sl: {message}");
        return 2;
    }

    static void PrintUsage()
    {
        Console.Error.WriteLine($"hubasic2sl {Version} - Hu-BASIC to SLANG transpiler");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage: hubasic2sl INPUT.BAS [-o OUT.SL] [options]");
        Console.Error.WriteLine("       hubasic2sl INPUT.BAS --emit disk|tape [-o OUT] [options]");
        Console.Error.WriteLine("       hubasic2sl --check-bin PROG.bin [--lst PROG.LST]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o <path>                出力する SLANG のファイル (既定: 入力の拡張子を .SL に)");
        Console.Error.WriteLine("  --env <name>             x1native (既定) / x1native_slfs");
        Console.Error.WriteLine("  --wait <mode>            none (既定) / line[:N] / frame[:N]");
        Console.Error.WriteLine("                           line:N = 各行の頭で約 0.1ms × N 待つ (N=450〜500 で Hu-BASIC に近い速さの目安)");
        Console.Error.WriteLine("                           frame = '#VSYNC n の指示コメントの位置で n フレーム待つ (n がなければ 1)");
        Console.Error.WriteLine("                           (VSYNC [n] の文は --wait によらず n フレーム待つ)");
        Console.Error.WriteLine("  --input-encoding <enc>   auto (既定。UTF-8 として読めなければ X1 の文字コード) / utf8 / sjis /");
        Console.Error.WriteLine("                           x1 (X1 でアスキーセーブしたファイル。1 バイト = X1 の 1 文字)");
        Console.Error.WriteLine("  --emit-line-comments     元の行をコメントで出す");
        Console.Error.WriteLine("  --assume-defint          接尾辞も DEFxxx もない変数を整数にする (Hu-BASIC の既定は単精度の浮動小数)");
        Console.Error.WriteLine("  --no-line-numbers        各行の頭で実行中の行番号を書かない (小さく速くなる。実行時エラーに行番号が出ない)");
        Console.Error.WriteLine("  --string-len <N>         文字列 1 本の長さの上限 (1〜255、既定 255)。文字列の変数と配列が小さくなる");
        Console.Error.WriteLine("                           (超える文字列を入れると String too long)");
        Console.Error.WriteLine("  --data-budget <bytes>    静的データの上限 (既定 24576)");
        Console.Error.WriteLine("  --check-bin <bin>        組み立て後の大きさを検査する (bin と、LST の __WORKEND__)");
        Console.Error.WriteLine("  --lst <lst>              --check-bin で読む LST (既定: bin と同じ名前の .LST)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  --emit disk|tape         変換 → slangbuild → 大きさの検査を続けてして、ディスクイメージ (.d88) かテープ (.tap) を作る");
        Console.Error.WriteLine("                           (disk は x1native_slfs、tape は x1native。名前は -o か入力の拡張子を除いたもの + .SL / .d88 / .tap)");
        Console.Error.WriteLine("  --disk-main-name <name>  IPL の起動名 (--emit disk。既定は BASIC のファイル名)");
        Console.Error.WriteLine("  --tape-name <name>       テープの名前 (--emit tape。既定は PROG)");
        Console.Error.WriteLine("  --wav                    .wav も作る (--emit tape)");
        Console.Error.WriteLine("  --slangbuild <path>      呼ぶ slangbuild (既定: hubasic2sl と同じ所、bin/、開発の publish、PATH、dotnet run の順に探す)");
    }
}
