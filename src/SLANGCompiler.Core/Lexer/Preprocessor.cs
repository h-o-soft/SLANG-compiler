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
    // #IF 条件式で参照するCONST値（Process中にCONSTトークンから抽出）
    private readonly Dictionary<string, int> _constValues = new(StringComparer.OrdinalIgnoreCase);

    public Preprocessor(DiagnosticBag diagnostics, List<string>? includePaths = null, int maxIncludeDepth = 8)
    {
        _diagnostics = diagnostics;
        _includePaths = includePaths ?? new List<string> { "." };
        _maxIncludeDepth = maxIncludeDepth;
    }

    /// <summary>プリプロセッサ定数を事前登録（環境定数等）。#IF条件式で参照可能。</summary>
    public void DefineConst(string name, int value) => _constValues[name] = value;

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
                    // Stray #ELSE outside #IF block - skip
                    i++;
                    break;

                // PreprocEnd は Parser (ParseModuleBlock) が `#END` として消費する
                // ため、ここでは skip せず result に流す。`#IF...#END` ブロック内の
                // `#END` は ProcessIf 内で消費済みなので、ここに到達するのは
                // `#MODULE...#END` の終端等の trail-level な `#END` のみ。
                // (PR-B2 で発見した PR-A バグ修正: 従来は `#END` も skip され、
                //  ParseModuleBlock の `#END` 検出が機能しなかった)

                case TokenKind.Const:
                    // CONST宣言からプリプロセッサ用の定数値を抽出
                    ExtractConstValues(tokens, i);
                    result.Add(token);
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

    /// <summary>
    /// CONSTトークンの後続トークンから name=value パターンを抽出。
    /// 整数リテラル右辺のみ対応。トークン列自体は変更しない（パーサーにも渡す）。
    /// </summary>
    private void ExtractConstValues(List<Token> tokens, int constIndex)
    {
        int i = constIndex + 1;
        while (i < tokens.Count)
        {
            // name = value パターン
            if (i + 2 < tokens.Count
                && tokens[i].Kind == TokenKind.Identifier
                && tokens[i + 1].Kind == TokenKind.Eq
                && tokens[i + 2].Kind == TokenKind.IntegerLiteral)
            {
                _constValues[tokens[i].StringValue] = Convert.ToInt32(tokens[i + 2].Value);
                i += 3;
                // カンマで続く場合
                if (i < tokens.Count && tokens[i].Kind == TokenKind.Comma)
                    i++;
                else
                    break;
            }
            else if (i + 2 < tokens.Count
                     && tokens[i].Kind == TokenKind.Identifier
                     && tokens[i + 1].Kind == TokenKind.Eq)
            {
                // 右辺が整数リテラルでない → この定数はスキップ（式やラベル参照）
                // 次のカンマかセミコロンまで飛ばす
                i += 2;
                while (i < tokens.Count && tokens[i].Kind != TokenKind.Comma && tokens[i].Kind != TokenKind.Semicolon)
                    i++;
                if (i < tokens.Count && tokens[i].Kind == TokenKind.Comma)
                    i++;
                else
                    break;
            }
            else
            {
                break;
            }
        }
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
        bool condition = EvaluateCondition(ifToken);

        int i = startIndex + 1;
        int depth = 1;

        // 真/偽に応じて採用するトークンを一旦collectに集める
        var collect = new List<Token>();

        if (condition)
        {
            // 真の場合: #ELSEまでのトークンを採用
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

                if (depth > 0) collect.Add(tokens[i]);
                i++;
            }
        }
        else
        {
            // 偽の場合: #ELSEまでスキップ、#ELSEから#ENDまでを採用
            while (i < tokens.Count && depth > 0)
            {
                if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                else if (tokens[i].Kind == TokenKind.PreprocEnd) { depth--; if (depth == 0) { i++; break; } }
                else if (tokens[i].Kind == TokenKind.PreprocElse && depth == 1)
                {
                    // #ELSE到達: ここから採用
                    i++;
                    while (i < tokens.Count)
                    {
                        if (tokens[i].Kind == TokenKind.PreprocIf) depth++;
                        else if (tokens[i].Kind == TokenKind.PreprocEnd) { depth--; if (depth == 0) { i++; break; } }

                        if (depth > 0) collect.Add(tokens[i]);
                        i++;
                    }
                    break;
                }
                i++;
            }
        }

        // 採用したトークンを再帰的にProcess（内側の#IFを評価）
        var processed = Process(collect, ".", 0);
        result.AddRange(processed);

        return i;
    }

    /// <summary>#IF 条件式を評価。評価不能の場合はfalse（trueフォールバック禁止）。</summary>
    private bool EvaluateCondition(Token ifToken)
    {
        string expr = ifToken.StringValue.Trim();
        if (expr.Equals("TRUE", StringComparison.OrdinalIgnoreCase)) return true;
        if (expr.Equals("FALSE", StringComparison.OrdinalIgnoreCase)) return false;
        if (int.TryParse(expr, out int val)) return val != 0;

        // 式をトークン化して再帰下降で評価
        var tokens = new Lexer(expr, "#IF").Tokenize();
        int pos = 0;
        int? result = EvalBitOps(tokens, ref pos);

        // 全トークン消費チェック
        if (result != null && pos < tokens.Count && tokens[pos].Kind != TokenKind.EOF)
            result = null;

        if (result == null)
        {
            _diagnostics.Error($"#IF condition could not be evaluated: {expr}", ifToken.Span);
            return false;
        }
        return result.Value != 0;
    }

    // === 再帰下降式評価器 ===
    // 優先順位: BitOps(AND/OR/XOR) → Compare(==,!=,<,>,<=,>=) → Add(+,-) → Mul(*,/) → Unary(-,NOT,CPL) → Primary

    /// <summary>AND / OR / XOR（同一優先度、整数演算）</summary>
    private int? EvalBitOps(List<Token> tokens, ref int pos)
    {
        var left = EvalCompare(tokens, ref pos);
        while (left != null && pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind == TokenKind.And || kind == TokenKind.Ampersand || kind == TokenKind.LogAnd)
            {
                pos++;
                var right = EvalCompare(tokens, ref pos);
                if (right == null) return null;
                left = left.Value & right.Value;
            }
            else if (kind == TokenKind.Or || kind == TokenKind.Pipe || kind == TokenKind.LogOr)
            {
                pos++;
                var right = EvalCompare(tokens, ref pos);
                if (right == null) return null;
                left = left.Value | right.Value;
            }
            else if (kind == TokenKind.Xor)
            {
                pos++;
                var right = EvalCompare(tokens, ref pos);
                if (right == null) return null;
                left = left.Value ^ right.Value;
            }
            else break;
        }
        return left;
    }

    /// <summary>== / != / &lt; / &gt; / &lt;= / &gt;= （結果は0/1）</summary>
    private int? EvalCompare(List<Token> tokens, ref int pos)
    {
        var left = EvalAdd(tokens, ref pos);
        while (left != null && pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind == TokenKind.EqEq)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value == r.Value ? 1 : 0; }
            else if (kind == TokenKind.NotEq)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value != r.Value ? 1 : 0; }
            else if (kind == TokenKind.Lt)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value < r.Value ? 1 : 0; }
            else if (kind == TokenKind.Gt)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value > r.Value ? 1 : 0; }
            else if (kind == TokenKind.Le)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value <= r.Value ? 1 : 0; }
            else if (kind == TokenKind.Ge)
            { pos++; var r = EvalAdd(tokens, ref pos); if (r == null) return null; left = left.Value >= r.Value ? 1 : 0; }
            else break;
        }
        return left;
    }

    /// <summary>+ / -</summary>
    private int? EvalAdd(List<Token> tokens, ref int pos)
    {
        var left = EvalMul(tokens, ref pos);
        while (left != null && pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind == TokenKind.Plus) { pos++; var r = EvalMul(tokens, ref pos); if (r == null) return null; left = left.Value + r.Value; }
            else if (kind == TokenKind.Minus) { pos++; var r = EvalMul(tokens, ref pos); if (r == null) return null; left = left.Value - r.Value; }
            else break;
        }
        return left;
    }

    /// <summary>* / /</summary>
    private int? EvalMul(List<Token> tokens, ref int pos)
    {
        var left = EvalUnary(tokens, ref pos);
        while (left != null && pos < tokens.Count)
        {
            var kind = tokens[pos].Kind;
            if (kind == TokenKind.Star) { pos++; var r = EvalUnary(tokens, ref pos); if (r == null) return null; left = left.Value * r.Value; }
            else if (kind == TokenKind.Slash) { pos++; var r = EvalUnary(tokens, ref pos); if (r == null) return null; left = r.Value != 0 ? left.Value / r.Value : 0; }
            else break;
        }
        return left;
    }

    /// <summary>- / NOT / CPL</summary>
    private int? EvalUnary(List<Token> tokens, ref int pos)
    {
        if (pos < tokens.Count)
        {
            if (tokens[pos].Kind == TokenKind.Minus) { pos++; var v = EvalUnary(tokens, ref pos); return v != null ? -v.Value : null; }
            if (tokens[pos].Kind == TokenKind.Not) { pos++; var v = EvalUnary(tokens, ref pos); return v != null ? (v.Value != 0 ? 0 : 1) : null; }
            if (tokens[pos].Kind == TokenKind.Cpl) { pos++; var v = EvalUnary(tokens, ref pos); return v != null ? ~v.Value : null; }
        }
        return EvalPrimary(tokens, ref pos);
    }

    /// <summary>数値 / 識別子(CONST参照) / (expr)</summary>
    private int? EvalPrimary(List<Token> tokens, ref int pos)
    {
        if (pos >= tokens.Count || tokens[pos].Kind == TokenKind.EOF) return null;

        var token = tokens[pos];

        // 整数リテラル
        if (token.Kind == TokenKind.IntegerLiteral)
        {
            pos++;
            return Convert.ToInt32(token.Value);
        }

        // 識別子 → _constValuesから値を取得
        if (token.Kind == TokenKind.Identifier)
        {
            pos++;
            if (_constValues.TryGetValue(token.StringValue, out int constVal))
                return constVal;
            // 未定義の識別子 → 0として扱う（旧コンパイラ互換）
            return 0;
        }

        // 括弧
        if (token.Kind == TokenKind.LParen)
        {
            pos++; // skip (
            var val = EvalBitOps(tokens, ref pos);
            if (pos < tokens.Count && tokens[pos].Kind == TokenKind.RParen)
                pos++; // skip )
            return val;
        }

        return null;
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
