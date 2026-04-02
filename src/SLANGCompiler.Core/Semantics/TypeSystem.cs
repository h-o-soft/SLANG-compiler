namespace SLANGCompiler.Semantics;

/// <summary>
/// SLANG の型を表す。不変(immutable)。
/// 多次元配列は Dimensions リストで明示的に表現。
/// </summary>
public abstract record SlangType
{
    public abstract int ByteSize { get; }

    /// <summary>Word型 (16-bit unsigned)</summary>
    public static readonly SlangType Word = new PrimitiveType(PrimitiveKind.Word);
    /// <summary>Byte型 (8-bit unsigned)</summary>
    public static readonly SlangType Byte = new PrimitiveType(PrimitiveKind.Byte);
    /// <summary>Float型</summary>
    public static readonly SlangType Float = new PrimitiveType(PrimitiveKind.Float);
    /// <summary>Void型 (関数の戻り値なし等)</summary>
    public static readonly SlangType Void = new PrimitiveType(PrimitiveKind.Void);
}

public enum PrimitiveKind
{
    Byte,
    Word,
    Float,
    Void,
}

/// <summary>
/// プリミティブ型
/// </summary>
public record PrimitiveType(PrimitiveKind Kind) : SlangType
{
    public override int ByteSize => Kind switch
    {
        PrimitiveKind.Byte => 1,
        PrimitiveKind.Word => 2,
        PrimitiveKind.Float => 3,   // 24ビット浮動小数点(元実装: f24)
        PrimitiveKind.Void => 0,
        _ => 0,
    };

    public override string ToString() => Kind.ToString().ToLower();
}

/// <summary>
/// ポインタ型（間接参照）
/// </summary>
public record PointerType(SlangType ElementType) : SlangType
{
    public override int ByteSize => 2;  // Z80: 16-bit address
    public override string ToString() => $"ptr<{ElementType}>";
}

/// <summary>
/// 配列型。多次元は Dimensions リストで表現。
/// 例: ARRAY BYTE a[3][4] → ArrayType(Byte, [3, 4])
/// </summary>
public record ArrayType(SlangType ElementType, List<int> Dimensions) : SlangType
{
    public int Rank => Dimensions.Count;

    public int TotalElements
    {
        get
        {
            int total = 1;
            foreach (var dim in Dimensions) total *= dim;
            return total;
        }
    }

    public override int ByteSize => TotalElements * ElementType.ByteSize;

    /// <summary>
    /// 指定次元のストライドを計算。
    /// 例: [3][4] のBYTE配列で dimension=0 → stride=4, dimension=1 → stride=1
    /// </summary>
    public int GetStride(int dimension)
    {
        int stride = ElementType.ByteSize;
        for (int i = Dimensions.Count - 1; i > dimension; i--)
        {
            stride *= Dimensions[i];
        }
        return stride;
    }

    public override string ToString()
    {
        var dims = string.Join(",", Dimensions);
        return $"array<{ElementType}>[{dims}]";
    }
}

/// <summary>
/// 関数型
/// </summary>
public record FunctionType(SlangType ReturnType, List<SlangType> ParameterTypes) : SlangType
{
    public override int ByteSize => 2;  // 関数ポインタのサイズ
    public override string ToString()
    {
        var parms = string.Join(", ", ParameterTypes);
        return $"func({parms}) -> {ReturnType}";
    }
}

/// <summary>
/// ポート配列型（I/Oポートアクセス用）
/// </summary>
public record PortArrayType(SlangType ElementType) : SlangType
{
    public override int ByteSize => 0;  // メモリ上のサイズは不定
    public override string ToString() => $"port<{ElementType}>";
}

/// <summary>
/// メモリ配列型（絶対アドレスアクセス用: MEM[], MEMW[]）
/// </summary>
public record MemoryArrayType(SlangType ElementType) : SlangType
{
    public override int ByteSize => 0;
    public override string ToString() => $"mem<{ElementType}>";
}
