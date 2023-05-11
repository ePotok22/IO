using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace FFF.IO
{
    public static class PathExtensions
    {
        private static readonly char[] InvalidPathCharacters = Path.GetInvalidPathChars();
        private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();

        public static bool IsPathBaseOf(this string basePath, string toCheck)
        {
            try
            {
                return basePath != null && toCheck != null && new Uri(Path.Combine(basePath, Path.PathSeparator.ToString())).IsBaseOf(new Uri(toCheck));
            }
            catch { }
            return false;
        }

        public static bool IsPathEqual(this string basePath, string toCheck)
        {
            try
            {
                if (basePath == null && toCheck == null)
                    return true;
                if (basePath == null || toCheck == null)
                    return false;
                return basePath.EnforcePrimaryDirectorySeparator().Equals(toCheck.EnforcePrimaryDirectorySeparator(), StringComparison.OrdinalIgnoreCase) || Uri.Compare(new Uri(basePath), new Uri(toCheck), UriComponents.AbsoluteUri, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;
            }
            catch
            {
            }
            return false;
        }

        public static bool IsPathEqualOrBaseOf(this string basePath, string toCheck) =>
            basePath.IsPathBaseOf(toCheck) || basePath.IsPathEqual(toCheck);

        public static bool IsPathAbsolute(string path)
        {
            try
            {
                Uri uri = new Uri(path);
                return uri.IsUnc || uri.IsAbsoluteUri;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsNetworkPath(this string path)
        {
            if (IsUri(path))
            {
                Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                    return false;
                else if (uri.IsFile)
                    return uri.IsUnc;
                else
                    return false;
            }
            else
                return false;
        }

        public static bool IsExpandEnvironmentVariables(this string path) =>
             path != Environment.ExpandEnvironmentVariables(path);

        public static bool IsUri(this string path) =>
            Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri uri);

        public static bool IsPathHttp(this string path)
        {
            if (IsUri(path))
            {
                try
                {
                    Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
                    return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
                }
                catch
                {
                    return false;
                }
            }
            else
                return false;
        }

        public static bool IsPathRootApplication(this string path) =>
            path.Trim().StartsWith(@"~", StringComparison.OrdinalIgnoreCase);

        public static bool IsFullPath(this string path) =>
            path != Path.GetFullPath(path);

        public static bool IsPathRelative(this string path)
        {
            if (IsUri(path))
            {
                Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
                return !uri.IsAbsoluteUri;
            }
            else
                return false;
        }

        public static bool IsPathFile(this string path)
        {
            if (IsUri(path))
            {
                Uri uri = new Uri(path, UriKind.RelativeOrAbsolute);
                if (!uri.IsAbsoluteUri)
                    return false;
                else if (uri.IsFile)
                    return !uri.IsUnc;
                else
                    return false;
            }
            else
                return false;
        }

        public static string MakeRelative(this string filePath, string referencePath)
        {
            try
            {
                return Uri.UnescapeDataString((!referencePath.EndsWith(Path.DirectorySeparatorChar.ToString()) || !referencePath.EndsWith(Path.AltDirectorySeparatorChar.ToString()) ? new Uri(referencePath + Path.DirectorySeparatorChar.ToString()) : new Uri(referencePath)).MakeRelativeUri(new Uri(filePath)).OriginalString);
            }
            catch (Exception ex)
            {
                Trace.TraceError(string.Format("MakeRelative error {0}", (object)ex));
            }
            return (string)null;
        }

        public static string MakeAbsolute(this string relativePath, string referencePath)
        {
            try
            {
                return IsPathAbsolute(relativePath) ? relativePath : Path.Combine(referencePath, relativePath);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
            return (string)null;
        }

        public static string EnforcePrimaryDirectorySeparator(this string filePath) =>
            filePath?.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        public static bool FileNameContainsInvalidCharacters(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || fileName.GetCharactersIncompatibleWithWorkflowIdentity().Any<char>();
        }

        public static bool PathContainsInvalidCharacters(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            return fileName.IndexOfAny(PathExtensions.InvalidPathCharacters) >= 0 || fileName.GetCharactersIncompatibleWithWorkflowIdentity().Any<char>();
        }

        public static string ExpandEnvironmentVariables(this string path) =>
            string.IsNullOrEmpty(path) ? path : Environment.ExpandEnvironmentVariables(path);

        public static string GetSafeFilename(this string filename, string separator = null) =>
            string.Join(separator ?? string.Empty, filename.Split(Path.GetInvalidFileNameChars()));

        public static string GetSafePath(this string path)
        {
            if (path.Trim().StartsWith("@\""))
                path = path.Remove(0, 1);

            return string.Join(string.Empty, path.Split(Path.GetInvalidPathChars()));
        }

        public static IEnumerable<char> GetInvalidFileNameCharacters(this string fileName) =>
            string.IsNullOrEmpty(fileName) ? null : fileName.Where(c => InvalidFileNameCharacters.Contains(c)).Union(fileName.GetCharactersIncompatibleWithWorkflowIdentity());

        public static IEnumerable<char> GetInvalidPathCharacters(this string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return (IEnumerable<char>)null;
            IEnumerable<char> source = filePath.Where<char>(c => InvalidPathCharacters.Contains(c)).Union(filePath.GetCharactersIncompatibleWithWorkflowIdentity());
            if (!source.Any<char>())
                source = PathExtensions.GetInvalidFileNameCharactersInPath(filePath);
            return source;
        }

        private static IEnumerable<char> GetInvalidFileNameCharactersInPath(string filePath)
        {
            try
            {
                string pathRoot = Path.GetPathRoot(filePath);
                return filePath.Substring(pathRoot.Length).Where<char>((Func<char, bool>)(c => (int)c != (int)Path.AltDirectorySeparatorChar && (int)c != (int)Path.DirectorySeparatorChar && ((IEnumerable<char>)PathExtensions.InvalidFileNameCharacters).Contains<char>(c)));
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
            }
            return Enumerable.Empty<char>();
        }

        private static IEnumerable<char> GetCharactersIncompatibleWithWorkflowIdentity(this string text) =>
             text.Where(c => c == ';' || char.IsControl(c));

        public static string GetShortFileName(this string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return filePath;

            string fullPath = Path.GetFullPath(filePath);
            char directorySeparatorChar = Path.AltDirectorySeparatorChar;
            string oldValue = directorySeparatorChar.ToString();

            directorySeparatorChar = Path.DirectorySeparatorChar;

            string newValue = directorySeparatorChar.ToString();
            string str = fullPath.Replace(oldValue, newValue);
            int num = str.LastIndexOf(Path.DirectorySeparatorChar);

            return num >= 0 ? str.Substring(num + 1) : filePath;
        }

        public static string RemoveExtension(this string file, string extension)
        {
            if (string.IsNullOrEmpty(extension) || file == null || !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return file;
            file = file.Substring(0, file.LastIndexOf(extension, StringComparison.OrdinalIgnoreCase));
            return file;
        }

        public static string AddExtension(this string file, string extension) =>
            file.RemoveExtension(extension) + extension;

        public static bool CheckDirectoryExists(this string path, IFileSystem fileSystem)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && fileSystem.Directory.Exists(path) && Uri.TryCreate(path, UriKind.Absolute, out Uri _);
            }
            catch (Exception ex)
            {
                Trace.TraceError(ex.Message);
                return false;
            }
        }

        public static string RemoveTrailingSlash(this string path) =>
            path?.TrimEnd('\\', '/');

    }
}
