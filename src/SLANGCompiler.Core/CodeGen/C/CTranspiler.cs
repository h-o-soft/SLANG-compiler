using SLANGCompiler;
using SLANGCompiler.Parser.Ast;
using SLANGCompiler.Runtime;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.CodeGen.C;

/// <summary>
/// SLANG AST → C ソース (oscar64 入力) への transpile entry point。
///
/// 既存 <see cref="IR.IrGenerator"/> + <see cref="CodeGenerator"/> (Z80) と
/// 並列の経路として、<see cref="EnvironmentConfig.Backend"/> ==
/// <see cref="BackendKind.OscarC"/> の env で <see cref="CLI"/> から呼ばれる。
///
/// この経路は IR を通らず、AST 直接 → C 文字列。oscar64 invoke は slangbuild
/// 側 (<see cref="Build.OscarInvoker"/>) で行う。
/// </summary>
public class CTranspiler
{
    private readonly SymbolTable _symbols;
    private readonly EnvironmentConfig _envConfig;
    private readonly DiagnosticBag _diagnostics;

    public CTranspiler(SymbolTable symbols, EnvironmentConfig envConfig, DiagnosticBag diagnostics)
    {
        _symbols = symbols;
        _envConfig = envConfig;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// CompilationUnit (= SemanticAnalyzer 通過後の AST 全体) を C ソース文字列に変換。
    /// 失敗した場合は <see cref="DiagnosticBag.HasErrors"/> を確認すること。
    /// </summary>
    public string Transpile(CompilationUnit unit)
    {
        var scope = new CScopeTracker(_symbols);
        var emitter = new CEmitter(_symbols, scope, _diagnostics);
        return unit.Accept(emitter).Text;
    }
}
