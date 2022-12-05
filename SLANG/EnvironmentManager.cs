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
            public string defaultOrg = null;
            public string[] libraries = null;
        }

        /// <summary>
        ///  ランタイムのコードを管理し、使われたもののみを出力するクラス
        /// </summary>
        public class EnvironmentManager
        {


            private RuntimeManager runtimeManager;
            private IORGSetter orgSetter;

            public EnvironmentManager(RuntimeManager runtimeManager, IORGSetter orgSetter)
            {
                this.runtimeManager = runtimeManager;
                this.orgSetter = orgSetter;
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
                    var orgNum = SLANGCommonUtility.GetValue(info.defaultOrg);
                    orgSetter.SetOrg(orgNum);
                }

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
