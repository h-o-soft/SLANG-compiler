using System;
using System.Collections.Generic;
using System.IO;
using AILZ80ASM.IO;

namespace ModuleSplitter
{
    public class ModuleSplitter
    {
        public class ModuleInfo
        {
            public int startAddress;
            public int endAddress;
            public byte[] moduleData;
        }

        private List<ModuleInfo> moduleInfoList = new List<ModuleInfo>();

        private int GetSymAddress(string symText)
        {
            var addressStr = symText.Split(' ')[0];
            return int.Parse(addressStr, System.Globalization.NumberStyles.HexNumber);
        }

        private string GetModulePath(string basePath, int idx)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(basePath);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(basePath), $"{fileName}M{idx}.bin");
        }

        private string GetMainPath(string basePath)
        {
            var fileName = System.IO.Path.GetFileNameWithoutExtension(basePath);
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(basePath), $"{fileName}MAIN.bin");
        }

        private void ProcModule(string symPath, string binPath, bool exportCmt = false)
        {
            // symファイルを読み込みつつモジュール情報を得る
            var moduleCount = -1;
            var mainStartAddress = 0;
            using(var reader = new StreamReader(symPath))
            {
                ModuleInfo moduleInfo = null;
                while(!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if(line.Contains("_MODULE_") && !line.Contains("NAME_SPACE_DEFAULT"))
                    {
                        if(line.EndsWith("_START"))
                        {
                            var splitNames = line.Split('_');
                            var moduleNumber = int.Parse(splitNames[splitNames.Length - 2]);
                            if(moduleNumber > moduleCount)
                            {
                                moduleCount = moduleNumber;
                            }
                            moduleInfo = new ModuleInfo();
                            moduleInfo.startAddress = GetSymAddress(line);
                        } else if(line.EndsWith("_END"))
                        {
                            moduleInfo.endAddress = GetSymAddress(line);
                            moduleInfoList.Add(moduleInfo);
                            Console.WriteLine($"Module {moduleCount} Start: {moduleInfo.startAddress:X4} End: {moduleInfo.endAddress:X4} ({moduleInfo.endAddress - moduleInfo.startAddress}bytes)");
                        }
                    }
                    if(line.EndsWith(" INIT"))
                    {
                        // ここがメイン部の開始なのでアドレスを得ておく
                        mainStartAddress = GetSymAddress(line);
                    }
                }
            }
            moduleCount++;

            // モジュールバイナリを読み込みつつ出力する
            var moduleAlignSize = 0x10000;
            for(int i = 0; i < moduleCount; i++)
            {
                var moduleInfo = moduleInfoList[i];
                var moduleSize = moduleInfo.endAddress - moduleInfo.startAddress;
                moduleInfo.moduleData = new byte[moduleSize];
                using(var reader = new BinaryReader(new FileStream(binPath, FileMode.Open)))
                {
                    reader.BaseStream.Seek(moduleAlignSize * i, SeekOrigin.Begin);
                    reader.Read(moduleInfo.moduleData, 0, moduleSize);
                }

                var moduleFileName = GetModulePath(binPath, i);
                using(var writer = new BinaryWriter(new FileStream(moduleFileName, FileMode.Create)))
                {
                    writer.Write(moduleInfo.moduleData);
                }

                if(exportCmt)
                {
                    var cmtPath = System.IO.Path.ChangeExtension( moduleFileName, ".cmt");
                    using(var stream = new FileStream( cmtPath, FileMode.Create))
                    {
                        var writer = new CMTBinaryWriter((UInt16)moduleInfo.startAddress, moduleInfo.moduleData, stream);
                        writer.Write();
                    }
                }
            }

            // メイン部を取得して出力する
            {
                // 全ファイルサイズからモジュール数*moduleAlignSizeを引いたサイズがメイン部のサイズとなる
                var binSize = new System.IO.FileInfo(binPath).Length;
                var mainSize = (int)(binSize - moduleAlignSize * moduleCount);
                var mainData = new byte[mainSize];

                Console.WriteLine($"Main Start: {mainStartAddress:X4} Size: {mainSize}bytes bin:{binSize}bytes modCount:{moduleCount}");
                using(var reader = new BinaryReader(new FileStream(binPath, FileMode.Open)))
                {
                    reader.BaseStream.Seek(moduleAlignSize * moduleCount, SeekOrigin.Begin);
                    reader.Read(mainData, 0, mainSize);
                }

                var mainFileName =  GetMainPath(binPath);
                using(var writer = new BinaryWriter(new FileStream(mainFileName, FileMode.Create)))
                {
                    writer.Write(mainData);
                }

                if(exportCmt)
                {
                    var cmtPath = System.IO.Path.ChangeExtension( mainFileName, ".cmt");
                    using(var stream = new FileStream(cmtPath, FileMode.Create))
                    {
                        var writer = new CMTBinaryWriter((UInt16)mainStartAddress, mainData, stream);
                        writer.Write();
                    }
                }
            }

            if(exportCmt)
            {
                // 全てのファイルを連結して出力する
                var allFileName = System.IO.Path.ChangeExtension(binPath, $".cmt");

                // まずはmainのcmtファイルを読み込む
                var mainCmtFileName = System.IO.Path.ChangeExtension(GetMainPath(binPath), ".cmt");
                var mainCmtData = new byte[new System.IO.FileInfo(mainCmtFileName).Length];
                using(var reader = new BinaryReader(new FileStream(mainCmtFileName, FileMode.Open)))
                {
                    reader.Read(mainCmtData, 0, mainCmtData.Length);
                }
                // allFileNameのファイルに書き込む
                using(var stream = new FileStream(allFileName, FileMode.Create))
                {
                    stream.Write(mainCmtData, 0, mainCmtData.Length);
                }

                // 全てのモジュールのcmtファイルをそのまま連結する
                for(int i = 0; i < moduleCount; i++)
                {
                    var moduleFileName = GetModulePath(binPath, i);
                    var cmtFileName = System.IO.Path.ChangeExtension( moduleFileName, ".cmt");

                    var cmtData = new byte[new System.IO.FileInfo(cmtFileName).Length];
                    using(var reader = new BinaryReader(new FileStream(cmtFileName, FileMode.Open)))
                    {
                        reader.Read(cmtData, 0, cmtData.Length);
                    }

                    // allFileNameのファイルに書き込む(追記)
                    using(var stream = new FileStream(allFileName, FileMode.Append))
                    {
                        stream.Write(cmtData, 0, cmtData.Length);
                    }
                }

                // MAINのcmtと連結cmtは同一なので削除する
                if(moduleCount == 0)
                {
                    System.IO.File.Delete(System.IO.Path.ChangeExtension(GetMainPath(binPath), ".cmt"));
                }
            }
            if(moduleCount == 0)
            {
                // MAINのbinを削除する
                System.IO.File.Delete(GetMainPath(binPath));
            }
        }

        public void Proc(string fileName, bool exportCmt)
        {
            // fileNameの拡張子をsym及びbinにした文字列を得る
            var symFileName = System.IO.Path.ChangeExtension(fileName, "sym");
            var binFileName = System.IO.Path.ChangeExtension(fileName, "bin");
            
            // それぞれのファイルが存在しているか確認し、存在しない場合はエラーを例外として出力する
            if(!System.IO.File.Exists(symFileName))
            {
                throw new FileNotFoundException($"{symFileName} not found.");
            }
            if(!System.IO.File.Exists(binFileName))
            {
                // binFileNameが存在しない場合、ファイル名をPROG.binにして再度探す
                var binFileName2 = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fileName),"PROG.bin");
                Console.WriteLine($"{binFileName} not found. Try {binFileName2}...");
                if(!System.IO.File.Exists(binFileName2))
                {
                    throw new FileNotFoundException($"{binFileName} not found.");
                }
                binFileName = binFileName2;
            }

            // binファイルのファイルサイズを取得する
            var binFileSize = new System.IO.FileInfo(binFileName).Length;
            if(binFileSize < 65536)
            {
                if(exportCmt)
                {
                    Console.WriteLine($"{binFileName} is not module binary. but export cmt file.");
                } else {
                    // モジュールバイナリとして不適切なサイズなので例外を出力して戻る
                    throw new InvalidDataException($"{binFileName} is not module binary. nothing to do. ");
                }
            }

            ProcModule(symFileName, binFileName, exportCmt);
        }
    }
}
