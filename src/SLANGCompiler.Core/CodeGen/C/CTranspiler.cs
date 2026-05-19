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
        // env file `c_bindings:` を lookup layer (CBindingRegistry) に load。
        // SymbolTable には注入しない (= レビュー M3 反映、SLANG 起源シンボルと
        // env 起源 binding の責務分離)。
        var registry = new CBindingRegistry();
        if (_envConfig.CBindings != null)
        {
            foreach (var b in _envConfig.CBindings)
                registry.Add(b);
        }

        // SLANG 側 CFUNC 宣言 (= SymbolKind.CFunction) が env binding を override
        // していたら info diagnostics で警告 (= silent override 防止)。
        // SymbolKind.MachineFunction (= SemanticAnalyzer builtin 自動登録、INPUT /
        // LOCATE 等) は env binding が override する正常パターンなので info 不要。
        if (_symbols != null)
        {
            foreach (var b in registry.All)
            {
                var sym = _symbols.GlobalScope.ResolveLocal(b.Name);
                if (sym != null && sym.Kind == SymbolKind.CFunction)
                {
                    _diagnostics.Info(
                        $"env c_bindings entry `{b.Name}` is overridden by user CFUNC declaration",
                        new Lexer.SourceSpan());
                }
            }
        }

        var scope = new CScopeTracker(_symbols);
        var emitter = new CEmitter(_symbols, scope, _diagnostics, registry);
        return unit.Accept(emitter).Text;
    }
}
