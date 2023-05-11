using System.IO;
using System.Security.AccessControl;

namespace FFF.IO
{
    public static class DirectoryInfoExtensions
    {
        public static bool HasAccess(this DirectoryInfo directoryInfo, FileSystemRights right) =>
            DirectoryIO.HasAccess(directoryInfo, right);
    }
}
