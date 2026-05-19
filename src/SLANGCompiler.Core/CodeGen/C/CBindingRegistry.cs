using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// env file `c_bindings:` 経由の C 関数 binding lookup table。
///
/// 設計意図: SLANG ソース由来の <c>SymbolKind.CFunction</c> とは別 lookup
/// layer として保持し、SymbolTable に env-only シンボルを後注入する責務
/// 混ざりを避ける (= レビュー M3 反映)。
///
/// <see cref="CTranspiler"/> が env.CBindings からロードし、<see cref="CEmitter"/>
/// にコンストラクタで渡す。<see cref="CEmitter.VisitCallExpr"/> の resolve 順:
///   1. <see cref="RuntimeBinding"/> (builtin: WIDTH / LOCATE / HEX2$ 等)
///   2. <see cref="SymbolTable"/> (SLANG 宣言: Function / CFunction / MachineFunction)
///   3. <see cref="CBindingRegistry"/> (env c_bindings)
///   4. undeclared → error
///
/// SLANG 側 CFUNC 宣言と同名の env binding は SymbolTable が先に hit する
/// ため、SLANG 側 (= ユーザー override) が優先される。
///
/// name 重複の reject は <see cref="EnvironmentLoader"/> 側 (case-insensitive
/// HashSet) で行う。本クラスは loader が事前 validate 済の前提で overwrite 動作。
/// </summary>
public class CBindingRegistry
{
    private readonly Dictionary<string, CBindingDef> _entries
        = new(StringComparer.OrdinalIgnoreCase);

    public void Add(CBindingDef def) { _entries[def.Name] = def; }
    public CBindingDef? Lookup(string name) => _entries.TryGetValue(name, out var d) ? d : null;
    public IEnumerable<CBindingDef> All => _entries.Values;
    public bool IsEmpty => _entries.Count == 0;

    /// <summary>
    /// CBindingType → SLANG SlangType への変換。CEmitter の Cast / extern 出力で使う。
    /// </summary>
    public static SlangType MapType(CBindingType t) => t switch
    {
        CBindingType.Byte     => SlangType.Byte,
        CBindingType.Word     => SlangType.Word,
        CBindingType.Float    => SlangType.Float,
        CBindingType.BytePtr  => new PointerType(SlangType.Byte),
        CBindingType.WordPtr  => new PointerType(SlangType.Word),
        CBindingType.FloatPtr => new PointerType(SlangType.Float),
        CBindingType.Void     => SlangType.Void,
        _ => SlangType.Word,
    };
}
