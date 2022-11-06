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
    /// FOR文に関する情報を保持するクラス
    /// </summary>
    public class ForInfo
    {
        /// <summary>FOR文加算対象の変数式</summary>
        public Expr Expr { get; set; }
        /// <summary>FOR文の終了値の変数式</summary>
        public Expr CheckExpr { get; set; }
        /// <summary>FOR文のループの頭のラベル</summary>
        public int Label { get; set; }
        /// <summary>TOまたはDOWNTO</summary>
        public string Op { get; set; }
    }

    internal partial class SLANGParser
    {
        // IFのラベルスタック
        private Stack<int> ifLabelStack = new Stack<int>();
        // ELSEのラベルスタック
        private Stack<int> elseStack = new Stack<int>();    // 多分これはスタックにしなくても良いやつ


        // IF開始時にIF終了ラベルを保存しておく
        private void pushIfLabel(int label)
        {
            ifLabelStack.Push(label);
        }

        // IF終了時にIF終了ラベルをスタックから得る
        private int popIfLabel()
        {
            if(ifLabelStack.Count > 0)
            {
                return ifLabelStack.Pop();
            }
            Error("label stack empty");
            return -1;
        }

        // 現在のIF終了ラベルを得る
        private int peekIfLabel()
        {
            if(ifLabelStack.Count == 0)
            {
                Error("label stack empty");
                return -1;
            }
            return ifLabelStack.Peek();
        }

        // ELSEラベルを保存する
        private void pushElseLabel(int label)
        {
            elseStack.Push(label);
        }

        // ELSEラベルを戻し、ELSEラベルを取得する
        private int popElseLabel()
        {
            if(elseStack.Count == 0)
            {
                // error("else stack empty");
                return -1;
            }
            return elseStack.Pop();
        }

        /// <summary>
        /// CASE文に関する情報を保持するクラス
        /// </summary>
        private class CaseInfo
        {
            /// <summary>CASE文内の判定の現在の数</summary>
            public int CurrentCount { get; set; }
            /// <summary>CASE文で次の判定のラベル</summary>
            public int NextLabel { get; set; }
            /// <summary>CASE文を抜けるラベル</summary>
            public int ExitLabel { get; set; }
            /// <summary>OTHERS宣言の有無</summary>
            public bool HasOthers { get; set; }
        }

        // CASE文のスタック
        private Stack<CaseInfo> caseStack;
        // 現在処理中のCASE文の情報
        private CaseInfo currentCaseInfo;

        // CASE文の開始
        private void doCaseHead(Expr expr)
        {
            if(currentCaseInfo != null)
            {
                caseStack.Push(currentCaseInfo);
            }
            currentCaseInfo = new CaseInfo()
            {
                CurrentCount = 0,
                NextLabel = genNewLabel(),
                ExitLabel = genNewLabel(),
                HasOthers = false
            };

            expr = coerce(expr, OperatorType.Word);

            // 比較対象をDEに入れる
            if(expr.CanLoadDirect())
            {
                genld(Register.DE, expr);
            } else {
                genexptop(expr);
                gencode(" EX DE,HL\n");
            }
        }

        // CASE文内の数値判定の出力
        private void doCase(Expr expr)
        {
            if(currentCaseInfo.CurrentCount > 0)
            {
                genjump(currentCaseInfo.ExitLabel);
                genlabel(currentCaseInfo.NextLabel);
                currentCaseInfo.NextLabel = genNewLabel();
            }
            if(expr == null)
            {
                // その他系なので何もしない(そのまま下に流す)
                currentCaseInfo.HasOthers = true;
            } else if(expr.IsConst())
            {
                // 単独一致
                gencode($" LD HL,{expr.Value}\n");
                gencode(" OR A\n");
                gencode(" SBC HL,DE\n");
                gencondjump(OperatorType.Word, ComparisonOp.Neq, currentCaseInfo.NextLabel, 0);
            } else if(expr.Opcode == Opcode.Comma)
            {
                var values = GetCommaConstValues(expr);
                var stmtLabel = genNewLabel();
                for(int i = 0; i < values.Count; i++)
                {
                    var value = values[i];
                    gencode($" LD HL,{value}\n");
                    gencode(" OR A\n");
                    gencode(" SBC HL,DE\n");
                    if(i != values.Count - 1)
                    {
                        // TODO JRにしてstmtLabelを最後の条件JPの前に持っていくと少しだけ縮む
                        gencondjump(OperatorType.Word, ComparisonOp.Eq, stmtLabel, 0);
                    } else {
                        gencondjump(OperatorType.Word, ComparisonOp.Neq, currentCaseInfo.NextLabel, 0);
                        genlabel(stmtLabel);
                    }
                }
            } else if(expr.Opcode == Opcode.Range)
            {
                var rangeLabel = genNewLabel();
                gencode(" LD A,E\n");
                gencode($" SUB {expr.Left.Value & 0xff}\n");            // left low byte
                gencode(" LD A,D\n");
                gencode($" SBC A,{(expr.Left.Value >> 8) & 0xff}\n");   // left high byte
                gencondjump(OperatorType.Word, ComparisonOp.Le, rangeLabel, 0);
                gencode($" LD A,{expr.Right.Value & 0xff}\n");            // right low byte
                gencode(" SUB E\n");
                gencode($" LD A,{(expr.Right.Value >> 8) & 0xff}\n");     // right high byte
                gencode(" SBC A,D\n");
                genlabel(rangeLabel);
                gencondjump(OperatorType.Word, ComparisonOp.Le, currentCaseInfo.NextLabel, 0);
            } else {
                Error($"CASE must be const parameter");
            }
            currentCaseInfo.CurrentCount++;
        }

        // CASE文の終了
        private void doCaseEnd()
        {
            // OTHERSが無い場合はnextLabelの出力が必要
            if(currentCaseInfo.CurrentCount > 0 && !currentCaseInfo.HasOthers)
            {
                genlabel(currentCaseInfo.NextLabel);
            }
            genlabel(currentCaseInfo.ExitLabel);
            if(caseStack.Count > 0)
            {
                currentCaseInfo = caseStack.Pop();
            } else {
                currentCaseInfo = null;
            }
        }

        // カンマでつながれたTreeからconst値をintのListで得る
        private List<int> GetCommaConstValues(Expr expr)
        {
            var valueList = new List<int>();
            var commaExpr = expr;
            while(commaExpr != null)
            {
                if(commaExpr.Opcode == Opcode.Comma)
                {
                    if(commaExpr.Right == null)
                    {
                        if(commaExpr.Left != null)
                        {
                            commaExpr = commaExpr.Left;
                            continue;
                        }
                    }
                    var value = ((Expr)commaExpr.Right).Value;
                    valueList.Insert(0, value);
                } else{
                    var value = commaExpr.Value;
                    valueList.Insert(0, value);
                    break;
                }
                commaExpr = commaExpr.Left;
            }
            return valueList;
        }

        // ラベルの元となる値
        private int labelSeed = 1;


        /// <summary>
        /// 新規にラベルを作る(ILabelCreator)
        /// </summary>
        public int CreateLabel()
        {
            labelSeed++;
            return labelSeed;
        }

        // 新規にラベルを作る( .y から呼ばれる。CreateLabelでも良い……)
        private int genNewLabel()
        {
            return CreateLabel();
        }

        // ラベル番号からラベル名を得る
        private string GetLabelName(int num)
        {
            return $"_L{num}";
        }

        // EXIT / CONTINUE管理用のラベルスタック
        private Stack<int> labelStack = new Stack<int>();

        // 現在のEXITラベル
        private int breakLabel;
        // 現在のCONTINUEラベル
        private int contLabel;

        // EXIT/CONTINUEラベルをPUSH
        private void pushLabels()
        {
            labelStack.Push(breakLabel);
            labelStack.Push(contLabel);
        }

        // EXIT/CONTINUEラベルをPOP
        private void popLabels()
        {
            contLabel = labelStack.Pop();
            breakLabel = labelStack.Pop();
        }
    }
}
