using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using System.IO;

namespace ModuleSplitter
{
    internal class Program
    {
        public class Options
        {
            [Option("cmt", Required = false, HelpText = "Output CMT files.")]
            public bool ExportCmt { get; set; }

            [Value(0, Required = true, MetaName = "input files")]
            public IEnumerable<string> Files { get; set; }
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
            var splitter = new ModuleSplitter();

            try
            {
                foreach(var fileName in opt.Files)
                {
                    Console.WriteLine($"Processing {fileName}...");
                    splitter.Proc(fileName, opt.ExportCmt);
                }
            } catch(FileNotFoundException e)
            {
                Error(e.Message);
                Environment.Exit(1);
            } catch(InvalidDataException e)
            {
                Warning(e.Message);
                Environment.Exit(0);
            }

            Environment.Exit(0);
            return 0;
        }

        static void Error(string message)
        {
            Console.WriteLine($"Error: {message}");
        }

        static void Warning(string message)
        {
            Console.WriteLine($"Warning: {message}");
        }

    }
}
