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

/// <summary>
/// #MODULE のランタイム集約ポリシー (ヘッダ位置で指定)。
/// `#MODULE $8000 RESIDENT` のように書く。省略時は Local。
/// </summary>
public enum OverlayRuntimePolicy
{
    /// <summary>省略時のデフォルト。各 ASM ファイルに runtime を local 展開 (現状互換)</summary>
    Local = 0,
    /// <summary>RESIDENT 指定。@resident shared な runtime を main 集約 + overlay は EXTERN 参照</summary>
    Resident,
    /// <summary>SELFCONTAIN 指定 (将来予約、現時点は未実装エラー)。@resident shared を強制 local 化</summary>
    SelfContain,
    /// <summary>AUTO 指定 (将来予約、現時点は未実装エラー)。別途検討</summary>
    Auto,
}

public class ModuleBlock : AstNode
{
    public Expression Name { get; }
    public List<AstNode> Definitions { get; }
    public OverlayRuntimePolicy RuntimePolicy { get; }
    public ModuleBlock(Expression name, List<AstNode> definitions, SourceSpan span,
                       OverlayRuntimePolicy runtimePolicy = OverlayRuntimePolicy.Local) : base(span)
    {
        Name = name;
        Definitions = definitions;
        RuntimePolicy = runtimePolicy;
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
    public bool IsArrayKeyword { get; set; }       // ARRAYキーワードで宣言（VARではない）

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
    public CodeExpr? CodeBody { get; }
    public List<AstNode> StaticDeclarations { get; }

    public MachineDecl(string name, Expression? address, int? paramCount, SourceSpan span,
                       CodeExpr? codeBody = null, List<AstNode>? staticDecls = null) : base(span)
    {
        Name = name;
        Address = address;
        ParamCount = paramCount;
        CodeBody = codeBody;
        StaticDeclarations = staticDecls ?? new List<AstNode>();
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
    public DataSize ReturnSize { get; }  // 戻り値の型 (デフォルト Word)
    public List<ParamDecl> Parameters { get; }
    public List<AstNode> StaticDeclarations { get; }  // BEGINの前
    public List<AstNode> LocalDeclarations { get; }   // BEGINの後
    public Block Body { get; }
    public Expression? ReturnValue { get; }  // END(expr)

    public FuncDef(string name, Expression? address, List<ParamDecl> parameters,
                   List<AstNode> staticDecls, List<AstNode> localDecls,
                   Block body, Expression? returnValue, SourceSpan span,
                   DataSize returnSize = DataSize.Word) : base(span)
    {
        Name = name;
        Address = address;
        ReturnSize = returnSize;
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

// -- CFUNC declaration --

/// <summary>
/// CFUNC 引数 (型あり形式) の 1 entry。
/// 略式 (= <c>CFUNC FOO(2):foo</c>) では <see cref="CFuncDecl.Parameters"/> が
/// null、ParamCount のみが意味を持つ。
/// </summary>
public readonly record struct CFuncParam(DataSize Size, bool IsArray);

/// <summary>
/// CFUNC 宣言。SLANG 名前 → C 関数 (oscar64 build 時に link される) への
/// 直接マッピング。
///
/// 文法:
///   CFUNC NAME(PARAMS) [RET] : C_NAME;
///
/// 略式 (= 後方互換 + interop 用):
///   <c>CFUNC FOO(2):foo;</c>  → Parameters=null, ParamCount=2, ReturnSize=null
///   (= 引数 2 個全て WORD、戻り値 WORD 仮定)
///
/// 型あり (= 標準 binding 推奨):
///   <c>CFUNC SPR_SET(BYTE id, WORD x, WORD y) VOID :spr_set;</c>
///   → Parameters=[(Byte,false),(Word,false),(Word,false)], ReturnSize=null + IsVoidReturn=true
///
/// 型あり VOID 引数 + BYTE return:
///   <c>CFUNC PEEK(WORD addr) BYTE :peek;</c>
///   → Parameters=[(Word,false)], ReturnSize=Byte
/// </summary>
public class CFuncDecl : AstNode
{
    public string Name { get; }                    // SLANG 側名前
    public string CName { get; }                   // C 側 ident、case preserve
    /// <summary>略式 = ParamCount のみ意味あり (Parameters は null)。型あり = Parameters.Count と一致。</summary>
    public int ParamCount { get; }
    /// <summary>型あり時の引数列。null = 略式 (= 全 WORD)。</summary>
    public List<CFuncParam>? Parameters { get; }
    /// <summary>戻り型。null = 略式 (= WORD 仮定) または IsVoidReturn=true。</summary>
    public DataSize? ReturnSize { get; }
    public bool IsVoidReturn { get; }

    public CFuncDecl(string name, string cName, int paramCount,
                     List<CFuncParam>? parameters,
                     DataSize? returnSize, bool isVoidReturn,
                     SourceSpan span) : base(span)
    {
        Name = name;
        CName = cName;
        ParamCount = paramCount;
        Parameters = parameters;
        ReturnSize = returnSize;
        IsVoidReturn = isVoidReturn;
    }

    public override T Accept<T>(IAstVisitor<T> visitor) => visitor.VisitCFuncDecl(this);
}
