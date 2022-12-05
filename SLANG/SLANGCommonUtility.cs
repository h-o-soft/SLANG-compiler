using System;
using System.Collections.Generic;
using System.IO;

namespace SLANGCompiler.SLANG
{
    public class SLANGCommonUtility
    {
        public static string GetConfigPath(string fileName)
        {
            if(!File.Exists(fileName))
            {
                var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config");
                configPath = Path.Combine(configPath,"SLANG");
                fileName = Path.Combine(configPath, Path.GetFileName(fileName));
                if(!File.Exists(fileName))
                {
                    return null;
                }
            }
            return fileName;
        }
        
        public static int GetValue(string valueString)
        {
            int number;

            if(valueString[0] == '$')
            {
                number = Convert.ToInt32(valueString.Substring(1), 16);
            } else if(Char.ToLower(valueString[valueString.Length-1]) == 'h')
            {
                number = Convert.ToInt32(valueString.Substring(0, valueString.Length - 1), 16);
            } else if(Char.ToLower(valueString[valueString.Length-1]) == 'b')
            {
                number = Convert.ToInt32(valueString.Substring(0, valueString.Length - 1), 2);
            } else if(valueString.StartsWith("0x"))
            {
                number = Convert.ToInt32(valueString.Substring(2), 16);
            } else {
                number = int.Parse(valueString);
            }

            return number;
        }
    }
}