namespace SLANGCompiler.SlfsPack;

public static class Program
{
    public static int Main(string[] args)
    {
        string? outputPath = null;
        string? mainBinPath = null;
        ushort mainLoad = 0x1000;
        ushort mainExec = 0x1000;
        string volume = "GAMEDISK";
        string mainName = "SLFSMAIN";
        var assets = new List<SlfsPackerLibrary.AssetEntry>();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": outputPath = args[++i]; break;
                case "--main": mainBinPath = args[++i]; break;
                case "--main-load": mainLoad = ParseAddress(args[++i]); break;
                case "--main-exec": mainExec = ParseAddress(args[++i]); break;
                case "--main-name": mainName = args[++i]; break;
                case "--volume": volume = args[++i]; break;
                case "--add":
                    ResolveAddArg(args[++i], assets);
                    break;
                default:
                    Console.Error.WriteLine($"slfs-pack: unknown option: {args[i]}");
                    PrintUsage();
                    return 2;
            }
        }

        if (outputPath == null || mainBinPath == null)
        {
            Console.Error.WriteLine("slfs-pack: -o and --main are required");
            PrintUsage();
            return 2;
        }

        try
        {
            var opts = new SlfsPackerLibrary.Options
            {
                MainBinary = File.ReadAllBytes(mainBinPath),
                MainLoadAddress = mainLoad,
                MainExecuteAddress = mainExec,
                MainFileName = mainName,
                VolumeName = volume,
                Assets = assets,
            };
            var packer = new SlfsPackerLibrary(opts);
            var img = packer.Build();
            File.WriteAllBytes(outputPath, img);
            Console.Error.WriteLine($"slfs-pack: wrote {outputPath} ({img.Length} byte, {assets.Count} asset(s))");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"slfs-pack: error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// --add の引数を解決:
    ///   "name:path[:type]" = 単一 file
    ///   "dir/" or "dir"    = directory (= non-recursive walk、 各 file の name = basename)
    /// </summary>
    private static void ResolveAddArg(string arg, List<SlfsPackerLibrary.AssetEntry> assets)
    {
        // directory 判定: path が dir として存在 + name:path 形式でない (= ":" を含まない or path だけ)
        if (Directory.Exists(arg))
        {
            // non-recursive walk + ordinal sort
            var files = Directory.GetFiles(arg);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files)
            {
                assets.Add(new SlfsPackerLibrary.AssetEntry
                {
                    Name = Path.GetFileName(f),
                    Data = File.ReadAllBytes(f),
                    Type = 0,
                });
            }
            return;
        }

        // name:path[:type] 形式
        var parts = arg.Split(':', 3);
        if (parts.Length < 2)
            throw new ArgumentException($"--add: invalid format '{arg}' (expected 'name:path[:type]' or dir)");
        var name = parts[0];
        var path = parts[1];
        byte type = parts.Length >= 3 ? byte.Parse(parts[2]) : (byte)0;
        assets.Add(new SlfsPackerLibrary.AssetEntry
        {
            Name = name,
            Data = File.ReadAllBytes(path),
            Type = type,
        });
    }

    private static ushort ParseAddress(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x") || s.StartsWith("0X")) return Convert.ToUInt16(s[2..], 16);
        if (s.StartsWith("$")) return Convert.ToUInt16(s[1..], 16);
        return ushort.Parse(s);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: slfs-pack -o <output.d88> --main <main.bin> [options]");
        Console.Error.WriteLine("  --main-load <addr>          main program load address (default $1000)");
        Console.Error.WriteLine("  --main-exec <addr>          main program execute address (default $1000)");
        Console.Error.WriteLine("  --main-name <name>          main file name (default SLFSMAIN, 13 char)");
        Console.Error.WriteLine("  --volume <name>             disk volume name (default GAMEDISK)");
        Console.Error.WriteLine("  --add <name>:<path>[:<type>]  add single file as asset (repeatable)");
        Console.Error.WriteLine("  --add <dir_path>            add all files in dir (non-recursive, repeatable)");
    }
}
