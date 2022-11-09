using System.Collections.Generic;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// Treeノードの種類
    /// </summary>
    public enum DeclNode
    {
        /// <summary>ダミー(Treeをつなぎたい時に使う)</summary>
        Dummy,
        /// <summary>識別子</summary>
        Id,
        /// <summary>ポインタ</summary>
        Ptr,
        /// <summary>関数</summary>
        Func,
        /// <summary>配列</summary>
        Array,
        /// <summary>定数</summary>
        Const,
        /// <summary>型情報</summary>
        TypeInfo,
        /// <summary>MACHINE定義</summary>
        Machine,

        None,
    }

    /// <summary>
    /// Tree情報
    /// </summary>
    public class Tree
    {
        /// <summary>
        /// Treeのノード情報
        /// </summary>
        public DeclNode Node { get; set; }

        //////////////////////////////////////
        // 変数関連
        //////////////////////////////////////
        /// <summary>
        /// 識別子の名称
        /// </summary>
        public string IdentifierName { get; set; }
        /// <summary>
        /// 識別子が配置されるアドレス(-1の場合はコンパイラが定義を行う)
        /// </summary>
        public int Address { get; set; }
        /// <summary>
        /// 初期値(無い場合はnull)
        /// </summary>
        public List<int> InitialValues { get; set; }

        /// <summary>
        /// 初期値となるCODEリスト(Tree)
        /// </summary>
        public Tree initialValueCodeTree { get; set; }

        /// <summary>
        /// 型情報
        /// </summary>
        public TypeInfo TypeInfo { get; set; }

        //////////////////////////////////////
        // 配列関連
        //////////////////////////////////////
        /// <summary>
        /// 配列のサイズ
        /// </summary>
        public int ArraySize { get; set; }

        /// <summary>
        /// ツリーの1つ目の枝
        /// </summary>
        public Tree First { get; set; }
        /// <summary>
        /// ツリーの2つ目の枝
        /// </summary>
        public Tree Second { get; set; }
        /// <summary>
        /// ツリーが持つ式情報
        /// </summary>
        public Expr Expr { get; set; }

        public Tree()
        {
            this.Node = DeclNode.Dummy;
            this.IdentifierName = null;
            this.Address = -1;
            this.InitialValues = null;
            this.initialValueCodeTree = null;
            this.TypeInfo = null;
            this.ArraySize = 0;
            this.First = null;
            this.Second = null;
            this.Expr = null;
        }


        /// <summary>
        /// ツリーにツリーを追加する(Secondの末尾に追加される)
        /// </summary>
        public Tree Append(Tree addTree)
        {
            Tree p = this;
            while(p.Second != null)
            {
                p = p.Second;
            }
            p.Second = addTree;

            return this;
        }


        /// <summary>
        /// 枝を持たないTreeを作る
        /// </summary>
        public static Tree CreateTree1(DeclNode nodeType)
        {
            Tree p = new Tree()
            {
                Node = nodeType
            };
            return p;
        }

        /// <summary>
        /// 枝を1つ持つTreeを作る
        /// </summary>
        public static Tree CreateTree2(DeclNode nodeType, Tree tree)
        {
            Tree p = new Tree()
            {
                Node = nodeType,
                First = tree
            };
            return p;
        }

        /// <summary>
        /// 枝を2つ持つTreeを作る
        /// </summary>
        public static Tree CreateTree3(DeclNode nodeType, Tree first, Tree second)
        {
            Tree p = new Tree()
            {
                Node = nodeType,
                First = first,
                Second = second
            };
            return p;
        }

        /// <summary>
        /// 枝を1つと式を持つTreeを作る
        /// </summary>
        public static Tree CreateTreeExpr(DeclNode nodeType, Tree first, Expr expr)
        {
            Tree p = new Tree()
            {
                Node = nodeType,
                First = first,
                Expr = expr
            };
            return p;
        }

        /// <summary>
        /// 型情報を持つTreeを作る
        /// </summary>
        public static Tree CreateIdentifierTypeTree(TypeDataSize dataSize, Tree tree)
        {
            var typeInfo = new TypeInfo(TypeInfoClass.Normal, 0, dataSize, null);
            var p = new Tree()
            {
                Node = DeclNode.TypeInfo,
                TypeInfo = typeInfo,
                First = tree
            };
            return p;
        }

        /// <summary>
        /// 宣言用の識別子Treeを作る
        /// </summary>
        public static Tree CreateDeclIdentifier(DeclNode nodeType, string identifierName, Expr address = null, int? initialValue = null )
        {
            Tree p = new Tree()
            {
                Node = nodeType,
                IdentifierName = identifierName
            };
            if(initialValue != null)
            {
                p.InitialValues = new List<int>(){ (int)initialValue };
            }
            p.UpdateIdentifier(address);
            return p;
        }

        /// <summary>
        /// 宣言用Treeを作る
        ///   ※DummyTreeにブラさげる
        /// </summary>
        public static Tree CreateDecl(Tree tree)
        {
            Tree p = new Tree()
            {
                Node = DeclNode.Dummy,
                First = tree
            };
            return p;
        }

        /// <summary>
        /// 関数宣言のTreeを作る
        /// </summary>
        public static Tree CreateFuncDecl(Tree tree, Expr paramCount)
        {
            Tree p = new Tree()
            {
                Node = DeclNode.Func,
                First = tree
            };
            if( paramCount != null)
            {
                if(paramCount.OpType != OperatorType.Constant)
                {
                    throw new System.Exception("MACHINE定義の引数が定数ではありません");
                }
                // arraySizeではないが、引数の数として拝借する
                p.ArraySize = paramCount.Value;
            } else {
                // 引数の数なしの宣言。この場合はスタックに引数がPUSHされ、数がHLに入る
                p.ArraySize = -1;
            }
            return p;
        }

        /// <summary>
        /// 配列宣言のTreeを作る
        /// </summary>
        public static Tree CreateArray(Tree tree, Expr size = null, Expr address = null )
        {
            Tree p = new Tree()
            {
                Node = DeclNode.Array,
                First = tree
            };
            if( size != null)
            {
                if(size.OpType != OperatorType.Constant)
                {
                    throw new System.Exception("配列宣言の添字が定数ではありません");
                }
                p.ArraySize = size.Value;
            }
            if( address != null)
            {
                if(address.OpType != OperatorType.Constant)
                {
                    throw new System.Exception("配列宣言のアドレスが定数ではありません");
                }
                p.Address = address.Value;
            }
            return p;
        }

        /// <summary>
        /// Treeが持つ初期値コードリストにTreeを設定する
        /// </summary>
        public Tree SetInitialValueCode(Tree codeTree)
        {
            this.initialValueCodeTree = codeTree;
            return this;
        }

        /// <summary>
        /// Treeが持つ識別子の情報(アドレスまたは初期値)を更新する
        /// </summary>
        public Tree UpdateIdentifier(Expr address = null, Expr initialValue = null)
        {
            if(address != null)
            {
                if(address.OpType != OperatorType.Constant)
                {
                    throw new System.Exception("識別子のアドレス指定は定数である必要があります");
                }
                this.Address = address.Value;
            }
            if(initialValue != null)
            {
                if(this.Node == DeclNode.Array)
                {
                    List<int> initValueList = new List<int>();

                    if(initialValue.Opcode == Opcode.Comma)
                    {
                        // Commaでつながれた初期値群
                        var commaTree = initialValue;
                        while(commaTree != null)
                        {
                            if(commaTree.Opcode == Opcode.Comma)
                            {
                                if(commaTree.Right == null)
                                {
                                    if(commaTree.Left != null)
                                    {
                                        commaTree = commaTree.Left;
                                        continue;
                                    }
                                }
                                var value = ((Expr)commaTree.Right).Value;
                                initValueList.Insert(0, value);
                            } else{
                                var value = commaTree.Value;
                                initValueList.Insert(0, value);
                                break;
                            }
                            commaTree = commaTree.Left;
                        }
                    } else {
                        // 単独の初期値
                        initValueList.Add(initialValue.Value);
                    }
                    this.InitialValues = initValueList;
                } else{
                    this.InitialValues = new List<int>(){ initialValue.Value };
                }
            }
            return this;
        }
    }
}
