using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using SLANGCompiler.HuBasic.Web;

namespace SLANGCompiler.HuBasic.Wasm;

/// <summary>
/// JS から呼ぶ関数。中は HuWebApi を呼ぶだけ (引数と結果の形は HuWebApi と docs/HUBASIC.md を参照)。
/// JS からは getAssemblyExports("hubasic2sl-wasm") の SLANGCompiler.HuBasic.Wasm.HuBasicWeb で指す
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class HuBasicWeb
{
    public static void Main() { }

    [JSExport]
    public static string Transpile(byte[] source, string sourceName, string optionsJson)
        => HuWebApi.Transpile(source, sourceName, optionsJson);

    [JSExport]
    public static string CheckLayout(int binLength, int workEnd)
        => HuWebApi.CheckLayout(binLength, workEnd);

    [JSExport]
    public static string Version()
        => HuWebApi.Version();
}
