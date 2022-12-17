using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SLANGCompiler.SLANG
{
    internal partial class SLANGParser
    {

        /// <summary>
        /// 環境情報(YAMLで読まれる)
        /// </summary>
        public class EnvironmentInfo
        {
            public int osType = 0;
            public int envType = 0;
            public string defaultOrg = null;
            public string defaultWork = null;
            public string[] libraries = null;
        }

        /// <summary>
        ///  ランタイムのコードを管理し、使われたもののみを出力するクラス
        /// </summary>
        public class EnvironmentManager
        {
            private RuntimeManager runtimeManager;
            private IORGSetter orgSetter;
            private IWORKSetter workSetter;

            public int OSType { get; private set; }
            public int EnvironmentType { get; private set; }

            public EnvironmentManager(RuntimeManager runtimeManager, IORGSetter orgSetter, IWORKSetter workSetter)
            {
                this.runtimeManager = runtimeManager;
                this.orgSetter = orgSetter;
                this.workSetter = workSetter;
            }

            /// <summary>
            /// 環境設定ファイル(YAML)を読み込む
            /// </summary>
            public void Load(string fileName)
            {
                if(!File.Exists(fileName))
                {
                    throw new FileNotFoundException($"could not found environment file. {fileName}");
                }
                StreamReader sr = new StreamReader(fileName, Encoding.GetEncoding("UTF-8"));
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                var environment = deserializer.Deserialize<EnvironmentInfo>(sr);
                sr.Close();

                Setup(environment);
            }


            // 環境設定ファイルの内容を処理する
            // * ORGの設定
            // * ランタイムの読み込み
            private void Setup(EnvironmentInfo info)
            {
                // デフォルトのORGを設定
                if(!string.IsNullOrEmpty(info.defaultOrg))
                {
                    var orgNum = SLANGCommonUtility.GetIntValue(info.defaultOrg);
                    orgSetter.SetOrg(orgNum);
                }

                // デフォルトのWORKを設定
                if(!string.IsNullOrEmpty(info.defaultWork))
                {
                    var workNum = SLANGCommonUtility.GetIntValue(info.defaultWork);
                    workSetter.SetWork(workNum);
                }

                // 環境の種別を設定(ENV_TYPEとして定義される)
                // 0 = LSX-Dodgers
                // 1 = X1(LSX-Dodgers)
                // 2 = S-OS
                // 3 = MSX-DOS2
                // 4 = MSX ROM
                this.EnvironmentType = info.envType;

                // OSの種別を設定(OS_TYPEとして定義される)
                // 0 = LSX-Dodgers
                // 1 = S-OS
                // 2 = MSX-DOS2
                // 3 = MSX ROM
                this.OSType = info.osType;

                // ランタイムを読み込み
                if(info.libraries != null)
                {
                    foreach(var lib in info.libraries)
                    {
                        var libPath = SLANGCommonUtility.GetConfigPath(lib);
                        runtimeManager.LoadRuntime(libPath);
                    }
                }
            }
        }
    }
}
