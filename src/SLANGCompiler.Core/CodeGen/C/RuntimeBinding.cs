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
        // --- 文字列を返す PRINT 系 (式コンテキストでも使える、StringFuncExpr 経由) ---
        // SLANG 仕様の static buffer 踏襲 (= 連続呼び出しで前回の buffer 上書き)。
        // PRINT 文の中で使われた場合は CEmitter.EmitPrintArg が直接 print 系
        // (slang_print_str / slang_print_sint 等) に dispatch するため、これらは
        // 式コンテキスト (例: `S = HEX2$(N)`) のフォールバック用。
        { "CHR$", new BuiltinInfo("slang_chr", new PointerType(SlangType.Byte)) },
        { "DECI$", new BuiltinInfo("slang_deci", new PointerType(SlangType.Byte)) },
        { "HEX2$", new BuiltinInfo("slang_hex2", new PointerType(SlangType.Byte)) },
        { "HEX4$", new BuiltinInfo("slang_hex4", new PointerType(SlangType.Byte)) },
        { "PN$", new BuiltinInfo("slang_pn", new PointerType(SlangType.Byte)) },
        { "FL$", new BuiltinInfo("slang_fl", new PointerType(SlangType.Byte)) },
        { "MSX$", new BuiltinInfo("slang_msx", new PointerType(SlangType.Byte)) },
        { "MSG$", new BuiltinInfo("slang_msg", new PointerType(SlangType.Byte)) },
        // FORM$/STR$/SPC$/CR$/TAB$ は PRINT 文専用副作用関数のため、式コンテキスト
        // からは呼ばない設計 (= 呼ばれたら CEmitter で診断 error)。

        // --- 標準数学関数 (oscar64 同梱の math.h を流用、C 名は標準名そのまま) ---
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

        // --- 端末制御 (= MACHINE 関数のうち c64 で実装可能なもの) ---
        // 既存 Z80 backend では env 別の MACHINE 宣言 (libmsxrom_print.asm /
        // libmz25iocs_print.asm 等) で提供されているが、C backend では oscar64
        // conio (<conio.h>) を経由して slang_* wrapper に集約する。SLANG 側に
        // MACHINE 宣言は不要で、未宣言呼び出しがそのまま builtin として通る
        // (= CEmitter の undeclared-call 診断はこの table を確認して救済する)。
        { "WIDTH", new BuiltinInfo("slang_width", SlangType.Void) },     // C64 は常に 40 桁 = no-op、引数受け流し
        { "LOCATE", new BuiltinInfo("slang_locate", SlangType.Void) },   // gotoxy(x, y), 0-indexed
        { "INKEY", new BuiltinInfo("slang_inkey", SlangType.Word) },     // kbhit + getch
        { "SCREEN", new BuiltinInfo("slang_screen", SlangType.Word) },   // 画面文字読み (cpeekc 経由)
        { "PRMODE", new BuiltinInfo("slang_prmode", SlangType.Void) },   // C64 では切替先なし = no-op
    };

    /// <summary>
    /// builtin 名で table を引く。見つからなければ null (= user 関数として扱う)。
    /// </summary>
    public static BuiltinInfo? Lookup(string name) => _table.TryGetValue(name, out var info) ? info : null;
}
