﻿using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using System.Globalization;
using SLANGCompiler.SLANG;
namespace SLANGCompiler
{
    public class Program
    { 
        public class Options
        {
            [Option('E', "env", Required = false, HelpText = "Environment name.")]
            public string EnvironmentName { get; set; }
            [Option('l', "lib", Required = false, HelpText = "Library name(s). ( lib*.yml )")]
            public IEnumerable<string> LibraryNames { get; set; }
            [Option('I', "include", Required = false, HelpText = "Include path(s).")]
            public IEnumerable<string> IncludePaths { get; set; }
            [Option('L', "library", Required = false, HelpText = "Library path(s).")]
            public IEnumerable<string> LibraryPaths { get; set; }

            [Option('O', "output", Required = false, HelpText = "Output file path.")]
            public string OutputPath { get; set; }

            [Option("use-symbol", Required = false, HelpText = "Use original symbol name.")]
            public bool UseOriginalSymbol { get; set; }

            [Option("case-sensitive", Required = false, HelpText = "Set symbols to be case-sensitive.")]
            public bool CaseSensitive { get; set; }
            [Option("source-comment", Required = false, HelpText = "Include source code as comments.")]
            public bool SourceComment { get; set; }

            [Option("output-debug-symbol", Required = false, HelpText = "Output original symbol name for debug.")]
            public bool OutputDebugSymbol { get; set; }

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

        static void InitializePathManager()
        {
            // SLANGPathManagerの初期化
            SLANGPathManager.Instance.Initialize();

            // INCLUDE / LIBRARY関連の設定
            // 環境変数「SLANG_INCLUDE」を登録
            var includePath = Environment.GetEnvironmentVariable("SLANG_INCLUDE");
            if(includePath != null)
            {
                SLANGPathManager.Instance.AddIncludePath(includePath);
            }

            // 環境変数「SLANG_LIBRARY」を登録
            var libPath = Environment.GetEnvironmentVariable("SLANG_LIBRARY");
            if(libPath != null)
            {
                SLANGPathManager.Instance.AddLibraryPath(libPath);
            }

            // .config/SLANG 配下の include と pathもパスに加える(旧仕様対策)
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config");
            configPath = Path.Combine(configPath,"SLANG");

            var additionalPaths = new string[]{
                ".",
                configPath
            };

            // カレントパスと、.config/SLANG以下のincludeとlibをそれぞれに登録
            foreach(var path in additionalPaths)
            {
                var currentIncludePath = Path.Combine(path, "include");
                var currentLibPath = Path.Combine(path, "lib");
                SLANGPathManager.Instance.AddIncludePath(currentIncludePath);
                SLANGPathManager.Instance.AddLibraryPath(currentLibPath);
            }
        }

        static int Run(Options opt)
        {
            //if(opt.DispVersion)
            //{
            //    Console.WriteLine($"  Build Date: {LoadBuildDateTime(typeof(Program).Assembly)}");
            //    Environment.Exit(0);
            //}

            InitializePathManager();

            var parser = new SLANG.SLANGParser();

            // ソースファイルで指定した変数名、関数名をそのまま使うか、使わないか
            parser.SetOriginalSymbolUse(opt.UseOriginalSymbol);
            parser.SetCaseSensitiveSymbol(opt.CaseSensitive);
            parser.SetSourceComment(opt.SourceComment);
            parser.SetOutputDebugSymbol(opt.OutputDebugSymbol);

            // 環境名を設定する(設定されていない場合はLSX-Dodgers環境をデフォルトとする)
            string envName = opt.EnvironmentName;
            if(string.IsNullOrEmpty(envName))
            {
                envName = "lsx";
            }

            // Includeパスが指定されていたら読み込む
            if(opt.IncludePaths != null)
            {
                foreach(var path in opt.IncludePaths)
                {
                    SLANGPathManager.Instance.AddIncludePath(path);
                }
            }

            // ライブラリパスが指定されていたら読み込む
            if(opt.LibraryPaths != null)
            {
                foreach(var path in opt.LibraryPaths)
                {
                    SLANGPathManager.Instance.AddLibraryPath(path);
                }
            }

            try
            {
                parser.SetupEnvironment(envName);

                // ライブラリが指定されていたら読み込む
                if(opt.LibraryNames != null)
                {
                    foreach(var lib in opt.LibraryNames)
                    {
                        parser.LoadRuntime($"lib{lib}.yml");
                    }
                }

            } catch(Exception e)
            {
                Console.Error.WriteLine($"system error: " + e.Message);
                Environment.Exit(1);
            }


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
                var origPath = opt.Files.ElementAt(0);
                outputPath = Path.Combine(Path.GetDirectoryName(origPath), Path.GetFileNameWithoutExtension(origPath) + ".ASM");
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
