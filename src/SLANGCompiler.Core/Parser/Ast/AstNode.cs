using SLANGCompiler.Lexer;

namespace SLANGCompiler.Parser.Ast;

/// <summary>
/// 全ASTノードの基底クラス
/// </summary>
public abstract class AstNode
{
    public SourceSpan Span { get; set; }

    protected AstNode(SourceSpan span)
    {
        Span = span;
    }

    public abstract T Accept<T>(IAstVisitor<T> visitor);
}

/// <summary>
/// ASTビジター
/// </summary>
public interface IAstVisitor<T>
{
    // -- Top-level --
    T VisitCompilationUnit(CompilationUnit node);
    T VisitOrgDirective(OrgDirective node);
    T VisitWorkDirective(WorkDirective node);
    T VisitOffsetDirective(OffsetDirective node);
    T VisitModuleBlock(ModuleBlock node);
    T VisitPlainAsm(PlainAsm node);

    // -- Declarations --
    T VisitVarDecl(VarDecl node);
    T VisitArrayDecl(ArrayDecl node);
    T VisitConstDecl(ConstDecl node);
    T VisitMachineDecl(MachineDecl node);
    T VisitFuncDef(FuncDef node);
    T VisitParamDecl(ParamDecl node);

    // -- Statements --
    T VisitBlock(Block node);
    T VisitExpressionStmt(ExpressionStmt node);
    T VisitIfStmt(IfStmt node);
    T VisitWhileStmt(WhileStmt node);
    T VisitRepeatStmt(RepeatStmt node);
    T VisitLoopStmt(LoopStmt node);
    T VisitForStmt(ForStmt node);
    T VisitCaseStmt(CaseStmt node);
    T VisitExitStmt(ExitStmt node);
    T VisitContinueStmt(ContinueStmt node);
    T VisitReturnStmt(ReturnStmt node);
    T VisitGotoStmt(GotoStmt node);
    T VisitLabelStmt(LabelStmt node);
    T VisitPrintStmt(PrintStmt node);

    // -- Expressions --
    T VisitIntegerLiteral(IntegerLiteral node);
    T VisitFloatLiteral(FloatLiteral node);
    T VisitStringLiteral(StringLiteral node);
    T VisitIdentifier(IdentifierExpr node);
    T VisitBinaryExpr(BinaryExpr node);
    T VisitUnaryExpr(UnaryExpr node);
    T VisitAssignExpr(AssignExpr node);
    T VisitCompoundAssignExpr(CompoundAssignExpr node);
    T VisitIncrementExpr(IncrementExpr node);
    T VisitCallExpr(CallExpr node);
    T VisitArrayAccessExpr(ArrayAccessExpr node);
    T VisitConditionalExpr(ConditionalExpr node);
    T VisitCommaExpr(CommaExpr node);
    T VisitAddressOfExpr(AddressOfExpr node);
    T VisitHighLowExpr(HighLowExpr node);
    T VisitCodeExpr(CodeExpr node);
    T VisitCastExpr(CastExpr node);
    T VisitStringFuncExpr(StringFuncExpr node);
}
