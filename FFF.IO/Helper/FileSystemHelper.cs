using System.IO;

namespace FFF.IO.Helper
{
    internal static class FileSystemHelper
    {
        public static void SetAttributes(FileSystemInfo destination, FileSystemInfo source) =>
            destination.Attributes = source.Attributes;
    }
}
