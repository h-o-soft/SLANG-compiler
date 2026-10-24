using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Lowering;

namespace SLANGCompiler.HuBasic.CLI;

class Program
{
    const string Version = "0.1.0-alpha";

    /// <summary>x1native / x1native_slfs のプログラムの配置先と、割り込み用に予約された領域の先頭</summary>
    const int LoadAddress = 0x1000;
    const int ReservedStart = 0xFFE0;

    static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }
        if (args.Contains("--version"))
        {
            Console.WriteLine($"hub2sl {Version}");
            return 0;
        }

        string? input = null, output = null, checkBin = null, checkLst = null;
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
                    var env = NextArg();
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
                default:
                    if (a.StartsWith('-')) return Fail($"不明なオプション {a}");
                    input = a;
                    break;
            }
        }

        if (checkBin != null) return CheckBin(checkBin, checkLst);
        if (input == null) return Fail("入力ファイルを指定してください");
        if (!File.Exists(input)) return Fail($"入力ファイルがありません: {input}");

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
    /// 組み立て後のプログラムが、配置先から予約領域の手前に収まるか。
    /// bin の大きさだけでなく、ファイルに含まれない作業領域 (SLANG の __WORK__ から __WORKEND__ まで。
    /// 起動時に 0 で埋められる) の終わりも見る。__WORKEND__ はアセンブラの LST (既定は bin と同じ名前の .LST) から読む。
    /// </summary>
    static int CheckBin(string path, string? lstPath)
    {
        if (!File.Exists(path)) return Fail($"bin がありません: {path}");
        long binEnd = LoadAddress + new FileInfo(path).Length;
        lstPath ??= Path.ChangeExtension(path, ".LST");
        if (!File.Exists(lstPath))
            return Fail($"LST がありません: {lstPath} (--lst で指定してください。作業領域の終わりを確かめるのに要ります)");
        var workEnd = ReadWorkEnd(lstPath);
        if (workEnd == null)
            return Fail($"{lstPath} に __WORKEND__ が見つかりません");
        long end = Math.Max(binEnd, workEnd.Value);
        if (end > ReservedStart)
        {
            Console.Error.WriteLine(
                $"hub2sl: error HB0312: プログラムの終わり ${end:X4} (bin ${binEnd:X4}、作業領域 ${workEnd:X4}) が " +
                $"予約領域 ${ReservedStart:X4} を越えています ({end - ReservedStart} バイト超過)");
            return 1;
        }
        Console.WriteLine($"hub2sl: ${LoadAddress:X4}-${end - 1:X4} に収まります " +
                          $"(bin ${binEnd:X4}、作業領域 ${workEnd:X4}、残り {ReservedStart - end} バイト)");
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
        Console.Error.WriteLine($"hub2sl: {message}");
        return 2;
    }

    static void PrintUsage()
    {
        Console.Error.WriteLine($"hub2sl {Version} - Hu-BASIC to SLANG transpiler");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage: hub2sl INPUT.BAS [-o OUT.SL] [options]");
        Console.Error.WriteLine("       hub2sl --check-bin PROG.bin [--lst PROG.LST]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o <path>                出力する SLANG のファイル (既定: 入力の拡張子を .SL に)");
        Console.Error.WriteLine("  --env <name>             x1native (既定) / x1native_slfs");
        Console.Error.WriteLine("  --wait <mode>            none (既定) / line[:N] / frame[:N]");
        Console.Error.WriteLine("                           line:N = 各行の頭で約 0.1ms × N 待つ (N=450〜500 で Hu-BASIC に近い速さの目安)");
        Console.Error.WriteLine("                           frame:N = '#VSYNC の指示コメントの位置で N フレーム待つ");
        Console.Error.WriteLine("  --input-encoding <enc>   auto (既定。UTF-8 として読めなければ X1 の文字コード) / utf8 / sjis /");
        Console.Error.WriteLine("                           x1 (X1 でアスキーセーブしたファイル。1 バイト = X1 の 1 文字)");
        Console.Error.WriteLine("  --emit-line-comments     元の行をコメントで出す");
        Console.Error.WriteLine("  --assume-defint          接尾辞も DEFxxx もない変数を整数にする (Hu-BASIC の既定は単精度の浮動小数)");
        Console.Error.WriteLine("  --data-budget <bytes>    静的データの上限 (既定 24576)");
        Console.Error.WriteLine("  --check-bin <bin>        組み立て後の大きさを検査する (bin と、LST の __WORKEND__)");
        Console.Error.WriteLine("  --lst <lst>              --check-bin で読む LST (既定: bin と同じ名前の .LST)");
    }
}
