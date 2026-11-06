namespace SLANGCompiler.SlfsPack;

/// <summary>slfs-pack hu …: Hu-BASIC と同じ形のディスク (x1native_hufs。docs/HUFS.md) の道具</summary>
public static class HuCommands
{
    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }
        var rest = args.Skip(1).ToArray();
        return args[0] switch
        {
            "pack" => Pack(rest),
            "list" => List(rest),
            "extract" => Extract(rest),
            "add" => Add(rest),
            "delete" => Delete(rest),
            _ => Unknown(args[0]),
        };
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"slfs-pack hu: unknown command: {cmd}");
        PrintUsage();
        return 2;
    }

    // ===== hu pack -o <d88> --main <bin> [--main-name 名前] [--main-load a] [--main-exec a] [--add spec]... =====
    private static int Pack(string[] args)
    {
        string? output = null, main = null;
        string mainName = "HUFSMAIN";
        ushort load = 0x1000, exec = 0x1000;
        bool execSet = false;
        var adds = new List<HuPack.AddSpec>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": output = args[++i]; break;
                case "--main": main = args[++i]; break;
                case "--main-name": mainName = args[++i]; break;
                case "--main-load": load = ParseAddress(args[++i]); break;
                case "--main-exec": exec = ParseAddress(args[++i]); execSet = true; break;
                case "--add": adds.Add(HuPack.ParseAddSpec(args[++i])); break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }
        if (output == null || main == null)
        {
            Console.Error.WriteLine("slfs-pack hu pack: -o と --main が要ります");
            return 2;
        }
        if (!execSet) exec = load;
        var d88 = HuPack.Build(File.ReadAllBytes(main), mainName, load, exec, adds);
        File.WriteAllBytes(output, d88);
        Console.Error.WriteLine($"slfs-pack hu pack: wrote {output} ({d88.Length} byte, files={adds.Count + 1})");
        return 0;
    }

    // ===== hu list <d88> =====
    private static int List(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("slfs-pack hu list <d88>"); return 2; }
        var disk = HuDiskImage.FromD88(File.ReadAllBytes(args[0]));
        Console.WriteLine("  type  name               size  start  records");
        foreach (var e in disk.Entries())
        {
            string records;
            try
            {
                var (chain, last) = disk.Chain(e.StartCluster);
                records = ((chain.Count - 1) * HuDiskImage.RecordsPerCluster + last).ToString();
            }
            catch (InvalidDataException ex) { records = "broken: " + ex.Message; }
            Console.WriteLine($"  ${e.Type:X2}   {e.DisplayName,-17}  {e.Size,5}  {e.StartCluster,5}  {records}");
        }
        Console.WriteLine($"{disk.FreeClusters()} clusters free");
        return 0;
    }

    // ===== hu extract <d88> <名前> -o <file> [--text] =====
    private static int Extract(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("slfs-pack hu extract <d88> <name> -o <file> [--text]"); return 2; }
        string? output = null;
        bool text = false;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-o": output = args[++i]; break;
                case "--text": text = true; break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }
        if (output == null) { Console.Error.WriteLine("slfs-pack hu extract: -o が要ります"); return 2; }
        var disk = HuDiskImage.FromD88(File.ReadAllBytes(args[0]));
        var data = disk.Extract(args[1], text);
        File.WriteAllBytes(output, data);
        Console.Error.WriteLine($"slfs-pack hu extract: wrote {output} ({data.Length} byte)");
        return 0;
    }

    // ===== hu add <d88> <spec> =====
    private static int Add(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("slfs-pack hu add <d88> [<name>=]<file>[,bin|,text]"); return 2; }
        var raw = File.ReadAllBytes(args[0]);
        var disk = HuDiskImage.FromD88(raw);
        var spec = HuPack.ParseAddSpec(args[1]);
        disk.AddFile(spec.Name, File.ReadAllBytes(spec.Path), spec.Kind);
        File.WriteAllBytes(args[0], ReplaceSectors(raw, disk));
        return 0;
    }

    // ===== hu delete <d88> <名前> =====
    private static int Delete(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("slfs-pack hu delete <d88> <name>"); return 2; }
        var raw = File.ReadAllBytes(args[0]);
        var disk = HuDiskImage.FromD88(raw);
        disk.Delete(args[1]);
        File.WriteAllBytes(args[0], ReplaceSectors(raw, disk));
        return 0;
    }

    /// <summary>元の D88 のレコードの中身だけを入れ替える (見出し・セクタの見出し・後ろの別のボリュームは残す)</summary>
    private static byte[] ReplaceSectors(byte[] original, HuDiskImage disk) => disk.PatchD88(original);

    private static ushort ParseAddress(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x") || s.StartsWith("0X")) return Convert.ToUInt16(s[2..], 16);
        if (s.StartsWith("$")) return Convert.ToUInt16(s[1..], 16);
        return ushort.Parse(s);
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("slfs-pack hu: Hu-BASIC (CZ-8FB01) と同じ形の 2D ディスク (x1native_hufs)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  slfs-pack hu pack -o <out.d88> --main <main.bin> [--main-name <name>] [--main-load <a>] [--main-exec <a>] [--add <spec>]...");
        Console.Error.WriteLine("  slfs-pack hu list <d88>");
        Console.Error.WriteLine("  slfs-pack hu extract <d88> <name> -o <file> [--text]");
        Console.Error.WriteLine("  slfs-pack hu add <d88> <spec>");
        Console.Error.WriteLine("  slfs-pack hu delete <d88> <name>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  <spec> = [<name>=]<file>[,bin|,text]");
        Console.Error.WriteLine("    ,text  LF・CRLF を CR にして $1A を付ける (種類 Asc。Hu-BASIC の順ファイル)");
        Console.Error.WriteLine("    ,bin   種類 Bin (大きさの欄に長さ)");
        Console.Error.WriteLine("    なし   中身そのまま、種類 Asc");
        Console.Error.WriteLine("  --text (extract)  $1A の前までにして CR を LF に");
    }
}
