using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// CTranspiler が AST walk 中に維持する独自の scope/型 stack。
///
/// 設計背景:
///   <see cref="SemanticAnalyzer"/> は関数解析後に <c>SymbolTable.PopScope()</c>
///   するため、解析完了後の <see cref="SymbolTable"/> には global symbol しか
///   残っていない。<see cref="IR.IrGenerator"/> は visitor 内で
///   <c>_localVars: Dictionary&lt;string, LocalVarInfo&gt;</c> を並走させてこの
///   制約に対処しており、CTranspiler も同じパターンを採る。
///
/// 役割:
///   - 関数 enter/leave で local scope を push/pop
///   - param / local VAR 宣言で current scope に名前→型を登録
///   - 識別子参照時の解決順 = local scope chain → global <see cref="SymbolTable"/>
/// </summary>
public class CScopeTracker
{
    private readonly Stack<Dictionary<string, SlangType>> _stack = new();
    private readonly SymbolTable? _globals;

    public CScopeTracker(SymbolTable? globals)
    {
        _globals = globals;
    }

    /// <summary>現在 push されている scope 数 (関数外で 0)</summary>
    public int ScopeDepth => _stack.Count;

    /// <summary>関数 entry で新しい local scope を push する</summary>
    public void EnterFunction()
    {
        _stack.Push(new Dictionary<string, SlangType>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>関数 leave で current local scope を pop する</summary>
    public void LeaveFunction()
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("LeaveFunction called outside any function scope");
        _stack.Pop();
    }

    /// <summary>
    /// 現在 scope に local / param を登録。同名 redeclare は silent overwrite
    /// (= SLANG semantics に従う、SemanticAnalyzer 側で診断済み)。
    /// </summary>
    public void DeclareLocal(string name, SlangType type)
    {
        if (_stack.Count == 0)
            throw new InvalidOperationException("DeclareLocal called outside any function scope");
        _stack.Peek()[name] = type;
    }

    /// <summary>
    /// 識別子を解決して型を返す。
    /// 解決順: local scope (深い順) → global <see cref="SymbolTable"/>。
    /// 未解決なら null。
    /// </summary>
    public SlangType? Resolve(string name)
    {
        foreach (var scope in _stack)
        {
            if (scope.TryGetValue(name, out var t)) return t;
        }
        var sym = _globals?.GlobalScope.Resolve(name);
        return sym?.Type;
    }

    /// <summary>local scope に存在するかどうか (= 関数内宣言かどうかの判定)</summary>
    public bool IsLocal(string name)
    {
        foreach (var scope in _stack)
        {
            if (scope.ContainsKey(name)) return true;
        }
        return false;
    }
}
