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
        // CONST値が即値になる（StoreLocal即値最適化で$64/$00に分離される場合あり）
        Assert.True(asm.Contains("$0064") || asm.Contains("$64"), "100 ($64) should appear in output");
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
    public void StringInlinePrint()
    {
        // PRINT文字列はMPRNT方式（インライン）で出力される
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("CALL\tMPRNT", asm);
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
        // 配列初期値はデフォルトBYTE、%指定でWORD(2バイト)
        var asm = Compile("ARRAY ARI[4]={1,%5,7}; MAIN() BEGIN END;");
        // 1→DB $01, %5→DW(DB $05,$00), 7→DB $07
        Assert.Contains("$01", asm);
        Assert.Contains("$05,$00", asm);
        Assert.Contains("$07", asm);
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
    public void PrintString_CallsMPRNT()
    {
        // PRINT文字列はMPRNT方式（インライン文字列）で出力される
        var asm = Compile("MAIN() BEGIN PRINT(\"Hello\"); END;");
        Assert.Contains("CALL\tMPRNT", asm);
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
        Assert.Contains("CALL\tMPRNT", body);
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

    [Fact]
    public void LocalArray_ConstIndex_Word_DirectIY()
    {
        // WORD local array の定数添字 → (IY+d) 直接アクセス
        var asm = Compile("MAIN() BEGIN ARRAY A[5]; A[2]=99; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // 定数添字: StoreLocalで(IY+offset)直接ストア
        Assert.Contains("(IY+", body);
        // 配列アクセスでPUSH IY; POP HL; ADD HL,DEは不要（プロローグのPUSH IYは許容）
        Assert.DoesNotContain("POP\tHL", body);
    }

    [Fact]
    public void LocalArray_ConstIndex_Byte_DirectIY()
    {
        // BYTE local array の定数添字 → (IY+d) 直接アクセス
        var asm = Compile("MAIN() BEGIN ARRAY BYTE B[10]; B[3]=42; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("(IY+", body);
        Assert.DoesNotContain("POP\tHL", body);
    }

    [Fact]
    public void LocalArray_ConstIndex_MultiDim_DirectIY()
    {
        // 多次元 local array の定数添字 → (IY+d) 直接アクセス
        var asm = Compile("MAIN() BEGIN ARRAY A[3][5]; A[1][2]=7; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("(IY+", body);
        Assert.DoesNotContain("POP\tHL", body);
    }

    [Fact]
    public void GlobalArray_ConstIndex_Word_LabelOffset()
    {
        // グローバルWORD配列の定数添字 → label+offset 直接アクセス
        var asm = Compile("ARRAY AR[5]; MAIN() BEGIN AR[2]=99; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // AR[2] → offset=2*2=4 → LD (_V_AR+4),HL
        Assert.Contains("(_V_AR+4)", body);
        Assert.DoesNotContain("ADD\tHL,DE", body); // アドレス計算不要
    }

    [Fact]
    public void GlobalArray_ConstIndex_Byte_LabelOffset()
    {
        // グローバルBYTE配列の定数添字 → label+offset 直接アクセス
        var asm = Compile("ARRAY BYTE AB[10]; MAIN() BEGIN AB[3]=42; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        Assert.Contains("(_V_AB+3)", body);
        Assert.DoesNotContain("ADD\tHL,DE", body);
    }

    [Fact]
    public void GlobalArray_ConstIndex_MultiDim_LabelOffset()
    {
        // グローバル多次元配列の定数添字 → label+offset 直接アクセス
        var asm = Compile("ARRAY AR2[3][5]; MAIN() BEGIN AR2[1][2]=7; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // AR2[1][2]: stride[0]=6*2=12, stride[1]=2 → offset=1*12+2*2=16
        Assert.Contains("(_V_AR2+16)", body);
        Assert.DoesNotContain("ADD\tHL,DE", body);
    }

    [Fact]
    public void StaticArray_ConstIndex_LabelOffset()
    {
        // static配列（BEGIN前宣言=グローバル）の定数添字 → label+offset 直接アクセス
        var asm = Compile("ARRAY SA[5]; MAIN() BEGIN SA[2]=10; END;");
        Assert.Contains("(_V_SA+4)", asm); // offset=2*2=4
    }

    [Fact]
    public void StoreLocal_ConstValue_DirectImmediate()
    {
        // ローカル変数への定数代入 → LD (IY+d),imm 直接ストア
        var asm = Compile("MAIN() BEGIN VAR X; X=42; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // LD (IY+xx),$2A が出力される（LD HL,$002A 経由でない）
        Assert.Contains("(IY+", body);
        Assert.Contains("$2A", body);
        Assert.DoesNotContain("LD\tHL,$002A", body);
    }

    [Fact]
    public void ForByte_StaticVar_ByteAccess()
    {
        // FOR文のBYTE静的変数がBYTEアクセスされること（WORDで隣接変数を破壊しない）
        var asm = Compile("F() VAR BYTE I; VAR BYTE J; { FOR I=0 TO 10 { J=I; } } MAIN() BEGIN F(); END;");
        var body = asm.Split("F:")[1].Split("_F_EXIT")[0];
        // BYTE: LD A,(addr) / LD (addr),A パターン
        Assert.Contains("LD\tA,(_V_F_I)", body);
        Assert.Contains("LD\t(_V_F_I),A", body);
        // WORD: LD HL,(addr) が出ないこと
        Assert.DoesNotContain("LD\tHL,(_V_F_I)", body);
    }

    [Fact]
    public void ArrayDecl_PointerType_LoadAddr()
    {
        // ARRAY X[]:$addr はLoadAddr（アドレス直接）、VAR X[] はLoadVar（値読み）
        var asm = Compile("ARRAY BYTE BUF[]:$8000; MAIN() BEGIN MEMSET(BUF, 0, 10); END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // LD HL,_V_BUF（括弧なし=アドレス直接）
        Assert.Contains("HL,_V_BUF", body);
        // LD HL,(_V_BUF)（括弧付き=値読み）が出ないこと
        Assert.DoesNotContain("HL,(_V_BUF)", body);
    }

    [Fact]
    public void Case_ExprValue_NotDestroyed()
    {
        // CASE文で式の値がSBC連鎖で破壊されないこと
        var asm = Compile(@"
            MAIN() BEGIN
                VAR X; X=2;
                CASE X { 0: X=10; 1: X=20; 2: X=30; }
            END;");
        // CASE 2にマッチしてX=30になるべき。
        // SBC連鎖でHLが壊れると2番目以降のcaseにマッチしない。
        // 各caseで式を再評価していることを確認（複数回のLoadLocal/LoadVar）
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // 少なくとも3回X値をロードする（case 0, 1, 2の比較）
        var loadCount = System.Text.RegularExpressions.Regex.Matches(body, @"LD\tL,\(IY\+").Count;
        Assert.True(loadCount >= 3, $"Expected >= 3 loads of X, got {loadCount}");
    }

    [Fact]
    public void AddressOf_Array_ReturnsAddress()
    {
        // &array[idx] がアドレスを返す（値ではない）
        var asm = Compile("ARRAY AR[10]; VAR P[]; MAIN() BEGIN P = &AR[3]; END;");
        var body = asm.Split("MAIN:")[1].Split("_MAIN_EXIT")[0];
        // &AR[3] → LoadAddr _V_AR+6（値読みのLD HL,(_V_AR+6)ではない）
        Assert.Contains("_V_AR+6", body);
        Assert.DoesNotContain("(_V_AR+6)", body);
    }

    [Fact]
    public void LocalBytePointerArray_ByteElemSize()
    {
        // ローカルBYTEポインタ配列のインデックスがBYTE(×1)で計算されること
        // VAR BYTE sptr[] → sptr[1] = offset 1 (WORDなら2になるバグ)
        var asm = Compile("F() { VAR BYTE P[]; P = 0; P[1] = 42; } MAIN() BEGIN F(); END;");
        var body = asm.Split("F:")[1].Split("_F_EXIT")[0];
        // BYTE: INC HL (offset+1) が使われる。ADD HL,HL (×2) は使われない
        Assert.DoesNotContain("ADD\tHL,HL", body);
    }

    [Fact]
    public void BytePointerVar_Static_StoreAsWord()
    {
        // static VAR BYTE P[] のポインタ変数自体はWORD(2byte)でストア＆WORK確保されること
        var asm = Compile("F() VAR BYTE P[]; VAR Q; { P = $9000; P[0] = 1; Q = 0; } MAIN() BEGIN F(); END;");
        // WORDストア
        Assert.Contains("(_V_F_P),HL", asm);
        Assert.DoesNotContain("(_V_F_P),A", asm);
        // WORK確保: Pが2byte、Qが2byte。P+2のアドレスにQが来る
        // _V_F_P と _V_F_Q のアドレス差が2以上であること
        var pMatch = System.Text.RegularExpressions.Regex.Match(asm, @"_V_F_P EQU \(__WORK__ \+ (\d+)\)");
        var qMatch = System.Text.RegularExpressions.Regex.Match(asm, @"_V_F_Q EQU \(__WORK__ \+ (\d+)\)");
        Assert.True(pMatch.Success && qMatch.Success, "P and Q EQU definitions not found");
        int pOff = int.Parse(pMatch.Groups[1].Value);
        int qOff = int.Parse(qMatch.Groups[1].Value);
        int gap = System.Math.Abs(qOff - pOff);
        Assert.True(gap >= 2, $"BYTE pointer P should occupy 2 bytes in WORK, but gap to Q is {gap}");
    }

    [Fact]
    public void BytePointerVar_Local_StoreAsWord()
    {
        // ローカル(BEGIN内) VAR BYTE P[] のポインタ変数自体はWORD(2byte)でアクセスされること
        var asm = Compile("F() { VAR BYTE P[]; P = $9000; P[0] = 1; } MAIN() BEGIN F(); END;");
        var body = asm.Split("F:")[1].Split("_F_EXIT")[0];
        // P = $9000: 2バイト分のIYストア（即値最適化 or L/H経由）
        // (IY+$6E) と (IY+$6F) の2箇所にストア
        var iyStoreCount = System.Text.RegularExpressions.Regex.Matches(body, @"LD\t\(IY\+\$6[EF]\)").Count;
        Assert.True(iyStoreCount >= 2, $"Expected >= 2 IY stores for WORD pointer, got {iyStoreCount}");
    }

    [Fact]
    public void AddressOf_StaticPointerArray_UsesValue()
    {
        // &pcur[8] でpcurがstatic VAR pcur[]のとき、pcurの値(ポインタ)+16のアドレスを返す
        // pcur変数のWORKアドレス+16ではない
        var asm = Compile("F() VAR P[]; { P = $C000; VAR Q; Q = &P[8]; } MAIN() BEGIN F(); END;");
        // P値をLoadVar(括弧付き)で読むこと
        Assert.Contains("(_V_F_P)", asm);
    }

    [Fact]
    public void StaticByteArray_ByteStore()
    {
        // static ARRAY BYTE のストアが1バイトであること（WORDストアで隣接データ破壊しない）
        var asm = Compile("F() ARRAY BYTE B[3]; { B[0] = 42; B[1] = 99; } MAIN() BEGIN F(); END;");
        // BYTE配列へのストア: LD (HL),E のみ (INC HL; LD (HL),D なし)
        var body = asm.Split("F:")[1].Split("_F_EXIT")[0];
        // BYTE store回数（LD (HL),E without INC HL; LD (HL),D）
        Assert.DoesNotContain("INC\tHL\n\tLD\t(HL),D", body);
    }
}
