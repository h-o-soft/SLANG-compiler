using System;
using System.Collections.Generic;

namespace SLANGCompiler.SLANG
{
    internal partial class SLANGParser
    {
        // TODO このあたりExprクラスがファクトリメソッドとして持った方がいいけど面倒なのでこのままにしておく

        // 子を1つ持つ式を作る
        private Expr makeNode1( Opcode opcode, OperatorType operatorType, TypeInfo typeInfo, Expr left)
        {
            Expr result = new Expr()
            {
                Opcode = opcode,
                OpType = operatorType,
                TypeInfo = typeInfo,
                Left = left
            };

            return result;
        }

        // 子を2つ持つ式を作る
        private Expr makeNode2( Opcode opcode, OperatorType operatorType, TypeInfo typeInfo, Expr left, Expr right)
        {
            Expr result = new Expr()
            {
                Opcode = opcode,
                OpType = operatorType,
                TypeInfo = typeInfo,
                Left = left,
                Right = right
            };
            return result;
        }

        // 子を1つとパラメータを持つ式を作る
        private Expr makeNode2( Opcode opcode, OperatorType operatorType, TypeInfo typeInfo, Expr left, Tree paramList)
        {
            Expr result = new Expr()
            {
                Opcode = opcode,
                OpType = operatorType,
                TypeInfo = typeInfo,
                Left = left,
                paramList = paramList
            };
            return result;
        }

        // 子を2つと式を1つ持つ式を作る(三項演算子)
        private Expr makeNode3( Opcode opcode, OperatorType operatorType, TypeInfo typeInfo, Expr cond, Expr a, Expr b)
        {
            Expr result = new Expr()
            {
                Opcode = opcode,
                OpType = operatorType,
                TypeInfo = typeInfo.Clone(),
                Left = cond,
                Right = a,
                Third = b,
            };
            return result;
        }

        // 演算子により左右の式を1つにまとめた定数を得る
        private int fold2(Opcode opcode, int leftValue, int rightValue)
        {
            switch(opcode)
            {
                //case Opcode.Plus: return leftValue + rightValue;
                //case Opcode.Minus: return leftValue - rightValue;
                case Opcode.Add: return leftValue + rightValue;
                case Opcode.Sub: return leftValue - rightValue;
                case Opcode.Mul:
                case Opcode.SMul:
                    return leftValue * rightValue;
                case Opcode.Div:
                    return leftValue / rightValue;
                case Opcode.SDiv:
                    return (short)leftValue / (short)rightValue;
                case Opcode.Mod:
                    return leftValue % rightValue;
                case Opcode.SMod:
                    return (short)leftValue % (short)rightValue;
                case Opcode.Shl:
                    return leftValue << rightValue;
                case Opcode.SShl:
                    return (short)leftValue << (short)rightValue;
                case Opcode.Shr:
                    return leftValue >> rightValue;
                case Opcode.SShr:
                    return (short)leftValue >> (short)rightValue;
                case Opcode.And:
                    return leftValue & rightValue;
                case Opcode.Or:
                    return leftValue | rightValue;
                case Opcode.Eq: return leftValue == rightValue ? 1 : 0;
                case Opcode.Neq: return leftValue != rightValue ? 1 : 0;

                case Opcode.Gt:
                    return leftValue > rightValue ? 1 : 0;
                case Opcode.SGt:
                    return (short)leftValue > (short)rightValue ? 1 : 0;
                case Opcode.Ge:
                    return leftValue >= rightValue ? 1 : 0;
                case Opcode.SGe:
                    return (short)leftValue >= (short)rightValue ? 1 : 0;
                case Opcode.Lt:
                    return leftValue < rightValue ? 1 : 0;
                case Opcode.SLt:
                    return (short)leftValue < (short)rightValue ? 1 : 0;
                case Opcode.Le:
                    return leftValue <= rightValue ? 1 : 0;
                case Opcode.SLe:
                    return (short)leftValue <= (short)rightValue ? 1 : 0;
            }
            bug("could not fold op : " + opcode);
            return 0;
        }

        // 演算子により式の定数を演算して返す
        private int fold1(Opcode opcode, int value)
        {
            switch(opcode)
            {
                case Opcode.Plus:
                    return value;
                case Opcode.Minus:
                    return -value;    
                case Opcode.Not:
                    return value == 0 ? 1 : 0;
                case Opcode.Cpl:
                    return ~value;
                default:
                    bug("fold1");
                    break;
            }
            return value;
        }

        // 式のコードを生成する
        private void expstmt(Expr p)
        {
            if(ErrorCount == 0)
            {
                genexptop(p);
            }
        }

        // 文字列式を作る
        private Expr expString(string str)
        {
            var strNum = stringDataManager.AddString(str);
            var result = makeNode1( Opcode.Str, OperatorType.Constant, TypeInfo.WordTypeInfo, null);
            result.Value = strNum;

            return result;
        }

        // ラベル式を作る
        public Expr expLabel(string label)
        {
            var labelNum = labelManager.DefineLabel(label);
            var result = makeNode1( Opcode.Label, OperatorType.Constant, TypeInfo.WordTypeInfo, null);
            result.Value = labelNum;
            return result;
        }

        // 定数式を作る
        public Expr expConst(Const c, TypeDataSize dataSize = TypeDataSize.Word)
        {
            TypeInfo typeInfo = dataSize == TypeDataSize.Word ? TypeInfo.WordTypeInfo : TypeInfo.ByteTypeInfo;
            Expr result = makeNode1( Opcode.Const, OperatorType.Constant, typeInfo, null );
            result.ConstValue = c;
            // TODO 本来は不必要
            result.Value = c.value;

            return result;
        }

        // 識別子式を作る(存在しない場合は一時定義関数として定義する)
        public Expr expIdent(string name)
        {

            SymbolTable table;

            // ローカル変数を優先して探す
            table = localSymbolTableManager.SearchSymbol(name);
            if(table == null)
            {
                // 無ければグローバルを探す
                table = symbolTableManager.SearchSymbol(name);
            }

            Expr e;
            if(table == null)
            {
                // 宣言されていない識別子は一時定義関数として自動定義してやり、定義時に差し替える。定義されなかった場合はエラーとなる。
                table = symbolTableManager.AddSymbol(name, TypeInfo.TempFunc, null);
            }

            // 該当シンボルが使われた(^BCなどが使われない場合、CALL関数を最適化するために利用している)
            table.Used = true;

            var typeInfo = table.TypeInfo.Clone();
            if(typeInfo.IsArray())
            {
                // 配列
                e = makeNode1(Opcode.Adr, OperatorType.Pointer, typeInfo, null);
                e.Symbol = table;
                e.SymbolOffset = 0;
                return e;
            } else if(typeInfo.IsFunction())
            {
                // 関数
                e = makeNode1(Opcode.Adr, OperatorType.Pointer, typeInfo.MakePointer(), null);
                e.Symbol = table;
                e.SymbolOffset = 0;
                return e;
            } else {
                // 単純変数または間接変数
                e = makeNode1(Opcode.Adr, OperatorType.Pointer, typeInfo.MakePointer(), null);
                e.Symbol = table;
                e.SymbolOffset = 0;
                return makeNode1(Opcode.Indir, typeInfo.ToOptype(), typeInfo, e);
            }
        }

        // 配列のインデックス加算式を作る
        public Expr expScaled(Opcode op, Expr ptr, Expr num)
        {
            int scale = computeSize(ptr.TypeInfo.Parent);

            // MemoryArrayは特殊処理。必ずscaleは1になる
            if(ptr.Symbol != null && ptr.Symbol.TypeInfo.IsMemoryArray())
            {
                scale = 1;
            }

            if(ptr.TypeInfo.IsArray())
            {
                ptr.TypeInfo = ptr.TypeInfo.Parent.MakePointer();
            }
            if(ptr.Opcode == Opcode.Adr && num.OpType == OperatorType.Constant)
            {
                if(op == Opcode.Add)
                {
                    ptr.SymbolOffset += num.Value * scale;
                } else {
                    ptr.SymbolOffset -= num.Value * scale;
                }
                return ptr;
            }
            return makeNode2( op == Opcode.Add ? Opcode.ScaleAdd : Opcode.ScaleSub, OperatorType.Pointer, ptr.TypeInfo, ptr, coerce(num, OperatorType.Word));
        }

        // 組み込み文字列関数を通常の関数呼び出しに変換(冗長な感じ)
        // ※$とか%とか/とか使えないはずなので……
        private static readonly Dictionary<string, string> stringFuncDictionary = new Dictionary<string, string>()
        {
            {"/", "PCRONE" },
            {"FORM$", "P10toN" },
            {"DECI$", "P10to5" },
            {"%",     "P10to5" },
            {"PN$",   "PSIGN" },
            {"HEX2$", "PHEX2" },
            {"HEX4$", "PHEX4" },
            {"MSG$",  "PMSG" },
            {"MSX$",  "PMSX" },
            {"!",     "PMSX" },
            {"STR$",  "PSTR" },
            {"CHR$",  "PCHR" },
            {"SPC$",  "PSPC" },
            {"CR$",   "PCR" },
            {"TAB$",  "PTAB" },
        };

        // 文字列関数呼び出し式を作る
        public Expr expStrFuncall(string funcName, Tree paramList)
        {
            string callName;
            if(!stringFuncDictionary.TryGetValue(funcName.ToUpper(), out callName))
            {
                Error($"could not found str function : " + funcName);
                return null;
            }
            return makeNode1(Opcode.StrFunc, OperatorType.Word, TypeInfo.WordTypeInfo, expFuncall( expIdent(callName), paramList ));
        }

        // 関数呼び出し式を作る
        public Expr expFuncall(Expr func, Tree paramList)
        {
            if(func == null)
            {
                return null;
            }
            TypeInfo typeInfo = func.TypeInfo.Clone();
            if(typeInfo.IsFunction())
            {
                typeInfo = typeInfo.Parent.Clone();
            } else if(typeInfo.IsPointer() && typeInfo.Parent.IsFunction())
            {
                typeInfo = typeInfo.Parent.Parent.Clone();
            } else {
                Error("function required.");
                return null;
            }

            int paramCount = 0;
            for(Tree p = paramList; p != null; p = p.First)
            {
                Expr param = p.Expr;
                if(param == null)
                {
                    return null;
                }
                if(param.TypeInfo.IsNumeric())
                {
                    p.Expr = coerce(param, OperatorType.Word);
                }
                paramCount++;
            }

            // このタイミングまでに関数が定義されていない場合は仮の引数の数を設定しておく(関数定義時に数が異なっていたらエラーを出すため)
            var funcSymbol = func.Symbol;
            if(funcSymbol.Size == 0)
            {
                funcSymbol.Size = paramCount;
            } else if(funcSymbol.Size != paramCount && funcSymbol.Size >= 0)
            {
                Error("invalid function paramater size " + funcSymbol.Name);
                return null;
            }

            Expr x = makeNode2(Opcode.Func, OperatorType.Word, TypeInfo.WordTypeInfo, func, paramList);

            // 関数の戻り値は常にWORDとする
            if(typeInfo.IsByteTypeInfo())
            {
                return coerce(x, OperatorType.Byte);
            } else {
                return x;
            }
        }

        // Bool式を作る
        private Expr enBool(Expr p)
        {
            if( p == null)
            {
                return null;
            }
            if(p.OpType == OperatorType.Bool)
            {
                return p;
            }
            ComparisonOp boolOp;
            if(p.Opcode == Opcode.Not)
            {
                // 条件を逆にする
                boolOp = ComparisonOp.Eq;
                p = p.Left;
            } else {
                boolOp = ComparisonOp.Neq;
            }
            Expr zero = makeNode1(Opcode.Const, OperatorType.Constant, TypeInfo.WordTypeInfo, null);
            zero.Value = 0;
            p = makeNode2(Opcode.Bool, OperatorType.Bool, TypeInfo.WordTypeInfo, coerce(p, OperatorType.Word), zero);
            p.ComparisonOp = boolOp;
            return p;
        }

        // Bool式を数値に変換する式を作る
        private Expr deBool(Expr p, OperatorType operatorType)
        {
            if(p == null)
            {
                return null;
            }
            if(p.OpType == OperatorType.Bool)
            {
                // コード生成時にどうするか考える
                p = makeNode1(Opcode.DeBool, operatorType, operatorType.ToType() ,p );
            }
            return p;
        }

        // 指定した式をtoTypeにキャストする
        private Expr coerce(Expr a, OperatorType toType)
        {
            OperatorType from;

            from = a.OpType;
            switch(toType)
            {
                case OperatorType.Byte:
                {
                    switch(from)
                    {
                        case OperatorType.Constant:
                            a.TypeInfo = TypeInfo.ByteTypeInfo;
                            return a;
                        case OperatorType.Byte:
                            return a;
                        case OperatorType.Word:
                            return makeNode1(Opcode.WtoB, OperatorType.Byte, TypeInfo.ByteTypeInfo, a);
                        case OperatorType.Bool:
                            return deBool(a, OperatorType.Byte);
                        default:
                            bug("coerce1");
                            break;
                    }
                    break;
                }
                case OperatorType.Word:
                {
                    switch(from)
                    {
                        case OperatorType.Constant:
                            a.TypeInfo = TypeInfo.WordTypeInfo;
                            return a;
                        case OperatorType.Word:
                        case OperatorType.Pointer:
                            return a;
                        case OperatorType.Bool:
                            return deBool(a, OperatorType.Word);
                        case OperatorType.Byte:
                        {
                            switch(a.Opcode)
                            {
                                case Opcode.Adr:
                                case Opcode.Indir:
                                case Opcode.Const:
                                case Opcode.Str:
                                case Opcode.Assign:
                                case Opcode.PreInc:
                                case Opcode.PreDec:
                                case Opcode.PostInc:
                                case Opcode.PostDec:
                                case Opcode.WtoB:
                                case Opcode.BtoW:
                                case Opcode.High:
                                case Opcode.Low:
                                    return makeNode1(Opcode.BtoW, OperatorType.Word, TypeInfo.WordTypeInfo, a);
                                case Opcode.Add:
                                case Opcode.Sub:
                                case Opcode.Mod:
                                case Opcode.Div:
                                case Opcode.Mul:
                                case Opcode.Shr:
                                case Opcode.Shl:
                                case Opcode.Gt:
                                case Opcode.Ge:
                                case Opcode.Lt:
                                case Opcode.Le:
                                case Opcode.Eq:
                                case Opcode.Neq:
                                case Opcode.And:
                                case Opcode.Xor:
                                case Opcode.Or:
                                case Opcode.Land:
                                case Opcode.Lor:
                                    a.Left  = coerce(a.Left, OperatorType.Word);
                                    a.Right = coerce(a.Right, OperatorType.Word);
                                    a.OpType = OperatorType.Word;
                                    a.TypeInfo = TypeInfo.WordTypeInfo;
                                    return a;
                                case Opcode.Plus:
                                case Opcode.Minus:
                                case Opcode.Bnot:
                                    a.Left  = coerce(a.Left, OperatorType.Word);
                                    a.OpType = OperatorType.Word;
                                    a.TypeInfo = TypeInfo.WordTypeInfo;
                                    return a;
                                case Opcode.Cond:
                                    a.Right  = coerce(a.Right, OperatorType.Word);
                                    a.Third = coerce(a.Third, OperatorType.Word);
                                    a.OpType = OperatorType.Word;
                                    a.TypeInfo = TypeInfo.WordTypeInfo;
                                    return a;
                                case Opcode.PortAccess:
                                    return a;
                                case Opcode.DeBool:
                                    return a;
                                default:
                                    bug("coerce2 : " + a.Opcode);
                                    if(a.Left != null)
                                    {
                                        bug($"  left op={a.Left.Opcode}" );
                                    }
                                    break;
                            }
                            break;
                        }
                        default:
                            bug($"coerce3 : op={from}" );
                            break;
                    }
                    break;
                }
                case OperatorType.Constant:
                    if(from != OperatorType.Constant)
                    {
                        bug("coerce4");
                    }
                    break;
                default:
                    bug("coerce5:" + toType);
                    break;
            }
            return a;
        }

        // 間接変数式を作る
        private Expr expIndirect(Expr p)
        {
            TypeInfo typeInfo;

            if(p == null)
            {
                return null;
            }
            typeInfo = p.TypeInfo;
            if(typeInfo.IsArray())
            {
                p.TypeInfo = typeInfo =  typeInfo.Parent.MakePointer();
            }
            if(!typeInfo.IsPointer())
            {
                Error("pointer required");
                return null;
            }

            typeInfo = typeInfo.Parent;
            if(typeInfo.IsArray())
            {
                p.TypeInfo = typeInfo;
                return p;
            }
            return makeNode1(Opcode.Indir, typeInfo.ToOptype(), typeInfo, p);
        }


        // 配列式またはポートアクセス式を作る
        private Expr expArray(Expr a, Expr b)
        {
            TypeInfo ltype, rtype;
            if(a == null || b == null)
            {
                return null;
            }
            ltype = a.TypeInfo.Clone();
            rtype = b.TypeInfo.Clone();
            if(ltype.IsPortArray())
            {
                // ポートアクセスOpcode(IN/OUT)
                return makeNode2(Opcode.PortAccess, a.OpType, ltype.Clone(), a, b);
            }
            if(ltype.IsArray() || ltype.IsIndirect())
            {
                a.TypeInfo = ltype = ltype.Parent.MakePointer();
            }
            if(rtype.IsArray() || rtype.IsIndirect())
            {
                b.TypeInfo = rtype = rtype.Parent.MakePointer();
            }
            if(ltype.IsPointer() && rtype.IsNumeric())
            {
                // 正常
            } else if(ltype.IsNumeric() && rtype.IsPointer())
            {
                Expr tmp = a;
                a = b;
                b = tmp;
            } else {
                Error("[]: type mismatch :\n ltype:" + ltype + "\n rtype:" + rtype );
                return null;
            }
            return expIndirect(expScaled(Opcode.Add, a, b));
        }

        // NOT / CPL / PLUS / MINUS式を作る
        private Expr expUnary(Opcode opcode, Expr expr)
        {
            if(expr == null)
            {
                return null;
            }
            if(!expr.TypeInfo.IsNumeric())
            {
                Error($"{opcode} type mismatch");
                return null;
            }
            if(expr.IsValueConst())
            {
                expr.Value = fold1(opcode, expr.Value);
                return expr;
            }
            if(opcode == Opcode.Not)
            {
                return logNot(enBool(expr));
            }
            return makeNode1(opcode, OperatorType.Word, expr.TypeInfo, coerce(expr, OperatorType.Word));
        }

        // 論理NOT式を作る
        private Expr logNot(Expr expr)
        {
            switch(expr.Opcode)
            {
                case Opcode.Bool:
                {
                    expr.ComparisonOp = expr.ComparisonOp ^ ComparisonOp.Not;
                    break;
                }
                case Opcode.Land:
                {
                    logNot(expr.Left);
                    logNot(expr.Right);
                    expr.Opcode = Opcode.Lor;
                    break;
                }
                case Opcode.Lor:
                {
                    logNot(expr.Left);
                    logNot(expr.Right);
                    expr.Opcode = Opcode.Land;
                    break;
                }
            }
            return expr;
        }

        // インクリメント/デクリメント式を作る
        private Expr expIncdec(Opcode opcode, Expr expr)
        {
            TypeInfo typeInfo;

            if(expr == null)
            {
                return null;
            }
            typeInfo = expr.TypeInfo.Clone();
            return makeNode1(opcode, typeInfo.ToOptype(), typeInfo, expr);
        }

        // アドレス参照式を作る
        private Expr expAddrof(Expr expr)
        {
            if(expr == null)
            {
                return null;
            }

            // 配列は特殊なつながり方をしている(やめた方が良さそう)
            if(expr.Opcode == Opcode.Adr && expr.Symbol != null && expr.Symbol.TypeInfo.IsArray())
            {
                return expr;
            }

            if(expr.Opcode != Opcode.Indir)
            {
                Error("&: l-value required : " + expr.Opcode + ":" + expr.Left.TypeInfo);
                return null;
            }
            return expr.Left;
        }

        // HIGH / LOW 式を作る
        private Expr expHighlow(Opcode opcode, Expr expr)
        {
            if(expr == null)
            {
                return null;
            }
            // 事前最適化
            if(expr.IsValueConst())
            {
                int value;
                if(opcode == Opcode.Low)
                {
                    value = expr.Value & 0xff;
                } else{ // Opcode.High
                    value = (expr.Value >> 8) & 0xff;
                }
                expr.Value = value;
                return expr;
            }
            return makeNode1(opcode, OperatorType.Byte, expr.TypeInfo, expr);
        }

        // 加算、減算式を作る
        private Expr expAddsub(Opcode opcode, Expr left, Expr right)
        {
            if( left == null || right == null)
            {
                return null;
            }
            TypeInfo ltype = left.TypeInfo;
            TypeInfo rtype = right.TypeInfo;
            // 加減算をまとめる
            if(left.IsValueConst() && right.IsValueConst())
            {
                left.Value = fold2(opcode, left.Value, right.Value);
                return left;
            }
            // 強制的にWORD同士の演算にする
            var opType = OperatorType.Word;
            return makeNode2(opcode, opType, opType.ToType(), coerce(left, opType), coerce(right, opType) );
        }

        // 左右シフト式を作る
        private Expr expShiftop(Opcode opcode, Expr left, Expr right)
        {
            if( left == null || right == null)
            {
                return null;
            }
            TypeInfo ltype = left.TypeInfo;
            TypeInfo rtype = right.TypeInfo;
            // 定数はまとめる
            if(left.IsValueConst() && right.IsValueConst())
            {
                left.Value = fold2(opcode, left.Value, right.Value);
                return left;
            }
            var opType = OperatorType.Word;
            return makeNode2(opcode, opType, opType.ToType(),  left, right );
        }

        // カンマ式を作る
        private Expr expComma(Expr left, Expr right)
        {
            return makeNode2(Opcode.Comma, right.OpType, right.TypeInfo, left, right);
        }

        // 符号つき乗算、除算、剰余算数式を作る
        private Expr expSBinary(Opcode opcode, Expr left, Expr right)
        {
            if( left == null || right == null)
            {
                return null;
            }
            // 乗除算をまとめる
            if(left.IsValueConst() && right.IsValueConst())
            {
                left.Value = fold2(opcode, left.Value, right.Value);
                return left;
            }
            OperatorType opType = adjust(left.OpType, right.OpType);
            // とりあえずBoolをAND/OR/XOR等する場合はByteにしてみる(うーん？)
            if(opType == OperatorType.Bool)
            {
                opType = OperatorType.Byte;
            }
            // この計算についてはWORD専用の関数を呼ぶため、WORDに拡張しないと駄目
            if(opcode == Opcode.Mul || opcode == Opcode.Div || opcode == Opcode.Mod)
            {
                opType = OperatorType.Word;
            }
            return makeNode2(opcode, opType, opType.ToType(), coerce(left, opType), coerce(right, opType) );
        }


        // 符号なし乗算、除算、剰余算数式を作る
        private Expr expBinary(Opcode opcode, Expr left, Expr right)
        {
            if( left == null || right == null)
            {
                return null;
            }
            // 乗除算をまとめる
            if(left.IsValueConst() && right.IsValueConst())
            {
                left.Value = fold2(opcode, left.Value, right.Value);
                return left;
            }
            var opType = adjust(left.OpType, right.OpType);

            // BoolのANDまたはORの場合はLAnd、LOrに差し替えて戻す
            if(left.OpType == OperatorType.Bool && right.OpType == OperatorType.Bool && (opcode == Opcode.And || opcode == Opcode.Or))
            {
                return makeNode2(opcode == Opcode.And ? Opcode.Land : Opcode.Lor, opType, TypeInfo.WordTypeInfo, left, right );
            }
            // とりあえずBoolをAND/OR/XOR等する場合はByteにしてみる(えーと？)
            if(opType == OperatorType.Bool)
            {
                opType = OperatorType.Byte;
            }
            // この計算についてはWORD専用の関数を呼ぶため、WORDに拡張しないと駄目
            if(opcode == Opcode.Mul || opcode == Opcode.Div || opcode == Opcode.Mod)
            {
                opType = OperatorType.Word;
            }
            return makeNode2(opcode, opType, opType.ToType(), coerce(left, opType), coerce(right, opType) );
        }

        // 比較式を作る
        // ※全体的に比較は怪しい
        private Expr expCompare(Opcode opcode, Expr left, Expr right)
        {
            if( left == null || right == null)
            {
                return null;
            }
            if(left.IsValueConst() && right.IsValueConst())
            {
                left.Value = fold2(opcode, left.Value, right.Value);
                return left;
            }

            ComparisonOp boolOp = ComparisonOp.Eq;
            switch(opcode)
            {
                case Opcode.Eq:
                {
                    boolOp = ComparisonOp.Eq;
                    break;
                }
                case Opcode.Neq:
                {
                    boolOp = ComparisonOp.Neq;
                    break;
                }
                case Opcode.Gt:
                {
                    boolOp = ComparisonOp.Gt;
                    break;
                }
                case Opcode.Ge:
                {
                    var tmp = left;
                    left = right;
                    right = tmp;
                    boolOp = ComparisonOp.Le;
                    break;
                }
                case Opcode.Lt:
                {
                    var tmp = left;
                    left = right;
                    right = tmp;
                    boolOp = ComparisonOp.Gt;
                    break;
                }
                case Opcode.Le:
                {
                    boolOp = ComparisonOp.Le;
                    break;
                }
                case Opcode.SGt:
                {
                    boolOp = ComparisonOp.SGt;
                    break;
                }
                case Opcode.SGe:
                {
                    var tmp = left;
                    left = right;
                    right = tmp;
                    boolOp = ComparisonOp.SLe;
                    break;
                }
                case Opcode.SLt:
                {
                    var tmp = left;
                    left = right;
                    right = tmp;
                    boolOp = ComparisonOp.SGt;
                    break;
                }
                case Opcode.SLe:
                {
                    boolOp = ComparisonOp.SLe;
                    break;
                }
                default:
                    Error("compare error " + opcode);
                    break;
            }

            var opType =  OperatorType.Word;
            var p = makeNode2(Opcode.Bool, OperatorType.Bool, TypeInfo.WordTypeInfo.Clone(), coerce(left, opType), coerce(right, opType));
            p.ComparisonOp = boolOp;
            return p;
        }

        // 論理AND、論理OR式を作る
        private Expr expLogop(Opcode opcode, Expr left, Expr right)
        {
            if(left == null || right == null)
            {
                return null;
            }
            if(!left.TypeInfo.IsNumeric() || !right.TypeInfo.IsNumeric())
            {
                Error("{opcode} type mismatch");
                return null;
            }
            return makeNode2(opcode, OperatorType.Bool, TypeInfo.WordTypeInfo, left, right);
        }

        // 三項演算子式を作る
        private Expr expConditional(Expr cond, Expr left, Expr right)
        {
            if(cond == null || left == null || right == null)
            {
                return null;
            }
            var ltype = left.TypeInfo;
            var rtype = right.TypeInfo;
            if(!cond.TypeInfo.IsNumeric())
            {
                Error("?: condition must be numeric type");
                return null;
            }
            cond = enBool(cond);
            var optype = adjust(left.OpType, right.OpType);
            left = coerce(left, optype);
            right = coerce(right, optype);
            return makeNode3(Opcode.Cond, optype, optype.ToType(), cond, left, right);
        }
        

        // 演算代入式を作る
        private Expr expAssignOp(string opStr, Expr left, Expr right)
        {
            var opDic = new Dictionary<string, Opcode>()
            {
                {"+=", Opcode.Add},
                {"-=", Opcode.Sub},
                {"*=", Opcode.Mul},
                {"/=", Opcode.Div},
            };

            if(left == null || right == null)
            {
                return null;
            }
            if(left.Opcode != Opcode.Indir)
            {
                Error($"l-value required. {left.Opcode}");
                return null;
            }

            Opcode opcode;
            if(!opDic.TryGetValue(opStr, out opcode))
            {
                Error($"not assign op {opStr}");
                return null;
            }
            var ltype = left.TypeInfo;
            var rtype = right.TypeInfo;

            OperatorType opType = OperatorType.Word;

            if((ltype.IsNumeric() || ltype.IsPointer()) && (rtype.IsNumeric() || rtype.IsPointer()))
            {
                var result = makeNode2(Opcode.AssignOp, ltype.ToOptype(), ltype, left, coerce(right, opType));
                result.AssignOpCode = opcode;
                return result;
            }
            Error("=: type mismatch");
            return null;
        }

        // 代入式を作る
        private Expr expAssign(Expr left, Expr right)
        {
            if(left == null || right == null)
            {
                return null;
            }

            var ltype = left.TypeInfo;
            var rtype = right.TypeInfo;
            if(left.Opcode == Opcode.PortAccess)
            {
                return makeNode2(Opcode.Assign, ltype.ToOptype(), ltype, left, coerce(right, ltype.ToOptype()) );
            }
            if(left.Opcode != Opcode.Indir)
            {
                Console.WriteLine("l-value required " + left.Opcode);
                return null;
            }
            if((ltype.IsNumeric() || ltype.IsPointer()) && (rtype.IsNumeric() || rtype.IsPointer()))
            {
                return makeNode2(Opcode.Assign, ltype.ToOptype(), ltype, left, coerce(right, ltype.ToOptype()) );
            }
            Error("=: type mismatch " + ltype + "\n:" + rtype);
            return null;
        }

        private Expr expCode(Tree tree)
        {
            var expr = makeNode1(Opcode.Code, OperatorType.Word, TypeInfo.WordTypeInfo, null);
            expr.paramList = tree;
            return expr;
        }

        public Expr LastConstExpr { get; private set; }
        // 最終評価されたCONST式を保存しておく
        void setConstExpr(Expr expr)
        {
            LastConstExpr = expr;
        }
    }
}
