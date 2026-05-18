using SLANGCompiler.Parser.Ast;

namespace SLANGCompiler.Semantics;

/// <summary>
/// シンボルの種類
/// </summary>
public enum SymbolKind
{
    Variable,
    Parameter,
    Function,
    MachineFunction,
    /// <summary>
    /// CFUNC 宣言由来。C backend (= oscar_c) で SLANG → C 関数の直接マッピング
    /// として扱われる。Z80 backend では IrGenerator が診断 error。
    /// CName field に C 側 ident (case preserve) を持つ。
    /// </summary>
    CFunction,
    Constant,
    Label,
}

/// <summary>
/// シンボル（変数、関数、定数など）
/// </summary>
public class Symbol
{
    public string Name { get; }
    public SymbolKind Kind { get; }
    public SlangType Type { get; set; }
    public int? Address { get; set; }       // 固定アドレス指定
    public int Offset { get; set; }         // ローカル変数のスタックオフセット
    public object? ConstValue { get; set; }  // 定数値(int)
    public double? ConstFloatValue { get; set; }  // FLOAT定数値
    public bool IsGlobal { get; set; }
    public bool IsCodeBlock { get; set; }    // CODEブロック定数（アドレス参照）
    public string? AsmLabel { get; set; }    // アセンブリラベル名
    public string? CName { get; set; }       // CFUNC 宣言由来の C 側 ident (case preserve、Kind==CFunction で使用)
    public bool IsArrayDecl { get; set; }   // ARRAY宣言由来（PointerTypeでもアドレス参照）

    // AST保持（semantic段階で設定、IR段階で文字列化）
    public Expression? ConstAst { get; set; }          // CONST値のAST（非整数の場合）
    public Expression? AddressAst { get; set; }        // MACHINE:式のAST

    // キャッシュ（IR段階で初回解決時に設定）
    public string? ConstAsmExpr { get; set; }          // CONST値の文字列化済みアセンブラ式
    public List<string>? ConstAsmDeps { get; set; }    // CONST式の依存シンボル
    public bool ConstAsmResolved { get; set; }         // 解決試行済みフラグ（失敗時の再試行防止）
    public string? AddressExpr { get; set; }           // MACHINE:式の文字列化済みアセンブラ式
    public List<string>? AddressExprDeps { get; set; } // MACHINE:式の依存シンボル
    public bool AddressExprResolved { get; set; }      // 解決試行済みフラグ

    public Symbol(string name, SymbolKind kind, SlangType type)
    {
        Name = name;
        Kind = kind;
        Type = type;
    }

    public override string ToString() => $"{Kind} {Name}: {Type}";
}

/// <summary>
/// スコープ付きシンボルテーブル。
/// スコープの入れ子をスタックで管理。
/// </summary>
public class SymbolTable
{
    private readonly Stack<Scope> _scopes = new();
    private readonly bool _caseSensitive;

    public SymbolTable(bool caseSensitive = false)
    {
        _caseSensitive = caseSensitive;
        PushScope("global");
    }

    public Scope CurrentScope => _scopes.Peek();
    public Scope GlobalScope => _scopes.ElementAt(_scopes.Count - 1);
    public bool IsGlobalScope => _scopes.Count == 1;
    public int ScopeCount => _scopes.Count;

    public void PushScope(string name)
    {
        _scopes.Push(new Scope(name, _scopes.Count > 0 ? _scopes.Peek() : null, _caseSensitive));
    }

    public void PopScope()
    {
        if (_scopes.Count <= 1)
            throw new InvalidOperationException("Cannot pop the global scope.");
        _scopes.Pop();
    }

    public Symbol Define(string name, SymbolKind kind, SlangType type)
    {
        var symbol = new Symbol(name, kind, type) { IsGlobal = IsGlobalScope };
        CurrentScope.Define(symbol);
        return symbol;
    }

    /// <summary>
    /// overlay scope を push 中でも global (最外) scope にシンボルを登録する。
    /// #MODULE 内の関数/MACHINE/CONST を main から参照可能に保つための API。
    /// </summary>
    public Symbol DefineInGlobal(string name, SymbolKind kind, SlangType type)
    {
        var symbol = new Symbol(name, kind, type) { IsGlobal = true };
        GlobalScope.Define(symbol);
        return symbol;
    }

    public Symbol? Resolve(string name) => CurrentScope.Resolve(name);
}

/// <summary>
/// 単一スコープ
/// </summary>
public class Scope
{
    public string Name { get; }
    public Scope? Parent { get; }
    private readonly Dictionary<string, Symbol> _symbols;
    private readonly StringComparer _comparer;

    public IReadOnlyDictionary<string, Symbol> Symbols => _symbols;

    public Scope(string name, Scope? parent, bool caseSensitive)
    {
        Name = name;
        Parent = parent;
        _comparer = caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
        _symbols = new Dictionary<string, Symbol>(_comparer);
    }

    public void Define(Symbol symbol)
    {
        _symbols[symbol.Name] = symbol;
    }

    public Symbol? Resolve(string name)
    {
        if (_symbols.TryGetValue(name, out var symbol))
            return symbol;
        return Parent?.Resolve(name);
    }

    public Symbol? ResolveLocal(string name)
    {
        _symbols.TryGetValue(name, out var symbol);
        return symbol;
    }
}
