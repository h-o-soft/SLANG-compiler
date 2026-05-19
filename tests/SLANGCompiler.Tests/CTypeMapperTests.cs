using SLANGCompiler.CodeGen.C;
using SLANGCompiler.Semantics;
using Xunit;

namespace SLANGCompiler.Tests;

/// <summary>
/// <see cref="CTypeMapper"/> の単体テスト。
/// SLANG 型 → oscar64 C 型へのマッピングを各 variant について確認。
/// </summary>
public class CTypeMapperTests
{
    [Fact]
    public void MapDeclType_Primitives()
    {
        Assert.Equal("unsigned char", CTypeMapper.MapDeclType(SlangType.Byte));
        Assert.Equal("unsigned int", CTypeMapper.MapDeclType(SlangType.Word));
        Assert.Equal("float", CTypeMapper.MapDeclType(SlangType.Float));
        Assert.Equal("void", CTypeMapper.MapDeclType(SlangType.Void));
    }

    [Fact]
    public void MapDeclType_Pointer()
    {
        Assert.Equal("unsigned char *", CTypeMapper.MapDeclType(new PointerType(SlangType.Byte)));
        Assert.Equal("unsigned int *", CTypeMapper.MapDeclType(new PointerType(SlangType.Word)));
        // ポインタのポインタ
        Assert.Equal("unsigned char * *",
            CTypeMapper.MapDeclType(new PointerType(new PointerType(SlangType.Byte))));
    }

    [Fact]
    public void MapDeclType_Array_ReturnsElementType()
    {
        var arr = new ArrayType(SlangType.Byte, new List<int> { 10 });
        Assert.Equal("unsigned char", CTypeMapper.MapDeclType(arr));
    }

    [Fact]
    public void ArrayDimsSuffix_SingleDimension()
    {
        var arr = new ArrayType(SlangType.Byte, new List<int> { 10 });
        Assert.Equal("[10]", CTypeMapper.ArrayDimsSuffix(arr));
    }

    [Fact]
    public void ArrayDimsSuffix_MultiDimension()
    {
        var arr = new ArrayType(SlangType.Word, new List<int> { 3, 4 });
        Assert.Equal("[3][4]", CTypeMapper.ArrayDimsSuffix(arr));
    }

    [Fact]
    public void ArrayDimsSuffix_NonArray_ReturnsEmpty()
    {
        Assert.Equal("", CTypeMapper.ArrayDimsSuffix(SlangType.Byte));
    }

    [Fact]
    public void ZeroInitializer_Primitives()
    {
        Assert.Equal("0", CTypeMapper.ZeroInitializer(SlangType.Byte));
        Assert.Equal("0", CTypeMapper.ZeroInitializer(SlangType.Word));
        // oscar64 は `0.0f` を受け付けないため `0.0` を出す
        Assert.Equal("0.0", CTypeMapper.ZeroInitializer(SlangType.Float));
        Assert.Equal("0", CTypeMapper.ZeroInitializer(new PointerType(SlangType.Byte)));
        Assert.Equal("{0}", CTypeMapper.ZeroInitializer(
            new ArrayType(SlangType.Byte, new List<int> { 10 })));
    }

    [Fact]
    public void WrapCastFor_Primitives()
    {
        Assert.Equal("unsigned char", CTypeMapper.WrapCastFor(SlangType.Byte));
        Assert.Equal("unsigned int", CTypeMapper.WrapCastFor(SlangType.Word));
        Assert.Null(CTypeMapper.WrapCastFor(SlangType.Float));
        Assert.Null(CTypeMapper.WrapCastFor(SlangType.Void));
    }
}
