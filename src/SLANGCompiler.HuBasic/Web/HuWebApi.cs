using System.Reflection;
using System.Text;
using System.Text.Json;
using SLANGCompiler.HuBasic.Analysis;
using SLANGCompiler.HuBasic.Lowering;

namespace SLANGCompiler.HuBasic.Web;

/// <summary>
/// ブラウザ (src/SLANGCompiler.HuBasic.Wasm の [JSExport]) から呼ぶ口。入力のオプションも結果も JSON の文字列で受け渡す。
/// 変換は CLI と同じ HuTranspiler・HuLayout を通るので、出力・診断・大きさの検査の文は CLI と同じ。
/// JSON はリフレクションを使わずに手で読み書きする (wasm のトリミングで壊れないように)
/// </summary>
public static class HuWebApi
{
    /// <summary>
    /// BASIC のファイルの中身を変換する。sourceName は入力のファイル名 (SL の頭の `// source:` と IPL の起動名の元)。
    /// optionsJson は CLI のオプションに当たる項目の object (空文字列は {} と同じ)。
    /// 結果は {"ok","sl","diskMainName","diagnostics":[…]}、オプションの誤りは {"ok":false,"error"}
    /// </summary>
    public static string Transpile(byte[] source, string sourceName, string optionsJson)
    {
        var error = ParseOptions(optionsJson, out var encoding, out var opt);
        if (error != null) return ErrorJson(error);

        var fileName = Path.GetFileName(sourceName ?? "");
        var result = HuTranspiler.TranspileBytes(source, encoding, opt with { SourceName = fileName });
        return Json(w =>
        {
            w.WriteBoolean("ok", result.Output != null);
            if (result.Output != null) w.WriteString("sl", result.Output);
            else w.WriteNull("sl");
            w.WriteString("diskMainName", result.Output != null ? ReadDiskMainName(result.Output) : "");
            w.WriteStartArray("diagnostics");
            foreach (var d in result.Diagnostics.Items)
            {
                w.WriteStartObject();
                w.WriteString("severity", d.Severity == HuSeverity.Error ? "error" : "warning");
                w.WriteString("code", d.Code);
                w.WriteString("message", d.Message);
                w.WriteNumber("sourceLine", d.SourceLine);
                w.WriteNumber("column", d.Column);
                w.WriteNumber("basicLine", d.BasicLine);
                w.WriteString("text", d.Format(fileName));
                w.WriteEndObject();
            }
            w.WriteEndArray();
        });
    }

    /// <summary>
    /// 組み立て後の大きさの検査。binLength は bin のバイト数、workEnd はアセンブラの記号 __WORKEND__ の値。
    /// 結果は {"ok","message","loadAddress","reservedStart"}。message は CLI の --check-bin と同じ 1 行
    /// </summary>
    public static string CheckLayout(int binLength, int workEnd)
    {
        if (binLength < 0) return ErrorJson($"hubasic2sl: bin の大きさが負です: {binLength}");
        if (workEnd < HuLayout.LoadAddress)
            return ErrorJson($"hubasic2sl: 作業領域の終わり ${workEnd:X4} が配置先 ${HuLayout.LoadAddress:X4} より前です");
        var (fits, message) = HuLayout.Check(binLength, workEnd);
        return Json(w =>
        {
            w.WriteBoolean("ok", fits);
            w.WriteString("message", message);
            w.WriteNumber("loadAddress", HuLayout.LoadAddress);
            w.WriteNumber("reservedStart", HuLayout.ReservedStart);
        });
    }

    /// <summary>{"version","informational"}。informational は組み立てた git の SHA を含む (例: 0.1.0-alpha+&lt;SHA&gt;)</summary>
    public static string Version()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var plus = info.IndexOf('+');
        return Json(w =>
        {
            w.WriteString("version", plus >= 0 ? info[..plus] : info);
            w.WriteString("informational", info);
        });
    }

    /// <summary>オプションの JSON を読む。誤りなら CLI と同じ文 (CLI に無い誤りは独自の文) を返す</summary>
    internal static string? ParseOptions(string? optionsJson, out string encoding, out HuTranspileOptions opt)
    {
        encoding = "auto";
        opt = new HuTranspileOptions();
        if (string.IsNullOrEmpty(optionsJson)) return null;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(optionsJson);
        }
        catch (JsonException e)
        {
            return $"hubasic2sl: オプションの JSON を読めません: {e.Message}";
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "hubasic2sl: オプションの JSON は object で書いてください";
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var v = p.Value;
                switch (p.Name)
                {
                    case "inputEncoding":
                        if (v.ValueKind != JsonValueKind.String || v.GetString() is not ("auto" or "utf8" or "sjis" or "x1"))
                            return $"hubasic2sl: --input-encoding は auto・utf8・sjis・x1 のどれかです: {(v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())}";
                        encoding = v.GetString()!;
                        break;
                    case "wait":
                    {
                        // CLI の --wait と同じ読み方 (N が読めないか 0 以下なら 1、3 つ目からは見ない)
                        var w = v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText();
                        var parts = w.Split(':');
                        int n = parts.Length > 1 && int.TryParse(parts[1], out var nv) && nv > 0 ? nv : 1;
                        if (v.ValueKind != JsonValueKind.String || parts[0] is not ("none" or "line" or "frame"))
                            return $"hubasic2sl: --wait は none / line[:N] / frame[:N] です: {w}";
                        opt = parts[0] switch
                        {
                            "line" => opt with { Wait = HuWaitMode.Line, WaitArg = n },
                            "frame" => opt with { Wait = HuWaitMode.Frame, WaitArg = n },
                            _ => opt with { Wait = HuWaitMode.None },
                        };
                        break;
                    }
                    case "stringLen":
                        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var len) || len < 1 || len > HuAnalyzer.MaxStringLen)
                            return $"hubasic2sl: --string-len には文字列 1 本の長さの上限 (1〜{HuAnalyzer.MaxStringLen}) を指定してください";
                        opt = opt with { StringLen = len };
                        break;
                    case "dataBudget":
                        if (v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var budget) || budget <= 0)
                            return "hubasic2sl: --data-budget にはバイト数を指定してください";
                        opt = opt with { DataBudget = budget };
                        break;
                    case "noLineNumbers":
                    case "assumeDefint":
                    case "emitLineComments":
                        if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                            return $"hubasic2sl: オプション {p.Name} は true か false で書いてください";
                        var on = v.GetBoolean();
                        opt = p.Name switch
                        {
                            "noLineNumbers" => opt with { LineNumbers = !on },
                            "assumeDefint" => opt with { AssumeDefint = on },
                            _ => opt with { LineComments = on },
                        };
                        break;
                    default:
                        return $"hubasic2sl: 不明なオプション {p.Name}";
                }
            }
        }
        return null;
    }

    /// <summary>SL の頭のコメントの並びから `// disk-main-name:` の名前を読む (無ければ空)</summary>
    private static string ReadDiskMainName(string sl)
    {
        const string prefix = "// disk-main-name:";
        foreach (var line in sl.Split('\n'))
        {
            if (!line.StartsWith("//")) break;
            if (line.StartsWith(prefix)) return line[prefix.Length..].Trim();
        }
        return "";
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
