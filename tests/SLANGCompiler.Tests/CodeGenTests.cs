using Xunit;
using SLANGCompiler.Lexer;
using SLANGCompiler.Parser;
using SLANGCompiler.IR;
using SLANGCompiler.CodeGen;
using SLANGCompiler.Semantics;

namespace SLANGCompiler.Tests;

public class CodeGenTests
{
    private string Compile(string source)
    {
        var diag = new DiagnosticBag();
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var preprocessor = new Preprocessor(diag);
        tokens = preprocessor.Process(tokens, ".");
        var parser = new Parser.Parser(tokens, diag);
        var ast = parser.ParseCompilationUnit();
        Assert.False(diag.HasErrors, $"Parse errors: {string.Join("; ", diag.Diagnostics)}");

        var analyzer = new SemanticAnalyzer(diag);
        analyzer.Analyze(ast);

        var irGen = new IrGenerator(diag, analyzer.Symbols);
        var irModule = irGen.Generate(ast);
        Assert.False(diag.HasErrors, $"IR errors: {string.Join("; ", diag.Diagnostics)}");

        var codeGen = new CodeGenerator(irModule);
        return codeGen.Generate();
    }

    [Fact]
    public void SimpleAddition_DirectLoad()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X+Y; END;");
        // 直接ロード最適化: PUSH/POP なし
        Assert.Contains("LD\tHL,(_V_X)", asm);
        Assert.Contains("LD\tDE,(_V_Y)", asm);
        Assert.Contains("ADD\tHL,DE", asm);
        Assert.DoesNotContain("PUSH\tHL", asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0]);
    }

    [Fact]
    public void ConstantFolding()
    {
        var asm = Compile("CONST MAX=100; MAIN() BEGIN VAR X; X=MAX; END;");
        // CONST値が即値になる
        Assert.Contains("$0064", asm); // 100 = $64
    }

    [Fact]
    public void LocalVariable_IYAccess()
    {
        var asm = Compile("MAIN() BEGIN VAR LOCAL; LOCAL=42; END;");
        // ローカル変数はIYオフセットアクセス
        Assert.Contains("(IY+", asm);
    }

    [Fact]
    public void GlobalVariable_WorkArea()
    {
        var asm = Compile("VAR GLOBAL; MAIN() BEGIN GLOBAL=1; END;");
        // グローバル変数は__WORK__内にEQU配置
        Assert.Contains("_V_GLOBAL EQU (__WORK__", asm);
    }

    [Fact]
    public void GlobalInit()
    {
        var asm = Compile("VAR X=42; MAIN() BEGIN END;");
        // エントリポイントでグローバル初期化
        var entry = asm.Split("MAIN:")[0];
        Assert.Contains("$002A", entry); // 42
        Assert.Contains("(_V_X)", entry);
    }

    [Fact]
    public void StringTable()
    {
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("_S0:", asm);
        Assert.Contains("DB\t\"Hello\",0", asm);
    }

    [Fact]
    public void FunctionCall_IYArgs()
    {
        var asm = Compile("MAIN() BEGIN ADD(1,2); END; ADD(A,B) BEGIN END;");
        // ユーザー関数呼び出し: IY+$70に引数
        Assert.Contains("(IY+$70)", asm);
        Assert.Contains("(IY+$72)", asm);
        Assert.Contains("CALL\tADD", asm);
    }

    [Fact]
    public void ArrayInit_DBOutput()
    {
        var asm = Compile("ARRAY BYTE D[2]={10,20,30}; MAIN() BEGIN END;");
        Assert.Contains("DB\t$0A,$14,$1E", asm);
    }

    [Fact]
    public void ArrayInit_PercentCast()
    {
        // %はWORD型指定: BYTE配列でも%5は2バイト($05,$00)で出力される
        var asm = Compile("ARRAY ARI[4]={1,%5,7}; MAIN() BEGIN END;");
        Assert.Contains("$01,$00,$05,$00,$07,$00", asm);
    }

    [Fact]
    public void CodeConst_NotInWorkArea()
    {
        // CODEブロック定数はWORK内ではなくコード領域に配置
        var asm = Compile("CONST D=[1,2,3]; MAIN() BEGIN END;");
        Assert.Contains("_V_D:", asm);              // コード領域にラベル
        Assert.Contains("DB\t$01,$02,$03", asm);    // DB出力
        Assert.DoesNotContain("_V_D EQU (__WORK__", asm); // WORKには入らない
    }

    [Fact]
    public void ByteVar_DirectLoadZeroExtend()
    {
        // BYTE変数の直接ロード（direct path含む）でゼロ拡張されること
        var asm = Compile("VAR BYTE X,BYTE Y,Z; MAIN() BEGIN Z=X+Y; END;");
        // direct path: LoadToDE側もD=$00でゼロ拡張
        Assert.Contains("D,$00", asm);
    }

    [Fact]
    public void EntryPoint()
    {
        var asm = Compile("MAIN() BEGIN END;");
        Assert.Contains("LD\tIY,__IYWORK", asm);
        Assert.Contains("CALL\tMAIN", asm);
        Assert.Contains("__IYWORK EQU (__WORK__", asm);
        Assert.Contains("SLANG_PROG_END:", asm);
        Assert.Contains("__WORK__:", asm);
        Assert.Contains("__WORKEND__", asm);
    }

    [Fact]
    public void OrgDirective()
    {
        var asm = Compile("ORG $8000; MAIN() BEGIN END;");
        Assert.Contains("ORG\t$8000", asm);
    }

    [Fact]
    public void PrintString_CallsPMSX()
    {
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("CALL\tPMSX", asm);
        Assert.DoesNotContain("CALL\tPSTR", asm);
    }

    [Fact]
    public void PrintNumber_CallsP10()
    {
        var asm = Compile("VAR X; MAIN() BEGIN PRINT(X); END;");
        Assert.Contains("CALL\tP10", asm);
    }

    [Fact]
    public void LocalArray_IYFrameAdjustment()
    {
        var asm = Compile("MAIN() BEGIN ARRAY A[5]; A[0]=1; END;");
        var mainBody = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("ADD\tIY,BC", mainBody);
    }

    [Fact]
    public void Arithmetic_CallsMULHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X*Y; END;");
        Assert.Contains("CALL\tMULHLDE", asm);
    }

    [Fact]
    public void Arithmetic_CallsDIVHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X/Y; END;");
        Assert.Contains("CALL\tDIVHLDE", asm);
    }

    [Fact]
    public void Shift_CallsLSHIFTHLDE()
    {
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=X<<Y; END;");
        Assert.Contains("CALL\tLSHIFTHLDE", asm);
    }

    [Fact]
    public void SignedCompare_InlineFused()
    {
        // IF文での符号付き比較はインライン展開（CALL OPS*不要）
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN IF X.<.Y THEN Z=1; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("XOR\tD", body);    // 符号ビット比較
        Assert.Contains("BIT\t7,H", body);  // 符号判定
        Assert.DoesNotContain("CALL\tOPSLTHLDE", body);
    }

    [Fact]
    public void SignedCompare_NonFused_CallsRuntime()
    {
        // IF文以外（代入等）ではCALL OPS*が使われる
        var asm = Compile("VAR X,Y,Z; MAIN() BEGIN Z=(X.<.Y); END;");
        Assert.Contains("CALL\tOPSLTHLDE", asm);
    }

    [Fact]
    public void UserVar_NoCollisionWithSystemRegs()
    {
        // VAR A はシステム変数 _A (=_AF+1) と衝突しない
        var asm = Compile("VAR A; MAIN() BEGIN A=1; END;");
        // ユーザー変数は__プレフィックス
        Assert.Contains("_V_A EQU (__WORK__", asm);
        // システム変数は_プレフィックス
        Assert.Contains("_A EQU (_AF + 1)", asm);
        // 両方存在して衝突しない
        Assert.Contains("_AF EQU (__WORK__", asm);
    }

    [Fact]
    public void SystemVar_CorrectLabel()
    {
        // ^AF, ^CARRY等のシステム変数は_プレフィックスでアクセスされること
        var asm = Compile("MAIN() BEGIN ^AF=1; ^CARRY=0; END;");
        Assert.Contains("(_AF)", asm);
        Assert.Contains("(_CARRY)", asm);
        // __プレフィックスが付かないこと
        Assert.DoesNotContain("(__AF)", asm);
        Assert.DoesNotContain("(__CARRY)", asm);
    }

    [Fact]
    public void StaticVar_InWorkArea()
    {
        var asm = Compile("FUNC(A) VAR S; BEGIN VAR L; S=1; L=2; END; MAIN() BEGIN FUNC(0); END;");
        Assert.Contains("_V_FUNC_S EQU (__WORK__", asm);
        Assert.Contains("(IY+", asm);  // ローカルLはIYアクセス
    }

    [Fact]
    public void StaticVar_FunctionScoped()
    {
        var asm = Compile("F1() VAR S; BEGIN S=1; END; F2() VAR S; BEGIN S=2; END; MAIN() BEGIN F1(); F2(); END;");
        Assert.Contains("_V_F1_S EQU (__WORK__", asm);
        Assert.Contains("_V_F2_S EQU (__WORK__", asm);
    }

    [Fact]
    public void StaticVar_InitAtStartup_NotInFunction()
    {
        var asm = Compile("FUNC() VAR S=42; BEGIN END; MAIN() BEGIN FUNC(); END;");
        var entry = asm.Split("MAIN:")[0];
        Assert.Contains("_V_FUNC_S", entry);
        Assert.Contains("$002A", entry);

        var funcBody = asm.Split("FUNC:")[1].Split("_FUNC_EXIT")[0];
        Assert.DoesNotContain("_V_FUNC_S", funcBody);
    }

    [Fact]
    public void StaticArray_InWorkArea()
    {
        var asm = Compile("FUNC() ARRAY A[5]; BEGIN A[0]=1; END; MAIN() BEGIN FUNC(); END;");
        Assert.Contains("_V_FUNC_A EQU (__WORK__", asm);
    }

    [Fact]
    public void PrintNewline_CallsPCRONE()
    {
        var asm = Compile("MAIN() BEGIN PRINT(/); END;");
        Assert.Contains("CALL\tPCRONE", asm);
    }

    [Fact]
    public void ConstantIf_TrueEliminated()
    {
        // 定数TRUE条件: 条件チェックコードが生成されないこと
        var asm = Compile("CONST C=1; MAIN() BEGIN IF C==1 THEN PRINT(\"Y\",/); END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("CALL\tPMSX", body);
        Assert.DoesNotContain("JP\tZ,", body);
    }

    [Fact]
    public void ConstantIf_FalseEliminated()
    {
        // 定数FALSE条件: thenブランチが省略されELSEのみ残ること
        var asm = Compile("CONST C=1; MAIN() BEGIN IF C==999 THEN PRINT(\"N\",/) ELSE PRINT(\"E\",/); END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // "E"のPMSXはあるが"N"のPMSXに対応する条件分岐はない
        Assert.DoesNotContain("JP\tZ,", body);
    }

    [Fact]
    public void MachineCodeDef_NoFuncWrapper()
    {
        // MACHINE+CODEブロック関数: PUSH IY/RET等の関数ラッパーなし、DB/DWデータ+RET出力
        var asm = Compile("MACHINE MF(1); MF(1)[CODE($CD,%$1234,$EB);] MAIN() BEGIN MF(1); END;");
        // CODEブロックラベル配下にDBデータが出力される
        Assert.Contains("_MF:", asm);
        Assert.Contains("$CD", asm);
        Assert.Contains("$34,$12", asm); // %$1234 → little-endian 2バイト
        Assert.Contains("$EB", asm);
        Assert.Contains("$C9", asm); // 旧コンパイラ互換の自動RET
        // 関数ラッパーが出力されないこと（PUSH IYなし、_EXIT無し）
        Assert.DoesNotContain("_MF_EXIT", asm);
        // CALL先が正しいラベル
        Assert.Contains("CALL\t_MF", asm);
    }

    [Fact]
    public void MachineCodeDef_StaticDeclScoped()
    {
        // MACHINE CODE定義の静的宣言が関数スコープでラベル付けされること
        var asm = Compile("MACHINE MF(1); MF(1) ARRAY BUF[5]; [CODE($CD,%BUF,$C9);] MAIN() BEGIN MF(0); END;");
        // BUFが関数名プレフィックス付き
        Assert.Contains("_V_MF_BUF", asm);
        Assert.DoesNotContain("_V_BUF EQU", asm);
    }

    [Fact]
    public void LocalArray_AddressLoad()
    {
        // ローカル配列をMACHINE関数に渡すとアドレスがロードされること
        var asm = Compile("MACHINE MF(1); MAIN() BEGIN ARRAY A[5]; MF(A); END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // PUSH IY; POP HL; LD DE,offset; ADD HL,DE でアドレス計算
        Assert.Contains("PUSH\tIY", body);
        Assert.Contains("POP\tHL", body);
        Assert.Contains("ADD\tHL,DE", body);
    }
}
