using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// SLANG の <see cref="SlangType"/> を oscar64 C の型表現に変換する。
/// oscar64 の前提:
///   - sizeof(int) == 2 (16-bit signed)、SLANG WORD は <c>unsigned int</c> にマップ
///   - sizeof(float) == 4 (32-bit IEEE)、SLANG FLOAT (24-bit f24) → float32
///   - sizeof(char*) == 2、SLANG ポインタはそのまま <c>T *</c>
/// 既存 IR / Z80 backend は触らず、ここで C 表現を中央集約する。
/// </summary>
public static class CTypeMapper
{
    /// <summary>
    /// SlangType → C type 文字列 (変数宣言の左辺)。
    /// 配列型は要素型のみ返す (Dimensions は宣言側で別途付与)。
    /// </summary>
    public static string MapDeclType(SlangType type) => type switch
    {
        PrimitiveType { Kind: PrimitiveKind.Byte } => "unsigned char",
        PrimitiveType { Kind: PrimitiveKind.Word } => "unsigned int",
        PrimitiveType { Kind: PrimitiveKind.Float } => "float",
        PrimitiveType { Kind: PrimitiveKind.Void } => "void",
        PointerType pt => $"{MapDeclType(pt.ElementType)} *",
        ArrayType at => MapDeclType(at.ElementType),
        FunctionType => "void *",  // v1 では関数ポインタ未サポート、placeholder
        // PortArrayType / MemoryArrayType は v1 では宣言型として直接使わない
        // (PortArrayType → error、MemoryArrayType は SLANG_MEM/MEMW マクロ経由)。
        _ => "unsigned int",
    };

    /// <summary>
    /// SlangType → 0 初期化用の C リテラル (グローバル変数の暗黙初期化に使う)。
    /// 配列はゼロクリア用に <c>{0}</c>、それ以外は型相当の 0。
    /// </summary>
    public static string ZeroInitializer(SlangType type) => type switch
    {
        PrimitiveType { Kind: PrimitiveKind.Float } => "0.0f",
        PointerType => "0",
        ArrayType => "{0}",
        _ => "0",
    };

    /// <summary>
    /// 配列宣言の右側次元表記 (例: ArrayType(Byte, [3,4]) → <c>"[3][4]"</c>)。
    /// 配列以外は空文字を返す。
    /// </summary>
    public static string ArrayDimsSuffix(SlangType type)
    {
        if (type is not ArrayType at) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var dim in at.Dimensions)
        {
            sb.Append('[');
            sb.Append(dim);
            sb.Append(']');
        }
        return sb.ToString();
    }

    /// <summary>
    /// 算術演算結果の wrap キャスト先 (= Byte 演算は <c>unsigned char</c>、
    /// Word 演算は <c>unsigned int</c>)。Float は wrap 不要。
    /// oscar64 は usual arithmetic conversion で 16-bit 値を 32-bit に
    /// 拡張することがあるため、SLANG WORD の意味論 (16-bit wrap) を保つには
    /// 各演算の結果を毎回キャストし直す必要がある。
    /// </summary>
    public static string? WrapCastFor(SlangType type) => type switch
    {
        PrimitiveType { Kind: PrimitiveKind.Byte } => "unsigned char",
        PrimitiveType { Kind: PrimitiveKind.Word } => "unsigned int",
        _ => null,
    };
}
