namespace SLANGCompiler.Runtime;

/// <summary>
/// ランタイムライブラリ関数の定義。
/// 新形式: .asmファイルにメタデータをコメントとして埋め込む。
///
/// 形式例:
/// ; @name PSTR2
/// ; @param_count 2
/// ; @calls PCHR
/// ; @works sXYADR:2,sKBFAD:128
/// ; @init_code
///   LD HL,seed
///   ...
///   RET
/// ; @end_init
/// .pstr1
///   LD A,D
///   ...
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
    public int LoadOrder { get; set; }                        // ファイル内定義順
    public List<(string Label, int Size)>? Works { get; set; }  // @works (順序付き)
}

/// <summary>
/// ランタイムライブラリの読み込み・管理
/// </summary>
public class RuntimeManager
{
    private readonly Dictionary<string, RuntimeFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedFunctions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedFromOutput = new(StringComparer.OrdinalIgnoreCase);
    private int _loadOrderCounter;

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
            func.LoadOrder = _loadOrderCounter++;
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
    /// ランタイム関数のコードを取得し、通常出力から除外する（SLANGINITのインライン展開用）。
    /// 依存関数はマークされるが、指定関数自体は通常runtime出力から除外される。
    /// </summary>
    public string? GetAndExclude(string name)
    {
        MarkUsed(name);
        _excludedFromOutput.Add(name);
        return _functions.TryGetValue(name, out var func) ? func.Code : null;
    }

    /// <summary>
    /// 使用済み関数のコードを取得（依存関係順、除外されたものはスキップ）
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

    /// <summary>
    /// 使用済みランタイムのworks変数を依存解決順で集約し、重複排除して返す
    /// </summary>
    public IEnumerable<(string Label, int Size)> GetUsedWorkVariables()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var func in GetUsedFunctions())
        {
            if (func.Works == null) continue;
            foreach (var (label, size) in func.Works)
            {
                if (seen.Add(label))
                    yield return (label, size);
            }
        }
    }

    public IEnumerable<(string Label, int Size, string? LibName)> GetUsedWorkVariablesWithLib()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var func in GetUsedFunctions())
        {
            if (func.Works == null) continue;
            foreach (var (label, size) in func.Works)
            {
                if (seen.Add(label))
                    yield return (label, size, func.LibName);
            }
        }
    }

    /// <summary>
    /// 通常出力対象の使用済み関数（除外されたものをスキップ）
    /// </summary>
    public IEnumerable<RuntimeFunction> GetOutputFunctions()
    {
        // ファイル内定義順で出力（フォールスルー関係を維持するため）
        return GetUsedFunctions()
            .Where(f => !_excludedFromOutput.Contains(f.Name))
            .OrderBy(f => f.LoadOrder);
    }

    /// <summary>
    /// 指定した関数名セットのみを起点に依存解決し、ランタイム関数を返す。
    /// ユーザー定義関数名は除外する。除外済み関数もスキップ。
    /// </summary>
    public IEnumerable<RuntimeFunction> ResolveForNames(IEnumerable<string> names, ISet<string> userFuncs)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RuntimeFunction>();
        foreach (var name in names)
        {
            if (!userFuncs.Contains(name))
                CollectDependencies(name, visited, result);
        }
        return result.Where(f => !_excludedFromOutput.Contains(f.Name))
            .OrderBy(f => f.LoadOrder);
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

                    case "works":
                        if (current != null && !string.IsNullOrEmpty(value))
                        {
                            current.Works ??= new();
                            foreach (var item in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                            {
                                var colonIdx = item.IndexOf(':');
                                if (colonIdx > 0)
                                {
                                    var label = item[..colonIdx].Trim();
                                    if (int.TryParse(item[(colonIdx + 1)..].Trim(), out int size))
                                        current.Works.Add((label, size));
                                }
                            }
                        }
                        break;

                    case "init_code":
                        inInitCode = true;
                        break;

                    case "end_init":
                        inInitCode = false;
                        break;

                    case "function_type":
                        // function_type is metadata only, no action needed
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
