namespace SLANGCompiler.SlfsPack;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        var cmd = args[0];
        var rest = args.Skip(1).ToArray();
        try
        {
            return cmd switch
            {
                "pack" => CmdPack(rest),
                "list" => CmdList(rest),
                "info" => CmdInfo(rest),
                "extract" => CmdExtract(rest),
                "extract-main" => CmdExtractMain(rest),
                "extract-save" => CmdExtractSave(rest),
                _ => UnknownCommand(cmd),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"slfs-pack {cmd}: error: {ex.Message}");
            return 1;
        }
    }

    private static int UnknownCommand(string cmd)
    {
        Console.Error.WriteLine($"slfs-pack: unknown command: {cmd}");
        PrintUsage();
        return 2;
    }

    // ===== pack =====
    private static int CmdPack(string[] args)
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
                case "--add": ResolveAddArg(args[++i], assets); break;
                default:
                    throw new ArgumentException($"unknown option: {args[i]}");
            }
        }

        if (outputPath == null || mainBinPath == null)
        {
            Console.Error.WriteLine("slfs-pack pack: -o and --main are required");
            PrintUsage();
            return 2;
        }

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
        Console.Error.WriteLine($"slfs-pack pack: wrote {outputPath} ({img.Length} byte, {assets.Count} asset(s))");
        return 0;
    }

    private static void ResolveAddArg(string arg, List<SlfsPackerLibrary.AssetEntry> assets)
    {
        if (Directory.Exists(arg))
        {
            var files = Directory.GetFiles(arg);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var f in files)
                assets.Add(new() { Name = Path.GetFileName(f), Data = File.ReadAllBytes(f), Type = 0 });
            return;
        }
        var parts = arg.Split(':', 3);
        if (parts.Length < 2)
            throw new ArgumentException($"--add: invalid format '{arg}' (expected 'name:path[:type]' or dir)");
        assets.Add(new()
        {
            Name = parts[0],
            Data = File.ReadAllBytes(parts[1]),
            Type = parts.Length >= 3 ? byte.Parse(parts[2]) : (byte)0,
        });
    }

    // ===== list =====
    private static int CmdList(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("slfs-pack list <d88>"); return 2; }
        var reader = SlfsReader.FromFile(args[0]);
        var sb = reader.ReadSuperblock();
        if (!sb.MagicValid)
        {
            Console.Error.WriteLine("error: not a SLFS disk image (= superblock magic mismatch)");
            return 1;
        }
        var dir = reader.ReadDirectory();
        Console.WriteLine($"volume:  {sb.VolumeName}");
        Console.WriteLine($"entries: {dir.Count}");
        Console.WriteLine();
        Console.WriteLine("  ID  filename     type  start_sec  byte_size");
        Console.WriteLine("  --  -----------  ----  ---------  ---------");
        foreach (var e in dir)
            Console.WriteLine($"  {e.Id,2}  {e.FileName,-11}  ${e.Type:X2}    {e.StartSector,9}  {e.ByteSize,9}");
        return 0;
    }

    // ===== info =====
    private static int CmdInfo(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("slfs-pack info <d88>"); return 2; }
        var reader = SlfsReader.FromFile(args[0]);
        var boot = reader.ReadBootHeader();
        Console.WriteLine("=== boot header (sector 0) ===");
        Console.WriteLine($"  boot_flag:    ${boot.BootFlag:X2}");
        Console.WriteLine($"  filename:     {boot.FileName}");
        Console.WriteLine($"  extension:    {boot.Extension}");
        Console.WriteLine($"  data_size:    {boot.DataSize} byte");
        Console.WriteLine($"  load_addr:    ${boot.LoadAddress:X4}");
        Console.WriteLine($"  exec_addr:    ${boot.ExecuteAddress:X4}");
        Console.WriteLine($"  disk_offset:  ${boot.DiskOffset:X6} (= sector {boot.DiskOffset / 256})");

        var sb = reader.ReadSuperblock();
        Console.WriteLine();
        Console.WriteLine("=== SLFS superblock (sector 1) ===");
        Console.WriteLine($"  magic:        {(sb.MagicValid ? "SLFS (valid)" : "INVALID")}");
        if (!sb.MagicValid) return 1;
        Console.WriteLine($"  version:      {sb.Version}");
        Console.WriteLine($"  geometry:     {sb.Sides} sides × {sb.Tracks} tracks × {sb.SectorsPerTrack} sec/track");
        Console.WriteLine($"  dir_start:    sector {sb.DirStartSector}");
        Console.WriteLine($"  dir_entries:  {sb.DirEntryCount}");
        Console.WriteLine($"  data_start:   sector {sb.DataAreaStartSector}");
        Console.WriteLine($"  save_start:   sector {sb.SaveAreaStartSector}");
        Console.WriteLine($"  save_size:    {sb.SaveSectorCount} sector ({sb.SaveSectorCount * 256} byte)");
        Console.WriteLine($"  volume:       {sb.VolumeName}");
        return 0;
    }

    // ===== extract <d88> <name_or_id> -o <out> =====
    private static int CmdExtract(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("slfs-pack extract <d88> <name|id> -o <out>"); return 2; }
        var d88 = args[0];
        var nameOrId = args[1];
        string? outputPath = null;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": outputPath = args[++i]; break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }
        if (outputPath == null) { Console.Error.WriteLine("slfs-pack extract: -o required"); return 2; }
        var reader = SlfsReader.FromFile(d88);
        byte[] data = int.TryParse(nameOrId, out var id)
            ? reader.ExtractAsset(id)
            : reader.ExtractAssetByName(nameOrId);
        File.WriteAllBytes(outputPath, data);
        Console.Error.WriteLine($"slfs-pack extract: wrote {outputPath} ({data.Length} byte)");
        return 0;
    }

    // ===== extract-main <d88> -o <out> =====
    private static int CmdExtractMain(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("slfs-pack extract-main <d88> -o <out>"); return 2; }
        var d88 = args[0];
        string? outputPath = null;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": outputPath = args[++i]; break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }
        if (outputPath == null) { Console.Error.WriteLine("slfs-pack extract-main: -o required"); return 2; }
        var reader = SlfsReader.FromFile(d88);
        var data = reader.ExtractMain();
        File.WriteAllBytes(outputPath, data);
        Console.Error.WriteLine($"slfs-pack extract-main: wrote {outputPath} ({data.Length} byte)");
        return 0;
    }

    // ===== extract-save <d88> [--offset N] [--count N] -o <out> =====
    private static int CmdExtractSave(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine("slfs-pack extract-save <d88> [--offset N] [--count N] -o <out>"); return 2; }
        var d88 = args[0];
        string? outputPath = null;
        int offset = 0;
        int? count = null;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": outputPath = args[++i]; break;
                case "--offset": offset = int.Parse(args[++i]); break;
                case "--count": count = int.Parse(args[++i]); break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }
        if (outputPath == null) { Console.Error.WriteLine("slfs-pack extract-save: -o required"); return 2; }
        var reader = SlfsReader.FromFile(d88);
        var data = reader.ExtractSave(offset, count);
        File.WriteAllBytes(outputPath, data);
        Console.Error.WriteLine($"slfs-pack extract-save: wrote {outputPath} ({data.Length} byte, offset={offset} count={count?.ToString() ?? "all"})");
        return 0;
    }

    // ===== shared helper =====
    private static ushort ParseAddress(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x") || s.StartsWith("0X")) return Convert.ToUInt16(s[2..], 16);
        if (s.StartsWith("$")) return Convert.ToUInt16(s[1..], 16);
        return ushort.Parse(s);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("slfs-pack: SLFS disk image tool (= pack / list / extract)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  slfs-pack pack    -o <out.d88> --main <main.bin> [--add <spec>] [options]");
        Console.Error.WriteLine("  slfs-pack list    <d88>                                          # asset 一覧");
        Console.Error.WriteLine("  slfs-pack info    <d88>                                          # boot header + superblock");
        Console.Error.WriteLine("  slfs-pack extract <d88> <name|id> -o <out>                       # 個別 asset 抽出");
        Console.Error.WriteLine("  slfs-pack extract-main <d88> -o <out.bin>                        # main bin 抽出");
        Console.Error.WriteLine("  slfs-pack extract-save <d88> [--offset N] [--count N] -o <out>   # save area raw dump");
        Console.Error.WriteLine();
        Console.Error.WriteLine("pack options:");
        Console.Error.WriteLine("  --main-load <addr>          main program load address (default $1000)");
        Console.Error.WriteLine("  --main-exec <addr>          main program execute address (default $1000)");
        Console.Error.WriteLine("  --main-name <name>          main file name (default SLFSMAIN, 13 char)");
        Console.Error.WriteLine("  --volume <name>             disk volume name (default GAMEDISK)");
        Console.Error.WriteLine("  --add <name>:<path>[:<type>]  add single file as asset (repeatable)");
        Console.Error.WriteLine("  --add <dir_path>            add all files in dir (non-recursive, repeatable)");
    }
}
