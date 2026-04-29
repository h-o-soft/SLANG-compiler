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
    /// <param name="lstPath">non-null なら `-lst &lt;path&gt;` を付与してリスト
    /// ファイルを出力。本番アセンブル (単段モード or prelink Pass 3) では指定し、
    /// prelink Pass 1 (中間用) では null にして無駄な出力を避ける。</param>
    /// <param name="outputFlag">AILZ80ASM の出力 format flag (= shortcut option 名)。
    /// default <c>"-bin"</c> (raw バイナリ)。env file `output: cmt` の場合は
    /// <c>"-cmt"</c> を渡して CMT (cassette tape) format で出力する。
    /// `-bin` と `-cmt` は AILZ80ASM の異なる shortcut であり、両者を同時に
    /// 渡すと両 format のファイルが出力されるので、format 切替時はこの引数で
    /// 1 つに切り替える。`outBinPath` の path / 拡張子はこの flag の値とは独立。</param>
    /// <param name="extraArgs">format 切替で必要な追加引数 (例: cmt なら
    /// <c>["-gap", "0"]</c>)。Pass 1 / Pass 3 でアドレス整合を保つため、prelink
    /// 全段で同じ extraArgs を渡す必要がある。</param>
    public AssemblerResult AssembleMain(string asmPath, string outBinPath, string outSymPath,
                                        bool superAssemble = true, string? lstPath = null,
                                        string outputFlag = "-bin",
                                        string[]? extraArgs = null)
    {
        var args = BuildArgs(new[] { asmPath }, outBinPath, outSymPath, superAssemble, lstPath,
                             outputFlag, extraArgs);
        return Run(args);
    }

    /// <summary>
    /// overlay ASM を imports.asm (filtered EQU 集) と一緒にアセンブル。
    /// imports は AILZ80ASM 側で先頭ファイル扱いになるため、その EQU 群が
    /// overlay 側の CALL から参照される。
    /// </summary>
    /// <param name="superAssemble">true (default) = 既存挙動 (PR-B 単段フロー用)、
    /// false = `-nsa` 付与 (PR-B2 prelink Pass 1/3 用)</param>
    /// <param name="lstPath">non-null なら `-lst &lt;path&gt;` を付与</param>
    /// <param name="outputFlag"><see cref="AssembleMain"/> と同じ。default <c>"-bin"</c>、
    /// CMT 出力時は <c>"-cmt"</c>。</param>
    /// <param name="extraArgs"><see cref="AssembleMain"/> と同じ。Pass 1/3 で main と
    /// 同じ extraArgs を渡す必要。</param>
    public AssemblerResult AssembleOverlay(string importsAsmPath, string overlayAsmPath,
                                           string outBinPath, string outSymPath,
                                           bool superAssemble = true, string? lstPath = null,
                                           string outputFlag = "-bin",
                                           string[]? extraArgs = null)
    {
        var args = BuildArgs(new[] { importsAsmPath, overlayAsmPath },
                             outBinPath, outSymPath, superAssemble, lstPath,
                             outputFlag, extraArgs);
        return Run(args);
    }

    private static string[] BuildArgs(string[] inputs, string outBinPath, string outSymPath,
                                      bool superAssemble, string? lstPath,
                                      string outputFlag, string[]? extraArgs)
    {
        var list = new List<string>(inputs);
        // outputFlag は AILZ80ASM の shortcut option (-bin / -cmt 等) 名。
        // path との 2 引数で 1 つの出力 format を指定する (= -bin と -cmt を同時に
        // 渡すと両 format の file が出るので、env 別に switch する)。
        list.Add(outputFlag); list.Add(outBinPath);
        list.Add("-sym"); list.Add(outSymPath);
        if (lstPath != null) { list.Add("-lst"); list.Add(lstPath); }
        list.Add("-sm");  list.Add("minimal-equ");
        list.Add("-f");
        if (!superAssemble) list.Add("-nsa");
        if (extraArgs != null && extraArgs.Length > 0) list.AddRange(extraArgs);
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
        else if (code != 0)
        {
            // 失敗時は verbose 無しでもエラー詳細を出す。
            // AILZ80ASM はエラーを stdout に流す癖があり、stderr だけだと
            // 「main assembly failed (exit 1)」だけ見えて原因不明になる。
            // unix 慣習に合わせて stdout / stderr とも stderr へ流す
            // (= ユーザーが `2>` で捕まえられる)。
            if (!string.IsNullOrEmpty(stdout)) Console.Error.Write(stdout);
            if (!string.IsNullOrEmpty(stderr)) Console.Error.Write(stderr);
        }

        return new AssemblerResult(code, stdout, stderr);
    }
}

public record AssemblerResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;
}
