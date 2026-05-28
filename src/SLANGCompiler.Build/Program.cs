namespace SLANGCompiler.Build;

/// <summary>
/// slangbuild — SLANG 二段アセンブル driver。
///
/// slangc が出力する main.ASM / overlay._mN.ASM を AILZ80ASM で 2 段階に
/// アセンブルし、shared runtime シンボルを overlay 側で解決する:
///   1) main.ASM → main.bin + main.sym
///   2) 各 overlay について、main.sym と overlay の `; EXTERN` リストの
///      交集合から imports.asm (filtered EQU) を生成
///   3) overlay.imports.asm + overlay._mN.ASM をまとめて AILZ80ASM に投入
///      → overlay._mN.bin (CALL がリンク済み)
/// </summary>
internal class Program
{
    private const string Version = "0.25.0";

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        if (args[0] is "-v" or "--version")
        {
            Console.WriteLine($"slangbuild {Version}");
            return 0;
        }

        var opts = new Driver.Options();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "-o" when i + 1 < args.Length:
                    opts.OutputPrefix = args[++i];
                    break;
                case "-E" when i + 1 < args.Length:
                    opts.Environment = args[++i];
                    break;
                case "--asm" when i + 1 < args.Length:
                    opts.AsmPath = args[++i];
                    break;
                case "--slangc" when i + 1 < args.Length:
                    opts.SlangcPath = args[++i];
                    break;
                case "--ndc" when i + 1 < args.Length:
                    opts.NdcPath = args[++i];
                    break;
                case "--hudisk" when i + 1 < args.Length:
                    opts.HudiskPath = args[++i];
                    break;
                case "--udostool" when i + 1 < args.Length:
                    opts.UdostoolPath = args[++i];
                    break;
                case "--mzd88" when i + 1 < args.Length:
                    opts.Mzd88Path = args[++i];
                    break;
                case "--oscar-path" when i + 1 < args.Length:
                    opts.OscarPath = args[++i];
                    break;
                case "--c-source" when i + 1 < args.Length:
                    opts.CSourceFiles.Add(args[++i]);
                    break;
                case "--emit" when i + 1 < args.Length:
                    opts.EmitMode = args[++i];
                    break;
                case "--disk-image" when i + 1 < args.Length:
                    opts.DiskImagePath = args[++i];
                    break;
                case "--disk-template" when i + 1 < args.Length:
                    opts.DiskTemplatePath = args[++i];
                    break;
                // --- Phase B: --emit tape 関連 ---
                case "--wav":
                    opts.EmitWav = true;
                    break;
                case "--tape-name" when i + 1 < args.Length:
                    opts.TapeName = args[++i];
                    break;
                case "--tape-load" when i + 1 < args.Length:
                    if (!TryParseAddress(args[++i], out var tload))
                    {
                        Console.Error.WriteLine($"slangbuild: invalid --tape-load: {args[i]}");
                        return 1;
                    }
                    opts.TapeLoad = tload;
                    break;
                case "--tape-exec" when i + 1 < args.Length:
                    if (!TryParseAddress(args[++i], out var texec))
                    {
                        Console.Error.WriteLine($"slangbuild: invalid --tape-exec: {args[i]}");
                        return 1;
                    }
                    opts.TapeExec = texec;
                    break;
                case "--slfs-add" when i + 1 < args.Length:
                    opts.SlfsAddSpecs.Add(args[++i]);
                    break;
                case "-I" when i + 1 < args.Length:
                    opts.IncludePaths.Add(args[++i]);
                    break;
                case "-L" when i + 1 < args.Length:
                    opts.LibraryPaths.Add(args[++i]);
                    break;
                case "--keep-asm":
                    opts.KeepAsm = true;
                    break;
                case "--verbose":
                    opts.Verbose = true;
                    break;
                default:
                    if (a.StartsWith("-"))
                    {
                        Console.Error.WriteLine($"slangbuild: unknown option: {a}");
                        return 1;
                    }
                    if (!string.IsNullOrEmpty(opts.InputPath))
                    {
                        Console.Error.WriteLine($"slangbuild: multiple input files (only one supported): {a}");
                        return 1;
                    }
                    opts.InputPath = a;
                    break;
            }
        }

        // option validation
        if (opts.EmitMode != "bin" && opts.EmitMode != "disk" && opts.EmitMode != "tape")
        {
            Console.Error.WriteLine(
                $"slangbuild: --emit must be 'bin' / 'disk' / 'tape' (got: {opts.EmitMode})");
            return 1;
        }
        if (opts.EmitMode != "disk" && !string.IsNullOrEmpty(opts.DiskImagePath))
        {
            Console.Error.WriteLine(
                "slangbuild: --disk-image requires --emit disk");
            return 1;
        }
        if (opts.EmitMode != "disk" && !string.IsNullOrEmpty(opts.DiskTemplatePath))
        {
            Console.Error.WriteLine(
                "slangbuild: --disk-template requires --emit disk");
            return 1;
        }
        // Phase B: --tape-* / --wav は --emit tape 必須 (= 既存 --disk-image と同規律)
        if (opts.EmitMode != "tape")
        {
            if (opts.EmitWav)
            {
                Console.Error.WriteLine("slangbuild: --wav requires --emit tape");
                return 1;
            }
            if (opts.TapeName != null || opts.TapeLoad.HasValue || opts.TapeExec.HasValue)
            {
                Console.Error.WriteLine(
                    "slangbuild: --tape-name / --tape-load / --tape-exec require --emit tape");
                return 1;
            }
        }

        try
        {
            var driver = new Driver(opts);
            return driver.Run();
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"slangbuild: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            // EnvironmentLoader から throw される env file 構文エラー
            // (= `output:` 値の typo 等)。stack trace は不要 = 一行 message 化。
            Console.Error.WriteLine($"slangbuild: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// CLI で受ける address 文字列を 16-bit int に parse:
    ///   - `$XXXX` (= SLANG / asm 流儀、 必要なら shell quote)
    ///   - `0xXXXX` (= C 流儀)
    ///   - `XXXX` (= decimal)
    /// 範囲外 / parse 失敗 = false。 0..0xFFFF 範囲は呼出側 (TapeImageBuilder) で再 check。
    /// </summary>
    private static bool TryParseAddress(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (s.StartsWith("$"))
            return int.TryParse(s.Substring(1), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        if (s.StartsWith("0x") || s.StartsWith("0X"))
            return int.TryParse(s.Substring(2), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        return int.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine($"SLANG Build Driver v{Version}");
        Console.Error.WriteLine("Usage: slangbuild <input.SL> [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  -o <prefix>     Output file prefix (default: derived from input)");
        Console.Error.WriteLine("  -E <env>        Environment name (default: lsx)");
        Console.Error.WriteLine("  -I <path>       Include search path passed to slangc (repeatable)");
        Console.Error.WriteLine("  -L <path>       Library search path passed to slangc (repeatable)");
        Console.Error.WriteLine("  --asm <path>    AILZ80ASM executable path (override resolution)");
        Console.Error.WriteLine("  --slangc <path> slangc executable path (override resolution)");
        Console.Error.WriteLine("  --ndc <path>    ndc executable path (override resolution; --emit disk + tool=ndc)");
        Console.Error.WriteLine("  --hudisk <path> HuDisk executable path (override resolution; --emit disk + tool=hudisk)");
        Console.Error.WriteLine("  --udostool <p>  udostool executable path (override resolution; --emit disk + tool=udostool)");
        Console.Error.WriteLine("  --mzd88 <path>  mzd88 executable path (override resolution; --emit disk + tool=mzd88)");
        Console.Error.WriteLine("  --oscar-path <p> oscar64 executable path (override resolution; backend=oscar_c env)");
        Console.Error.WriteLine("  --c-source <p>  Extra C source file passed to oscar64 (repeatable; backend=oscar_c only)");
        Console.Error.WriteLine("  --emit <mode>   Output mode: 'bin' (default) / 'disk' (build d88) / 'tape' (build .tap)");
        Console.Error.WriteLine("  --disk-image <p> Output disk image path (default: <output_prefix>.d88)");
        Console.Error.WriteLine("  --disk-template <p> Override env's disk.template path (--emit disk)");
        Console.Error.WriteLine("  --wav           Also emit .wav (--emit tape only、 48kHz/8bit/mono default)");
        Console.Error.WriteLine("  --tape-name <s> Override tape file name (--emit tape only; ASCII printable 1..13 char)");
        Console.Error.WriteLine("  --tape-load <a> Override tape load address (--emit tape only; '$1000' / 0x1000 / 4096)");
        Console.Error.WriteLine("  --tape-exec <a> Override tape exec address (--emit tape only; default = load)");
        Console.Error.WriteLine("  --keep-asm      Keep intermediate ASM / sym files");
        Console.Error.WriteLine("  --verbose       Show subprocess (slangc / AILZ80ASM) output");
        Console.Error.WriteLine("  -h, --help      Show this help");
        Console.Error.WriteLine("  -v, --version   Show version");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Tool resolution order:");
        Console.Error.WriteLine("  slangc:    --slangc → bundled bin → PATH → dotnet run (dev)");
        Console.Error.WriteLine("  AILZ80ASM: --asm → AILZ80ASM_PATH env → PATH → bundled tools/ → repo root (dev)");
        Console.Error.WriteLine("  ndc:       --ndc → NDC_PATH env → bundled tools/ → PATH → repo root (dev)");
        Console.Error.WriteLine("  HuDisk:    --hudisk → HUDISK_PATH env → bundled tools/ → PATH → repo root (dev)");
        Console.Error.WriteLine("  udostool:  --udostool → UDOSTOOL_PATH env → bundled tools/ → install dir → PATH → repo root (dev)");
        Console.Error.WriteLine("  mzd88:     --mzd88 → MZD88_PATH env → bundled tools/ → install dir → PATH → repo root (dev)");
        Console.Error.WriteLine("  oscar64:   --oscar-path → env file oscar_path: → $OSCAR64 → PATH (backend=oscar_c env)");
    }
}
