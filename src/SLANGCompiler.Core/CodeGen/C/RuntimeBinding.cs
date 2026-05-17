using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// SLANG builtin / 標準関数を C runtime (<c>slang_*</c>) や oscar64 同梱の C 標準
/// ライブラリ関数に resolve する table。CTranspiler が builtin 呼び出しを emit
/// する際に参照する。
///
/// Z80 backend では <see cref="Runtime.RuntimeManager"/> が <c>.asm</c> 経由で
/// 同等の機能を提供しており、ここはその C 版に相当する。
/// </summary>
public static class RuntimeBinding
{
    /// <summary>
    /// SLANG builtin 関数の情報。
    /// </summary>
    /// <param name="CName">C 側の関数名 (例: <c>slang_print_int</c>)</param>
    /// <param name="ReturnType">戻り型 (void 含む)</param>
    public record BuiltinInfo(string CName, SlangType ReturnType);

    private static readonly Dictionary<string, BuiltinInfo> _table = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- PRINT 系 (PrintStmt 引数の型 dispatch で個別関数を呼ぶ) ---
        // PRINT 文自体は CEmitter.VisitPrintStmt で個別 dispatch するため、ここには登録しない。
        // 代わりに PRINT が呼ぶ runtime 関数名を CEmitter から参照する。

        // --- 文字列ヘルパ (StringFuncExpr 経由) ---
        // SLANG 仕様の static buffer 踏襲 (CHR$/DECI$ は呼び出しごとに上書き)
        { "CHR$", new BuiltinInfo("slang_chr", new PointerType(SlangType.Byte)) },
        { "DECI$", new BuiltinInfo("slang_deci", new PointerType(SlangType.Byte)) },

        // --- 標準数学関数 (SLANG identifier or builtin call) ---
        // oscar64 同梱の math.h を流用するため C 名は標準名そのまま。
        { "ABS", new BuiltinInfo("abs", SlangType.Word) },
        { "SQR", new BuiltinInfo("sqrtf", SlangType.Float) },
        { "SIN", new BuiltinInfo("sinf", SlangType.Float) },
        { "COS", new BuiltinInfo("cosf", SlangType.Float) },
        { "TAN", new BuiltinInfo("tanf", SlangType.Float) },
        { "LOG", new BuiltinInfo("logf", SlangType.Float) },
        { "EXP", new BuiltinInfo("expf", SlangType.Float) },
        { "ATN", new BuiltinInfo("atanf", SlangType.Float) },

        // --- 乱数 ---
        { "RND", new BuiltinInfo("slang_rnd", SlangType.Word) },
        { "SRND", new BuiltinInfo("slang_srnd", SlangType.Void) },

        // --- ビット操作 ---
        { "BIT", new BuiltinInfo("slang_bit", SlangType.Word) },
        { "SET", new BuiltinInfo("slang_set", SlangType.Void) },
        { "RESET", new BuiltinInfo("slang_reset", SlangType.Void) },

        // --- 文字列長 ---
        { "STRLEN", new BuiltinInfo("strlen", SlangType.Word) },
    };

    /// <summary>
    /// builtin 名で table を引く。見つからなければ null (= user 関数として扱う)。
    /// </summary>
    public static BuiltinInfo? Lookup(string name) => _table.TryGetValue(name, out var info) ? info : null;
}
