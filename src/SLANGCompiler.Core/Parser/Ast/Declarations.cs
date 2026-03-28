using SLANGCompiler.Lexer;

namespace SLANGCompiler.Parser.Ast;

/// <summary>
/// コンパイル単位（ファイル全体）
/// </summary>
public class CompilationUnit : AstNode
{
    public List<AstNode> Definitions { get; }

    public CompilationUnit(List<AstNode> definitions, SourceSpan span) : base(span)
    {
        Definitions = definitions;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCompilationUnit(this);
}

// -- Directives --

public class OrgDirective : AstNode
{
    public Expression Value { get; }
    public OrgDirective(Expression value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitOrgDirective(this);
}

public class WorkDirective : AstNode
{
    public Expression Value { get; }
    public WorkDirective(Expression value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitWorkDirective(this);
}

public class OffsetDirective : AstNode
{
    public Expression Value { get; }
    public OffsetDirective(Expression value, SourceSpan span) : base(span) { Value = value; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitOffsetDirective(this);
}

public class ModuleBlock : AstNode
{
    public Expression Name { get; }
    public List<AstNode> Definitions { get; }
    public ModuleBlock(Expression name, List<AstNode> definitions, SourceSpan span) : base(span)
    {
        Name = name;
        Definitions = definitions;
    }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitModuleBlock(this);
}

public class PlainAsm : AstNode
{
    public string AsmText { get; }
    public PlainAsm(string asmText, SourceSpan span) : base(span) { AsmText = asmText; }
    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitPlainAsm(this);
}

// -- Variable declarations --

/// <summary>
/// データサイズ指定
/// </summary>
public enum DataSize
{
    Word,   // default (16-bit)
    Byte,   // 8-bit
    Float,  // floating point (multi-byte)
}

/// <summary>
/// VAR 変数宣言
/// </summary>
public class VarDecl : AstNode
{
    public string Name { get; }
    public DataSize Size { get; }
    public Expression? Address { get; }         // :address 指定
    public Expression? InitialValue { get; }    // = expr
    public List<Expression>? InitialCode { get; } // = { code... }

    public VarDecl(string name, DataSize size, Expression? address,
                   Expression? initialValue, List<Expression>? initialCode,
                   SourceSpan span) : base(span)
    {
        Name = name;
        Size = size;
        Address = address;
        InitialValue = initialValue;
        InitialCode = initialCode;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitVarDecl(this);
}

/// <summary>
/// ARRAY 配列宣言。多次元をDimensionsリストで明示的に表現。
/// </summary>
public class ArrayDecl : AstNode
{
    public string Name { get; }
    public DataSize Size { get; }
    public Expression? Address { get; }
    public List<Expression?> Dimensions { get; }  // 各次元のサイズ (nullはサイズ未指定=間接参照)
    public Expression? InitialValue { get; }
    public List<Expression>? InitialCode { get; }

    public ArrayDecl(string name, DataSize size, Expression? address,
                     List<Expression?> dimensions,
                     Expression? initialValue, List<Expression>? initialCode,
                     SourceSpan span) : base(span)
    {
        Name = name;
        Size = size;
        Address = address;
        Dimensions = dimensions;
        InitialValue = initialValue;
        InitialCode = initialCode;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitArrayDecl(this);
}

/// <summary>
/// CONST 定数宣言
/// </summary>
public class ConstDecl : AstNode
{
    public string Name { get; }
    public Expression Value { get; }
    public bool IsAsmEqu { get; }  // ASM CONST → アセンブラのEQUとして定義

    public ConstDecl(string name, Expression value, bool isAsmEqu, SourceSpan span) : base(span)
    {
        Name = name;
        Value = value;
        IsAsmEqu = isAsmEqu;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitConstDecl(this);
}

/// <summary>
/// MACHINE 宣言（外部マシン語関数）
/// </summary>
public class MachineDecl : AstNode
{
    public string Name { get; }
    public Expression? Address { get; }
    public int? ParamCount { get; }

    public MachineDecl(string name, Expression? address, int? paramCount, SourceSpan span) : base(span)
    {
        Name = name;
        Address = address;
        ParamCount = paramCount;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitMachineDecl(this);
}

/// <summary>
/// 関数定義
/// </summary>
public class FuncDef : AstNode
{
    public string Name { get; }
    public Expression? Address { get; }
    public List<ParamDecl> Parameters { get; }
    public List<AstNode> StaticDeclarations { get; }  // BEGINの前
    public List<AstNode> LocalDeclarations { get; }   // BEGINの後
    public Block Body { get; }
    public Expression? ReturnValue { get; }  // END(expr)

    public FuncDef(string name, Expression? address, List<ParamDecl> parameters,
                   List<AstNode> staticDecls, List<AstNode> localDecls,
                   Block body, Expression? returnValue, SourceSpan span) : base(span)
    {
        Name = name;
        Address = address;
        Parameters = parameters;
        StaticDeclarations = staticDecls;
        LocalDeclarations = localDecls;
        Body = body;
        ReturnValue = returnValue;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitFuncDef(this);
}

/// <summary>
/// 関数パラメータ宣言
/// </summary>
public class ParamDecl : AstNode
{
    public string Name { get; }
    public DataSize Size { get; }
    public bool IsArray { get; }

    public ParamDecl(string name, DataSize size, bool isArray, SourceSpan span) : base(span)
    {
        Name = name;
        Size = size;
        IsArray = isArray;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitParamDecl(this);
}
