using System.Diagnostics;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// `slangbuild --emit disk` のディスクイメージ組み立てロジック。
///
/// フロー:
///   1) template d88 を output へ <b>コピー</b> (= template の direct mutate 禁止)
///   2) 一時 staging dir に main bin を <c>disk.main_name</c> (= "PROG.COM") の
///      名前で copy → <c>disk.tool</c> (ndc / hudisk) の delete → put で書き込み
///   3) overlay 0..N について、<c>disk.overlay_name</c> ("M{index}.BIN") に
///      置換した名前で同様に copy → delete → put
///   4) finally で staging dir を必ず削除
///
/// tool 別コマンド形式:
///   - ndc    : <c>ndc D &lt;d88&gt; 0 &lt;name&gt;</c> / <c>ndc P &lt;d88&gt; 0 &lt;file&gt;</c>
///   - hudisk : <c>HuDisk -d &lt;d88&gt; &lt;name&gt;</c> /
///              <c>HuDisk -a &lt;d88&gt; &lt;file&gt; [-r &lt;load&gt;] [-g &lt;exec&gt;]</c>
///              (main は -r main_load -g main_exec、overlay は -r overlay_load のみ)
/// </summary>
public class DiskImageBuilder
{
    private readonly DiskConfig _disk;
    private readonly string? _ndcPath;
    private readonly string? _hudiskPath;
    private readonly string? _templateOverride;
    private readonly bool _verbose;

    /// <summary>
    /// </summary>
    /// <param name="disk">env file の disk: セクション</param>
    /// <param name="ndcPath">tool == "ndc" 時の ndc 実行ファイル絶対パス。それ以外は null 可</param>
    /// <param name="hudiskPath">tool == "hudisk" 時の HuDisk 実行ファイル絶対パス。それ以外は null 可</param>
    /// <param name="verbose">subprocess の I/O を stderr に流す</param>
    /// <param name="templateOverride">--disk-template による env.Disk.Template の上書き</param>
    public DiskImageBuilder(DiskConfig disk,
                            string? ndcPath = null,
                            string? hudiskPath = null,
                            bool verbose = false,
                            string? templateOverride = null)
    {
        _disk = disk;
        _ndcPath = ndcPath;
        _hudiskPath = hudiskPath;
        _verbose = verbose;
        _templateOverride = templateOverride;
    }

    /// <summary>
    /// disk image を組み立てる。成功時 0、失敗時 non-zero を返す。
    /// </summary>
    public int Build(string mainBinPath, IList<string> overlayBinPaths, string outputDiskPath)
    {
        // === 事前チェック ===
        var templatePath = !string.IsNullOrEmpty(_templateOverride)
            ? _templateOverride
            : _disk.Template;
        if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
        {
            Console.Error.WriteLine(
                $"slangbuild: disk template not found: {templatePath}");
            return 1;
        }

        var templateAbs = Path.GetFullPath(templatePath);
        var outputAbs = Path.GetFullPath(outputDiskPath);
        if (string.Equals(templateAbs, outputAbs, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                $"slangbuild: --disk-image must differ from disk template: {templateAbs}");
            return 1;
        }

        if (!File.Exists(mainBinPath))
        {
            Console.Error.WriteLine($"slangbuild: main bin not found: {mainBinPath}");
            return 1;
        }
        if (string.IsNullOrEmpty(_disk.MainName))
        {
            Console.Error.WriteLine("slangbuild: disk.main_name is empty in env file");
            return 1;
        }

        // tool ごとに必要な実行ファイル path がセットされていることを確認
        switch (_disk.Tool)
        {
            case "ndc":
                if (string.IsNullOrEmpty(_ndcPath))
                {
                    Console.Error.WriteLine("slangbuild: ndc path not provided for tool: ndc");
                    return 1;
                }
                break;
            case "hudisk":
                if (string.IsNullOrEmpty(_hudiskPath))
                {
                    Console.Error.WriteLine("slangbuild: HuDisk path not provided for tool: hudisk");
                    return 1;
                }
                break;
            default:
                Console.Error.WriteLine(
                    $"slangbuild: unsupported disk.tool: {_disk.Tool}");
                return 1;
        }

        // 出力 dir 作成
        var outputDir = Path.GetDirectoryName(outputAbs);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // === template copy ===
        if (_verbose)
            Console.Error.WriteLine($"slangbuild: disk: copy {templateAbs} -> {outputAbs}");
        File.Copy(templateAbs, outputAbs, overwrite: true);

        // === staging dir ===
        var stagingDir = outputAbs + ".staging";
        try
        {
            Directory.CreateDirectory(stagingDir);

            // main: MainName 名で copy → 書き込み
            int rc = WriteEntry(mainBinPath, _disk.MainName, isMain: true,
                                outputAbs, stagingDir);
            if (rc != 0) return rc;

            // overlay: overlay_name 名で copy → 書き込み
            for (int i = 0; i < overlayBinPaths.Count; i++)
            {
                if (string.IsNullOrEmpty(_disk.OverlayName))
                {
                    Console.Error.WriteLine(
                        "slangbuild: disk.overlay_name is empty but overlay bins exist");
                    return 1;
                }
                var entryName = _disk.OverlayName.Replace("{index}", i.ToString());
                rc = WriteEntry(overlayBinPaths[i], entryName, isMain: false,
                                outputAbs, stagingDir);
                if (rc != 0) return rc;
            }

            if (_verbose)
                Console.Error.WriteLine($"slangbuild: disk image written: {outputAbs}");
            return 0;
        }
        finally
        {
            try
            {
                if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true);
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>
    /// 1 entry を staging copy → tool 別の delete + put で D88 へ書き込む。
    /// staging に entryName 名で copy してから put することで、D88 内のファイル名が
    /// source bin の元名 (= PROG.BIN) ではなく entryName (= PROG.COM) になる。
    /// </summary>
    private int WriteEntry(string sourceBin, string entryName, bool isMain,
                           string d88, string stagingDir)
    {
        var staged = Path.Combine(stagingDir, entryName);
        File.Copy(sourceBin, staged, overwrite: true);

        return _disk.Tool switch
        {
            "ndc"    => WriteEntryNdc(entryName, staged, d88),
            "hudisk" => WriteEntryHudisk(entryName, staged, d88, isMain),
            _        => 1, // Build() 側で validation 済 (= unreachable)
        };
    }

    private int WriteEntryNdc(string entryName, string staged, string d88)
    {
        // 旧 entry 削除 (= 失敗無視; entry が無い場合 ndc D は non-zero 終了)
        RunTool(_ndcPath!, new[] { "D", d88, "0", entryName }, ignoreFailure: true);

        // 新 entry 書き込み
        var rc = RunTool(_ndcPath!, new[] { "P", d88, "0", staged }, ignoreFailure: false);
        if (rc != 0)
            Console.Error.WriteLine($"slangbuild: ndc P failed for {entryName} (exit {rc})");
        return rc;
    }

    private int WriteEntryHudisk(string entryName, string staged, string d88, bool isMain)
    {
        // 旧 entry 削除 (= 失敗無視)
        RunTool(_hudiskPath!, new[] { "-d", d88, entryName }, ignoreFailure: true);

        // 新 entry 追加: -a <d88> <file> [-r <load>] [-g <exec>]
        var args = new List<string> { "-a", d88, staged };
        var load = isMain ? _disk.MainLoad : _disk.OverlayLoad;
        if (load.HasValue)
        {
            args.Add("-r");
            args.Add($"${load.Value:X}");
        }
        if (isMain && _disk.MainExec.HasValue)
        {
            args.Add("-g");
            args.Add($"${_disk.MainExec.Value:X}");
        }
        var rc = RunTool(_hudiskPath!, args.ToArray(), ignoreFailure: false);
        if (rc != 0)
            Console.Error.WriteLine($"slangbuild: HuDisk -a failed for {entryName} (exit {rc})");
        return rc;
    }

    /// <summary>
    /// subprocess 実行 (ndc / HuDisk 共通)。pipe バッファ詰まり対策で stdout/stderr を
    /// async で吸い、WaitForExit の timeout が確実に効くようにする (= 同期 ReadToEnd
    /// を先に呼ぶと child が hang した時に永遠に block する)。
    /// </summary>
    private int RunTool(string exePath, string[] args, bool ignoreFailure)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        if (_verbose)
            Console.Error.WriteLine($"+ {exePath} {string.Join(" ", args)}");

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(30_000))
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
            Console.Error.WriteLine(
                $"slangbuild: tool timed out after 30s: {exePath} {string.Join(" ", args)}");
            return 1;
        }
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (_verbose)
        {
            if (!string.IsNullOrEmpty(stdout)) Console.Out.Write(stdout);
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }
        else if (!ignoreFailure && proc.ExitCode != 0)
        {
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }

        return proc.ExitCode;
    }
}
