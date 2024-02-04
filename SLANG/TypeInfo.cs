
namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// 型のInfoClass(種類)
    /// </summary>
    public enum TypeInfoClass
    {
        /// <summary>単純変数</summary>
        Normal,
        /// <summary>間接変数</summary>
        Indirect,

        /// <summary>ポインタ</summary>
        Pointer,
        /// <summary>配列変数</summary>
        Array,
        /// <summary>I/Oポートアクセス配列</summary>
        PortArray,
        /// <summary>メモリアクセス配列</summary>
        MemoryArray,
        /// <summary>関数</summary>
        Function,

        /// <summary>一時定義関数(定義前に呼び出された関数)</summary>
        TempFunc,
    }

    /// <summary>
    /// 型の基準サイズ
    /// </summary>
    public enum TypeDataSize
    {
        /// <summary>バイト(1byte)</summary>
        Byte,
        /// <summary>ワード(2bytes)</summary>
        Word,
        /// <summary>浮動小数点(3bytes)</summary>
        Float
    }

    public static partial class TypeDataSizeExtend {
            /// <summary>
            /// TypeDataSizeの保存に必要なメモリバイト数を返す
            /// </summary>
            public static int GetDataSize(this TypeDataSize param)
            {
                switch(param)
                {
                    case TypeDataSize.Byte:
                        return 1;
                    case TypeDataSize.Word:
                        return 2;
                    case TypeDataSize.Float:
                        return 3;
                }
                return 2;
            }
    }
    /// <summary>
    /// 型情報クラス
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// 型の種類
        /// </summary>
        public TypeInfoClass InfoClass { get; private set; }
        /// <summary>
        /// 型の親情報(複数の型情報をつなげて全体の型情報が作られる)
        /// </summary>
        public TypeInfo Parent { get; private set; }
        /// <summary>
        /// この型単独でのサイズ(配列の場合は個数)
        /// </summary>
        public int Size { get; private set; }
        /// <summary>
        /// この型の基準サイズ(Byte or Word)
        /// </summary>
        public TypeDataSize DataSize { get; private set; }

        /// <summary>
        /// <para>型情報を複製する</para>
        /// <para>本来は構造体にした方が良さそう。</para>
        /// </summary>
        public TypeInfo Clone()
        {
            var parent = this.Parent;
            if(parent != null)
            {
                parent = parent.Clone();
            }
            var result = new TypeInfo(this.InfoClass, this.Size, this.DataSize, this.Parent);
            return result;
        }

        /// <summary>
        /// 型情報を文字列で返す
        /// </summary>
        public override string ToString()
        {
            string result = "";
            var typeInfo = this;
            bool isFirst = true;
            while(typeInfo != null)
            {
                if(!isFirst)
                {
                    result += "\n";
                }
                isFirst = false;
                result += $"    [{typeInfo.GetType()}] typeInfoClass:{typeInfo.InfoClass} dataSize:{typeInfo.DataSize} size:{typeInfo.Size}";
                typeInfo = typeInfo.Parent;
            }
            return result;
        }

        /// <summary>
        /// この型が単純変数のByte型の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsByteTypeInfo()
        {
            return (InfoClass == TypeInfoClass.Normal && DataSize == TypeDataSize.Byte);
        }

        /// <summary>
        /// この型が単純変数のWord型の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsWordTypeInfo()
        {
            return (InfoClass == TypeInfoClass.Normal && DataSize == TypeDataSize.Word);
        }

        /// <summary>
        /// この型が単純変数のFloat型の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsFloatTypeInfo()
        {
            return (InfoClass == TypeInfoClass.Normal && DataSize == TypeDataSize.Float);
        }

        /// <summary>
        /// この型が単純変数(Byte or Word)の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsPrimeType()
        {
            return IsByteTypeInfo() || IsWordTypeInfo();
        }

        /// <summary>
        /// この型が関数の場合true、そうでない場合falseを返す。一時定義関数の場合もtrueとなる。
        /// </summary>
        public bool IsFunction()
        {
            return InfoClass == TypeInfoClass.Function || InfoClass == TypeInfoClass.TempFunc;
        }

        /// <summary>
        /// この型がポインタの場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsPointer()
        {
            return this.InfoClass == TypeInfoClass.Pointer;
        }

        /// <summary>
        /// この型が配列またはメモリ配列の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsArray()
        {
            return this.InfoClass == TypeInfoClass.Array || this.InfoClass == TypeInfoClass.MemoryArray;
        }

        /// <summary>
        /// この型が間接変数の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsIndirect()
        {
            return this.InfoClass == TypeInfoClass.Indirect;
        }

        /// <summary>
        /// この型が間接変数の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsIndirectType()
        {
            var parent = this.Parent;
            while(parent != null)
            {
                if(parent.InfoClass == TypeInfoClass.Indirect)
                {
                    return true;
                }
                parent = parent.Parent;
            }
            return this.InfoClass == TypeInfoClass.Indirect;
        }

        /// <summary>
        /// この型がI/Oポートアクセス配列の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsPortArray()
        {
            return this.InfoClass == TypeInfoClass.PortArray;
        }

        /// <summary>
        /// この型がメモリアクセス配列の場合true、そうでない場合falseを返す
        /// </summary>
        public bool IsMemoryArray()
        {
            return this.InfoClass == TypeInfoClass.MemoryArray;
        }

        /// <summary>
        /// <para>数値化出来る型の場合true、そうでない場合falseを返す</para>
        /// <para>現状常にtrueを返すので無意味である(要整理)</para>
        /// </summary>
        public bool IsNumeric()
        {
            return true;
        }

        /// <summary>
        /// 自身をポインタ化した型を返す
        /// </summary>
        public TypeInfo MakePointer()
        {
            if(this == TypeInfo.ByteTypeInfo)
            {
                return TypeInfo.PtrToByte;
            } else if(this == TypeInfo.WordTypeInfo)
            {
                return TypeInfo.PtrToWord;
            } else if(this == TypeInfo.IndirectByteTypeInfo)
            {
                return TypeInfo.PtrToIndirectByte;
            } else if(this == TypeInfo.IndirectWordTypeInfo)
            {
                return TypeInfo.PtrToIndirectWord;
            }
            // Cloneしなくてもいい気もするが念のため……
            return TypeInfo.CreateTypeInfo(TypeInfoClass.Pointer, this.Clone());
        }

        /// <summary>
        /// この型の基準サイズを返す
        /// </summary>
        public TypeDataSize GetDataSize()
        {
            // 根本にTypeInfoが存在しており、そのTypeInfoが持つDataSizeが、この型が持つベースの型となる(Byte or Word)
            TypeDataSize dataSize = TypeDataSize.Word;
            var parent = this;
            while(parent != null)
            {
                dataSize = parent.DataSize;
                parent = parent.Parent;
            }
            return dataSize;
        }

        /// <summary>
        /// 型情報クラスを生成する(newすればいい気がする)
        /// </summary>
        public static TypeInfo CreateTypeInfo(TypeInfoClass tc, TypeInfo parent, int size = 0, TypeDataSize dataSize = TypeDataSize.Word)
        {
            return new TypeInfo(tc, size, dataSize, parent);
        }

        /// <summary>
        /// この型のOperatorTypeを返す
        /// </summary>
        public OperatorType ToOptype()
        {
            // TODO これは GetDataSize() で返ってくる型のサイズを元に返してやった方がいい気がする

            if((this.InfoClass == TypeInfoClass.Normal || this.InfoClass == TypeInfoClass.Indirect) && this.DataSize == TypeDataSize.Byte)
            {
                return OperatorType.Byte;
            } else if((this.InfoClass == TypeInfoClass.Normal || this.InfoClass == TypeInfoClass.Indirect) && this.DataSize == TypeDataSize.Word)
            {
                return OperatorType.Word;
            } else if((this.InfoClass == TypeInfoClass.Normal || this.InfoClass == TypeInfoClass.Indirect) && this.DataSize == TypeDataSize.Float)
            {
                return OperatorType.Float;
            } else if(this.InfoClass == TypeInfoClass.Array)
            {
                if(this.DataSize == TypeDataSize.Byte)
                {
                    return OperatorType.Byte;
                } else 
                {
                    return OperatorType.Word;
                }
            } else if(this.InfoClass == TypeInfoClass.MemoryArray)
            {
                // Byteのみを返すべきか？
                if(this.DataSize == TypeDataSize.Byte)
                {
                    return OperatorType.Byte;
                } else 
                {
                    return OperatorType.Word;
                }
            } else if(this.InfoClass == TypeInfoClass.PortArray)
            {
                if(this.DataSize == TypeDataSize.Byte)
                {
                    return OperatorType.Byte;
                } else 
                {
                    return OperatorType.Word;
                }
            }
            throw new System.Exception("ToOptype");
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TypeInfo(TypeInfoClass infoClass, int size, TypeDataSize dataSize, TypeInfo parent)
        {
            this.InfoClass = infoClass;
            this.Size = size;
            this.DataSize = dataSize;
            this.Parent = parent;
        }

        // 基本となる型を生成しておく
        static TypeInfo()
        {
            ByteTypeInfo = CreateTypeInfo(TypeInfoClass.Normal, null, 1, TypeDataSize.Byte);
            WordTypeInfo = CreateTypeInfo(TypeInfoClass.Normal, null, 2, TypeDataSize.Word);
            FloatTypeInfo = CreateTypeInfo(TypeInfoClass.Normal, null, 3, TypeDataSize.Float);

            IndirectByteTypeInfo = CreateTypeInfo(TypeInfoClass.Indirect, null, 1, TypeDataSize.Byte);
            IndirectWordTypeInfo = CreateTypeInfo(TypeInfoClass.Indirect, null, 2, TypeDataSize.Word);
            IndirectFloatTypeInfo = CreateTypeInfo(TypeInfoClass.Indirect, null, 3, TypeDataSize.Float);

            PtrToByte = CreateTypeInfo(TypeInfoClass.Pointer, ByteTypeInfo);
            PtrToWord = CreateTypeInfo(TypeInfoClass.Pointer, WordTypeInfo);
            PtrToFloat = CreateTypeInfo(TypeInfoClass.Pointer, FloatTypeInfo);

            PtrToIndirectByte = CreateTypeInfo(TypeInfoClass.Pointer, IndirectByteTypeInfo);
            PtrToIndirectWord = CreateTypeInfo(TypeInfoClass.Pointer, IndirectWordTypeInfo);
            PtrToIndirectFloat = CreateTypeInfo(TypeInfoClass.Pointer, IndirectFloatTypeInfo);

            PortByte = CreateTypeInfo(TypeInfoClass.PortArray, ByteTypeInfo.Clone(), 1, TypeDataSize.Byte);
            PortWord = CreateTypeInfo(TypeInfoClass.PortArray, WordTypeInfo.Clone(), 2, TypeDataSize.Word);

            MemoryByte = CreateTypeInfo(TypeInfoClass.MemoryArray, ByteTypeInfo.Clone(), 1, TypeDataSize.Byte);
            MemoryWord = CreateTypeInfo(TypeInfoClass.MemoryArray, WordTypeInfo.Clone(), 2, TypeDataSize.Word);
            MemoryFloat = CreateTypeInfo(TypeInfoClass.MemoryArray, WordTypeInfo.Clone(), 3, TypeDataSize.Float);

            TempFunc = CreateTypeInfo(TypeInfoClass.TempFunc, WordTypeInfo.Clone(), 2, TypeDataSize.Word);
        }

        /// <summary>
        /// 単純BYTE型
        /// </summary>
        public static TypeInfo ByteTypeInfo { get; private set; }
        /// <summary>
        /// 単純WORD型
        /// </summary>
        public static TypeInfo WordTypeInfo { get; private set; }
        /// <summary>
        /// 単純FLOAT型
        /// </summary>
        public static TypeInfo FloatTypeInfo { get; private set; }
        /// <summary>
        /// 間接変数BYTE型
        /// </summary>
        public static TypeInfo IndirectByteTypeInfo { get; private set; }
        /// <summary>
        /// 間接変数WORD型
        /// </summary>
        public static TypeInfo IndirectWordTypeInfo { get; private set; }
        /// <summary>
        /// 間接変数FLOAT型
        /// </summary>
        public static TypeInfo IndirectFloatTypeInfo { get; private set; }
        /// <summary>
        /// BYTEポインタ型
        /// </summary>
        public static TypeInfo PtrToByte { get; private set; }
        /// <summary>
        /// WORDポインタ型
        /// </summary>
        public static TypeInfo PtrToWord { get; private set; }
        /// <summary>
        /// FLOATポインタ型
        /// </summary>
        public static TypeInfo PtrToFloat { get; private set; }
        /// <summary>
        /// BYTE間接変数ポインタ型
        /// </summary>
        public static TypeInfo PtrToIndirectByte { get; private set; }
        /// <summary>
        /// WORD間接変数ポインタ型
        /// </summary>
        public static TypeInfo PtrToIndirectWord { get; private set; }
        /// <summary>
        /// FLOAT間接変数ポインタ型
        /// </summary>
        public static TypeInfo PtrToIndirectFloat { get; private set; }
        /// <summary>
        /// I/OポートBYTE配列型
        /// </summary>
        public static TypeInfo PortByte { get; private set; }
        /// <summary>
        /// I/OポートWORD配列型
        /// </summary>
        public static TypeInfo PortWord { get; private set; }
        /// <summary>
        /// メモリBYTE配列型
        /// </summary>
        public static TypeInfo MemoryByte { get; private set; }
        /// <summary>
        /// メモリWORD配列型
        /// </summary>
        public static TypeInfo MemoryWord { get; private set; }
        /// <summary>
        /// メモリFLOAT配列型
        /// </summary>
        public static TypeInfo MemoryFloat { get; private set; }
        /// <summary>
        /// 一時定義関数型
        /// </summary>
        public static TypeInfo TempFunc { get; private set; }
    }
}
