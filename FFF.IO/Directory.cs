using FFF.IO.Helper;
using FFF.IO.Native;
using System;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace FFF.IO
{
    public sealed class DirectoryIO
    {
        public static void Rename(string directoryPath, string newName) =>
            Rename(new DirectoryInfo(directoryPath), newName);

        public static void Rename(DirectoryInfo directoryInfo, string newName)
        {
            DirectoryInfo tempDirectory = ValidateDirectoryLongPath(directoryInfo.FullName);
            if (!tempDirectory.Exists) throw new DirectoryNotFoundException(directoryInfo.FullName);
            DirectoryInfo tempRename = ValidateDirectoryLongPath(Path.Combine(directoryInfo.Parent.FullName, newName));
            tempDirectory.MoveTo(tempRename.FullName);
            SubscribeProcessCompleted(tempRename, true);
        }

        public static void Copy(string source, string destination, bool isOverwrite = false, bool includeDirectory = false, bool includeAccessControl = true, bool includeAttribute = true) =>
            Copy(new DirectoryInfo(source), new DirectoryInfo(destination), isOverwrite, includeDirectory, includeAccessControl, includeAttribute);

        public static void Copy(DirectoryInfo source, DirectoryInfo destination, bool isOverwrite = false, bool includeDirectory = false, bool includeAccessControl = true, bool includeAttribute = true)
        {
            source = ValidateDirectoryLongPath(source.FullName);
            destination = ValidateDirectoryLongPath(destination.FullName);
            if (!source.Exists) throw new DirectoryNotFoundException(source.FullName);
            if (includeDirectory)
            {
                DirectoryInfo tempDestination = ValidateDirectoryLongPath(destination.FullName);
                DirectoryInfo tempSource = ValidateDirectoryLongPath(source.FullName);
                destination = ValidateDirectoryLongPath(Path.Combine(tempDestination.FullName, tempSource.Name));
                // Check same file
                if (source.FullName == destination.FullName) destination = ValidateDirectoryLongPath(BuildNameForDestination(source.FullName, destination.Parent.FullName));
                // Exists
                if (!isOverwrite && destination.Exists) throw new IOException("Copy folder error because the destination folder is the same as the source folder. Please change destination path or replace folder then try again.");
                if (!destination.Exists)
                {
                    destination.Create();
                    // Wait directory file completed
                    SubscribeProcessCompleted(destination, true);
                }
                // Update file system permission
                if (includeAccessControl) SetAccessControl(destination, source);
                // Update file Attributes
                if (includeAttribute) FileSystemHelper.SetAttributes(destination, source);
            }
            else
            {
                if (!destination.Exists)
                {
                    destination.Create();
                    // Wait directory file completed
                    SubscribeProcessCompleted(destination, true);
                }
            }
            try
            {
                //Now Create all of the directories
                Action<DirectoryInfo> createDirectoties = null;
                createDirectoties = (itemSource) =>
                {
                    Parallel.ForEach(itemSource.GetDirectories(), ParallelHelper.Options, (item) =>
                    {
                        DirectoryInfo tempItem = ValidateDirectoryLongPath(item.FullName);
                        DirectoryInfo tempDirectory = ValidateDirectoryLongPath(item.FullName.Replace(source.FullName, destination.FullName));
                        if (!tempDirectory.FullName.Equals(tempItem.FullName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!tempDirectory.Exists)
                            {
                                tempDirectory.Create();
                                // Wait directory file completed
                                SubscribeProcessCompleted(tempDirectory, true);
                            }
                            // Update file system permission
                            if (includeAccessControl) SetAccessControl(tempDirectory, tempItem);
                            // Update file Attributes
                            if (includeAttribute) FileSystemHelper.SetAttributes(tempDirectory, tempItem);
                        }
                        if (tempItem.GetDirectories().Count() > 0) createDirectoties(item);
                    });
                };
                createDirectoties(source);
            }
            catch (AggregateException ex)
            {
                //throw ex.InnerException;
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            //---------------------------------------------------------------------------------
            try
            {
                //Copy all the files & Replaces any files with the same name
                Parallel.ForEach(source.GetFiles("*", SearchOption.AllDirectories), ParallelHelper.Options, (item) =>
                {
                    FileInfo tempItem = FileIO.ValidateLongPath(item.FullName);
                    string itemReplace = item.FullName.Replace(source.FullName, destination.FullName);
                    if (!itemReplace.Equals(tempItem.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        FileInfo tempfile = tempItem.CopyTo(itemReplace, isOverwrite);
                        // Wait copy file completed
                        FileIO.SubscribeProcessCompleted(tempfile);
                        // Update file system permission
                        if (includeAccessControl) FileIO.SetAccessControl(tempfile, tempItem);
                        // Update file Attributes
                        if (includeAttribute) FileSystemHelper.SetAttributes(tempfile, tempItem);
                    }
                });
            }
            catch (AggregateException ex)
            {
                //throw ex.InnerException;
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        public static void Move(string source, string destination, bool isOverwrite = false, bool includeDirectory = false) =>
            Move(new DirectoryInfo(source), new DirectoryInfo(destination), isOverwrite, includeDirectory);

        public static void Move(DirectoryInfo source, DirectoryInfo destination, bool isOverwrite = false, bool includeDirectory = false)
        {
            source = ValidateDirectoryLongPath(source.FullName);
            destination = ValidateDirectoryLongPath(destination.FullName);

            if (!source.Exists) throw new DirectoryNotFoundException(source.FullName);
            // Check same folder
            if (includeDirectory)
            {
                DirectoryInfo tempDestination = ValidateDirectoryLongPath(destination.FullName);
                DirectoryInfo tempSource = ValidateDirectoryLongPath(source.FullName);
                destination = ValidateDirectoryLongPath(Path.Combine(tempDestination.FullName, tempSource.Name));
                if (source.FullName == destination.FullName) throw new IOException("Move folder error because the destination folder is the same as the source folder. Please change destination path then try again.");
            }
            // Exists
            if (destination.Exists)
            {
                try
                {
                    //Now Create all of the directories
                    Action<DirectoryInfo> createDirectoties = null;
                    createDirectoties = (itemSource) =>
                    {
                        Parallel.ForEach(itemSource.GetDirectories(), ParallelHelper.Options, (item) =>
                        {
                            DirectoryInfo tempItem = ValidateDirectoryLongPath(item.FullName);
                            DirectoryInfo tempDirectory = ValidateDirectoryLongPath(item.FullName.Replace(source.FullName, destination.FullName));
                            if (!tempDirectory.Exists)
                            {
                                tempDirectory.Create();
                                // Wait directory file completed
                                SubscribeProcessCompleted(tempDirectory, true);
                            }
                            // Update file Access Control
                            SetAccessControl(tempDirectory, tempItem);
                            // Update file Attributes
                            FileSystemHelper.SetAttributes(tempDirectory, tempItem);
                            if (tempItem.GetDirectories().Count() > 0) createDirectoties(item);
                        });
                    };
                    createDirectoties(source);
                }
                catch (AggregateException ex)
                {
                    //throw ex.InnerException;
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
                try
                {
                    //Move all the files &Replaces any files with the same name
                    Parallel.ForEach(source.GetFiles("*", System.IO.SearchOption.AllDirectories), ParallelHelper.Options, (item) =>
                    {
                        FileInfo tempItem = FileIO.ValidateLongPath(item.FullName);
                        FileInfo tempfile = FileIO.ValidateLongPath(item.FullName.Replace(source.FullName, destination.FullName));
                        if (tempfile.Exists)
                        {
                            if (!isOverwrite) throw new IOException("The destination file is the same as the source file. Please change destination path or replace file then try again.");
                            tempfile.Delete();
                        }
                        tempItem.MoveTo(tempfile.FullName);
                    });
                    //Delete source directory
                    source.Delete(true);
                }
                catch (AggregateException ex)
                {
                    //throw ex.InnerException;
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            }
            else source.MoveTo(destination.FullName);
        }

        public static string GetName(DirectoryInfo directoryInfo) =>
            ValidateDirectoryLongPath(directoryInfo.FullName).Name;

        public static string[] GetDirectories(string directoryPath, System.IO.SearchOption searchOption, string searchPattern = "*") =>
            GetDirectories(new DirectoryInfo(directoryPath), searchOption, searchPattern);

        public static string[] GetDirectories(DirectoryInfo directoryInfo, System.IO.SearchOption searchOption, string searchPattern = "*") =>
            ValidateDirectoryLongPath(directoryInfo.FullName).GetDirectories(searchPattern, searchOption).Select(o => o.FullName).ToArray();

        public static string[] GetFiles(string directoryPath, System.IO.SearchOption searchOption, string searchPattern = "*") =>
            GetFiles(new DirectoryInfo(directoryPath), searchOption, searchPattern);

        public static string[] GetFiles(DirectoryInfo directoryInfo, System.IO.SearchOption searchOption, string searchPattern = "*") =>
            ValidateDirectoryLongPath(directoryInfo.FullName).GetFiles(searchPattern, searchOption).Select(o => o.FullName).ToArray();

        public static void Create(string directoryPath)
        {
            DirectoryInfo info = ValidateDirectoryLongPath(directoryPath);
            info.Create();
            SubscribeProcessCompleted(info, true);
        }

        public static void Delete(string directoryPath, bool isRecycleBin = false) =>
            Delete(new DirectoryInfo(directoryPath), isRecycleBin);

        public static void Delete(DirectoryInfo directoryInfo, bool isRecycleBin = false)
        {
            DirectoryInfo tempDirectory = ValidateDirectoryLongPath(directoryInfo.FullName);
            if (!tempDirectory.Exists) throw new DirectoryNotFoundException(directoryInfo.FullName);
            directoryInfo = tempDirectory;
            if (isRecycleBin) FileOperationAPIWrapper.MoveToRecycleBin(directoryInfo.FullName);
            else directoryInfo.Delete(true);
            SubscribeProcessCompleted(directoryInfo, false);
        }

        public static bool IsExists(DirectoryInfo directoryInfo) =>
            ValidateDirectoryLongPath(directoryInfo.FullName).Exists;

        public static bool IsExists(string directoryPath) =>
            System.IO.Directory.Exists(ResolveDirectoryLongPath(directoryPath));

        public static bool HasAccess(DirectoryInfo directory, FileSystemRights right)
        {
            // Get the collection of authorization rules that apply to the directory.
            AuthorizationRuleCollection acl = directory.GetAccessControl()
                .GetAccessRules(true, true, typeof(SecurityIdentifier));
            return UserSecurityAccessHelper.HasFileOrDirectoryAccess(right, acl);
        }

        public static bool HasAccess(string directoryPath, FileSystemRights right) =>
            HasAccess(new DirectoryInfo(ResolveDirectoryLongPath(directoryPath)), right);

        internal static DirectoryInfo ValidateDirectoryLongPath(string path) =>
            new DirectoryInfo(ResolveDirectoryLongPath(path));

        internal static string ResolveDirectoryLongPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (path.Length > 247 && !path.StartsWith(@"\\")) return $@"\\?\{path}";
            else return path;
        }

        internal static void SubscribeProcessCompleted(DirectoryInfo directory, bool isExists)
        {
            Action<DirectoryInfo> checkExistsDirectory = null;
            // Setup checkExistsDirectory
            checkExistsDirectory = (item) =>
            {
                if (isExists ? IsExists(item.FullName) : !IsExists(item.FullName)) return;
                else { Thread.Sleep(100); checkExistsDirectory(item); }
            };
            // Progress file
            checkExistsDirectory(directory);
        }

        internal static void SetAccessControl(DirectoryInfo destination, DirectoryInfo source)
        {
            System.Security.AccessControl.DirectorySecurity sourceSecurity = source.GetAccessControl();
            sourceSecurity.SetAccessRuleProtection(true, true);
            destination.SetAccessControl(sourceSecurity);
        }

        public static string BuildNameForDestination(string source, string destination)
        {
            string folderName = Path.GetFileNameWithoutExtension(source);
            bool isExists = true; int index = 0; string folderNameDestination = string.Empty;
            while (isExists)
            {
                folderNameDestination = $"{destination}\\{folderName}" + "{0}";
                if (index == 0) folderNameDestination = string.Format(folderNameDestination, string.Empty);
                if (index == 1) folderNameDestination = string.Format(folderNameDestination, $" - Copy");
                if (index > 1) folderNameDestination = string.Format(folderNameDestination, $" - Copy ({index})");
                isExists = IsExists(folderNameDestination);
                index++;
            }
            return folderNameDestination;
        }
    }
}
