using System.Diagnostics;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Build;

/// <summary>
/// `slangbuild --emit disk` のディスクイメージ組み立てロジック。
///
/// フロー:
///   1) template d88 を output へ <b>コピー</b> (= template の direct mutate 禁止)
///   2) 一時 staging dir に main bin を <c>disk.main_name</c> (= "PROG.COM") の
///      名前で copy → ndc D で旧 entry 削除 → ndc P で書き込み
///   3) overlay 0..N について、<c>disk.overlay_name</c> ("M{index}.BIN") に
///      置換した名前で同様に copy → D → P
///   4) finally で staging dir を必ず削除
///
/// Phase 1 制約 (= EnvironmentLoader 側でも一応 validate):
///   - format = "d88" のみ
///   - tool = "ndc" のみ
/// </summary>
public class DiskImageBuilder
{
    private readonly DiskConfig _disk;
    private readonly string _ndcPath;
    private readonly bool _verbose;

    public DiskImageBuilder(DiskConfig disk, string ndcPath, bool verbose = false)
    {
        _disk = disk;
        _ndcPath = ndcPath;
        _verbose = verbose;
    }

    /// <summary>
    /// disk image を組み立てる。成功時 0、失敗時 non-zero を返す。
    /// </summary>
    /// <param name="mainBinPath">main bin の絶対パス (slangbuild が生成した PROG.bin)</param>
    /// <param name="overlayBinPaths">overlay bin の絶対パスを index 順 (0..N) に並べたもの</param>
    /// <param name="outputDiskPath">出力 disk image の絶対パス</param>
    public int Build(string mainBinPath, IList<string> overlayBinPaths, string outputDiskPath)
    {
        // === 事前チェック ===
        if (string.IsNullOrEmpty(_disk.Template) || !File.Exists(_disk.Template))
        {
            Console.Error.WriteLine(
                $"slangbuild: disk template not found: {_disk.Template}");
            return 1;
        }

        var templateAbs = Path.GetFullPath(_disk.Template);
        var outputAbs = Path.GetFullPath(outputDiskPath);
        // template を直接書き換える事故を防ぐ
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

        // 出力 dir を作成
        var outputDir = Path.GetDirectoryName(outputAbs);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // === template copy ===
        if (_verbose)
            Console.Error.WriteLine($"slangbuild: disk: copy {templateAbs} -> {outputAbs}");
        File.Copy(templateAbs, outputAbs, overwrite: true);

        // === staging dir ===
        // staging は output と同じ dir に置く (= cross-device の File.Copy 失敗を避ける)
        var stagingDir = outputAbs + ".staging";
        try
        {
            Directory.CreateDirectory(stagingDir);

            // main bin を MainName 名で copy → 書き込み
            int rc = WriteEntry(mainBinPath, _disk.MainName, outputAbs, stagingDir);
            if (rc != 0) return rc;

            // overlay bin を overlay_name 名で copy → 書き込み
            for (int i = 0; i < overlayBinPaths.Count; i++)
            {
                if (string.IsNullOrEmpty(_disk.OverlayName))
                {
                    Console.Error.WriteLine(
                        "slangbuild: disk.overlay_name is empty but overlay bins exist");
                    return 1;
                }
                var entryName = _disk.OverlayName.Replace("{index}", i.ToString());
                rc = WriteEntry(overlayBinPaths[i], entryName, outputAbs, stagingDir);
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
    /// 1 entry を D88 へ書き込む (= 旧 entry 削除 → P で staging copy を書き込み)。
    /// staging に entryName 名で copy してから ndc P することで、D88 内の
    /// ファイル名が source bin の元名 (= PROG.BIN) ではなく entryName (= PROG.COM) になる。
    /// </summary>
    private int WriteEntry(string sourceBin, string entryName, string d88, string stagingDir)
    {
        var staged = Path.Combine(stagingDir, entryName);
        File.Copy(sourceBin, staged, overwrite: true);

        // 旧 entry 削除 (= 失敗無視; entry が無い場合 ndc D は non-zero 終了)
        RunNdc(new[] { "D", d88, "0", entryName }, ignoreFailure: true);

        // 新 entry 書き込み
        var rc = RunNdc(new[] { "P", d88, "0", staged }, ignoreFailure: false);
        if (rc != 0)
        {
            Console.Error.WriteLine(
                $"slangbuild: ndc P failed for {entryName} (exit {rc})");
            return rc;
        }
        return 0;
    }

    private int RunNdc(string[] args, bool ignoreFailure)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ndcPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        if (_verbose)
            Console.Error.WriteLine($"+ {_ndcPath} {string.Join(" ", args)}");

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30_000);

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
