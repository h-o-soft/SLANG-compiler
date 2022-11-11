
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// 式の演算子の種類
    /// </summary>
    public enum Opcode
    {
        /// <summary>Indirect(間接)</summary>
        Indir,
        /// <summary>Address。識別子が属する。</summary>
        Adr,
        /// <summary>I/Oポートアクセス演算用</summary>
        PortAccess,
        /// <summary>PRINT文内で使われる文字列関数</summary>
        StrFunc,
        /// <summary>Bool値を数値に変換</summary>
        DeBool,
        /// <summary>Bool値</summary>
        Bool,
        /// <summary>論理AND</summary>
        Land,
        /// <summary>論理OR</summary>
        Lor,
        /// <summary>(配列用の)アドレス加算</summary>
        ScaleAdd,
        /// <summary>(配列用の)アドレス減算</summary>
        ScaleSub,
        /// <summary>代入</summary>
        Assign,
        /// <summary>演算つき代入</summary>
        AssignOp,
        /// <summary>定数</summary>
        Const,
        /// <summary>文字列</summary>
        Str,
        /// <summary>ラベル</summary>
        Label,
        /// <summary>三項演算子</summary>
        Cond,
        /// <summary>前置+</summary>
        Plus,
        /// <summary>前置-</summary>
        Minus,
        /// <summary>多分未使用</summary>
        Bnot,
        /// <summary>論理否定</summary>
        Not,
        /// <summary>ビット反転</summary>
        Cpl,
        /// <summary>上位8ビットを値とする</summary>
        High,
        /// <summary>下位8ビットを値とする</summary>
        Low,
        /// <summary>前置インクリメント</summary>
        PreInc,
        /// <summary>前置デクリメント</summary>
        PreDec,
        /// <summary>後置インクリメント</summary>
        PostInc,
        /// <summary>後置デクリメント</summary>
        PostDec,
        /// <summary>加算</summary>
        Add,
        /// <summary>減算</summary>
        Sub,
        /// <summary>符号なし剰余</summary>
        Mod,
        /// <summary>符号なし乗算</summary>
        Mul,
        /// <summary>符号なし除算</summary>
        Div,
        /// <summary>符号あり剰余</summary>
        SMod,
        /// <summary>符号あり乗算</summary>
        SMul,
        /// <summary>符号あり除算</summary>
        SDiv,
        /// <summary>右シフト</summary>
        Shr,
        /// <summary>左シフト</summary>
        Shl,
        /// <summary>符号つき右シフト</summary>
        SShr,
        /// <summary>符号つき左シフト</summary>
        SShl,
        /// <summary>大きい</summary>
        Gt,
        /// <summary>大きいか等しい</summary>
        Ge,
        /// <summary>小さい</summary>
        Lt,
        /// <summary>小さいか等しい</summary>
        Le,
        /// <summary>符号つき大きい</summary>
        SGt,
        /// <summary>符号つき大きいか等しい</summary>
        SGe,
        /// <summary>符号つき小さい</summary>
        SLt,
        /// <summary>符号つき小さいか等しい</summary>
        SLe,
        /// <summary>等しい</summary>
        Eq,
        /// <summary>等しくない</summary>
        Neq,
        /// <summary>論理積</summary>
        And,
        /// <summary>排他的論理和</summary>
        Xor,
        /// <summary>論理和</summary>
        Or,
        /// <summary>関数</summary>
        Func,
        /// <summary>WORDからBYTEへのキャスト</summary>
        WtoB,
        /// <summary>BYTEからWORDへのキャスト</summary>
        BtoW,
        /// <summary>カンマ</summary>
        Comma,
        /// <summary>CASE文の範囲指定</summary>
        Range,
        /// <summary>CODE関数</summary>
        Code,
        /// <summary>CODE関数の中の式をアセンブルしたものを埋め込む指示</summary>
        CodeExpr,
    }

    /// <summary>
    /// 式の演算子の型
    /// </summary>
    public enum OperatorType
    {
        /// <summary>BYTE型</summary>
        Byte,
        /// <summary>WORD型</summary>
        Word,
        /// <summary>BOOL型</summary>
        Bool,
        /// <summary>ポインタ型</summary>
        Pointer,
        /// <summary>定数</summary>
        Constant
    }

    public static partial class OperatorTypeExtend {
        /// <summary>
        /// 式の演算子のをTypeInfoに変換する
        /// </summary>
        public static TypeInfo ToType(this OperatorType operatorType)
        {
            switch(operatorType)
            {
                case OperatorType.Byte:
                    return TypeInfo.ByteTypeInfo;
                case OperatorType.Word:
                    return TypeInfo.WordTypeInfo;
                case OperatorType.Constant:
                    return TypeInfo.WordTypeInfo;
            }
            throw new Exception("ToType");
        }
    }

    /// <summary>
    /// 比較演算子
    /// </summary>
    [System.Flags]
    public enum ComparisonOp
    {
        Eq  = 0,
        Not = 1 << 0,
        Gt  = 1 << 1,
        Neq = Eq | Not,
        Le  = Gt | Not,

        Signed = 1 << 2,
        SGt = Gt | Signed,
        SLe = Le | Signed,
    };

    /// <summary>
    /// 式クラス
    /// </summary>
    public class Expr
    {
        /// <summary>
        /// 式の演算子の種類
        /// </summary>
        public Opcode Opcode { get; set; }
        /// <summary>
        /// 式の演算子の型
        /// </summary>
        public OperatorType OpType { get; set; }
        /// <summary>
        /// 代入演算子における演算子の型(+=の場合はAdd、など)
        /// </summary>
        public Opcode AssignOpCode { get; set; }
        /// <summary>
        /// 式の型情報
        /// </summary>
        public TypeInfo TypeInfo { get; set; }

        /// <summary>
        /// 式が持つ値
        /// </summary>
        public int Value { get; set; }

        /// <summary>
        /// 式が持つシンボル値
        /// </summary>
        public SymbolTable Symbol { get; set; }
        /// <summary>
        /// シンボル値先頭からのオフセット
        /// </summary>
        public int SymbolOffset { get; set; }

        /// <summary>
        /// 式ツリーの左側
        /// </summary>
        public Expr Left { get; set; }
        /// <summary>
        /// 式ツリーの右側
        /// </summary>
        public Expr Right { get; set; }
        /// <summary>
        /// 式ツリーの三番目
        /// </summary>
        public Expr Third { get; set; }

        /// <summary>
        /// 比較演算情報
        /// </summary>
        public ComparisonOp ComparisonOp { get; set; }

        /// <summary>
        /// (関数の)パラメータリストTree
        /// </summary>
        public Tree paramList;


        /// <summary>
        /// CONST値の場合はtrue、そうでない場合はfalseを返す
        /// </summary>
        public bool IsConst()
        {
            return Opcode == Opcode.Const;
        }

        /// <summary>
        /// 変数の場合はtrue、そうでない場合はfalseを返す
        /// </summary>
        public bool IsVariable()
        {
            return Opcode == Opcode.Indir && Left.Opcode == Opcode.Adr;
        }

        /// <summary>
        /// HL,DE,BCに直接代入出来る場合はtrue、そうでない場合はfalseを返す
        /// </summary>
        public bool CanLoadDirect()
        {
            var checkTarget = this;

            // 定数は直接DEに入れられる
            if(checkTarget.IsConst())
            {
                return true;
            }

            // キャストでは(他の)レジスタは変化しないので、キャストの下を対象としてチェックする
            if(Opcode == Opcode.WtoB || Opcode == Opcode.BtoW)
            {
                checkTarget = Left;
            }

            // 変数については通常変数は可能
            // 配列変数は、添字が数値の場合のみ可能？か？
            // 間接変数はいかなる場合も無理(のはず)
            if(checkTarget.IsVariable())
            {
                var symbol = checkTarget.Left.Symbol;
                var symbolType = checkTarget.Left.Symbol.TypeInfo;

                // これがArrayの場合は単純な配列(scaleaddされたもの)のはず
                if(symbolType.IsArray())
                {
                    // 配列はGlobalの場合は直接代入可、Localの場合は不可
                    return symbol.SymbolClass == SymbolClass.Global;
                } else if(symbolType.IsWordTypeInfo() || symbolType.IsByteTypeInfo()){
                    return true;
                } else if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                {
                    return true;
                }
            } else if(checkTarget.Opcode == Opcode.Adr && checkTarget.Symbol.TypeInfo.IsArray())
            {
                // 配列アドレス取得の場合は直接レジスタに入れられるはず(グローバルのみ)
                return checkTarget.Symbol.SymbolClass == SymbolClass.Global;
            }
            return false;
        }

        /// <summary>
        /// Expr情報を表示する(デバッグ用)
        /// </summary>
        public override string ToString()
        {
            string result = $"Expr: opcode:{Opcode} optype:{OpType} symbol:{Symbol} symbolOfs:{SymbolOffset}";
            return result;
        }
    }
}
