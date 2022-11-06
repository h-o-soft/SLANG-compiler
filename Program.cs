using System;
using System.IO;
using System.Text;

namespace SLANGCompiler
{
    public class Program
    {

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

        static int Main(string[] args)
        {
            Console.WriteLine("SLANG Compiler version 0.0.1");
            if(args.Length == 0)
            {
                Console.WriteLine("usage:");
                Console.WriteLine(" SLANGCompiler [file]");
                return 0;
            }

            var parser = new SLANG.SLANGParser();

            var fileName = args[0];

            if(!File.Exists(fileName))
            {
                Console.Error.WriteLine($"file not found. : {fileName}");
                return 1;
            }

            var outputPath = Path.GetFileNameWithoutExtension(fileName) + ".ASM";
            var outputStream = new FileStream(outputPath, FileMode.Create);

            //parser.ParseString("CONST TEST=123,TEST2=456,TEST3=TEST1+TEST2+5;\nVAR TVAR=TEST3;");
            parser.Parse(fileName);

            if(parser.ErrorCount == 0)
            {
                parser.WriteToStream(outputStream);
            }
            outputStream.Close();

            return parser.ErrorCount == 0 ? 0 : 1;
        }
    }
}
