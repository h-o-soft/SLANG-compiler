using System.Reflection;
using System.Text;
using System.Text.Json;
using SLANGCompiler.Compilation;
using SLANGCompiler.Runtime;

namespace SLANGCompiler.Web;

/// <summary>
/// ブラウザ (src/SLANGCompiler.HuBasic.Wasm の [JSExport] の SlangWeb) から呼ぶ slangc の口。オプションも結果も JSON の文字列。
/// ファイル (ソース・#INCLUDE・env・ランタイム) は .NET の File の API で読む (ブラウザではメモリの中のファイルシステム。
/// JS が前もって SlangWeb.PutFile で入れておく)。コンパイルは CLI と同じ SlangCompilation を通るので、ASM と診断の文は CLI と同じ。
/// JSON はリフレクションを使わずに手で読み書きする (wasm のトリミングで壊れないように)
/// </summary>
public static class SlangWebApi
{
    /// <summary>
    /// sourcePath のソースをコンパイルする。optionsJson は {"env": 名前, "includePaths": [...], "libPaths": [...]}
    /// (slangc の -E・-I・-L と同じ意味。env は省けない)。
    /// 結果は {"ok","asm","overlays":[{"name","asm"}],"modules":[行]|null,"inc"|null,"c"|null,"log":[行]}。
    /// log は CLI の stderr と同じ行 (`; Environment:`・`; Output:` の行は CLI だけのもので、入らない)。
    /// オプションの誤りは {"ok":false,"error"}
    /// </summary>
    public static string Compile(string sourcePath, string optionsJson)
    {
        var error = ParseOptions(optionsJson, out var env, out var includePaths, out var libPaths);
        if (error != null) return ErrorJson(error);

        using var log = new StringWriter();
        var paths = new PathResolver(includePaths, libPaths);
        SlangCompileResult? result = null;
        try
        {
            var resolution = SlangCompilation.ResolveEnvironment(env, paths, log);
            if (resolution != null)
            {
                if (!File.Exists(sourcePath))
                    log.WriteLine($"Error: File not found: {sourcePath}");
                else
                {
                    var diagnostics = new DiagnosticBag();
                    result = SlangCompilation.CompileFile(sourcePath, resolution.Value.Config, paths, diagnostics, log);
                    // CLI は、全部のファイルが通ったあとに、もう一度 diagnostics を書く (warning があれば)
                    if (result.Ok && diagnostics.Diagnostics.Count > 0)
                        diagnostics.WriteTo(log);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException
                                     or ArgumentException or InvalidOperationException or InvalidDataException)
        {
            // 読めないファイル・書き方の違うランタイムなど。CLI では例外で止まる所。JS には例外でなく、それまでの log と一緒に返す
            result = null;
            log.WriteLine($"slangc: error: {e.Message}");
        }

        return Json(w =>
        {
            w.WriteBoolean("ok", result?.Ok == true);
            WriteNullableString(w, "asm", result?.MainAsm);
            w.WriteStartArray("overlays");
            foreach (var (name, asm) in result?.Overlays ?? Array.Empty<(string, string)>())
            {
                w.WriteStartObject();
                w.WriteString("name", name);
                w.WriteString("asm", asm);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            if (result?.ModulesLines is { } modules)
            {
                w.WriteStartArray("modules");
                foreach (var m in modules) w.WriteStringValue(m);
                w.WriteEndArray();
            }
            else w.WriteNull("modules");
            WriteNullableString(w, "inc", result?.SharedSymbolsInc);
            WriteNullableString(w, "c", result?.CSource);
            w.WriteStartArray("log");
            foreach (var line in Lines(log.ToString())) w.WriteStringValue(line);
            w.WriteEndArray();
        });
    }

    /// <summary>{"slangc": 版, "informational": "版+SHA"} (SHA は組み立てた木の commit。無ければ版だけ)</summary>
    public static string Version()
    {
        var info = typeof(SlangWebApi).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        int plus = info.IndexOf('+');
        var informational = plus >= 0 ? $"{SlangVersion.Version}+{info[(plus + 1)..]}" : SlangVersion.Version;
        return Json(w =>
        {
            w.WriteString("slangc", SlangVersion.Version);
            w.WriteString("informational", informational);
        });
    }

    internal static string? ParseOptions(string? optionsJson, out string env, out List<string> includePaths, out List<string> libPaths)
    {
        env = "";
        includePaths = new List<string>();
        libPaths = new List<string>();
        if (string.IsNullOrEmpty(optionsJson)) return "slangc: オプションの JSON に env がありません";
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(optionsJson);
        }
        catch (JsonException e)
        {
            return $"slangc: オプションの JSON を読めません: {e.Message}";
        }
        using (doc)
        {
            try
            {
                return ReadOptions(doc.RootElement, out env, out includePaths, out libPaths);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or FormatException)
            {
                // 項目の名前や文字列の値を読めないとき (対になっていないサロゲートの \ud800 など)
                return $"slangc: オプションの JSON を読めません: {e.Message}";
            }
        }
    }

    private static string? ReadOptions(JsonElement root, out string env, out List<string> includePaths, out List<string> libPaths)
    {
        env = "";
        includePaths = new List<string>();
        libPaths = new List<string>();
        if (root.ValueKind != JsonValueKind.Object)
            return "slangc: オプションの JSON は object で書いてください";
        bool hasEnv = false;
        foreach (var p in root.EnumerateObject())
        {
            switch (p.Name)
            {
                case "env":
                    if (p.Value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(p.Value.GetString()))
                        return "slangc: env は空でない文字列です";
                    env = p.Value.GetString()!;
                    hasEnv = true;
                    break;
                case "includePaths":
                case "libPaths":
                {
                    var list = p.Name == "includePaths" ? includePaths : libPaths;
                    if (p.Value.ValueKind != JsonValueKind.Array)
                        return $"slangc: {p.Name} は文字列の配列です";
                    foreach (var e in p.Value.EnumerateArray())
                    {
                        if (e.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(e.GetString()))
                            return $"slangc: {p.Name} は文字列の配列です";
                        list.Add(e.GetString()!);
                    }
                    break;
                }
                default:
                    return $"slangc: 知らないオプションです: {p.Name}";
            }
        }
        if (!hasEnv) return "slangc: オプションの JSON に env がありません";
        return null;
    }

    /// <summary>改行で分けた行 (最後の空の行は除く)</summary>
    internal static IEnumerable<string> Lines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        int n = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        return lines.Take(n);
    }

    private static void WriteNullableString(Utf8JsonWriter w, string name, string? value)
    {
        if (value != null) w.WriteString(name, value);
        else w.WriteNull(name);
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
