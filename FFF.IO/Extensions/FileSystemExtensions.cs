using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;

namespace FFF.IO
{
    public static class FileSystemExtensions
    {
        private const int DEFAULT_TIMEOUT_MS = 1500;

        public static bool ExistsWithTimeout(this DirectoryBase directoryBase,string path, int timeoutInMiliseconds = DEFAULT_TIMEOUT_MS) =>
            Task.Run<bool>(() => directoryBase.Exists(path)).DefaultAfter<bool>(TimeSpan.FromMilliseconds(timeoutInMiliseconds)).Result;

        public static bool? IsDirectoryWritableWithTimeout(this IFileSystem fileSystem, string dirPath, int timeoutInMiliseconds = DEFAULT_TIMEOUT_MS) =>
            new bool?(Task.Run(() => fileSystem.IsDirectoryWritable(dirPath)).DefaultAfter<bool>(TimeSpan.FromMilliseconds(timeoutInMiliseconds)).Result);

        public static bool IsDirectoryWritable(this IFileSystem fileSystem, string dirPath)
        {
            try
            {
                string path = Path.Combine(dirPath, fileSystem.Path.GetRandomFileName());
                using (fileSystem.File.Create(path, 1, FileOptions.DeleteOnClose)) { }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void EnforcePathDirectory(this IFileSystem fileSystem, string path)
        {
            string directoryName = fileSystem.Path.GetDirectoryName(path);
            if (fileSystem.Directory.Exists(directoryName))
                return;
            fileSystem.Directory.CreateDirectory(directoryName);
        }

        public static void EnforceDirectory(this IFileSystem fileSystem, string directoryName)
        {
            if (fileSystem.Directory.Exists(directoryName))
                return;
            fileSystem.Directory.CreateDirectory(directoryName);
        }

        public static void TryDeleteFile(this IFileSystem fileSystem, string path)
        {
            if (!fileSystem.File.Exists(path))
                return;
            fileSystem.File.Delete(path);
        }

        public static IEnumerable<string> GetFiles(this IFileSystem fileSystem, string directoryPath, string searchPattern) =>
            !fileSystem.Directory.Exists(directoryPath) ? Enumerable.Empty<string>() : (IEnumerable<string>)fileSystem.Directory.GetFiles(directoryPath, searchPattern) ?? Enumerable.Empty<string>();

        public static void DirectoryCopy(
          this IFileSystem fileSystem,
          string from,
          string dest,
          Func<DirectoryInfoBase, bool> directoryFilter = null,
          Func<FileInfoBase, bool> fileFilter = null,
          bool overwrite = false)
        {
            DirectoryInfoBase directoryInfoBase1 = fileSystem.DirectoryInfo.FromDirectoryName(from) as DirectoryInfoBase;
            DirectoryInfoBase directoryInfoBase2 = fileSystem.DirectoryInfo.FromDirectoryName(dest) as DirectoryInfoBase;
            fileSystem.Directory.CreateDirectory(directoryInfoBase2.FullName);
            foreach (string file in fileSystem.Directory.GetFiles(from))
            {
                FileInfoBase fileInfoBase = fileSystem.FileInfo.FromFileName(file) as FileInfoBase;
                if (fileFilter == null || !fileFilter(fileInfoBase))
                {
                    string str = file.Replace(from, dest);
                    if (!fileSystem.File.Exists(str) | overwrite)
                        fileSystem.File.Copy(file, str, overwrite);
                }
            }
            foreach (DirectoryInfoBase directory in directoryInfoBase1.GetDirectories())
            {
                if (directoryFilter == null || !directoryFilter(directory))
                {
                    string dest1 = fileSystem.Path.Combine(dest, directory.Name);
                    fileSystem.DirectoryCopy(directory.FullName, dest1, directoryFilter, fileFilter, overwrite);
                }
            }
        }
    }
}
