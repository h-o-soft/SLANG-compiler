namespace SLANGCompiler.Runtime;

/// <summary>
/// ランタイムライブラリ関数の定義。
/// 新形式: .asmファイルにメタデータをコメントとして埋め込む。
///
/// 形式例:
/// ; @name PSTR2
/// ; @param_count 2
/// ; @calls PCHR
/// ; @init_code (initialization code marker)
/// .pstr1
///   LD A,D
///   OR E
///   RET Z
///   CALL PCHR
///   DEC DE
///   JR .pstr1
/// </summary>
public class RuntimeFunction
{
    public string Name { get; set; } = "";
    public int ParamCount { get; set; }
    public List<string> Dependencies { get; set; } = new();  // @calls
    public string Code { get; set; } = "";
    public string? InitCode { get; set; }                     // @init_code
    public string? LibName { get; set; }                      // @lib
    public string SourceFile { get; set; } = "";
}

/// <summary>
/// ランタイムライブラリの読み込み・管理
/// </summary>
public class RuntimeManager
{
    private readonly Dictionary<string, RuntimeFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedFunctions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RuntimeFunction> Functions => _functions;

    /// <summary>
    /// 新形式の .asm ファイルからランタイム関数群を読み込む
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        LoadFromString(text, filePath);
    }

    /// <summary>
    /// 文字列からランタイム関数群をパース
    /// </summary>
    public void LoadFromString(string text, string sourcePath = "<inline>")
    {
        var functions = RuntimeParser.Parse(text, sourcePath);
        foreach (var func in functions)
        {
            _functions[func.Name] = func;
        }
    }

    /// <summary>
    /// 関数を使用済みとしてマーク。依存関係も再帰的にマーク。
    /// </summary>
    public void MarkUsed(string name)
    {
        if (!_usedFunctions.Add(name)) return; // already marked

        if (_functions.TryGetValue(name, out var func))
        {
            foreach (var dep in func.Dependencies)
            {
                MarkUsed(dep);
            }
        }
    }

    /// <summary>
    /// 使用済み関数のコードを取得（依存関係順）
    /// </summary>
    public IEnumerable<RuntimeFunction> GetUsedFunctions()
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RuntimeFunction>();

        foreach (var name in _usedFunctions)
        {
            CollectDependencies(name, visited, result);
        }

        return result;
    }

    private void CollectDependencies(string name, HashSet<string> visited, List<RuntimeFunction> result)
    {
        if (!visited.Add(name)) return;

        if (_functions.TryGetValue(name, out var func))
        {
            foreach (var dep in func.Dependencies)
            {
                CollectDependencies(dep, visited, result);
            }
            result.Add(func);
        }
    }
}

/// <summary>
/// 新形式 .asm ファイルのパーサー
/// </summary>
public static class RuntimeParser
{
    /// <summary>
    /// メタデータコメント付き .asm ファイルを解析して RuntimeFunction のリストを返す
    /// </summary>
    public static List<RuntimeFunction> Parse(string text, string sourcePath)
    {
        var functions = new List<RuntimeFunction>();
        RuntimeFunction? current = null;
        var codeBuilder = new System.Text.StringBuilder();
        var initCodeBuilder = new System.Text.StringBuilder();
        bool inInitCode = false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            // メタデータコメント行: ; @key value
            if (line.TrimStart().StartsWith("; @"))
            {
                var meta = line.TrimStart()[3..]; // skip "; @"
                var spaceIdx = meta.IndexOf(' ');
                var key = spaceIdx >= 0 ? meta[..spaceIdx].Trim() : meta.Trim();
                var value = spaceIdx >= 0 ? meta[(spaceIdx + 1)..].Trim() : "";

                switch (key.ToLowerInvariant())
                {
                    case "name":
                        // Save previous function
                        if (current != null)
                        {
                            FinishFunction(current, codeBuilder, initCodeBuilder, inInitCode);
                            functions.Add(current);
                        }
                        current = new RuntimeFunction { Name = value, SourceFile = sourcePath };
                        codeBuilder.Clear();
                        initCodeBuilder.Clear();
                        inInitCode = false;
                        break;

                    case "param_count":
                        if (current != null && int.TryParse(value, out int pc))
                            current.ParamCount = pc;
                        break;

                    case "calls":
                        if (current != null)
                        {
                            foreach (var dep in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                current.Dependencies.Add(dep);
                            }
                        }
                        break;

                    case "lib":
                        if (current != null)
                            current.LibName = value;
                        break;

                    case "init_code":
                        inInitCode = true;
                        break;

                    case "end_init":
                        inInitCode = false;
                        break;
                }
                continue;
            }

            // コード行
            if (current != null)
            {
                if (inInitCode)
                    initCodeBuilder.AppendLine(line);
                else
                    codeBuilder.AppendLine(line);
            }
        }

        // Last function
        if (current != null)
        {
            FinishFunction(current, codeBuilder, initCodeBuilder, inInitCode);
            functions.Add(current);
        }

        return functions;
    }

    private static void FinishFunction(RuntimeFunction func,
        System.Text.StringBuilder codeBuilder,
        System.Text.StringBuilder initCodeBuilder,
        bool inInitCode)
    {
        func.Code = codeBuilder.ToString().TrimEnd();
        if (initCodeBuilder.Length > 0)
        {
            func.InitCode = initCodeBuilder.ToString().TrimEnd();
        }
    }
}
