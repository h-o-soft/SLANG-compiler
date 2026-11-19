using System.Text;
using System.Text.Json;

namespace SLANGCompiler.SlfsPack;

/// <summary>
/// ブラウザ (src/SLANGCompiler.HuBasic.Wasm の [JSExport] の SlangWeb) から呼ぶ、Hu-BASIC と同じ形のディスク (x1native_hufs) を作る口。
/// slangbuild の `--emit disk` (disk.tool: hufs-pack) と同じ HuPack.ParseAddSpec・HuPack.Build を通るので、
/// 同じ入力からは同じ D88 になる。足すファイルは .NET の File の API で読む (ブラウザではメモリの中のファイルシステム)。
/// JSON はリフレクションを使わずに手で読み書きする (wasm のトリミングで壊れないように)
/// </summary>
public static class HuDiskWebApi
{
    /// <summary>
    /// 本体 mainBinary の起動ディスクを作る。optionsJson は
    /// {"mainName": 起動名, "load": 番地?, "exec": 番地?, "add": ["[名前=]ファイル[,bin|,text]", ...]?}。
    /// load を省けば $1000、exec を省けば load の値 (slangbuild と同じ決まり)。add は `--hufs-add` と同じ書き方。
    /// 結果は {"ok":true,"d88":base64} か {"ok":false,"error"}
    /// </summary>
    public static string BuildHuDisk(byte[] mainBinary, string optionsJson)
    {
        var error = ParseOptions(optionsJson, out var mainName, out var load, out var exec, out var add);
        if (error != null) return ErrorJson(error);
        // slangbuild の --disk-main-name と同じ決まり (IPL の見出しに書けるのは表示できる ASCII だけ)
        if (!BootName.IsValid(mainName))
            return ErrorJson($"hufs: disk main name '{mainName}' invalid ({BootName.Rule})");
        byte[] d88;
        try
        {
            var specs = add.Select(HuPack.ParseAddSpec).ToList();
            d88 = HuPack.Build(mainBinary ?? Array.Empty<byte>(), mainName, load, exec ?? load, specs);
        }
        catch (Exception e)   // 名前の決まり・足すファイルが無い・ディスクに入らないなど。JS には例外でなく JSON で返す
        {
            return ErrorJson($"hufs: {e.Message}");
        }
        return Json(w =>
        {
            w.WriteBoolean("ok", true);
            w.WriteString("d88", Convert.ToBase64String(d88));
        });
    }

    internal static string? ParseOptions(string? optionsJson, out string mainName, out ushort load, out ushort? exec, out List<string> add)
    {
        mainName = "";
        load = 0x1000;
        exec = null;
        add = new List<string>();
        if (string.IsNullOrEmpty(optionsJson)) return "hufs: オプションの JSON に mainName がありません";
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(optionsJson);
        }
        catch (JsonException e)
        {
            return $"hufs: オプションの JSON を読めません: {e.Message}";
        }
        using (doc)
        {
            try
            {
                return ReadOptions(doc.RootElement, out mainName, out load, out exec, out add);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
            {
                // 項目の名前や文字列の値を読めないとき (対になっていないサロゲートの \ud800 など)
                return $"hufs: オプションの JSON を読めません: {e.Message}";
            }
        }
    }

    private static string? ReadOptions(JsonElement root, out string mainName, out ushort load, out ushort? exec, out List<string> add)
    {
        mainName = "";
        load = 0x1000;
        exec = null;
        add = new List<string>();
        if (root.ValueKind != JsonValueKind.Object)
            return "hufs: オプションの JSON は object で書いてください";
        bool hasName = false;
        foreach (var p in root.EnumerateObject())
        {
            var v = p.Value;
            switch (p.Name)
            {
                case "mainName":
                    if (v.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(v.GetString()))
                        return "hufs: mainName は空でない文字列です";
                    mainName = v.GetString()!;
                    hasName = true;
                    break;
                case "load":
                case "exec":
                    if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var n) || n < 0 || n > 0xFFFF)
                        return $"hufs: {p.Name} は 0〜65535 の整数です";
                    if (p.Name == "load") load = (ushort)n;
                    else exec = (ushort)n;
                    break;
                case "add":
                    if (v.ValueKind != JsonValueKind.Array)
                        return "hufs: add は文字列の配列です";
                    foreach (var e in v.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(e.GetString()))
                            return "hufs: add は文字列の配列です";
                        add.Add(e.GetString()!);
                    }
                    break;
                default:
                    return $"hufs: 知らないオプションです: {p.Name}";
            }
        }
        if (!hasName) return "hufs: オプションの JSON に mainName がありません";
        return null;
    }

    private static string ErrorJson(string message) => Json(w =>
    {
        w.WriteBoolean("ok", false);
        w.WriteString("error", message);
    });

    private static string Json(Action<Utf8JsonWriter> body)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }
}
