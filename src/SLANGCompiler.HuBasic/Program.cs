using SLANGCompiler.HuBasic;
using SLANGCompiler.HuBasic.Lowering;

namespace SLANGCompiler.HuBasic.CLI;

class Program
{
    const string Version = "0.27.0";

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

        string? input = null, output = null, checkBin = null;
        string encoding = "utf8";
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
                    if (encoding != "utf8" && encoding != "sjis")
                        return Fail($"--input-encoding は utf8 か sjis です: {encoding}");
                    break;
                case "--emit-line-comments":
                    opt = opt with { LineComments = true };
                    break;
                case "--data-budget":
                    if (!int.TryParse(NextArg(), out var budget) || budget <= 0)
                        return Fail("--data-budget にはバイト数を指定してください");
                    opt = opt with { DataBudget = budget };
                    break;
                case "--check-bin":
                    checkBin = NextArg();
                    break;
                default:
                    if (a.StartsWith('-')) return Fail($"不明なオプション {a}");
                    input = a;
                    break;
            }
        }

        if (checkBin != null) return CheckBin(checkBin);
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

    /// <summary>組み立て後の bin が、配置先から予約領域の手前に収まるか</summary>
    static int CheckBin(string path)
    {
        if (!File.Exists(path)) return Fail($"bin がありません: {path}");
        long end = LoadAddress + new FileInfo(path).Length;
        if (end > ReservedStart)
        {
            Console.Error.WriteLine(
                $"hub2sl: error HB0312: {path} の末尾 ${end:X4} が予約領域 ${ReservedStart:X4} を越えています " +
                $"({end - ReservedStart} バイト超過)");
            return 1;
        }
        Console.WriteLine($"hub2sl: {path} は ${LoadAddress:X4}-${end - 1:X4} に収まります (残り {ReservedStart - end} バイト)");
        return 0;
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
        Console.Error.WriteLine("       hub2sl --check-bin PROG.bin");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o <path>                出力する SLANG のファイル (既定: 入力の拡張子を .SL に)");
        Console.Error.WriteLine("  --env <name>             x1native (既定) / x1native_slfs");
        Console.Error.WriteLine("  --wait <mode>            none (既定) / line[:N] / frame[:N]");
        Console.Error.WriteLine("  --input-encoding <enc>   utf8 (既定) / sjis");
        Console.Error.WriteLine("  --emit-line-comments     元の行をコメントで出す");
        Console.Error.WriteLine("  --data-budget <bytes>    静的データの上限 (既定 24576)");
        Console.Error.WriteLine("  --check-bin <bin>        組み立て後の bin の大きさを検査する");
    }
}
