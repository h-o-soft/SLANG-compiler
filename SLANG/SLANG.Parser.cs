using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using UtfUnknown;

namespace SLANGCompiler.SLANG
{
    /// <summary>
    /// SLANGパーサクラス
    /// </summary>
    internal partial class SLANGParser: ILabelCreator, IErrorReporter, IORGSetter, IWORKSetter
    {
        private static readonly bool DebugEnabled = false;

        /// <summary>
        /// コードを蓄積するリポジトリ
        /// </summary>
        CodeRepository codeRepository;
        /// <summary>
        /// ラベル管理用マネージャ(関数内で定義されるラベル用)
        /// </summary>
        protected LabelManager labelManager;
        /// <summary>
        /// 文字列を保存するマネージャ
        /// </summary>
        StringDataManager stringDataManager;
        /// <summary>
        /// ランタイムを管理するマネージャ
        /// </summary>
        RuntimeManager runtimeManager;

        /// <summary>
        /// 環境管理マネージャ
        /// </summary>
        EnvironmentManager environmentManager;

        CodeOptimizer codeOptimizer;

        bool isSourceComment;


        private int orgValue = 0x100;
        private int workAddressValue = -1;
        private int offsetAddressValue = -1;

        /// <summary>
        /// エラー発生数
        /// </summary>
        public int ErrorCount { get { return ((SLANGScanner)this.Scanner).ErrorCount; }}
        /// <summary>
        /// エラー出力先
        /// </summary>
        private TextWriter errorTextWriter;

        // OS依存しないランタイム
        readonly static string runtimeFileName = "runtime.yml";
        // OS依存するランタイム(オプションで差し替えられるようにしたい)
        readonly static string libFileName = "lib.yml";

        public SLANGParser() : base(null) {
            initSymbolTable();
            labelManager = new LabelManager(this, this);
            codeRepository = new CodeRepository(this);
            stringDataManager = new StringDataManager();
            caseStack = new Stack<CaseInfo>();
            runtimeManager = new RuntimeManager(symbolTableManager);

            SetErrorTextWriter(Console.Error);
        }

        /// <summary>
        /// ORGアドレスを設定する。複数回設定しても無意味である。
        /// </summary>
        public void SetOrg(Expr expr)
        {
            if(!expr.IsIntValueConst())
            {
                Error("ORG must be const.");
                return;
            }
            SetOrg(expr.ConstValue.Value);
        }

        /// <summary>
        /// ORGアドレスを設定する
        /// </summary>
        public void SetOrg(int orgValue)
        {
            this.orgValue = orgValue;
        }

        /// <summary>
        /// WORKアドレスを設定する
        /// </summary>
        public void SetWork(Expr expr)
        {
            if(!expr.IsConst())
            {
                Error("WORK must be const.");
                return;
            }
            SetWork(expr.Value);
        }

        /// <summary>
        /// WORKアドレスを設定する
        /// </summary>
        public void SetWork(int workValue)
        {
            workAddressValue = workValue;
        }

        /// <summary>
        /// OFFSETを設定する
        /// </summary>
        public void SetOffset(Expr expr)
        {
            if(!expr.IsConst())
            {
                Error("OFFSET must be const.");
                return;
            }
            offsetAddressValue = expr.Value;
        }

        public void StartModule(Expr expr)
        {
            if(!expr.IsConst())
            {
                Error("MODULE must be const.");
                return;
            }
            var moduleCount = ((SLANGScanner)this.Scanner).moduleCount - 1;
            gencode($"ORG ${expr.Value:X4},${moduleCount * 0x10000:X4}\n");
            gencode($"_MODULE_{moduleCount}_START:\n");
            isCurrentModuleMode = true;
        }

        public void EndModule()
        {
            var moduleCount = ((SLANGScanner)this.Scanner).moduleCount - 1;
            gencode($"_MODULE_{moduleCount}_END:\n");
            isCurrentModuleMode = false;
        }

        private int computeSize(TypeInfo typeInfo)
        {
            if(typeInfo == null)
            {
                return 0;
            }
            if(typeInfo.Parent == null && typeInfo.IsByteTypeInfo())
            {
                return 1;
            }
            if(typeInfo.Parent == null && typeInfo.IsWordTypeInfo())
            {
                return 2;
            }
            if(typeInfo.Parent == null && typeInfo.IsFloatTypeInfo())
            {
                return 3;
            }
            switch(typeInfo.InfoClass)
            {
                case TypeInfoClass.Pointer:
                    return 1;
                case TypeInfoClass.Array:
                    return typeInfo.Size * (typeInfo.DataSize == TypeDataSize.Byte ? 1 : 2); //computeSize(typeInfo.Parent);
                case TypeInfoClass.Function:
                    return 1;
                case TypeInfoClass.Indirect:
                    // ポインタなので常に2
                    return 2;
                    //return typeInfo.Size * (typeInfo.GetDataSize() == TypeDataSize.Byte ? 1 : 2);
                default:
                    Error("bug computeSize : " + typeInfo.InfoClass + ":" + typeInfo.DataSize);
                    break;
            }
            return 0;
        }

        /// <summary>
        /// コンパイラのバグメッセージを表示する(本来は表示されないはず)
        /// </summary>
        public void bug(string error)
        {
            this.Scanner.yyerror("bug:" + error);
        }

        /// <summary>
        /// 処理中の行番号とファイル名と共にコンパイラのエラーメッセージを表示する。
        /// </summary>
        public void Error(string error, bool noDispLine = false)
        {
            if(noDispLine)
            {
                ((SLANGScanner)this.Scanner).error("error:" + error);
            } else {
                this.Scanner.yyerror("error:" + error);
            }
        }

        /// <summary>
        /// コンパイラのエラーメッセージを表示する(ファイル名、行番号などは表示しない)
        /// </summary>
        public void SystemError(string error)
        {
            ((SLANGScanner)this.Scanner).error("system error: " + error);
        }

        /// <summary>
        /// 2つの型情報を元に型情報を作る(BYTEとWORDの計算時結果をWORDにする、といった処理)
        /// </summary>
        private OperatorType adjust(OperatorType left, OperatorType right)
        {
            if(left == OperatorType.Float || right == OperatorType.Float)
            {
                return OperatorType.Float;
            }
            if(left == OperatorType.Word || right == OperatorType.Word)
            {
                return OperatorType.Word;
            }
            if(left == OperatorType.Byte || right == OperatorType.Byte)
            {
                return OperatorType.Byte;
            }
            if(left == OperatorType.Bool || right == OperatorType.Bool)
            {
                return OperatorType.Bool;
            }
            return OperatorType.Constant;
        }

        /// <summary>
        /// 2つの型情報を元に型情報を作る(BYTEとWORDの計算時結果をWORDにする、といった処理)
        /// </summary>
        private OperatorType adjust(Expr leftExpr, Expr rightExpr)
        {
            var left = leftExpr.OpType;
            var right = rightExpr.OpType;

            // 特例としてFloat定数はFloat型としてやる(不気味。というか、Constantは必要ないのでは……)
            if(left == OperatorType.Constant && leftExpr.Opcode == Opcode.Const)
            {
                if(leftExpr.ConstValue.ConstInfoType == ConstInfoType.FloatValue)
                {
                    left = OperatorType.Float;
                }
            }
            if(right == OperatorType.Constant)
            {
                if(rightExpr.Opcode == Opcode.Cond)
                {
                    // 三項演算子の場合は左の型を得る
                    right = rightExpr.Left.OpType;
                } else if(rightExpr.ConstValue.ConstInfoType == ConstInfoType.FloatValue)
                {
                    right = OperatorType.Float;
                }
            }

            if(left == OperatorType.Float || right == OperatorType.Float)
            {
                return OperatorType.Float;
            }
            if(left == OperatorType.Word || right == OperatorType.Word)
            {
                return OperatorType.Word;
            }
            if(left == OperatorType.Byte || right == OperatorType.Byte)
            {
                return OperatorType.Byte;
            }
            if(left == OperatorType.Bool || right == OperatorType.Bool)
            {
                return OperatorType.Bool;
            }
            return OperatorType.Constant;
        }

        public void resetHeap()
        {
            // 特に何もしない？
        }

        /// <summary>
        /// プリプロセッサのIFの処理を行う。定数値が0の場合は以下、#ELSEまたは#ENDまで無効、0以外の場合は有効とする。
        /// </summary>
        private bool procPreprocessIf(Expr expr)
        {
            bool iftrue = false;
            if(expr.IsConst())
            {
                if(!expr.IsIntValueConst())
                {
                    Error("#IF must be value const parameter.");
                }
                var slangScanner = (SLANGScanner)this.Scanner;
                iftrue = expr.ConstValue.Value != 0;
                slangScanner.ProcIf(iftrue);
            } else {
                Error("#IF must be const parameter.");
            }
            return iftrue;
        }

        /// <summary>
        /// 渡された文字列をプログラムコードとして出力する(#ASM～#ENDの中など)
        /// </summary>
        private void procPlainString(string text)
        {
            gencode(text);
        }

        /// <summary>
        /// 最終的にコンパイルしたアセンブラソースをStreamに出力する
        /// </summary>
        public void WriteToStream(Stream outputStream)
        {
            StreamWriter outputStreamWriter = new StreamWriter(outputStream, System.Text.Encoding.GetEncoding("shift_jis"));

            // RuntimeManagerが保持するSymbolTableManagerに対して、利用されたワーク変数を追加する
            runtimeManager.AddWorkSymbol();

            // コード生成タイミングでワーク末尾に関数パラメータ等用の領域を確保する
            // ※IYがここを指すだけでいいのか？？
            var workArray = new SymbolTable()
            {
                Name = "__IYWORK",
                SymbolClass = SymbolClass.Global,
                TypeInfo = new TypeInfo(TypeInfoClass.Array, 256, TypeDataSize.Byte, TypeInfo.ByteTypeInfo),
                Size = 256
            };
            symbolTableManager.Add(workArray, true);

            // WORKENDをメモリ末尾に移動する
            symbolTableManager.Remove("WORKEND");
            var workEndArray = new SymbolTable()
            {
                Name = "WORKEND",
                SymbolClass = SymbolClass.Global,
                TypeInfo = new TypeInfo(TypeInfoClass.Array, 0, TypeDataSize.Byte, TypeInfo.ByteTypeInfo),
                Size = 0
            };
            symbolTableManager.Add(workEndArray, true);

            // CONST値のうちASM指定がされているものをEQUとして出力する
            constTableManager.Generate(outputStreamWriter);

            // WORK指定がされていて、それがORGのアドレスより前の場合は先にWORKの宣言を出力する(念のため)
            if(workAddressValue >=0 && workAddressValue < orgValue)
            {
                genSymbolTable(outputStreamWriter);
            }

            // 初期値つきシンボルテーブルを末尾に追加する
            symbolTableManager.GenerateInitialValueSymbol(codeRepository, this);

            // 関数の静的宣言の出力
            functionSymbolTableManagerList.Add(localSymbolTableManager);
            localSymbolTableManager = null;
            foreach(var manager in functionSymbolTableManagerList)
            {
                manager.GenerateInitialValueSymbol(codeRepository, this);
                //manager.GenerateCode(outputStreamWriter, null);
            }

            // プログラムコードを出力する
            var codeList = codeRepository.GenerateCodeList(orgValue, offsetAddressValue);

            // 出力されたコードを最適化する
            codeOptimizer.PeepholeOptimize(codeList);

            foreach(var code in codeList)
            {
                outputStreamWriter.WriteLine(code);
            }

            // ランタイムを出力する
            runtimeManager.Generate(outputStreamWriter);

            // ランタイムの初期化コードを出力する
            runtimeManager.GenerateInitializeCode(outputStreamWriter);

            // 文字列データの出力
            stringDataManager.GenerateCode(outputStreamWriter);

            // プログラムの最後のラベルを出力する
            outputStreamWriter.WriteLine("SLANG_PROG_END:");

            // ORGよりWORKの位置が後の場合のWORKの出力
            if(workAddressValue <0 || workAddressValue > orgValue)
            {
                genSymbolTable(outputStreamWriter);
            }

            outputStreamWriter.Flush();
        }


        // 環境設定ファイルを元に環境設定を行う
        // * デフォルトORGの設定
        // * ランタイムライブラリの読み込み
        public void SetupEnvironment(string envName)
        {
            var envPath = SLANGPathManager.Instance.GetEnvironmentPath(envName);
            environmentManager = new EnvironmentManager(runtimeManager, this, this);
            environmentManager.Load(envPath);
        }

        public void LoadRuntime(string fileName)
        {
            var filePath = SLANGPathManager.Instance.GetLibraryDefinePath(fileName);
            runtimeManager.LoadRuntime(filePath);
        }

        // パースを開始する
        private void StartParse()
        {
            // ランタイムの読み込み
            LoadRuntime(runtimeFileName);

            {
                var fileName = "opt.yml";
                codeOptimizer = new CodeOptimizer();
                var filePath = SLANGPathManager.Instance.GetLibraryDefinePath(fileName);
                if(filePath == null)
                {
                    Console.Error.WriteLine("could not found optimize rule file. " + fileName);
                    return;
                }
                codeOptimizer.LoadOptimizeRule(filePath);
            }

            var slangScanner = (SLANGScanner)this.Scanner;
            slangScanner.ErrorTextWriter = errorTextWriter;
        }

        /// <summary>
        /// エラー出力を行うTextWriterを設定する
        /// </summary>
        public void SetErrorTextWriter(TextWriter errorWriter)
        {
            errorTextWriter = errorWriter;
        }


        /// <summary>
        /// SLANGのソースコードをアセンブラソースにコメントとして含めるかどうかを設定する
        /// </summary>
        public void SetSourceComment(bool sourceComment)
        {
            this.isSourceComment = sourceComment;
        }

        public void ParseConstExpr(string code, ConstTableManager constTableManager = null)
        {
            try
            {
                byte[] inputBuffer = System.Text.Encoding.UTF8.GetBytes(code);
                MemoryStream stream = new MemoryStream(inputBuffer);

                this.Scanner = new SLANGScanner(stream, "UTF-8");
                var slangScanner = (SLANGScanner)this.Scanner;
                slangScanner.currentFileName = "inner-code";
                if(constTableManager == null)
                {
                    constTableManager = this.constTableManager;
                }
                slangScanner.SetConstTableManager(constTableManager);
                slangScanner.SetCodeRepository(codeRepository);

                StartParse();
                this.Parse();
                stream.Close();
            } catch(Exception e)
            {
                SystemError(e.ToString());
                //Console.Error.WriteLine($"fatal error : " + e.ToString());
            }
        }

        /// <summary>
        /// 文字列で与えられたSLANGコードをパースする(テスト用)
        /// </summary>
        public void ParseString(string code)
        {
            try
            {
                byte[] inputBuffer = System.Text.Encoding.UTF8.GetBytes(code);
                MemoryStream stream = new MemoryStream(inputBuffer);

                this.Scanner = new SLANGScanner(stream, "UTF-8");
                var slangScanner = (SLANGScanner)this.Scanner;
                slangScanner.currentFileName = "inner-code";
                slangScanner.SetConstTableManager(constTableManager);
                slangScanner.SetCodeRepository(codeRepository);
                slangScanner.SetSourceComment(isSourceComment);

                StartParse();
                this.Parse();
                stream.Close();

                // TempFuncのままのものが無いかチェックする(あるとエラーになる)
                symbolTableManager.CheckTempFunc();

                Console.WriteLine($"{ErrorCount} error(s)");
            } catch(Exception e)
            {
                SystemError(e.ToString());
                //Console.Error.WriteLine($"fatal error : " + e.ToString());
            }
        }

        /// <summary>
        /// fileNameで与えられたSLANGコードが含まれるファイルをパースする
        /// </summary>
        public void Parse(string fileName)
        {

            SLANGCommonUtility.SetCurrentSourcePath(fileName);

            Console.WriteLine($"; source file {fileName} opened\n");
            try
            {
                Stream stream = new FileStream(fileName, FileMode.Open);
                var charsetDetectedResult = CharsetDetector.DetectFromStream(stream);
                stream.Position = 0;
                this.Scanner = new SLANGScanner(stream, charsetDetectedResult.Detected.EncodingName);
                var slangScanner = (SLANGScanner)this.Scanner;
                slangScanner.currentFileName = fileName;
                slangScanner.SetConstTableManager(constTableManager);
                slangScanner.SetCodeRepository(codeRepository);
                slangScanner.SetSourceComment(isSourceComment);

                StartParse();
                // genInitCode();

                this.Parse();
                stream.Close();

                // TempFuncのままのものが無いかチェックする(あるとエラーになる)
                symbolTableManager.CheckTempFunc();

                Console.WriteLine($"{ErrorCount} error(s)");
                symbolTableManager.DebugDisp();
            } catch(Exception e)
            {
                // SystemError(e.ToString());
                SystemError(e.Message);
                //Console.Error.WriteLine("fatal error: " + e.Message);
            }
        }
    }
}
