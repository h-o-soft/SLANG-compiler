using System;
using System.Collections.Generic;
using System.IO;

namespace SLANGCompiler.SLANG
{
    public class PathManager
    {
        private List<string> pathList;

        public PathManager()
        {
            Initialize();
        }

        public void Initialize()
        {
            pathList = new List<string>();
        }

        public void AddPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            try
            {
                bool exists = false;
                var isDirectory = File
                    .GetAttributes( fullPath )
                    .HasFlag( FileAttributes.Directory );
                if(File.Exists(path) || Directory.Exists(fullPath))
                {
                    exists = true;
                }
                if(exists)
                {
                    pathList.Add(fullPath);
                }
            } catch(Exception)
            {
                // おそらくパスが無効なので無視する
            }
        }

        public void AddMultiPath(string paths)
        {
            var pathArray = paths.Split(';');
            foreach(var path in pathArray)
            {
                AddPath(path);
            }
        }

        public string GetFile(string fileName)
        {
            foreach(var path in pathList)
            {
                var fullPath = Path.Combine(path, fileName);
                if(File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            return null;
        }
    }
}