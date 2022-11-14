
using System;
using System.Collections.Generic;
using System.Text;

namespace SLANGCompiler.SLANG
{
    internal partial class SLANGParser : ICodeStatementGenerator
    {
        // 初期化コードの生成
        private void genInitCode()
        {
            // 初期化処理(指定ランタイムを埋め込む)
            genRuntimeInline("SLANGINIT");
        }

        // (l-valueの必要ない)トップレベルの式の生成
        private void genexptop(Expr p)
        {
            if(p == null)
            {
                return;
            }

            switch(p.Opcode)
            {
                case Opcode.Assign:
                    genassign(p, false);
                    break;
                case Opcode.AssignOp:
                    genassignop(p, false);
                    break;
                case Opcode.PreInc:
                case Opcode.PostInc:
                case Opcode.PreDec:
                case Opcode.PostDec:
                    genincdec(p, false);
                    break;
                default:
                    genexp(p);
                    break;
            }
        }

        // 関数内のラベル定義の生成
        private void genStringLabel(string labelName)
        {
            var label = labelName.Trim();
            var labelNum = labelManager.DefineLabel(label);
            if(codeRepository.IsLabelExists(labelNum))
            {
                Error($"already defined label : {labelName}");
                return;
            }
            codeRepository.AddLabel(labelNum);
        }

        // GOTO文の生成
        private void genGoto(string labelName)
        {
            var label = labelName.Trim();
            var labelNum = labelManager.ReferenceLabel(label);
            codeRepository.AddJump(labelNum);
        }

        // 式を文字列に戻すが、現状、アドレスに対する単純な加算にしか対応していない
        private string createExprString(Expr expr)
        {
            StringBuilder sb = new StringBuilder();
            switch(expr.Opcode)
            {
                case Opcode.Add:
                {
                    sb.Append(createExprString(expr.Left));
                    sb.Append("+");
                    sb.Append(createExprString(expr.Right));
                    break;
                }
                case Opcode.Const:
                {
                    if(expr.ConstValue.ConstInfoType == ConstInfoType.Code)
                    {
                        var constSymbol = symbolTableManager.SearchSymbol(expr.ConstValue.SymbolString);
                        sb.Append(constSymbol.LabelName);
                    } else {
                        sb.Append(expr.ConstValue.Value.ToString());
                    }
                    break;
                }
                case Opcode.Adr:
                {
                    sb.Append(expr.Symbol.LabelName);
                    break;
                }
            }
            return sb.ToString();
        }

        // CODE文の生成
        public int GenerateCodeStmt(Tree paramList)
        {
            int codeSize = 0;
            var exprList = new List<Expr>();
            for(Tree p = paramList; p != null; p = p.First)
            {
                Expr param = p.Expr;
                if(param == null)
                {
                    continue;
                }
                exprList.Add(param);
            }
            exprList.Reverse();

            foreach(var expr in exprList)
            {
                if(expr.IsConst())
                {
                    // CONSTの場合、ByteかWordかCodeか調べる
                    if(expr.ConstValue.ConstInfoType == ConstInfoType.Code)
                    {
                        var constSymbol = symbolTableManager.SearchSymbol(expr.ConstValue.SymbolString);
                        codeSize+=2;
                        gencode($" DW {constSymbol.LabelName}\n");
                    } else {
                        if(expr.TypeInfo.GetDataSize() == TypeDataSize.Byte)
                        {
                            codeSize++;
                            gencode($" DB ${expr.ConstValue.Value & 0xFF:X2}\n");
                        } else {
                            codeSize+=2;
                            gencode($" DW ${expr.ConstValue.Value & 0xFFFF:X4}\n");
                        }
                    }
                } else if(expr.Opcode == Opcode.Str)
                {
                    var strData = stringDataManager.GetString(expr.Value);
                    stringDataManager.Remove(expr.Value);
                    int strSize;
                    var code = stringDataManager.GetStringCode(strData, false, out strSize);
                    codeSize += strSize;
                    gencode($" DB {code}");
                } else if(expr.Opcode == Opcode.Label)
                {
                    codeSize += 2;
                    genLabelAddress(expr.Value);
                } else if(expr.Opcode == Opcode.CodeExpr)
                {
                    genexptop(expr.Left);
                } else {
                    // CONST式であると解釈しつつアセンブラが解釈出来る式を文字列で出力する
                    // ARRAY   BYTE    SCC[70];
                    // の場合、
                    // %SCC+1 → __SCC+1
                    // と、する(うーむ……)
                    //codeSize += GenerateConstExpr(expr);
                    // genexptop(expr);
                    gencode(" DW " + createExprString(expr) + "\n");
                }
            }
            return codeSize;
        }

        // PRINT文の生成
        private void genPrint(Tree paramList)
        {
            // paramListで指定された順に出力
            // paramListの先には、Exprがブラ下がっており、単純にOpcode.Strを表示する他、Constの場合は数値を表示、
            // 更にOpcode.StrFuncが挟まっている場合はHEX$とかSTR$とかでいい具合に加工してから表示するコードが出る

            // 逆順にする(最初から逆にしとけという話もある)
            var exprList = new List<Expr>();
            for(Tree p = paramList; p != null; p = p.First)
            {
                Expr param = p.Expr;
                if(param == null)
                {
                    continue;
                }
                exprList.Add(param);
            }
            exprList.Reverse();

            foreach(var expr in exprList)
            {
                if(expr.Opcode == Opcode.Str)
                {
                    // 単純な文字列の表示。文字列はプログラムコードの直下に埋め込まれる
                    genRuntimeCall("MPRNT");
                    var strData = stringDataManager.GetString(expr.Value);
                    stringDataManager.Remove(expr.Value);
                    int strSize;
                    var code = stringDataManager.GetStringCode(strData, false, out strSize);
                    gencode($" DB {code}");
                    gencode($" DB 0\n");

                    // MPRNTを使わない場合はこっちだが、基本、現状でいいと思う
                    // // HLに文字列のアドレスが入る
                    // genexptop(expr);
                    // genRuntimeCall("PMSX");
                } else if(expr.Opcode == Opcode.StrFunc )
                {
                    // 文字関数の処理
                    // 普通に関数呼び出しがブラ下がっているはずなので関数呼び出しを行い、文字を表示させる
                    genexptop(expr.Left);
                } else {
                    // それ以外の場合は10進数の数値として表示する
                    genexptop(coerce(expr, OperatorType.Word));
                    genRuntimeCall("P10");
                }
            }
        }

        // ラベルアドレスを埋め込む(CODEの中にラベルが書かれた場合ラベルアドレス値が埋め込まれる)
        private void genLabelAddress(int labelNum)
        {
            codeRepository.AddLabelAddress(labelNum);
        }

        // フォーマット文字列を元にコードを出力する
        // ※全体的に実装が雑
        private void gencode(string format, params Object[] obj)
        {
            StringBuilder sb = new StringBuilder();
            int idx = 0;
            for(var i = 0; i < format.Length; i++)
            {
                var c = format[i];
                if(c == '%')
                {
                    i++;
                    if(i >= format.Length)
                    {
                        break;
                    }
                    c = format[i];
                    switch(c)
                    {
                        case 'a':
                        {
                            // 配列のアドレス
                            var expr = (Expr)obj[idx];
                            var symbolOffset = expr.SymbolOffset;
                            var symbol = expr.Symbol;
                            if(symbol.TypeInfo.GetDataSize() == TypeDataSize.Word)
                            {
                                symbolOffset *= 2;
                            }
                            if(symbol.Address != null)
                            {
                                var ofs = symbolOffset >= 0 ? symbolOffset : 0;
                                var adr = symbol.Address.GetConstStr(symbolTableManager);
                                sb.Append($"{adr}+${ofs:X4}");
                            } else {
                                var baseName = symbol.LabelName;
                                if(symbolOffset != 0)
                                {
                                    sb.Append($"{baseName}+{symbolOffset}");
                                } else{
                                    sb.Append($"{baseName}");
                                }
                            }
                            break;
                        }
                        case 'v':
                        {
                            // シンボルを通常変数として取得する
                            var varExpr = (Expr)obj[idx];
                            var symbol = varExpr.Symbol;

                            string baseAdr;
                            string offsetAdr = "";
                            if(symbol.Address != null)
                            {
                                // 数値なので加算してやる
                                var ofs = varExpr.SymbolOffset >= 0 ? varExpr.SymbolOffset : 0;
                                var adr = symbol.Address.GetConstStr(symbolTableManager);
                                sb.Append($"({adr}+${ofs:X4})");
                            } else {
                                baseAdr = $"{symbol.LabelName}";
                                if(varExpr.SymbolOffset > 0)
                                {
                                    offsetAdr = $"+{varExpr.SymbolOffset}";
                                }
                                sb.Append($"({baseAdr}{offsetAdr})");
                            }
                            break;
                        }
                        case 'd':
                        {
                            var num = (int)obj[idx];
                            sb.Append($"{num}");
                            break;
                        }
                        case 'c':
                        {
                            var varExpr = (Expr)obj[idx];
                            var symbol = varExpr.Symbol;
                            string callStr = "";
                            if(symbol.Address != null)
                            {
                                callStr = $"{symbol.Address.GetConstStr(symbolTableManager)}";
                            } else {
                                callStr = symbol.LabelName;
                            }
                            sb.Append(callStr);
                            break;
                        }
                        case 'l':       // Label
                        {
                            var num = (int)obj[idx];
                            var labelName = GetLabelName(num);
                            sb.Append($"{labelName}");
                            break;
                        }
                        default:
                        {
                            sb.Append('%');
                            sb.Append(c);
                            break;
                        }
                    }
                } else {
                    sb.Append(c);
                }
            }
            codeRepository.AddCode(sb.ToString());
        }

        // 演算代入式を出力する
        private void genassignop(Expr expr, bool needLvalue = true)
        {
            Expr left = expr.Left;
            Expr right = expr.Right;

            var opcode = expr.AssignOpCode;

            switch(opcode)
            {
                case Opcode.Add:
                {
                    genadd(left, right);
                    break;
                }
                case Opcode.Sub:
                {
                    gensub(left, right);
                    break;
                }
                case Opcode.Mul:
                {
                    genmul(left, right);
                    break;
                }
                case Opcode.Div:
                {
                    gendivmod(left, right, OperatorType.Word, true);
                    break;
                }
                default:
                {
                    Error($"unsupported assign op {opcode}");
                    break;
                }
            }
            // HLを左辺に入れる
            genstore(left, needLvalue);
        }

        // expr.leftにHLを代入する
        private void genstore(Expr expr, bool needLvalue)
        {
            Expr left = expr;

            // Byteへの代入である
            bool isByte = left.TypeInfo.GetDataSize() == TypeDataSize.Byte;

            if(left.IsVariable())
            {
                if(left.Left.Symbol.TypeInfo.IsArray())
                {
                    gencode(" EX DE,HL\n");
                    genexp(left.Left);
                    if(isByte)
                    {
                        gencode(" LD (HL),E\n");
                    } else{
                        gencode(" LD (HL),E\n");
                        gencode(" INC HL\n");
                        gencode(" LD (HL),D\n");
                    }

                    if(needLvalue)
                    {
                        gencode(" EX DE,HL\n");
                    }
                } else {
                    if(isByte)
                    {
                        gencode(" LD A,L\n");
                        gencode(" LD %v,A\n", left.Left);
                    } else{
                        gencode(" LD %v,HL\n", left.Left);
                    }
                }
            } else {
                gencode(" PUSH HL\n");
                genexp(left.Left);
                gencode(" POP DE\n");

                if(isByte)
                {
                    gencode( " LD (HL),E\n");
                } else {
                    gencode(" LD (HL),E\n");
                    gencode(" INC HL\n");
                    gencode(" LD (HL),D\n");
                }
                if(needLvalue)
                {
                    gencode(" EX DE,HL\n");
                }
            }
        }

        // 代入を生成する
        protected void genassign(Expr expr, bool needLvalue = true)
        {
            Expr left = expr.Left;
            Expr right = expr.Right;

            bool isByte = left.TypeInfo.GetDataSize() == TypeDataSize.Byte;

            if(left.Opcode == Opcode.PortAccess)
            {
                // ポートへの書き込み
                if(right.CanLoadDirect())
                {
                    genld(Register.DE, right);
                } else {
                    genexp(right);
                    gencode(" EX DE,HL\n");
                }
                genportOut(left.Left, left.Right);
            } else if(left.IsVariable())
            {
                // 単純変数への代入
                var symbol = left.Left.Symbol;
                var typeInfo = left.Left.Symbol.TypeInfo;


                if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                {
                    var ofs = symbol.Address.GetConstStr(symbolTableManager) + "+" + left.Left.SymbolOffset;
                    if(right.IsConst())
                    {
                        if(right.IsValueConst())
                        {
                            var lowVal = right.ConstValue.Value & 0xff;
                            var highVal = (right.ConstValue.Value >> 8) & 0xff;
                            gencode($" LD (IY+{ofs}),{lowVal}\n");
                            if(!isByte)
                            {
                                gencode($" LD (IY+{ofs}+1),{highVal}\n");
                            }
                        } else {
                            var constStr = right.GetConstStr(localSymbolTableManager);
                            if(constStr == null)
                            {
                                constStr = right.GetConstStr(symbolTableManager);
                            }
                            if(constStr != null)
                            {
                                gencode($" LD (IY+{ofs}),LOW {constStr}\n");
                                if(!isByte)
                                {
                                    gencode($" LD (IY+{ofs}+1),HIGH {constStr}\n");
                                }
                            }
                        }
                    } else {
                        genexp(right);
                        gencode($" LD (IY+{ofs}),L\n");
                        if(!isByte)
                        {
                            gencode($" LD (IY+{ofs}+1),H\n");
                        }
                    }
                } else {
                    var loadByte = isByte && !typeInfo.IsIndirect();
                    if(loadByte)
                    {
                        if(right.CanLoadDirect())
                        {
                            genld(Register.DE, right);
                        } else {
                            genexp(right);
                            gencode(" EX DE,HL\n");
                        }
                        gencode(" LD HL,%a\n",left.Left);
                        gencode(" LD (HL),E\n");

                        if(needLvalue)
                        {
                            gencode(" EX DE,HL\n");
                        }
                    } else{
                        genexp(right);
                        gencode(" LD %v,HL\n", left.Left);
                    }
                }
            } else {
                genexp(left.Left);
                // HLにアドレス入る
                if(right.CanLoadDirect())
                {
                    genld(Register.DE, right);
                } else {
                    gencode(" PUSH HL\n");
                    genexp(right);
                    gencode(" EX DE,HL\n");
                    gencode(" POP HL\n");
                }
                if(isByte)
                {
                    gencode(" LD (HL),E\n");
                } else{
                    gencode(" LD (HL),E\n");
                    gencode(" INC HL\n");
                    gencode(" LD (HL),D\n");
                }
                if(needLvalue)
                {
                    gencode(" EX DE,HL\n");
                }
            }
        }

        // AND/OR/XORを出力する
        private void genbitop(Opcode opcode, Expr left, Expr right, OperatorType operatorType)
        {
            var bitopDictionary = new Dictionary<Opcode, string>()
            {
                {Opcode.And, "ANDHLDE"},
                {Opcode.Or, "ORHLDE"},
                {Opcode.Xor, "XORHLDE"},

            };
            if(!bitopDictionary.ContainsKey(opcode))
            {
                bug("not bitop opcode " + opcode);
                return;
            }
            string callName = bitopDictionary[opcode];

            if(left.IsConst() || left.IsVariable())
            {
                var tmp = left;
                left = right;
                right = tmp;
            }
            if(right.IsConst())
            {
                genexp(left);
                genld(Register.DE, right);
                genRuntimeCall(callName);
                return;
            } else if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
            } else {
                genexp(left);
                gencode(" PUSH HL\n");
                genexp(right);
                gencode(" POP DE\n");
            }
            genRuntimeCall(callName);
        }

        private string GetConstStr(Expr constExpr)
        {
            if(!constExpr.IsConst())
            {
                Error("Could not found const");
                return null;
            }
            var constStr = constExpr.GetConstStr(localSymbolTableManager);
            if(constStr == null)
            {
                constStr = constExpr.GetConstStr(symbolTableManager);
            }
            if(constStr != null)
            {
                return constStr;
            }
            Error("Could not found const");
            return null;
        }

        // CPL式を出力する
        private void gencpl(Expr expr)
        {
            if(expr.Left.IsConst())
            {
                var constExpr = expr.Left;
                if(constExpr.IsValueConst())
                {
                    var val = ~expr.ConstValue.Value;
                    gencode($" LD HL,{val}\n");
                } else {
                    var constStr = GetConstStr(constExpr);
                    gencode($" LD HL,~{constStr}\n");
                }
                return;
            }
            genexp(expr.Left);
            genRuntimeCall("CPLHL");
        }

        // NEG式(符号反転)を出力する
        private void genneg(Expr expr)
        {
            if(expr.Left.IsConst())
            {
                var constExpr = expr.Left;
                if(constExpr.IsValueConst())
                {
                    var val = expr.ConstValue.Value * -1;
                    gencode($" LD HL,{val}\n");
                } else {
                    var constStr = GetConstStr(constExpr);
                    gencode($" LD HL,-{constStr}\n");
                }
                return;
            }
            genexp(expr.Left);
            genRuntimeCall("NEGHL");
        }

        // 論理NOT式を出力する
        private void gennot(Expr expr)
        {
            if(expr.Left.IsConst())
            {
                var constExpr = expr.Left;
                if(constExpr.IsValueConst())
                {
                    var val = expr.ConstValue.Value == 0 ? 1 : 0;
                    gencode($" LD HL,{val}\n");
                } else {
                    var constStr = GetConstStr(constExpr);
                    gencode($" LD HL,{constStr} == 0 ? 1 : 0\n");
                }
                return;
            }
            genexp(expr.Left);
            // 0の場合は1、0以外の場合は0にする
            genRuntimeCall("NOTHL");
        }

        // 加算式を出力する
        private void genadd(Expr left, Expr right)
        {
            if(left.IsConst())
            {
                var tmp = left;
                left = right;
                right = tmp;
            }
            if(right != null & right.IsConst())
            {
                genexp(left);
                //if(left.TypeInfo.GetDataSize() == TypeDataSize.Word || left.OpType == OperatorType.Pointer)
                {
                    if(right.ConstValue.ConstInfoType == ConstInfoType.Code)
                    {
                        var constStr = GetConstStr(right);
                        gencode($" LD DE,{constStr}\n");
                        gencode($" ADD HL,DE\n");
                    } else {
                        // 4加算まではINCにしてそれ以上は普通に足してみる
                        var constVal = right.ConstValue.Value;
                        if(constVal < 4)
                        {
                            for(int i = 0; i < constVal; i++)
                            {
                                gencode(" INC HL\n");
                            }
                        } else {
                            gencode($" LD DE,{constVal}\n");
                            gencode($" ADD HL,DE\n");
                        }
                    }
                }
                // TODO 本当に全ての演算はキャストされるのか？後でチェック。
                // else {
                //    // 基本的にはWORDにキャストされているはずなのでここにはこないはず……
                //    if(right.Value < 4)
                //    {
                //        // 4加算まではINCにしてそれ以上は普通に足してみる
                //        for(int i = 0; i < right.Value; i++)
                //        {
                //            gencode(" INC L\n");
                //        }
                //    } else {
                //        gencode($" ADD L,{right.Value}\n");
                //    }
                //}
            } else if(right.CanLoadDirect())
            {
                // これとここの下、Byte加算に対応していないので注意(手前で左右ともにWORDになっているはず)
                genexp(left);
                genld(Register.DE, right);
                gencode(" ADD HL,DE\n");
            } else if(left.CanLoadDirect())
            {
                genexp(right);
                gencode(" EX DE,HL\n");
                genld(Register.HL, left);
                gencode(" ADD HL,DE\n");
            } else {
                genexp(left);
                gencode(" PUSH HL\n");
                genexp(right);
                gencode(" POP DE\n");
                gencode(" ADD HL,DE\n");
            }
        }

        // 減算式を生成する
        private void gensub(Expr left, Expr right)
        {
            if(right != null && right.IsConst())
            {
                if(right.ConstValue.ConstInfoType == ConstInfoType.Code)
                {
                    Error("CODE Const can not sub");
                    return;
                }
                genexp(left);
                if(left.TypeInfo.GetDataSize() == TypeDataSize.Word)
                {
                    int constVal = right.ConstValue.Value;
                    if(constVal< 4)
                    {
                        for(int i = 0; i < constVal; i++)
                        {
                            gencode(" DEC HL\n");
                        }
                    } else {
                        // 定数の減算
                        gencode($" LD DE,{-constVal}\n");
                        gencode($" ADD HL,DE\n");
                    }
                } else {
                    // BYTEは無いはず
                    if(right.Value < 4)
                    {
                        for(int i = 0; i < right.Value; i++)
                        {
                            gencode(" DEC L\n");
                        }
                    } else {
                        // うーん？
                        gencode($" LD A,L\n");
                        gencode($" SUB {right.Value}\n");
                        gencode($" LD L,A\n");
                    }
                }
            } else if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
                gencode($" OR A\n");
                gencode(" SBC HL,DE\n");
            } else if(left.CanLoadDirect())
            {
                genexp(right);
                gencode(" EX DE,HL\n");
                genld(Register.HL, left);
                gencode($" OR A\n");
                gencode(" SBC HL,DE\n");
            } else {
                genexp(left);
                gencode(" PUSH HL\n");
                genexp(right);
                gencode(" POP DE\n");
                gencode(" EX DE,HL\n");
                gencode($" OR A\n");
                gencode(" SBC HL,DE\n");
            }
        }

        // 乗算隙を生成する
        private void genmul(Expr left, Expr right, bool isUnsigned = true)
        {
            string callName = isUnsigned ? "MULHLDE" : "MULHLDE";       // Signedは無い？
            if(left.IsConst())
            {
                var tmp = left;
                left = right;
                right = tmp;
            }
            if(right.IsConst() && isUnsigned)
            {
                if(!right.IsValueConst())
                {
                    Error("CODE Const can not mul");
                    return;
                }
                genexp(left);
                var constValue = right.ConstValue.Value;
                if(constValue == 1)
                {
                } else if(constValue == 2)
                {
                    gencode(" ADD HL,HL\n");
                } else if(constValue == 3)
                {
                    gencode(" LD D,H\n");
                    gencode(" LD E,L\n");
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,DE\n");
                } else if(constValue == 4)
                {
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                } else if(constValue == 8)
                {
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                } else {
                    gencode($" LD DE,{constValue}\n");
                    genRuntimeCall(callName);
                }
            } else if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
                genRuntimeCall(callName);
            } else if(left.CanLoadDirect())
            {
                genexp(right);
                genld(Register.DE, left);
                genRuntimeCall(callName);
            } else {
                genexp(left);
                gencode(" PUSH HL\n");
                genexp(right);
                gencode(" POP DE\n");
                genRuntimeCall(callName);
            }
        }

        // 左シフトまたは右シフト式を生成する
        private void genshift(Expr left, Expr right, bool isLeft, bool isUnsigned = true)
        {
            if(left.IsConst() && right.IsConst())
            {
                if(!left.IsValueConst() || !right.IsValueConst())
                {
                    Error("CODE Const can not shift");
                    return;
                }
                // 事前最適化されているはずだが念の為処理しておく
                var val = isLeft ? (left.ConstValue.Value << right.ConstValue.Value) : (left.ConstValue.Value >> right.ConstValue.Value);
                gencode($" LD HL,{val}\n");
                return;
            }
            var ltype = left.TypeInfo;
            var rtype = right.TypeInfo;

            var callName = isLeft ? "LSHIFTHLDE" : "RSHIFTHLDE";
            if(!isUnsigned)
            {
                callName = "S" + callName;
            }
            if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
                genRuntimeCall(callName);
            } else if(left.CanLoadDirect())
            {
                genexp(right);
                gencode(" EX DE,HL\n");
                genld(Register.HL, left);
                genRuntimeCall(callName);
            } else {
                genexp(right);
                gencode(" PUSH HL\n");
                genexp(left);
                gencode(" POP DE\n");
                genRuntimeCall(callName);
            }
        }

        // 単純なCALLを生成する
        private void gencall(string callName)
        {
            gencode($" CALL {callName}\n");
        }

        // BOOL式を生成する
        private void genboolop(ComparisonOp boolOp, Expr left, Expr right)
        {
            var ltype = left.TypeInfo;
            var rtype = right.TypeInfo;
            var boolCallDic = new Dictionary<ComparisonOp, string>()
            {
                { ComparisonOp.Eq, "OPEQHL"},
                { ComparisonOp.Neq, "OPNEQHL"},
                { ComparisonOp.Gt, "OPGTHLDE"},
                { ComparisonOp.Le, "OPLEHLDE"},
                { ComparisonOp.SGt, "OPSGTHLDE"},
                { ComparisonOp.SLe, "OPSLEHLDE"},
            };

            // EQ、NEQについてはHLからDEを引いた値を渡す事でHL単独での数値化処理(0か1にする処理)を行う
            string additionalCode = "";
            if(boolOp == ComparisonOp.Eq || boolOp == ComparisonOp.Neq)
            {
                additionalCode = " OR A\n SBC HL,DE\n";
            }

            var callName = boolCallDic[boolOp];
            if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
                gencode(additionalCode);
                genRuntimeCall(callName);
            } else if(left.CanLoadDirect())
            {
                genexp(right);
                gencode(" EX DE,HL\n");
                genld(Register.HL, left);
                gencode(additionalCode);
                genRuntimeCall(callName);
            } else {
                genexp(right);
                gencode(" PUSH HL\n");
                genexp(left);
                gencode(" POP DE\n");
                gencode(additionalCode);
                genRuntimeCall(callName);
            }
        }

        // 指定ラベルへのジャンプを生成する
        private void genjump(int num)
        {
            codeRepository.AddJump(num);
        }

        // 条件つきジャンプを生成する
        private void gencondjump(OperatorType opType, ComparisonOp boolOp, int trueLabelNum, int falseLabelNum)
        {
            // EQ, NEQ, GT, LE
            var condCode = new ConditionalCode[]{
                ConditionalCode.Zero,
                ConditionalCode.NonZero,
                ConditionalCode.NonCarry,
                ConditionalCode.Carry
            };
            // Console.WriteLine($"JUMP Base OpType: {opType} boolOp:{boolOp} true: {trueLabelNum} false: {falseLabelNum}");

            int targetNum = 0, exitNum = 0;
            if(trueLabelNum != 0)
            {
                targetNum = trueLabelNum;
                exitNum = falseLabelNum;
            } else {
                boolOp ^= ComparisonOp.Not;
                targetNum = falseLabelNum;
                exitNum = 0;
            }
            int condBoolOp = (int)boolOp;
            condBoolOp &= ~(int)ComparisonOp.Signed;

            codeRepository.AddJump(targetNum, condCode[condBoolOp], false);
            if(exitNum != 0)
            {
                genjump(exitNum);
            }
            if(DebugEnabled)
            {
                Console.WriteLine($"JUMP OpType: {opType} boolOp:{boolOp} true: {trueLabelNum} false: {falseLabelNum}");
            }
        }

        // 比較ジャンプを生成する
        private void gencompare(Expr expr, int trueLabelNum, int falseLabelNum)
        {
            Expr left = expr.Left;
            Expr right = expr.Right;
            OperatorType opType = left.OpType;

            if(DebugEnabled)
            {
                Console.WriteLine("gencompare:" + opType);
            }
            ComparisonOp boolOp = expr.ComparisonOp;;
            if(expr.ComparisonOp == ComparisonOp.SGt || expr.ComparisonOp == ComparisonOp.SLe)
            {
                string[] boolCalls = new string[]
                {
                    "OPSGTHLDE",
                    "OPSLEHLDE"
                };
                var callName = boolCalls[expr.ComparisonOp == ComparisonOp.SGt ? 0 : 1];
                if(right.CanLoadDirect())
                {
                    genexp(left);
                    genld(Register.DE, right);
                    genRuntimeCall(callName);
                } else{
                    genexp(right);
                    gencode(" PUSH HL\n");
                    genexp(left);
                    gencode(" POP DE\n");
                    genRuntimeCall(callName);
                }
                gencode(" LD A,H\n");
                gencode(" OR L\n");

                // Neqでいいのか？？
                gencondjump(opType, ComparisonOp.Neq, trueLabelNum, falseLabelNum);
            } else {
                // 右が0でEQ or NEQの場合はいちいちSBCしないで0かどうかのみ調べる
                if(right.IsValueConst() && right.ConstValue.Value == 0 && (expr.ComparisonOp == ComparisonOp.Eq || expr.ComparisonOp == ComparisonOp.Neq))
                {
                    genexp(left);
                    gencode(" LD A,H\n");
                    gencode(" OR L\n");
                } else {
                    // この場合は逆条件にしないといけない？
                    if(trueLabelNum == 0)
                    {
                        switch(expr.ComparisonOp)
                        {
                            case ComparisonOp.Gt:
                            {
                                boolOp = ComparisonOp.Le;
                                var tmp = left;
                                left = right;
                                right = tmp;
                                break;
                            }
                            case ComparisonOp.Le:
                            {
                                boolOp = ComparisonOp.Gt;
                                var tmp = left;
                                left = right;
                                right = tmp;
                                break;
                            }
                        }
                    }

                    if(right.CanLoadDirect())
                    {
                        genexp(left);
                        genld(Register.DE, right);
                        gencode(" OR A\n");
                        gencode(" SBC HL,DE\n");
                    } else {
                        genexp(right);
                        gencode(" PUSH HL\n");
                        genexp(left);
                        gencode(" POP DE\n");
                        gencode(" OR A\n");
                        gencode(" SBC HL,DE\n");
                    }
                }
                gencondjump(opType, boolOp, trueLabelNum, falseLabelNum);
            }
        }

        // boolを数値として得る
        private void gendebool(Expr boolExpr)
        {
            switch(boolExpr.Opcode)
            {
                case Opcode.Bool:
                {
                    genboolop(boolExpr.ComparisonOp, boolExpr.Left, boolExpr.Right);
                    break;
                }
                case Opcode.Land:
                {
                    // どうにかする
                    gendebool(boolExpr.Left);
                    gencode(" PUSH HL\n");
                    gendebool(boolExpr.Right);
                    gencode(" POP DE\n");
                    genRuntimeCall("ANDHLDE");
                    break;
                }
                case Opcode.Lor:
                {
                    gendebool(boolExpr.Left);
                    gencode(" PUSH HL\n");
                    gendebool(boolExpr.Right);
                    gencode(" POP DE\n");
                    genRuntimeCall("ORHLDE");
                    break;
                }
                default:
                {
                    // error?
                    genexp(boolExpr);
                    break;
                }
            }
        }

        // 三項演算子の生成
        private void gencond(Expr expr)
        {
            var left = expr.Left;
            var right = expr.Right;
            var third = expr.Third;

            int label = genNewLabel();
            genNewLabel();
            genbool(left, 0, label);
            genexp(right);
            genjump(label + 1);
            genlabel(label);
            genexp(third);
            genlabel(label + 1);
        }

        // I/Oポート入力式の出力
        private void genportIn(Expr port, Expr addr)
        {
            genexp(addr);
            gencode(" LD B,H\n");
            gencode(" LD C,L\n");
            if(port.Left.Symbol.TypeInfo.GetDataSize() == TypeDataSize.Byte)
            {
                gencode(" IN L,(C)\n");
                gencode(" LD H,0\n");
            } else {
                gencode(" IN L,(C)\n");
                gencode(" INC BC\n");
                gencode(" IN H,(C)\n");
            }
        }

        // I/Oポート出力式の出力
        // DEに入っている値をHLのポートに書く
        private void genportOut(Expr port, Expr addr)
        {
            if(addr.CanLoadDirect())
            {
                // HLに直接入れるコードが出る……はず
                genexp(addr);
            } else{
                gencode(" PUSH DE\n");
                genexp(addr);
                gencode(" POP DE\n");
            }
            gencode(" LD B,H\n");
            gencode(" LD C,L\n");
            if(port.Left.Symbol.TypeInfo.GetDataSize() == TypeDataSize.Byte)
            {
                gencode(" OUT (C),E\n");
            } else {
                gencode(" OUT (C),E\n");
                gencode(" INC BC\n");
                gencode(" OUT (C),D\n");
            }
        }

        // BYTEをWORDにキャスト
        private void genbtow(Expr expr)
        {
            if(expr != null)
            {
                genexp(expr);
            }
            gencode(" LD H,0\n");
        }

        // WORDをBYTEにキャスト
        private void genwtob(Expr expr)
        {
            // 何もしなくていい気がする
            if(expr != null)
            {
                genexp(expr);
            }
        }

        // 比較ジャンプを生成する
        private void genbool(Expr expr, int trueLabelNum, int falseLabelNum)
        {
            int x;
            if(expr == null)
            {
                return;
            }
            switch(expr.Opcode)
            {
                case Opcode.Bool:
                    {
                        gencompare(expr, trueLabelNum, falseLabelNum);
                        break;
                    }
                case Opcode.Land:
                    {
                        if(falseLabelNum == 0)
                        {
                            genbool(expr.Left, 0, x = genNewLabel());
                            genbool(expr.Right, trueLabelNum, 0);
                            genlabel(x);
                        } else {
                            genbool(expr.Left, 0, falseLabelNum);
                            genbool(expr.Right, trueLabelNum, falseLabelNum);
                        }
                        break;
                    }
                case Opcode.Lor:
                    {
                        if(trueLabelNum == 0)
                        {
                            genbool(expr.Left, x = genNewLabel(), 0);
                            genbool(expr.Right, 0, falseLabelNum);
                            genlabel(x);
                        } else {
                            genbool(expr.Left, trueLabelNum, 0);
                            genbool(expr.Right, trueLabelNum, falseLabelNum);
                        }
                        break;
                    }
                default:
                    {
                        bug("genbool");
                        break;
                    }
            }
        }

        // ラベルを新規に生成する
        public void GenerateLabel(int labelNum)
        {
            codeRepository.AddLabel(labelNum);
        }


        // ラベルを新規に生成する
        private void genlabel(int labelNum)
        {
            GenerateLabel(labelNum);
        }

        // HIGH/LOW式を生成する
        private void genhighlow(Opcode opcode, Expr expr)
        {
            genexp(expr);
            if(opcode == Opcode.Low)
            {
                //gencode(" LD H,0\n"); // BtoWなどが挟まるはずなので0は入れなくて良い
            } else {
                gencode(" LD L,H\n");
                //gencode(" LD H,0\n"); // 同上
            }
        }

        // ランタイム関数の呼び出しを出力する
        private void genRuntimeCall(string runtimeName)
        {
            // ソースにランタイム内の該当コールを含める
            if(runtimeManager.Use(runtimeName))
            {
                Error($"can not found runtime call {runtimeName}");
            }
            var runtimeSymbol = symbolTableManager.SearchSymbol(runtimeName);

            // 必ずあるはずだが……
            if(runtimeSymbol == null)
            {
                Error($"can not found runtime call {runtimeName}");
            }
            if(runtimeSymbol.Address != null)
            {
                gencode($" CALL ${runtimeSymbol.Address.GetConstStr(symbolTableManager)}\n");
            } else {
                var name = runtimeSymbol.RuntimeName;
                gencode($" CALL {name}\n");
            }
        }

        // 指定ランタイムのコードを直接埋め込む
        private void genRuntimeInline(string runtimeName)
        {
            var code = runtimeManager.GetRuntimeCode(runtimeName);
            if(code != null)
            {
                gencode(code);
            }
        }

        // 乗算、剰余算を出力する
        private void gendivmod(Expr left, Expr right, OperatorType operatorType, bool isDiv, bool isUnsigned = true)
        {
            var callName = isDiv ? "DIVHLDE" : "MODHLDE";
            if(!isUnsigned)
            {
                callName = "S" + callName;
            }
            if(right != null && right.IsConst() && isUnsigned)
            {
                // HL / DEまたはHL % DEで、HLに返す
                genexp(left);
                gencode($" LD DE,{right.ConstValue.Value}\n");
                genRuntimeCall(callName);
            } else if(right.CanLoadDirect())
            {
                genexp(left);
                genld(Register.DE, right);
                genRuntimeCall(callName);
            } else {
                genexp(right);
                gencode(" PUSH HL\n");
                genexp(left);
                gencode(" POP DE\n");
                genRuntimeCall(callName);
            }
        }

        // 変数を出力する
        private void genindir(Expr expr)
        {
            if(expr.Left.Opcode == Opcode.Adr)
            {
                // 標準的な変数
                // 間接変数の場合はHLに入れる
                var symbol = expr.Left.Symbol;
                var typeInfo = expr.Left.TypeInfo;

                if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                {
                    // IYからのオフセットで得る
                    var ofs = symbol.Address.Value + expr.Left.SymbolOffset;
                    if(symbol.TypeInfo.GetDataSize()== TypeDataSize.Byte)
                    {
                        gencode($" LD L,(IY+{ofs})\n");
                    } else {
                        gencode($" LD L,(IY+{ofs})\n");
                        gencode($" LD H,(IY+{ofs+1})\n");
                    }
                } else {
                    var isIndirect = symbol.TypeInfo.IsIndirect();
                    if(!isIndirect && typeInfo.GetDataSize() == TypeDataSize.Byte)
                    {
                        gencode(" LD HL,%a\n", expr.Left);
                        gencode(" LD L,(HL)\n");
                    } else {
                        gencode(" LD HL,%v\n", expr.Left);
                    }
                }
            } else if(expr.Left.Opcode == Opcode.Indir && expr.Left.Left.Opcode == Opcode.Adr)
            {
                // アドレスを得る
                var symbol = expr.Left.Left.Symbol;
                var typeInfo = expr.Left.Left.TypeInfo;
                if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                {
                    gencode($" LD L,(IY+{symbol.Address.Value})\n");
                    gencode($" LD H,(IY+{symbol.Address.Value+1})\n");
                } else {
                    gencode(" LD HL,%a\n", expr.Left.Left);
                }
            } else {
                genexp(expr.Left);
                if(expr.Left.TypeInfo.GetDataSize() == TypeDataSize.Byte)
                {
                    gencode(" LD E,(HL)\n");
                    gencode(" EX DE,HL\n");
                } else {
                    gencode(" LD E,(HL)\n");
                    gencode(" INC HL\n");
                    gencode(" LD D,(HL)\n");
                    gencode(" EX DE,HL\n");
                }
            }
        }

        // インクリメント/デクリメント式を生成する
        private void genincdec(Expr expr, bool needLvalue = true)
        {
            string incdec;
            string incdecInv;
            Opcode opcode = expr.Opcode;
            Expr incDecExpr;
            Expr targetExpr;
            Expr castExpr = null;

            if(opcode == Opcode.BtoW || opcode == Opcode.WtoB)
            {
                incDecExpr = expr.Left;
                castExpr = expr;
            } else{
                incDecExpr = expr;
            }
            targetExpr = incDecExpr.Left;
            opcode = incDecExpr.Opcode;

            if(opcode == Opcode.PreInc || opcode == Opcode.PostInc)
            {
                incdec = "INC";
                incdecInv = "DEC";
            } else {
                incdec = "DEC";
                incdecInv = "INC";
            }

            var loadByte = targetExpr.TypeInfo.GetDataSize() == TypeDataSize.Byte;
            if(opcode == Opcode.PostInc || opcode == Opcode.PostDec)
            {
                if(targetExpr.IsVariable() && targetExpr.CanLoadDirect())
                {
                    var symbol = targetExpr.Left.Symbol;
                    genld(Register.HL, targetExpr);
                    gencode($" {incdec} HL\n");

                    if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                    {
                        gencode($" LD (IY+{symbol.Address.Value}),L\n");
                        gencode($" LD (IY+{symbol.Address.Value+1}),H\n");
                    } else {
                        if(loadByte)
                        {
                            gencode(" LD A,L\n");
                            gencode(" LD %v,A\n", targetExpr.Left);
                        } else{
                            gencode(" LD %v,HL\n", targetExpr.Left);
                        }
                    }

                    if(needLvalue)
                    {
                        gencode($" {incdecInv} HL\n");
                    }
                    if(castExpr != null)
                    {
                        if(castExpr.Opcode == Opcode.BtoW)
                        {
                            genbtow(null);
                        } else{
                            genwtob(null);
                        }
                    }
                    return;
                }
                // 上記以外は普通にアドレス出して対応(いいのかなー)
                genexp(targetExpr.Left);

                // 拾う
                gencode(" LD E,(HL)\n");
                if(!loadByte)
                {
                    gencode(" INC HL\n");
                    gencode(" LD D,(HL)\n");
                }

                // INC or DEC
                if(loadByte)
                {
                    gencode($" {incdec} E\n");
                } else {
                    gencode($" {incdec} DE\n");
                }

                // 数値書き戻す
                if(!loadByte)
                {
                    gencode(" DEC HL\n");
                }
                // gencode(" POP HL\n");
                gencode(" LD (HL),E\n");
                if(!loadByte)
                {
                    gencode(" INC HL\n");
                    gencode(" LD (HL),D\n");
                }

                if(needLvalue)
                {
                    if(loadByte)
                    {
                        gencode($" {incdecInv} E\n");
                    } else {
                        gencode($" {incdecInv} DE\n");
                    }
                    gencode(" EX DE,HL\n");
                }
                if(castExpr != null)
                {
                    if(castExpr.Opcode == Opcode.BtoW)
                    {
                        genbtow(null);
                    } else{
                        genwtob(null);
                    }
                }
            } else {
                // PreInc / PreDec
                if(targetExpr.IsVariable() && targetExpr.CanLoadDirect())
                {
                    genld(Register.HL, targetExpr);
                    gencode($" {incdec} HL\n");
                    if(loadByte)
                    {
                        gencode(" LD A,L\n");
                        gencode(" LD %v,A\n", targetExpr.Left);
                    } else{
                        gencode(" LD %v,HL\n", targetExpr.Left);
                    }
                    if(castExpr != null)
                    {
                        if(castExpr.Opcode == Opcode.BtoW)
                        {
                            genbtow(null);
                        } else{
                            genwtob(null);
                        }
                    }
                    return;
                }
                genexp(targetExpr.Left);

                // HLにアドレスが出ているので保存
                gencode(" PUSH HL\n");

                // 拾う
                gencode(" LD E,(HL)\n");
                if(!loadByte)
                {
                    gencode(" INC HL\n");
                    gencode(" LD D,(HL)\n");
                }

                // INC or DEC
                if(loadByte)
                {
                    gencode($" {incdec} E\n");
                } else {
                    gencode($" {incdec} DE\n");
                }

                // 数値書き戻す
                gencode(" POP HL\n");
                gencode(" LD (HL),E\n");
                if(!loadByte)
                {
                    gencode(" INC HL\n");
                    gencode(" LD (HL),D\n");
                }

                if(needLvalue)
                {
                    gencode(" EX DE,HL\n");
                }
                if(castExpr != null)
                {
                    if(castExpr.Opcode == Opcode.BtoW)
                    {
                        genbtow(null);
                    } else{
                        genwtob(null);
                    }
                }
            }
        }

        // 通常の(ユーザー定義の)関数呼び出しを出力する
        private void genfuncallNormal(Expr func, Tree paramList)
        {
            int count = 0;
            int startAdr = 0x70;

            // パラメータが逆順に入っているので逆にする
            var exprList = new List<Expr>();
            bool requirePush = false;
            for(Tree p = paramList; p != null; p = p.First)
            {
                Expr param = p.Expr;
                exprList.Insert(0, param);
                if(param.Opcode == Opcode.Func && param.Left.Symbol.FunctionType == FunctionType.Normal)
                {
                    requirePush = true;
                }
            }


            Expr prevParam = null;
            foreach(var param in exprList)
            {
                genexp(param);
                if(requirePush)
                {
                    gencode($" PUSH HL\n");
                } else {
                    gencode($" LD (IY+{startAdr}),L\n");
                    gencode($" LD (IY+{startAdr+1}),H\n");
                }
                prevParam = param;

                startAdr += 2;
                count++;
            }

            if(requirePush)
            {
                startAdr -= 2;
                foreach(var param in exprList)
                {
                    gencode($" POP HL\n");
                    gencode($" LD (IY+{startAdr}),L\n");
                    gencode($" LD (IY+{startAdr+1}),H\n");
                    startAdr -= 2;
                }
            }

            gencall(func.Symbol.LabelName);
        }

        // MACHINE定義またはランタイム関数の呼び出しを出力する
        private void genfuncallMachine(Expr func, Tree paramList)
        {
            Register[] paramRegs = new Register[]
            {
                Register.HL,
                Register.DE,
                Register.BC,
            };

            // パラメータが逆順に入っているので逆にする
            var exprList = new List<Expr>();
            for(Tree p = paramList; p != null; p = p.First)
            {
                Expr param = p.Expr;
                exprList.Insert(0, param);
            }

            // SymbolTableのsizeが負値だとMACHINEの引数なし宣言(スタックにパラメータを積んで、数をHLに入れる)である
            var isNormalCall = func.Symbol.Size >= 0;

            // 0個:何もせずCALLのみ
            // 1個:HL
            // 2個:HL、DE
            // 3個:HL、DE、BC
            // 4個以上:(全て)スタックに詰む

            // うへー
            if(isNormalCall && exprList.Count < 4)
            {
                // 1. 直接レジスタに入らないものを先に入れてPUSHする
                int pushIdx = 0;
                foreach(var param in exprList)
                {
                    if(!param.CanLoadDirect())
                    {
                        genexp(param);
                        gencode(" PUSH HL\n");
                        pushIdx++;
                    }
                }

                // 2. 直接レジスタに入るものを入れる
                int idx = 0;
                foreach(var param in exprList)
                {
                    if(param.CanLoadDirect())
                    {
                        genld(paramRegs[idx], param);
                    }
                    idx++;
                }

                // 3. 1でPUSHしたものを正しいレジスタで拾う
                for(idx = exprList.Count - 1; idx >= 0; idx-- )
                {
                    var param = exprList[idx];
                    if(!param.CanLoadDirect())
                    {
                        gencode($" POP {paramRegs[idx].GetCode()}\n");
                    }
                }
            } else {
                // 引数の数が4つ以上の場合は全て順にスタックに詰む
                foreach(var param in exprList)
                {
                    genexp(param);
                    gencode(" PUSH HL\n");
                }

                // 引数無し宣言の場合は最後にHLに引数の数を入れる
                if(!isNormalCall)
                {
                    gencode($" LD HL,{exprList.Count}\n");
                }
            }

            if(!runtimeManager.Use(func.Symbol.Name))
            {
                // 同名のランタイムコールがあればそれを有効にし、呼び出しをランタイム名で行う
                genRuntimeCall(func.Symbol.Name);
            } else {
                // ランタイムに存在しない場合は通常関数として呼び出す
                gencode(" CALL %c\n", func);
            }
        }

        // 関数呼び出しを生成する
        private void genfuncall(Expr func, Tree paramList)
        {
            switch(func.Symbol.FunctionType)
            {
                case FunctionType.Normal:
                {
                    genfuncallNormal(func, paramList);
                    break;
                }
                case FunctionType.Machine:
                {
                    genfuncallMachine(func, paramList);
                    break;
                }
            }
        }

        // FOR文のループ部分を生成する
        private void genForloop(string forOp, Expr forIdentifier, Expr forExpr, int label)
        {
            // HLに現在のFOR変数の値が入っているのでそれを使うと良い
            if(forExpr.IsConst())
            {
                if(!forExpr.IsValueConst())
                {
                    Error("for expr is not value const");
                    return;
                }
                var forValue = forExpr.ConstValue.Value;
                var lowValue  =  forValue & 0xff;
                var highValue = (forValue >> 8) & 0xff;
                if(forOp == "TO")
                {
                    gencode(" LD A,L\n");
                    gencode($" SUB ${lowValue:X}\n");
                    gencode(" LD A,H\n");
                    gencode($" SBC A,${highValue:X}\n");
                } else {    // DOWNTO
                    gencode($" LD A,${lowValue:X}\n");
                    gencode(" SUB L\n");
                    gencode($" LD A,${highValue:X}\n");
                    gencode(" SBC A,H\n");
                }
                gencondjump(OperatorType.Word, ComparisonOp.Gt, 0, label);
            } else {
                if(forExpr.CanLoadDirect())
                {
                    if(forOp != "TO")
                    {
                        gencode($" EX DE,HL\n");
                        genld(Register.HL, forExpr);
                    } else {
                        genld(Register.DE, forExpr);
                    }
                    gencode($" OR A\n");
                    gencode($" SBC HL,DE\n");
                } else {
                    gencode(" PUSH HL\n");
                    genexp(forExpr);
                    if(forOp == "TO")
                    {
                        gencode(" EX DE,HL\n");
                        gencode(" POP HL\n");
                    } else {
                        gencode(" POP DE\n");
                    }
                    gencode($" OR A\n");
                    gencode($" SBC HL,DE\n");
                }
                gencondjump(OperatorType.Word, ComparisonOp.Gt, 0, label);
            }
        }

        // 式を生成する(トップレベルではない)
        public void genexp(Expr expr)
        {
            if(expr == null)
            {
                return;
            }
            Expr left = expr.Left;
            Expr right = expr.Right;
            if(DebugEnabled)
            {
                Console.WriteLine(" genexp:" + expr.Opcode);
            }
            switch(expr.Opcode)
            {
                case Opcode.Assign:
                {
                    genassign(expr, true);
                    break;
                }
                case Opcode.AssignOp:
                {
                    genassignop(expr, true);
                    break;
                }
                case Opcode.Const:
                {
                    if(expr.ConstValue.ConstInfoType == ConstInfoType.Code)
                    {
                        var constSymbol = symbolTableManager.SearchSymbol(expr.ConstValue.SymbolString);
                        gencode($" LD HL,{constSymbol.LabelName}\n");
                    } else {
                        gencode($" LD HL,{expr.ConstValue.Value}\n");
                    }
                    break;
                }
                case Opcode.Str:
                {
                    var strLabel = stringDataManager.GetLabel(expr.Value);
                    gencode($" LD HL,{strLabel}\n");
                    break;
                }
                case Opcode.Func:
                {
                    genfuncall(left, expr.paramList);
                    break;
                }
                case Opcode.Add:
                {
                    genadd(left, right);
                    break;
                }
                case Opcode.Sub:
                {
                    gensub(left, right);
                    break;
                }
                case Opcode.Mul:
                {
                    genmul(left, right);
                    break;
                }
                case Opcode.SMul:
                {
                    genmul(left, right, false);
                    break;
                }
                case Opcode.Div:
                {
                    gendivmod(left, right, expr.OpType, true);
                    break;
                }
                case Opcode.SDiv:
                {
                    gendivmod(left, right, expr.OpType, true, false);
                    break;
                }
                case Opcode.Mod:
                {
                    gendivmod(left, right, expr.OpType, false);
                    break;
                }
                case Opcode.SMod:
                {
                    gendivmod(left, right, expr.OpType, false, false);
                    break;
                }
                case Opcode.And:
                case Opcode.Or:
                case Opcode.Xor:
                    genbitop(expr.Opcode, left, right, expr.OpType);
                    break;
                case Opcode.High:
                case Opcode.Low:
                    genhighlow(expr.Opcode, left);
                    break;
                case Opcode.Cpl:
                {
                    gencpl(expr);
                    break;
                }
                case Opcode.Minus:
                {
                    genneg(expr);
                    break;
                }
                case Opcode.Not:
                {
                    gennot(expr);
                    break;
                }
                case Opcode.Shl:
                case Opcode.Shr:
                {
                    genshift(left, right, expr.Opcode == Opcode.Shl);
                    break;
                }
                case Opcode.SShl:
                case Opcode.SShr:
                {
                    genshift(left, right, expr.Opcode == Opcode.SShl, false);
                    break;
                }
                case Opcode.Indir:
                {
                    genindir(expr);
                    //genindirold(left);
                    break;
                }
                case Opcode.ScaleAdd:
                {
                    genscaled(expr.Opcode, expr.Left, expr.Right);
                    break;
                }
                case Opcode.Adr:
                {
                    genld(expr);
                    break;
                }
                case Opcode.Bool:
                {
                    genboolop(expr.ComparisonOp, expr.Left, expr.Right);
                    break;
                }
                case Opcode.Land:
                {
                    int labelNumber;
                    genbool(expr, 0, labelNumber = genNewLabel());
                    genlabel(labelNumber);
                    break;
                }
                case Opcode.Lor:
                {
                    int labelNumber;
                    genbool(expr, labelNumber = genNewLabel(), 0);
                    genlabel(labelNumber);
                    break;
                }
                case Opcode.DeBool:
                {
                    gendebool(expr.Left);
                    break;
                }
                case Opcode.BtoW:
                {
                    genbtow(expr.Left);
                    break;
                }
                case Opcode.WtoB:
                {
                    genwtob(expr.Left);
                    break;
                }
                case Opcode.PostInc:
                case Opcode.PostDec:
                case Opcode.PreInc:
                case Opcode.PreDec:
                {
                    genincdec(expr, true);
                    break;
                }
                case Opcode.Cond:
                {
                    gencond(expr);
                    break;
                }
                case Opcode.Comma:
                {
                    genexp(left);
                    genexp(right);
                    break;
                }
                case Opcode.PortAccess:
                {
                    genportIn(left, right);
                    break;
                }
                case Opcode.Code:
                {
                    GenerateCodeStmt(expr.paramList);
                    break;
                }
                default:
                {
                    Console.WriteLine($"unsupported op: {expr.Opcode}");
                    break;
                }
            }
        }

        // 指定レジスタへの代入を生成する
        private void genld(Register register, Expr expr, TypeDataSize dataSize = TypeDataSize.Word)
        {
            var targetReg = register.GetCode();
            var regLow    = targetReg.Substring(1, 1);
            var regHigh   = targetReg.Substring(0, 1);

            var targetExpr = expr;
            // BtoW または WtoB の場合は leftがcanLoadDirectであれば代入してからBtoW、WtoBの処理をする
            if(expr.Opcode == Opcode.WtoB || expr.Opcode == Opcode.BtoW)
            {
                targetExpr = expr.Left;
            }

            // DE or HLにexprを代入する
            if(targetExpr.IsConst())
            {
                if(targetExpr.IsValueConst())
                {
                    var val = targetExpr.ConstValue.Value;
                    switch(dataSize)
                    {
                        case TypeDataSize.Byte:
                            gencode($" LD {regLow},{val & 0xff}\n");
                            break;
                        default:
                            gencode($" LD {targetReg},{val}\n");
                            break;
                    }
                } else {
                    var constStr = GetConstStr(targetExpr);
                    if(constStr == null)
                    {
                        Error("const load error");
                        return;
                    }
                    switch(dataSize)
                    {
                        case TypeDataSize.Byte:
                            gencode($" LD {regLow},LOW {constStr}\n");
                            break;
                        default:
                            gencode($" LD {targetReg},{constStr}\n");
                            break;
                    }
                }
                return;
            }

            bool isByte;
            if(targetExpr.IsVariable())
            {
                var symbol = targetExpr.Left.Symbol;
                if(symbol.TypeInfo.IsArray())
                {
                    // Arrayの場合は直下のDataSizeでアクセスする(MEMW対策)
                    isByte = targetExpr.Left.Symbol.TypeInfo.DataSize == TypeDataSize.Byte;
                } else {
                    isByte = targetExpr.Left.Symbol.TypeInfo.GetDataSize() == TypeDataSize.Byte;
                }
                if(symbol.SymbolClass == SymbolClass.Param || symbol.SymbolClass == SymbolClass.Local)
                {
                    gencode($" LD {regLow},(IY+{symbol.Address.Value})\n");
                    gencode($" LD {regHigh},(IY+{symbol.Address.Value+1})\n");
                } else {
                    if(isByte)
                    {
                        gencode($" LD A,%v\n", targetExpr.Left);
                        gencode($" LD {regLow},A\n");
                    } else {
                        gencode($" LD {targetReg},%v\n", targetExpr.Left);
                    }
                }
            } else if(targetExpr.Opcode == Opcode.Adr)
            {
                gencode($" LD {targetReg},%a\n", targetExpr);
            } else {
                Error("can not load opcode : " + targetExpr.Opcode);
                return;
            }

            // BtoW、WtoBをする
            if(expr != targetExpr)
            {
                switch(expr.Opcode)
                {
                    case Opcode.WtoB:
                    {
                        // なんもしない
                        break;
                    }
                    case Opcode.BtoW:
                    {
                        gencode($" LD {regHigh},0\n");
                        break;
                    }
                    default:
                    {
                        Error($"can not load to {targetReg} / " + expr.Opcode);
                        break;
                    }
                }
            }
        }

        // Adrを処理する(変数のアドレスを出力する)
        protected void genld(Expr p)
        {
            SymbolTable table;
            int offset;

            if(p.Opcode != Opcode.Adr)
            {
                bug("genld");
            }
            table = p.Symbol;
            offset = p.SymbolOffset;
            if(table.SymbolClass == SymbolClass.Local || table.SymbolClass == SymbolClass.Param)
            {
                gencode($" LD HL,{table.Address.Value}\n");
                gencode(" PUSH IY\n");
                gencode(" POP DE\n");
                gencode(" ADD HL,DE\n");
            } else {
                if(table.TypeInfo.IsFunction())
                {
                    // ここに来るという事は、TempFuncとして定義されているが関数呼び出しとして認識されていない変数……のはず
                    Error("Could not found identifier : " + table.Name);
                    // 最終的なチェックから外すためにマネージャ管理から外す
                    symbolTableManager.Remove(table.Name);
                } else {
                    gencode($" LD HL,%a\n", p);
                }
            }
        }

        // HLをscale倍する
        private void genscalecode(int scale, bool saveDe)
        {
            if(scale > 1)
            {
                if(scale == 2)
                {
                    gencode(" ADD HL,HL\n");
                } else if(scale == 4)
                {
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                } else if(scale == 8)
                {
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                    gencode(" ADD HL,HL\n");
                } else {
                    if(saveDe)
                    {
                        gencode(" PUSH DE\n");
                    }
                    gencode($" LD DE,{scale}\n");
                    genRuntimeCall("MULHLDE");
                    if(saveDe)
                    {
                        gencode(" POP DE\n");
                    }
                }
            }
        }

        // 配列の計算を行う
        private void genscaled(Opcode op, Expr left, Expr right)
        {
            string addsub = op == Opcode.ScaleAdd ? "ADD" : "SUB";
            int scale = computeSize(left.TypeInfo.Parent);
            var arrayExpr = left.Left;

            // MemoryArrayは特殊処理。必ずscaleは1になる
            if(left.Symbol != null && left.Symbol.TypeInfo.IsMemoryArray())
            {
                scale = 1;
            }

            if(right.IsConst())
            {
                if(!right.IsValueConst())
                {
                    Error("const scaled error");
                    return;
                }
                genexp(left);
                gencode($" LD DE,{right.ConstValue.Value * scale}\n");
                gencode(" ADD HL,DE\n");
            } else if(right.IsVariable())
            {
                if(right.CanLoadDirect() && scale <= 2)
                {
                    if(left.Opcode == Opcode.Adr && left.Symbol.IsMemoryArray())
                    {
                        genld(Register.HL, right);
                        genscalecode(scale, false);
                    } else {
                        if(left.CanLoadDirect())
                        {
                            genld(Register.HL, right);
                            genscalecode(scale, true);
                            genld(Register.DE, left);
                        } else {
                            genexp(left);
                            gencode(" EX DE,HL\n");
                            genld(Register.HL, right);
                            genscalecode(scale, true);
                        }
                        gencode(" ADD HL,DE\n");
                    }
                } else {
                    genexp(right);
                    genscalecode(scale, false);
                    if(left.Opcode == Opcode.Adr && left.Symbol.IsMemoryArray())
                    {
                        // アドレス0からの場合は添字をそのまま使う(MEM/MEMW)
                    } else {
                        if(left.CanLoadDirect())
                        {
                            genld(Register.DE, left);
                        } else {
                            gencode(" PUSH HL\n");
                            genexp(left);
                            gencode(" POP DE\n");
                        }
                        gencode(" ADD HL,DE\n");
                    }
                }
//            } else if(right.Opcode == Opcode.Adr)
//            {
//                // なんだここ
//                Error("scaleadd error (Adr)");
            } else {
                if(left.CanLoadDirect())
                {
                    genexp(right);
                    genscalecode(scale, false);
                    genld(Register.DE, left);
                } else {
                    genexp(left);
                    if(right.CanLoadDirect())
                    {
                        genld(Register.DE, right);
                        gencode(" EX DE,HL\n");
                        genscalecode(scale, true);
                    } else {
                        gencode(" PUSH HL\n");
                        genexp(right);
                        genscalecode(scale, false);
                        gencode(" POP DE\n");
                    }
                }
                gencode(" ADD HL,DE\n");
            }
        }
    }
}