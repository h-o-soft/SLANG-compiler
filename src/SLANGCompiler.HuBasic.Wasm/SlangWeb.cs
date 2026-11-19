using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using SLANGCompiler.SlfsPack;
using SLANGCompiler.Web;

namespace SLANGCompiler.HuBasic.Wasm;

/// <summary>
/// JS から呼ぶ slangc の口。中は SlangWebApi・HuDiskWebApi を呼ぶだけ (引数と結果の形はそれらと docs/HUBASIC.md を参照)。
/// JS からは getAssemblyExports("hubasic2sl-wasm") の SLANGCompiler.HuBasic.Wasm.SlangWeb で指す。
/// ファイル (env・ランタイム・include・ソース・ディスクに足すファイル) は、先に PutFile で .NET のメモリの中のファイルシステムに入れる
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class SlangWeb
{
    /// <summary>path にファイルを書く (親のフォルダは作る)。path は絶対の道</summary>
    [JSExport]
    public static void PutFile(string path, byte[] data)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, data);
    }

    [JSExport]
    public static string Compile(string sourcePath, string optionsJson)
        => SlangWebApi.Compile(sourcePath, optionsJson);

    [JSExport]
    public static string BuildHuDisk(byte[] mainBinary, string optionsJson)
        => HuDiskWebApi.BuildHuDisk(mainBinary, optionsJson);

    [JSExport]
    public static string Version()
        => SlangWebApi.Version();
}
