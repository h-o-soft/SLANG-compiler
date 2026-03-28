namespace SLANGCompiler.Lexer;

/// <summary>
/// プリプロセッサ: #INCLUDE のファイル展開と #IF/#ELSE/#ENDIF の条件コンパイルを処理する。
/// Lexer が生成したトークン列を入力として、展開済みのトークン列を返す。
/// </summary>
public class Preprocessor
{
    private readonly DiagnosticBag _diagnostics;
    private readonly List<string> _includePaths;
    private readonly HashSet<string> _includedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxIncludeDepth;

    public Preprocessor(DiagnosticBag diagnostics, List<string>? includePaths = null, int maxIncludeDepth = 8)
    {
        _diagnostics = diagnostics;
        _includePaths = includePaths ?? new List<string> { "." };
        _maxIncludeDepth = maxIncludeDepth;
    }

    /// <summary>
    /// トークン列を処理し、#INCLUDEを展開、#IF/#ELSE/#ENDIFを評価する。
    /// </summary>
    public List<Token> Process(List<Token> tokens, string baseDir, int depth = 0)
    {
        var result = new List<Token>();
        int i = 0;

        while (i < tokens.Count)
        {
            var token = tokens[i];

            switch (token.Kind)
            {
                case TokenKind.PreprocInclude:
                    ProcessInclude(token, result, baseDir, depth);
                    i++;
                    break;

                case TokenKind.PreprocIf:
                    i = ProcessIf(tokens, i, result);
                    break;

                case TokenKind.PreprocElse:
                case TokenKind.PreprocEnd:
                    // Stray #ELSE/#END outside #IF block - skip
                    i++;
                    break;

                default:
                    result.Add(token);
                    i++;
                    break;
            }
        }

        return result;
    }

    private void ProcessInclude(Token token, List<Token> result, string baseDir, int depth)
    {
        if (depth >= _maxIncludeDepth)
        {
            _diagnostics.Error($"#INCLUDE nesting too deep (max {_maxIncludeDepth})", token.Span);
            return;
        }

        var fileName = token.StringValue.Trim().Trim('"');
        var filePath = ResolveIncludePath(fileName, baseDir);

        if (filePath == null)
        {
            _diagnostics.Error($"#INCLUDE file not found: {fileName}", token.Span);
            return;
        }

        // 循環参照チェック
        var fullPath = Path.GetFullPath(filePath);
        if (!_includedFiles.Add(fullPath))
        {
            // 同じファイルの再インクルードは許可（循環でなければ）
            // ただしSLANG仕様では再インクルードの挙動は未定義
        }

        try
        {
            var source = File.ReadAllText(filePath);
            var lexer = new Lexer(source, fileName);
            var includeTokens = lexer.Tokenize();

            // EOF トークンを除去
            includeTokens.RemoveAll(t => t.Kind == TokenKind.EOF);

            // 再帰的にプリプロセス
            var includeDir = Path.GetDirectoryName(filePath) ?? baseDir;
            var processed = Process(includeTokens, includeDir, depth + 1);
            result.AddRange(processed);
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"#INCLUDE error: {ex.Message}", token.Span);
        }
        finally
        {
            _includedFiles.Remove(Path.GetFullPath(filePath));
        }
    }

    private int ProcessIf(List<Token> tokens, int startIndex, List<Token> result)
    {
        var ifToken = tokens[startIndex];
        var exprStr = ifToken.StringValue.Trim();
        bool condition = EvaluateCondition(exprStr);

        int i = startIndex + 1;
        int depth = 1;

        if (condition)
        {
            // 真の場合: #ELSEまでのトークンを含める
            while (i < tokens.Count && depth > 0)
            {
                if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                else if (tokens[i].Kind == TokenKind.PreprocEnd) { depth--; if (depth == 0) { i++; break; } }
                else if (tokens[i].Kind == TokenKind.PreprocElse && depth == 1)
                {
                    // #ELSE到達: 残りをスキップ
                    i++;
                    depth = 1;
                    while (i < tokens.Count && depth > 0)
                    {
                        if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                        else if (tokens[i].Kind == TokenKind.PreprocEnd) depth--;
                        i++;
                    }
                    break;
                }

                if (depth > 0) result.Add(tokens[i]);
                i++;
            }
        }
        else
        {
            // 偽の場合: #ELSEまでスキップ、#ELSEから#ENDまでを含める
            while (i < tokens.Count && depth > 0)
            {
                if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                else if (tokens[i].Kind == TokenKind.PreprocEnd) { depth--; if (depth == 0) { i++; break; } }
                else if (tokens[i].Kind == TokenKind.PreprocElse && depth == 1)
                {
                    // #ELSE到達: ここから含める
                    i++;
                    while (i < tokens.Count)
                    {
                        if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                        else if (tokens[i].Kind == TokenKind.PreprocEnd) { depth--; if (depth == 0) { i++; break; } }

                        if (depth > 0) result.Add(tokens[i]);
                        i++;
                    }
                    break;
                }
                i++;
            }
        }

        return i;
    }

    private bool EvaluateCondition(string expr)
    {
        // 簡易評価: TRUE/FALSE/数値
        if (expr.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
        if (expr.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
        if (int.TryParse(expr, out int val)) return val != 0;

        // CONST参照など複雑な式 → TODO: 定数式評価器
        // デフォルトは真
        return true;
    }

    private string? ResolveIncludePath(string fileName, string baseDir)
    {
        // カレントディレクトリから探す
        var path = Path.Combine(baseDir, fileName);
        if (File.Exists(path)) return path;

        // インクルードパスから探す
        foreach (var dir in _includePaths)
        {
            path = Path.Combine(dir, fileName);
            if (File.Exists(path)) return path;
        }

        return null;
    }
}
