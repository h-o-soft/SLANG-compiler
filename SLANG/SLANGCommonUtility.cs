using System;
using System.Collections.Generic;
using System.IO;

namespace SLANGCompiler.SLANG
{
    public class SLANGCommonUtility
    {
        private static string currentSourcePath;

        public static int Float24ToInt(int f24)
        {
            int a  = (f24 >> 16) & 0xff;
            int hl = (f24 & 0xffff);
            bool isMinus = (a & 0x80) != 0;
            int c = a;

            a *= 2;
            a &= 0xff;
            // Check if the input is 0
            if(a == 0)
            {
                // return zero
                return 0;
            }
            // check if inf or NaN
            if(a == 0xfe)
            {
                if(hl == 0)
                {
                    // return zero
                    return 0;
                }
                if(!isMinus)
                {
                    return 32767;
                }
                return -32768;
            }
            // ;now if exponent is less than 0, just return 0
            if(a < 63*2)
            {
                return 0;
            }

            // ;if the exponent is greater than 14, return +- "inf"
            // 右ローテート(だが必ずここにくる時はキャリーは0なので右シフト相当)
            a = (a >> 1);
            a -= 63;
            a &= 0xff;
            if( a >= 15 )
            {
                if(!isMinus)
                {
                    return 32767;
                }
                return -32768;
            }
            // ;all is good!
            // ;A is the exponent
            // ;1+A is the number of bits to read
            var aIsZero = (a == 0);
            int b = a;
            int d = 0;
            a = 1;
            if(!aIsZero)
            { 
                do{
                    hl *= 2;
                    int carry = 0;
                    if((hl & 0xffff0000) != 0)
                    {
                        carry = 1;
                    }
                    hl &= 0xffff;
                    // 左ローテート
                    a = (a << 1) | carry;
                    carry = (a & 0x100) != 0 ? 1 : 0;
                    a &= 0xff;

                    d = (d << 1) | carry;
                    carry = (d & 0x100) != 0 ? 1 : 0;
                    d &= 0xff;
                } while(--b != 0);

            }
            c <<= 1;
            var e = a;
            hl = (e & 0xff)| ((d << 8) & 0xff00);
            if(!isMinus)
            {
                return hl;
            }
            return -hl;
        }

        public static int ValueToFloat24(float value)
        {
            var f24Vals = ValueToFloat24Byte(value);
            int f24Val =    (f24Vals[0] & 0xff)
                        | ((f24Vals[1] << 8) & 0xff00)
                        | ((f24Vals[2] << 16) & 0xff0000);
            return f24Val;
        }

        public static int[] ValueToFloat24Byte(float value)
        {
            if(value == float.NaN)
            {
                return new int[3]{244,244,127};
            }
            if(value == 0)
            {
                return new int[3]{0,0,0};
            }
            int sign = 0;
            if(value < 0)
            {
                sign = 1;
                value = -value;
            }
            if(float.IsInfinity(value))
            {
                return new int[3]{0,0,(byte)(127 + (sign<<7))};
            }

            int exp=0;
            while(value < 1)
            {
                exp--;
                value += value;
            }
            while(value >= 2)
            {
                exp++;
                value /= 2f;
            }
            if(exp > 63)
            {
                // infinity
                return new int[3]{0,0,(byte)(127 + (sign<<7))};
            }
            if(exp < -62)
            {
                // zero
                return new int[3]{0,0,0};
            }
            var result = new List<int>(){ (int)(exp + 63 + (sign << 7)) };
            value -= 1f;
            value *= 256;
            var n = 2;
            for(int k = 0; k < n-1; k++)
            {
                var a = (int)value;
                value -= a;
                result.Insert(0,a);
                value *= 256;
            }
            // rounding
            value += 0.5f;
            result.Insert(0, (byte)value);
            int idx = 0;
            while((idx < n) && (result[idx] == 256))
            {
                result[idx] = 0;
                idx++;
                result[idx] = 1;
            }
            return result.ToArray();
        }

        public static void SetCurrentSourcePath(string fileName)
        {
            currentSourcePath = fileName;
        }

        public static string GetSourcePath(string fileName)
        {
            // その場所にあればそのまま返す
            if(File.Exists(fileName))
            {
                return fileName;
            }

            // ソースを開いたパスを探す
            var basePath = Path.GetDirectoryName(currentSourcePath);
            var searchPath = Path.Combine(basePath, fileName);
            if(File.Exists(searchPath))
            {
                return searchPath;
            }

            // Configパスを探す
            return GetConfigPath(fileName);
        }

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

        public static int GetIntValue(string valueString)
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