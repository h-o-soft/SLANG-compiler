using System.Collections.Generic;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// シンボルのクラス
    /// </summary>
    public enum SymbolClass
    {
        Global,
        Local,
        Param
    }

    /// <summary>
    /// 関数の種別
    /// </summary>
    public enum FunctionType
    {
        /// <summary>ユーザー定義の関数</summary>
        Normal,
        /// <summary>MACHINE宣言された関数 or ランタイムの関数</summary>
        Machine,
    }

    /// <summary>
    /// シンボルテーブル
    /// </summary>
    public class SymbolTable
    {
        /// <summary>
        /// ラベル名のヘッダ(関数ごとに変数を分離するために使われる)
        /// </summary>
        public string LabelHeader { get; set; }

        /// <summary>
        /// アセンブラソース内で使われるラベル名称
        /// </summary>
        public string LabelName
        {
            get
            {
                if(UseOriginalSymbol)
                {
                    // UseOriginalSymbolがtrueの場合はLabelHeaderは使われないので注意
                    return normalizeName;
                } else {
                    return "_" + LabelHeader+"_SYM"+Id;
                }
            }
        }

        /// <summary>
        /// アセンブラソース内で使ってはいけない文字を置き換えて、利用可能な名前に変換する
        /// </summary>
        private string normalizeName {
            get
            {
                return Name.Replace('^', '_');
            }
        }

        /// <summary>
        /// シンボル定義名
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// シンボルの別名
        /// </summary>
        public List<string> AliasNameList { get; set; }

        /// <summary>
        /// シンボルの内部名称(ランタイムにて、プログラムで使われる名称と実際のラベル名称が異なる場合の、ラベル名称)
        /// </summary>
        public string InsideName { get; set; }

        /// <summary>
        /// シンボルID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// ランタイム関数において実際にアセンブラソースで使われる名称
        /// </summary>
        public string RuntimeName
        {
            get {
                if(string.IsNullOrEmpty(InsideName))
                {
                    return Name;
                }
                return InsideName;
            }
        }

        /// <summary>
        /// シンボルのクラス(グローバル or ローカル or パラメータ)
        /// </summary>
        public SymbolClass SymbolClass { get; set; }

        /// <summary>
        /// シンボルの型情報
        /// </summary>
        public TypeInfo TypeInfo { get; set; }

        /// <summary>
        /// シンボルの存在するアドレス位置(-1の場合は適宜定義される)
        /// </summary>
        public ConstInfo Address { get; set; } 

        /// <summary>
        /// シンボルのサイズ(バイト)
        /// </summary>
        public int Size;

        /// <summary>
        /// シンボルの初期化値(単独)
        /// </summary>
        public List<int> InitialValueList { get; set; }

        /// <summary>
        /// シンボルの初期化値(CODE)
        /// </summary>
        public Tree InitialValueCode { get; set; }

        /// <summary>
        /// シンボルがプログラム内で使われたかどうか
        /// </summary>
        public bool Used { get; set; }

        /// <summary>
        /// シンボル名称を変数名、関数名などそのまま使うか、シンボル番号で置換するか
        /// </summary>
        public bool UseOriginalSymbol { get; set; }

        /// <summary>
        /// シンボルが関数の場合、ユーザー定義の関数か、MACHINE定義(or ランタイム)の関数かを示す
        /// </summary>
        public FunctionType FunctionType;

        /// <summary>
        /// ランタイムシンボルかどうか
        /// </summary>
        public bool IsRuntime;

        /// <summary>
        /// ネームスペース名称
        /// </summary>
        public string NamespaceName;

        public bool IsMemoryArray()
        {
             return Address != null && Address.ConstInfoType == ConstInfoType.IntValue && Address.Value == 0;
        }

        public SymbolTable AddAliasName(string name)
        {
            if(AliasNameList == null)
            {
                AliasNameList = new List<string>();
            }
            AliasNameList.Add(name);
            return this;
        }

        public SymbolTable()
        {
            this.LabelHeader = null;
            this.Name = null;
            this.AliasNameList = null;
            this.InsideName = null;
            this.SymbolClass = SymbolClass.Global;
            this.TypeInfo = null;
            this.Address = null;
            this.Size = 0;
            this.InitialValueList = null;
            this.InitialValueCode = null;
            this.Used = false;
            this.FunctionType = FunctionType.Normal;
            this.UseOriginalSymbol = false;
            this.IsRuntime = false;
        }

        /// <summary>
        /// このシンボルが一時定義関数の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsTempFunction()
        {
            var parent = TypeInfo;
            while(parent != null)
            {
                if(parent.InfoClass == TypeInfoClass.TempFunc)
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return false;
        }
    }
}
