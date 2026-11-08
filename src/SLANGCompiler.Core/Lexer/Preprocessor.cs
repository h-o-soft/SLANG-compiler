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

    /// <summary>
    /// #IF の条件に書かれた名前 (値があったかどうかによらない)。プリプロセッサは関数の中かどうかを知らないので、
    /// 関数の中の CONST の名前がここにあれば、意味解析がエラーにする (#IF はファイル全体に効くため)
    /// </summary>
    public HashSet<string> IdentifiersUsedByIf { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Preprocessor(DiagnosticBag diagnostics, List<string>? includePaths = null, int maxIncludeDepth = 8)
    {
        _diagnostics = diagnostics;
        _includePaths = includePaths ?? new List<string> { "." };
        _maxIncludeDepth = maxIncludeDepth;
    }

    /// <summary>プリプロセッサ定数を事前登録（環境定数等）。#IF条件式で参照可能。</summary>
    public void DefineConst(string name, int value) => SetConst(name, value);

    /// <summary>読んでいるファイルの文脈: 場所 (#INCLUDE・#CHAIN の探し方)・深さ・#CHAIN で続きへ移ったか</summary>
    private sealed class FileContext(string baseDir, int depth)
    {
        public string BaseDir { get; } = baseDir;
        public int Depth { get; } = depth;
        public bool Chained;       // #CHAIN で続きへ移った (このファイルの残りは、#IF の外も読まない)
        public bool ChainWarned;
    }

    // #IF の CONST の値を、式で決めるための記号 (_constValues と同じ中身)
    private readonly Dictionary<string, Semantics.Symbol> _constSymbols = new(StringComparer.OrdinalIgnoreCase);
    // 読んでいる途中のファイル (#INCLUDE・#CHAIN の先祖。#CHAIN の輪を見る)。名前の大文字小文字は、ファイルの仕組みに合わせる
    private readonly HashSet<string> _activeFiles = new(
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    // 右辺の終わりが分からず (続きが #IF の外・取り込んだ側にあるかもしれない)、値の分からない CONST。#IF で使ったらエラー
    private readonly HashSet<string> _unknownConsts = new(StringComparer.OrdinalIgnoreCase);
    // 右辺の終わりが分からないが、整数のリテラル 1 つなので値を覚えた CONST (これを使う CONST は値が分からない)
    private readonly HashSet<string> _provisionalConsts = new(StringComparer.OrdinalIgnoreCase);
    // 右辺の終わりが分からなかった CONST。宣言の後で選ばれた字句で決める (SettlePendingConsts)
    private readonly List<PendingConst> _pendingConsts = new();
    // CONST の右辺の式が使った名前 (たどった先も)
    private readonly Dictionary<string, HashSet<string>> _constDeps = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// トークン列を処理し、#INCLUDEを展開、#IF/#ELSE/#ENDIFを評価する。
    /// </summary>
    public List<Token> Process(List<Token> tokens, string baseDir, int depth = 0)
    {
        var result = new List<Token>();
        ProcessTokens(tokens, new FileContext(baseDir, depth), result);
        return result;
    }

    private void ProcessTokens(List<Token> tokens, FileContext ctx, List<Token> result)
    {
        int i = 0;

        while (i < tokens.Count)
        {
            if (ctx.Chained)
            {
                WarnTextAfterChain(tokens, i, ctx);
                return;
            }
            var token = tokens[i];
            if (_pendingConsts.Count > 0 && !IsDirective(token.Kind))
                SettlePendingConsts(tokens, i, token.Kind);

            switch (token.Kind)
            {
                case TokenKind.PreprocInclude:
                    ProcessInclude(token, result, ctx);
                    i++;
                    break;

                case TokenKind.PreprocChain:
                    ProcessChain(token, result, ctx);
                    i++;
                    break;

                case TokenKind.PreprocIf:
                    i = ProcessIf(tokens, i, result, ctx);
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
    }

    /// <summary>#CHAIN の後ろに字句があれば、読まないことを 1 度だけ知らせる</summary>
    private void WarnTextAfterChain(List<Token> tokens, int from, FileContext ctx)
    {
        if (ctx.ChainWarned) return;
        for (int j = from; j < tokens.Count; j++)
        {
            if (tokens[j].Kind == TokenKind.EOF) continue;
            _diagnostics.Warning("Text after #CHAIN is not read", tokens[j].Span);
            ctx.ChainWarned = true;
            return;
        }
    }

    private void SetConst(string name, int value)
    {
        _constValues[name] = value;
        _constSymbols[name] = new Semantics.Symbol(name, Semantics.SymbolKind.Constant, Semantics.SlangType.Word) { ConstValue = value };
    }

    /// <summary>
    /// CONSTトークンの後続トークンから name=value を抽出。右辺は構文解析器で式として読み (読み終えた所が右辺の終わり)、
    /// 整数のリテラル 1 つならその値、それ以外はそれまでに覚えた値で ConstEvaluator にかける。
    /// 値が出なければ覚えない (#IF では 0)。トークン列自体は変更しない（パーサーにも渡す）。
    /// </summary>
    private void ExtractConstValues(List<Token> tokens, int constIndex)
    {
        int i = constIndex + 1;
        while (i + 1 < tokens.Count
               && tokens[i].Kind == TokenKind.Identifier
               && tokens[i + 1].Kind == TokenKind.Eq)
        {
            var name = tokens[i].StringValue;
            int start = i + 2;
            int end;
            if (start < tokens.Count && IsDirective(tokens[start].Kind))
            {
                // 右辺が #IF などで始まる: 値は #IF を処理した後で決まるので、ここでは覚えない
                _unknownConsts.Add(name);
                break;
            }
            if (start < tokens.Count && IsBlockOpen(tokens[start].Kind))
            {
                // CODE の並び (CONST T = [1, 2]): 対になる閉じまで飛ばす。値は覚えない。
                // 並びの中に #IF などがあれば、どこで閉じるかは #IF を処理した後で決まるので、ここで止める
                // (選ばれない側の閉じの後ろの CONST を覚えない)
                end = SkipBlock(tokens, start);
                if (end < 0) break;
            }
            else
            {
                var expr = Parser.Parser.ParseExpressionAt(tokens, start, out end);
                bool literal = expr != null && end == start + 1 && tokens[start].Kind == TokenKind.IntegerLiteral;
                // 式の後ろが #IF・#INCLUDE などか、字句の列の終わり (#IF の中・取り込んだファイルの終わり。主のファイルの
                // 終わりには EOF がある) なら、右辺がそこで終わるとは限らない (続きが #IF の中・外や取り込んだ側にあるかも
                // しれない)。次に選ばれた字句 (指示でないもの) で決める (SettlePendingConsts)。それまで、整数のリテラル 1 つは
                // #IF で直接使うといままでどおりの値 (仮の値。これを使う CONST の値は分からない)、式は値が分からない
                bool mayContinue = expr != null && (end >= tokens.Count || IsDirective(tokens[end].Kind));
                if (mayContinue)
                {
                    if (literal)
                    {
                        SetConst(name, Convert.ToInt32(tokens[start].Value));
                        _provisionalConsts.Add(name);
                    }
                    else
                        _unknownConsts.Add(name);
                    _pendingConsts.Add(new PendingConst(name, expr!, literal, tokens, end));
                    break;
                }
                if (expr == null)
                {
                    // 読めない右辺: 次の , か ; まで飛ばす (いままでどおり)
                    end = start;
                    while (end < tokens.Count && tokens[end].Kind != TokenKind.Comma && tokens[end].Kind != TokenKind.Semicolon)
                    {
                        if (IsDirective(tokens[end].Kind)) return;   // #IF の中の字句から次の名前を拾わない
                        end++;
                    }
                }
                else if (literal)
                    SetConst(name, Convert.ToInt32(tokens[start].Value));
                else
                    RecordConst(name, expr);
            }

            // カンマで続く場合
            if (end < tokens.Count && tokens[end].Kind == TokenKind.Comma)
                i = end + 1;
            else
                break;
        }
    }


    /// <summary>
    /// 式の右辺の CONST の値を決める。使った名前 (とその名前が使った名前) を覚え、#IF で name を使ったら、それらも
    /// 使ったことにする (関数の中の CONST を、大域の CONST を通して #IF に使うのを、意味解析が見つけられるように)
    /// </summary>
    private void RecordConst(string name, Parser.Ast.Expression expr)
    {
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Semantics.SemanticAnalyzer.ForEachName(expr, (n, _) =>
        {
            deps.Add(n);
            if (_constDeps.TryGetValue(n, out var nd)) deps.UnionWith(nd);
        });
        _constDeps[name] = deps;
        if (deps.Any(d => _unknownConsts.Contains(d) || _provisionalConsts.Contains(d)))
        {
            _unknownConsts.Add(name);   // 値の分からない CONST・仮の値の CONST を使う CONST も、値が分からない
            return;
        }
        var eval = new Semantics.ConstEvaluator(null, _constSymbols);
        if (!eval.InvolvesFloat(expr) && eval.Evaluate(expr) is int value)
            SetConst(name, value);
    }

    /// <summary>右辺の終わりが分からなかった CONST (PendingConst)。List・End は、宣言のあった字句の列と、右辺の次の位置</summary>
    private sealed record PendingConst(string Name, Parser.Ast.Expression Expr, bool Literal, List<Token> List, int End);

    /// <summary>
    /// 宣言の後で選ばれた字句 (指示でないもの) が来たら、右辺の終わりが分からなかった CONST を決める。その字句が式を続ける
    /// もの (二項演算子・? ・添字の [ など) なら、右辺は続いていた (値が分からない)。ほか (; ・, ・次の宣言・EOF など) なら、
    /// 右辺はそこで終わっていた (リテラルは仮でなくなり、式はここで値を決める)
    /// </summary>
    private void SettlePendingConsts(List<Token> tokens, int index, TokenKind kind)
    {
        bool continues = ContinuesExpression(kind);
        for (int k = 0; k < _pendingConsts.Count; k++)
        {
            var p = _pendingConsts[k];
            if (ReferenceEquals(p.List, tokens) && index < p.End) continue;   // 宣言そのものの字句
            _pendingConsts.RemoveAt(k--);
            if (continues)
            {
                _provisionalConsts.Remove(p.Name);
                _constValues.Remove(p.Name);
                _constSymbols.Remove(p.Name);
                _unknownConsts.Add(p.Name);
            }
            else if (p.Literal)
                _provisionalConsts.Remove(p.Name);
            else
            {
                _unknownConsts.Remove(p.Name);
                RecordConst(p.Name, p.Expr);
            }
        }
    }

    private static bool ContinuesExpression(TokenKind k) => k is TokenKind.Plus or TokenKind.Minus or TokenKind.Star
        or TokenKind.Slash or TokenKind.Percent or TokenKind.Mod or TokenKind.And or TokenKind.Or or TokenKind.Xor
        or TokenKind.Ampersand or TokenKind.Pipe or TokenKind.EqEq or TokenKind.NotEq or TokenKind.Lt or TokenKind.Gt
        or TokenKind.Le or TokenKind.Ge or TokenKind.SignedLt or TokenKind.SignedGt or TokenKind.SignedLe or TokenKind.SignedGe
        or TokenKind.SignedMul or TokenKind.SignedDiv or TokenKind.SignedMod or TokenKind.SignedShl or TokenKind.SignedShr
        or TokenKind.Shl or TokenKind.Shr or TokenKind.LogAnd or TokenKind.LogOr or TokenKind.PlusPlus or TokenKind.MinusMinus
        or TokenKind.Question or TokenKind.Colon or TokenKind.ArrayBracketOpen or TokenKind.LParen;

    private static bool IsDirective(TokenKind k)
        => k is TokenKind.PreprocIf or TokenKind.PreprocElse or TokenKind.PreprocEnd
            or TokenKind.PreprocInclude or TokenKind.PreprocChain;

    private static bool IsBlockOpen(TokenKind k)
        => k is TokenKind.LBracket or TokenKind.ArrayBracketOpen or TokenKind.LBrace or TokenKind.Begin or TokenKind.LAngleBracket;

    private static bool IsBlockClose(TokenKind k)
        => k is TokenKind.RBracket or TokenKind.RBrace or TokenKind.End or TokenKind.RAngleBracket;

    /// <summary>tokens[start] の開きに対になる閉じの次の位置。途中に #IF などがあれば -1</summary>
    private static int SkipBlock(List<Token> tokens, int start)
    {
        int depth = 0, j = start;
        for (; j < tokens.Count; j++)
        {
            if (IsDirective(tokens[j].Kind)) return -1;
            if (IsBlockOpen(tokens[j].Kind)) depth++;
            else if (IsBlockClose(tokens[j].Kind) && --depth == 0) return j + 1;
        }
        return j;
    }

    private void ProcessInclude(Token token, List<Token> result, FileContext ctx)
    {
        if (ctx.Depth >= _maxIncludeDepth)
        {
            _diagnostics.Error($"#INCLUDE nesting too deep (max {_maxIncludeDepth})", token.Span);
            return;
        }

        var fileName = token.StringValue.Trim().Trim('"');
        var filePath = ResolveIncludePath(fileName, ctx.BaseDir);

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

        bool active = _activeFiles.Add(fullPath);
        try
        {
            var includeTokens = ReadTokens(filePath, fileName);

            // 再帰的にプリプロセス (取り込んだファイルの文脈で。#CHAIN はそのファイルの残りを止めるだけ)
            var includeDir = Path.GetDirectoryName(filePath) ?? ctx.BaseDir;
            ProcessTokens(includeTokens, new FileContext(includeDir, ctx.Depth + 1), result);
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"#INCLUDE error: {ex.Message}", token.Span);
        }
        finally
        {
            _includedFiles.Remove(Path.GetFullPath(filePath));
            if (active) _activeFiles.Remove(fullPath);
        }
    }

    /// <summary>
    /// #CHAIN ファイル: そのファイルへ移って戻らない (原典)。中身を出したあと、このファイルの残りは読まない。
    /// 移った先は続きなので、深さは同じ
    /// </summary>
    private void ProcessChain(Token token, List<Token> result, FileContext ctx)
    {
        var fileName = token.StringValue.Trim().Trim('"');
        var filePath = ResolveIncludePath(fileName, ctx.BaseDir);
        if (filePath == null)
        {
            _diagnostics.Error($"#CHAIN file not found: {fileName}", token.Span);
            return;
        }
        // 輪: 読んでいる途中のファイル (#INCLUDE・#CHAIN でたどってきた先祖) へ移る。同じファイルを、別々の
        // #INCLUDE から 2 度 #CHAIN で読むのは輪ではない
        var fullPath = Path.GetFullPath(filePath);
        if (!_activeFiles.Add(fullPath))
        {
            _diagnostics.Error($"#CHAIN loop: {fileName} is already being read", token.Span);
            return;
        }

        try
        {
            var chainTokens = ReadTokens(filePath, fileName);
            var chainDir = Path.GetDirectoryName(filePath) ?? ctx.BaseDir;
            ProcessTokens(chainTokens, new FileContext(chainDir, ctx.Depth), result);
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"#CHAIN error: {ex.Message}", token.Span);
        }
        finally
        {
            _activeFiles.Remove(fullPath);
        }
        ctx.Chained = true;
    }

    /// <summary>ファイルを読んで字句にする (EOF は除く)</summary>
    private static List<Token> ReadTokens(string filePath, string fileName)
    {
        var source = File.ReadAllText(filePath);
        var tokens = new Lexer(source, fileName).Tokenize();
        tokens.RemoveAll(t => t.Kind == TokenKind.EOF);
        return tokens;
    }

    private int ProcessIf(List<Token> tokens, int startIndex, List<Token> result, FileContext ctx)
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

        // 採用したトークンを、同じファイルの文脈で処理する（内側の#IFを評価。#INCLUDE はこのファイルの場所から探し、
        // #CHAIN ならこのファイルの残りも止める）
        ProcessTokens(collect, ctx, result);

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
            IdentifiersUsedByIf.Add(token.StringValue);
            if (_constDeps.TryGetValue(token.StringValue, out var deps))
                IdentifiersUsedByIf.UnionWith(deps);
            if (_unknownConsts.Contains(token.StringValue))
            {
                _diagnostics.Error($"#IF uses CONST '{token.StringValue}', whose value is not known here "
                    + "(its declaration continues past a preprocessor directive or the end of a block/file)", token.Span);
                return 0;
            }
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
