using FFF.IO.Helper;
using FFF.IO.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace FFF.IO
{
    public sealed class FileIO
    {
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;

        public static bool IsLocked(string filePath, bool throwIfNotExists = true) =>
            IsLocked(new FileInfo(filePath), throwIfNotExists);

        public static bool IsLocked(FileInfo fileInfo, bool throwIfNotExists = true)
        {
            FileInfo tempFile = ValidateLongPath(fileInfo.FullName);
            if (tempFile.Exists)
            {
                try
                {
                    //if this does not throw exception then the file is not use by another program
                    using (FileStream fs = tempFile.Open(FileMode.Open, FileAccess.Read, FileShare.None)) fs.Close();
                }
                catch (IOException)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    return true;
                }
            }
            else if (!throwIfNotExists) return true;
            else throw new FileNotFoundException("Specified path is not exists", fileInfo.FullName);
            return false;
        }

        public static string ReadText(FileInfo fileInfo, Encoding encoding) =>
            string.Join(Environment.NewLine, ReadTextWithLine(fileInfo, encoding));

        public static string ReadText(FileInfo fileInfo) =>
            string.Join(Environment.NewLine, ReadTextWithLine(fileInfo, Encoding.UTF8));

        public static string ReadText(string filePath, Encoding encoding) =>
            ReadText(new FileInfo(filePath), encoding);

        public static string ReadText(string filePath) =>
            ReadText(new FileInfo(filePath), Encoding.UTF8);

        public static string[] ReadTextWithLine(string filePath, Encoding encoding) =>
            ReadTextWithLine(new FileInfo(filePath), encoding);

        public static string[] ReadTextWithLine(string filePath) =>
            ReadTextWithLine(new FileInfo(filePath), Encoding.UTF8);

        public static string[] ReadTextWithLine(FileInfo fileInfo) =>
            ReadTextWithLine(fileInfo, Encoding.UTF8);

        public static string[] ReadTextWithLine(FileInfo fileInfo, Encoding encoding)
        {
            FileInfo tempFile = ValidateLongPath(fileInfo.FullName);
            if (!tempFile.Exists) throw new FileNotFoundException("Specified path is not exists", fileInfo.FullName);
            fileInfo = tempFile;
            Queue<string> lines = new Queue<string>();
            using (FileStream fileStream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader streamReader = new StreamReader(fileStream, encoding))
                {
                    StringBuilder sb = new StringBuilder();
                    int symbol = streamReader.Peek();
                    while (true)
                    {
                        symbol = streamReader.Read();
                        if (symbol.Equals(-1)) break;
                        // Check line delimiter
                        if ((symbol == 13 && streamReader.Peek() == 10) || // "\r\n": //Windows
                            symbol == 13 || // "\r": //Macintosh
                            symbol == 10) // "\n": //UnixLinux
                        {
                            // If line delimiter == windows, will read next current
                            if (symbol == 13 && streamReader.Peek() == 10) streamReader.Read();
                            string line = sb.ToString();
                            sb.Clear();
                            lines.Enqueue(line);
                        }
                        else sb.Append((char)symbol);
                    }
                    if (!string.IsNullOrWhiteSpace(sb.ToString()))
                    {
                        string line = sb.ToString();
                        sb.Clear();
                        lines.Enqueue(line);
                    }
                }
            }
            return lines.ToArray();
        }

        public static void WriteText(string filePath, string content, string lineDelimiterEnum, bool startNewLine, bool endNewLine, Encoding encoding, bool append = false) =>
            WriteText(new FileInfo(filePath), content, lineDelimiterEnum, startNewLine, endNewLine, encoding, append);

        public static void WriteText(FileInfo fileInfo, string content, string lineDelimiterEnum, bool startNewLine, bool endNewLine, Encoding encoding, bool append = false)
        {
            StringBuilder temp = new StringBuilder();
            if (startNewLine) temp.Append(lineDelimiterEnum);
            temp.Append(content);
            if (endNewLine) temp.Append(lineDelimiterEnum);
            WriteText(fileInfo, temp.ToString(), encoding, append);
        }

        public static void WriteText(string filePath, string content, Encoding encoding, bool append = false) =>
            WriteText(new FileInfo(filePath), content, encoding, append);

        public static void WriteText(FileInfo fileInfo, string content, Encoding encoding, bool append = false)
        {
            fileInfo = ValidateLongPath(fileInfo.FullName);

            using (FileStream fs = fileInfo.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                if (append) fs.Seek(0, SeekOrigin.End);
                else
                {
                    fs.Position = 0;
                    fs.SetLength(0);
                }
                using (StreamWriter sw = new StreamWriter(fs, encoding))
                {
                    sw.Write(content);
                    sw.Flush();
                }
            }

            SubscribeProcessCompleted(fileInfo);
        }

        public static void Rename(string filePath, string newName) =>
             Rename(new FileInfo(filePath), newName);

        public static void Rename(FileInfo fileInfo, string newName)
        {
            FileInfo tempFile = ValidateLongPath(fileInfo.FullName);

            if (!tempFile.Exists) throw new FileNotFoundException("Specified path is not exists", fileInfo.FullName);
            FileInfo tempRename = ValidateLongPath(Path.Combine(fileInfo.Directory.FullName, newName));
            tempFile.MoveTo(tempRename.FullName);
            SubscribeProcessCompleted(tempRename);
        }

        public static void Copy(string source, string destination, bool isOverwrite = false, bool includeAccessControl = true, bool includeAttribute = true, bool isGenarateSamePath = true) =>
            Copy(new FileInfo(source), new FileInfo(destination), isOverwrite, includeAccessControl, includeAttribute, isGenarateSamePath);

        public static void Copy(FileInfo source, FileInfo destination, bool isOverwrite = false, bool includeAccessControl = true, bool includeAttribute = true, bool isGenarateSamePath = true)
        {
            FileInfo tempSource = ValidateLongPath(source.FullName);
            FileInfo tempDestination = ValidateLongPath(destination.FullName);

            if (!tempSource.Exists) throw new FileNotFoundException("Specified path is not exists", source.FullName);
            // Check same file
            if (source.FullName == destination.FullName)
            {
                if (!isGenarateSamePath) return;
                tempDestination = ValidateLongPath(BuildFileNameForDestination(source.FullName, destination.DirectoryName, false));
                // Copy the file
                tempSource.CopyTo(tempDestination.FullName, isOverwrite);
            }
            else tempSource.CopyTo(tempDestination.FullName, isOverwrite);
            // Wait copy file completed
            SubscribeProcessCompleted(tempDestination);
            // Update file access control
            if (includeAccessControl) SetAccessControl(tempDestination, tempSource);
            // Update file Attributes
            if (includeAttribute) FileSystemHelper.SetAttributes(tempDestination, tempSource);
        }

        public static void CopyFileWithDirectory(string source, string destinationDirectory, bool isOverwrite = false, bool includeAccessControl = true, bool includeAttribute = true) =>
             CopyFileWithDirectory(new FileInfo(source), new DirectoryInfo(destinationDirectory), isOverwrite, includeAccessControl, includeAttribute);

        public static void CopyFileWithDirectory(FileInfo source, DirectoryInfo destinationDirectory, bool isOverwrite = false, bool includeAccessControl = true, bool includeAttribute = true)
        {
            FileInfo destinationFile = new FileInfo(Path.Combine(destinationDirectory.FullName, source.Name));
            // Call Internal Copy
            Copy(source, destinationFile, isOverwrite, includeAccessControl, includeAttribute);
        }

        public static void Move(string source, string destination, bool isOverwrite = false) =>
             Move(new FileInfo(source), new FileInfo(destination), isOverwrite);

        public static void Move(FileInfo source, FileInfo destination, bool isOverwrite = false)
        {
            FileInfo tempSource = ValidateLongPath(source.FullName);
            FileInfo tempDestination = ValidateLongPath(destination.FullName);
            if (!tempSource.Exists) throw new FileNotFoundException("Specified path is not exists", source.FullName);
            // Check same folder
            if (source.FullName == destination.FullName) throw new IOException("Move file error because the destination file is the same as the source file. Please change destination path then try again.");
            // Check requie is need overwrite and check file exists
            if (tempDestination.Exists)
            {
                if (!isOverwrite) throw new IOException("Move file error because the destination file is the same as the source file. Please change destination path or replace file then try again.");
                tempDestination.Delete();
            }
            // Move file
            tempSource.MoveTo(tempDestination.FullName);
            // Wait move file completed
            SubscribeProcessCompleted(tempDestination);
        }

        public static string GetName(string filePath) =>
            GetName(new FileInfo(filePath));

        public static string GetName(FileInfo fileInfo) =>
            ValidateLongPath(fileInfo.FullName).Name;

        public static string GetNameWithoutExtension(string filePath) =>
            GetNameWithoutExtension(new FileInfo(filePath));

        public static string GetNameWithoutExtension(FileInfo fileInfo) =>
            ValidateLongPath(fileInfo.FullName)?.Name?.Replace(fileInfo?.Extension, string.Empty);

        public static string GetExtension(string filePath) =>
            GetExtension(new FileInfo(filePath));

        public static string GetExtension(FileInfo fileInfo) =>
            ValidateLongPath(fileInfo.FullName).Extension;

        public static void Create(string filePath)
        {
            FileInfo info = ValidateLongPath(filePath);
            using (FileStream temp = info.Create()) { }
            SubscribeProcessCompleted(info);
        }

        public static void Delete(string filePath, bool isRecycleBin = false) =>
            Delete(new FileInfo(filePath), isRecycleBin);

        public static void Delete(FileInfo fileInfo, bool isRecycleBin = false)
        {
            FileInfo tempFile = ValidateLongPath(fileInfo.FullName);
            if (!fileInfo.Exists) throw new FileNotFoundException("Specified path is not exists", fileInfo.FullName);
            fileInfo = tempFile;
            if (isRecycleBin) FileOperationAPIWrapper.MoveToRecycleBin(fileInfo.FullName);
            else fileInfo.Delete();
            SubscribeProcessCompleted(fileInfo, false);
        }

        public static bool IsExists(string filePath) =>
            System.IO.File.Exists(ResolveLongPath(filePath));

        public static bool IsExists(FileInfo fileInfo) =>
            ValidateLongPath(fileInfo.FullName).Exists;

        public static bool HasAccess(FileInfo fileInfo, FileSystemRights right)
        {
            // Get the collection of authorization rules that apply to the directory.
            AuthorizationRuleCollection acl = fileInfo.GetAccessControl()
                .GetAccessRules(true, true, typeof(SecurityIdentifier));
            return UserSecurityAccessHelper.HasFileOrDirectoryAccess(right, acl);
        }

        public static bool HasAccess(string filePath, FileSystemRights right) =>
            HasAccess(new FileInfo(ResolveLongPath(filePath)), right);

        public static string BuildFileNameForDestination(string source, string destination, bool isOverwrite)
        {
            string fileName = Path.GetFileNameWithoutExtension(source);
            string extension = Path.GetExtension(source);
            string destinationPath = $"{destination}\\{fileName}{extension}";
            if (isOverwrite) return destinationPath;
            else
            {
                bool isExists = true; int index = 0; string fileNameDestination = string.Empty;
                while (isExists)
                {
                    fileNameDestination = $"{destination}\\{fileName}" + "{0}" + extension;
                    if (index == 0) fileNameDestination = string.Format(fileNameDestination, string.Empty);
                    if (index == 1) fileNameDestination = string.Format(fileNameDestination, $" - Copy");
                    if (index > 1) fileNameDestination = string.Format(fileNameDestination, $" - Copy ({index})");
                    isExists = IsExists(fileNameDestination);
                    index++;
                }
                return fileNameDestination;
            }
        }

        internal static void SubscribeProcessCompleted(FileInfo file)
        {
            Action<FileInfo> checkOpenFile = null;
            // Setup checkOpenFile
            checkOpenFile = (item) =>
            {
                FileStream stream = null;
                try
                {
                    stream = item.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (Exception ex)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    int errorCode = Marshal.GetHRForException(ex) & ((1 << 16) - 1);
                    if ((ex is IOException) && (errorCode == ERROR_SHARING_VIOLATION || errorCode == ERROR_LOCK_VIOLATION))
                    {
                        Thread.Sleep(100);
                        checkOpenFile(item);
                    }
                    else throw;
                }
                finally
                {
                    if (stream != null) stream.Close();
                }
            };
            // Progress file
            checkOpenFile(file);
        }

        internal static void SubscribeProcessCompleted(FileInfo file, bool isExists)
        {
            Action<FileInfo> checkExistsFile = null;
            // Setup checkExistsDirectory
            checkExistsFile = (item) =>
            {
                if (isExists ? DirectoryIO.IsExists(item.FullName) : !DirectoryIO.IsExists(item.FullName)) return;
                else
                {
                    Thread.Sleep(100);
                    checkExistsFile(item);
                }
            };
            // Progress file
            checkExistsFile(file);
        }

        internal static void SetAccessControl(FileInfo destination, FileInfo source)
        {
            FileSecurity sourceSecurity = source.GetAccessControl();
            sourceSecurity.SetAccessRuleProtection(true, true);
            destination.SetAccessControl(sourceSecurity);
        }

        internal static FileInfo ValidateLongPath(string path) =>
            new FileInfo(ResolveLongPath(path));

        internal static string ResolveLongPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.Length > 259 && !path.StartsWith(@"\\")) return $@"\\?\{path}";
            else return path;
        }

        public static IEnumerable<int> GetPids(string path) =>
            FileOperationAPIWrapper.GetPids(path);
    }
}
