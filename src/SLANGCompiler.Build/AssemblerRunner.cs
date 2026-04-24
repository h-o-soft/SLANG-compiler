using System.Diagnostics;
using System.Text;

namespace SLANGCompiler.Build;

/// <summary>
/// AILZ80ASM を Process spawn で起動するヘルパー。
///
/// 二段アセンブルの 2 つのモードを担当:
///   - <see cref="AssembleMain"/>: main.ASM → main.bin + main.sym (-sm minimal-equ)
///   - <see cref="AssembleOverlay"/>: imports.asm + overlay._mN.ASM → overlay._mN.bin
/// </summary>
public class AssemblerRunner
{
    private readonly string _ailz80AsmPath;
    private readonly bool _verbose;

    public AssemblerRunner(string ailz80AsmPath, bool verbose = false)
    {
        _ailz80AsmPath = ailz80AsmPath;
        _verbose = verbose;
    }

    /// <summary>
    /// main ASM をアセンブルして bin + sym を出力。
    /// `-sm minimal-equ` で出力 sym は overlay の filtered imports に流用可能な形式に。
    /// </summary>
    /// <param name="superAssemble">true (default) = 既存挙動 (JR/JP 自動最適化)。
    /// false = `-nsa` 付与で命令長を固定 (PR-B2 prelink Pass 1/3 用、Pass 1 と
    /// Pass 3 で同じファイル内ラベルアドレスを一致させるため)</param>
    public AssemblerResult AssembleMain(string asmPath, string outBinPath, string outSymPath,
                                        bool superAssemble = true)
    {
        var args = BuildArgs(new[] { asmPath }, outBinPath, outSymPath, superAssemble);
        return Run(args);
    }

    /// <summary>
    /// overlay ASM を imports.asm (filtered EQU 集) と一緒にアセンブル。
    /// imports は AILZ80ASM 側で先頭ファイル扱いになるため、その EQU 群が
    /// overlay 側の CALL から参照される。
    /// </summary>
    /// <param name="superAssemble">true (default) = 既存挙動 (PR-B 単段フロー用)、
    /// false = `-nsa` 付与 (PR-B2 prelink Pass 1/3 用)</param>
    public AssemblerResult AssembleOverlay(string importsAsmPath, string overlayAsmPath,
                                           string outBinPath, string outSymPath,
                                           bool superAssemble = true)
    {
        var args = BuildArgs(new[] { importsAsmPath, overlayAsmPath },
                             outBinPath, outSymPath, superAssemble);
        return Run(args);
    }

    private static string[] BuildArgs(string[] inputs, string outBinPath, string outSymPath,
                                      bool superAssemble)
    {
        var list = new List<string>(inputs);
        list.Add("-bin"); list.Add(outBinPath);
        list.Add("-sym"); list.Add(outSymPath);
        list.Add("-sm");  list.Add("minimal-equ");
        list.Add("-f");
        if (!superAssemble) list.Add("-nsa");
        return list.ToArray();
    }

    private AssemblerResult Run(string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ailz80AsmPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        if (_verbose)
            Console.Error.WriteLine($"+ {_ailz80AsmPath} {string.Join(" ", args)}");

        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(60_000); // 60s で十分
        var code = proc.ExitCode;

        if (_verbose)
        {
            if (!string.IsNullOrEmpty(stdout)) Console.Out.Write(stdout);
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }

        return new AssemblerResult(code, stdout, stderr);
    }
}

public record AssemblerResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;
}
