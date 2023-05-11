using System.IO;
using System.Security.AccessControl;

namespace FFF.IO
{
    public static class FileInfoExtensions
    {
        public static void Lock(this FileInfo file) =>
            FileLocker.Lock(file);

        public static void Release(this FileInfo file) =>
            FileLocker.Release(file);

        public static bool IsLocked(this FileInfo fileInfo, bool throwIfNotExists = true) =>
            FileIO.IsLocked(fileInfo, throwIfNotExists);

        public static bool HasAccess(this FileInfo fileInfo, FileSystemRights right) =>
            FileIO.HasAccess(fileInfo, right);
    }
}
