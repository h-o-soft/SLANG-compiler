using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using System.Globalization;

namespace SLANGCompiler
{
    public class Program
    { 
        public class Options
        {
            [Option('E', "env", Required = false, HelpText = "Environment name.")]
            public string EnvironmentName { get; set; }
            [Option('L', "lib", Required = false, HelpText = "Library name(s). ( lib*.yml )")]
            public IEnumerable<string> LibraryNames { get; set; }

            [Option('O', "output", Required = false, HelpText = "Output file path.")]
            public string OutputPath { get; set; }

            [Option("use-symbol", Required = false, HelpText = "Use original symbol name.")]
            public bool UseOriginalSymbol { get; set; }

            [Option("case-sensitive", Required = false, HelpText = "Set symbols to be case-sensitive.")]
            public bool CaseSensitive { get; set; }

            [Value(0, Required = true, MetaName = "input files")]
            public IEnumerable<string> Files { get; set; }
        }

        // テスト用
        public string ParseString(string code, Stream errorStream = null)
        {
            var parser = new SLANG.SLANGParser();

            var memStream = new MemoryStream();

            StreamWriter errorStreamWriter = null;
            if(errorStream != null)
            {
                errorStreamWriter = new StreamWriter(errorStream);
                parser.SetErrorTextWriter(errorStreamWriter);
            }
            parser.ParseString(code);
            parser.WriteToStream(memStream);

            var result = Encoding.UTF8.GetString(memStream.ToArray());

            memStream.Close();

            if(errorStreamWriter != null)
            {
                errorStreamWriter.Flush();
            }
            return result;
        }

        static void Main(string[] args)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance); 

            var parser = new CommandLine.Parser(with => with.HelpWriter = null);
            var parseResult = parser.ParseArguments<Options>(args);
            parseResult.MapResult(
                (Options options)=> Run(options),
                errs => DisplayHelp<Options>(parseResult, errs));
        }

        static int DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            HelpText helpText = null;
            if(errs.IsVersion())
            {
                helpText = HelpText.AutoBuild(result);
            } else {
                helpText = HelpText.AutoBuild(result, h =>
                {
                  h.AdditionalNewLineAfterOption = false;
                  return HelpText.DefaultParsingErrorsHandler(result, h);
                }, e => e);
            }
            Console.WriteLine(helpText);
            return 1;
        }

        static int Run(Options opt)
        {
            //if(opt.DispVersion)
            //{
            //    Console.WriteLine($"  Build Date: {LoadBuildDateTime(typeof(Program).Assembly)}");
            //    Environment.Exit(0);
            //}
            var parser = new SLANG.SLANGParser();

            // ソースファイルで指定した変数名、関数名をそのまま使うか、使わないか
            parser.SetOriginalSymbolUse(opt.UseOriginalSymbol);
            parser.SetCaseSensitiveSymbol(opt.CaseSensitive);

            // 環境名を設定する(設定されていない場合はLSX-Dodgers環境をデフォルトとする)
            string envName = opt.EnvironmentName;
            if(string.IsNullOrEmpty(envName))
            {
                envName = "lsx";
            }

            parser.SetupEnvironment(envName);

            foreach(var fileName in opt.Files)
            {
                if(!File.Exists(fileName))
                {
                    Console.Error.WriteLine($"file not found. : {fileName}");
                    Environment.Exit(1);
                }

                parser.Parse(fileName);

                if(parser.ErrorCount != 0)
                {
                    Environment.Exit(1);
                }
            }

            string outputPath;
            if(string.IsNullOrEmpty(opt.OutputPath))
            {
                outputPath = Path.GetFileNameWithoutExtension(opt.Files.ElementAt(0)) + ".ASM";
            } else {
                outputPath = opt.OutputPath;
            }
            var outputStream = new FileStream(outputPath, FileMode.Create);
            parser.WriteToStream(outputStream);
            outputStream.Close();

            Environment.Exit(0);
            return 0;
        }

        private static string LoadBuildDateTime(Assembly assembly)
        {
            var metadata = assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                ?.Where(a => a.Key == "BuildDateTime")
                ?.FirstOrDefault();
            if (metadata != null)
            {
                return DateTime.ParseExact(metadata.Value, "o", CultureInfo.InvariantCulture).ToString();
            }
            return null;
        }
    }
}
