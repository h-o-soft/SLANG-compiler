using System;
using System.Collections.Generic;
using System.IO;

namespace SLANGCompiler.SLANG
{
    public class SLANGPathManager: Singleton<SLANGPathManager>
    {
        private static readonly string EnvironmentPath = "env";
        private static readonly string LibraryDefinePath = "libdef";

        private PathManager includePathManager = new PathManager();
        private PathManager libraryPathManager = new PathManager();
        private PathManager sourcePathManager = new PathManager();

        private string currentSourcePath;

        public void Initialize()
        {
            includePathManager.Initialize();
            libraryPathManager.Initialize();
        }

        public void AddIncludePath(string path)
        {
            includePathManager.AddMultiPath(path);
        }

        public void AddLibraryPath(string path)
        {
            libraryPathManager.AddMultiPath(path);
        }

        public string GetIncludeSourcePath(string path)
        {
            var result = includePathManager.GetFile(path);
            if(result == null)
            {
                throw new FileNotFoundException($"could not found source {path}");
            }
            return result;
        }

        public string GetLibrarySourcePath(string path)
        {
            var result = libraryPathManager.GetFile(path);
            if(result == null)
            {
                throw new FileNotFoundException($"could not found source {path}");
            }
            return result;
        }

        public string GetEnvironmentPath(string environmentName)
        {
            // Environmentはlibrary path内のenvフォルダに格納されている
            var environmentFileName = Path.Combine(EnvironmentPath, environmentName + ".env");
            var result = libraryPathManager.GetFile(environmentFileName);
            if(result == null)
            {
                throw new FileNotFoundException($"could not found environment {environmentName}");
            }
            return result;
        }

        public string GetLibraryDefinePath(string libraryName)
        {
            // ライブラリ定義ファイル(yml)はlibrary path内のlibdefフォルダに格納されている
            var libFileName = Path.Combine(LibraryDefinePath, libraryName);
            var result = libraryPathManager.GetFile(libFileName);
            if(result == null)
            {
                throw new FileNotFoundException($"could not found library define {libraryName}");
            }
            return result;
        }

    }
}
