using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FFF.IO.Native
{
    internal static class FileOperationAPIWrapper
    {

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        /// <summary>
        /// Send file to recycle bin
        /// </summary>
        /// <param name="path">Location of directory or file to recycle</param>
        /// <param name="flags">FileOperationFlags to add in addition to FOF_ALLOWUNDO</param>
        public static bool Send(string path, FileOperationFlags flags)
        {
            try
            {
                var fs = new SHFILEOPSTRUCT
                {
                    wFunc = FileOperationType.FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = FileOperationFlags.FOF_ALLOWUNDO | flags
                };
                SHFileOperation(ref fs);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Send file to recycle bin.  Display dialog, display warning if files are too big to fit (FOF_WANTNUKEWARNING)
        /// </summary>
        /// <param name="path">Location of directory or file to recycle</param>
        public static bool Send(string path)
        {
            return Send(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_WANTNUKEWARNING);
        }

        /// <summary>
        /// Send file silently to recycle bin.  Surpress dialog, surpress errors, delete if too large.
        /// </summary>
        /// <param name="path">Location of directory or file to recycle</param>
        public static bool MoveToRecycleBin(string path)
        {
            return Send(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI | FileOperationFlags.FOF_SILENT);

        }

        private static bool deleteFile(string path, FileOperationFlags flags)
        {
            try
            {
                var fs = new SHFILEOPSTRUCT
                {
                    wFunc = FileOperationType.FO_DELETE,
                    pFrom = path + '\0' + '\0',
                    fFlags = flags
                };
                SHFileOperation(ref fs);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool DeleteCompletelySilent(string path)
        {
            return deleteFile(path,
                              FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI |
                              FileOperationFlags.FOF_SILENT);
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
          uint pSessionHandle,
          uint nFiles,
          string[] rgsFilenames,
          uint nApplications,
          [In] RmUniqueProcess[] rgApplications,
          uint nServices,
          string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        private static extern int RmStartSession(
          out uint pSessionHandle,
          int dwSessionFlags,
          string strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
          uint dwSessionHandle,
          out uint pnProcInfoNeeded,
          ref uint pnProcInfo,
          [In, Out] RmProcessInfo[] rgAffectedApps,
          ref uint lpdwRebootReasons);

        private struct RmUniqueProcess
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        private enum RmAppType
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000, // 0x000003E8
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RmProcessInfo
        {
            public RmUniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string strServiceShortName;
            public RmAppType ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        public static IEnumerable<int> GetPids(string filePath)
        {
            string strSessionKey = Guid.NewGuid().ToString();
            List<int> pids = new List<int>();
            uint pSessionHandle;
            if (RmStartSession(out pSessionHandle, 0, strSessionKey) != 0)
            {
                Trace.TraceError("Cannot start RmStartSession");
                return (IEnumerable<int>)pids;
            }
            try
            {
                uint pnProcInfoNeeded = 0;
                uint pnProcInfo1 = 0;
                uint lpdwRebootReasons = 0;
                string[] rgsFilenames = new string[1] { filePath };
                if (RmRegisterResources(pSessionHandle, (uint)rgsFilenames.Length, rgsFilenames, 0U, (RmUniqueProcess[])null, 0U, (string[])null) != 0)
                {
                    Trace.TraceError("RmRegisterResources failded " + filePath);
                    return (IEnumerable<int>)pids;
                }
                int list = RmGetList(pSessionHandle, out pnProcInfoNeeded, ref pnProcInfo1, (RmProcessInfo[])null, ref lpdwRebootReasons);
                switch (list)
                {
                    case 0:
                        break;
                    case 234:
                        RmProcessInfo[] rgAffectedApps = new RmProcessInfo[(int)pnProcInfoNeeded];
                        uint pnProcInfo2 = pnProcInfoNeeded;
                        if (RmGetList(pSessionHandle, out pnProcInfoNeeded, ref pnProcInfo2, rgAffectedApps, ref lpdwRebootReasons) == 0)
                        {
                            foreach (RmProcessInfo rmProcessInfo in rgAffectedApps)
                                pids.Add(rmProcessInfo.Process.dwProcessId);
                            break;
                        }
                        break;
                    default:
                        Trace.TraceError(string.Format("RmGetList failded resource:{0}, error {1}", (object)filePath, (object)list));
                        break;
                }
            }
            finally
            {
                RmEndSession(pSessionHandle);
            }
            return (IEnumerable<int>)pids;
        }
    }
}
