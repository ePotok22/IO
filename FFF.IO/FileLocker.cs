using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FFF.IO
{
    public sealed class FileLocker
    {
        private static readonly IDictionary<string, FileStream> _lockedFiles = new ConcurrentDictionary<string, FileStream>();
        private static readonly ConcurrentQueue<string> _waitResume = new ConcurrentQueue<string>();

        public static void Lock(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Lock(new FileInfo(path));
        }

        public static void Lock(FileInfo file)
        {
            if (file == null) return;
            if (FileIO.IsLocked(file.FullName)) return;
            try { _lockedFiles.Add(file.FullName, file.Open(FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)); }
            catch { }
        }

        public static void Release(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Release(new FileInfo(path));
        }

        public static void Release(FileInfo file)
        {
            if (file == null) return;
            if (!FileIO.IsLocked(file)) return;
            using (FileStream fs = _lockedFiles[file.FullName]) { }
            _lockedFiles.Remove(file.FullName);
        }

        public static void Disable()
        {
            foreach (string file in _lockedFiles.Keys.ToArray())
            {
                Release(file);
                _waitResume.Enqueue(file);
            }
        }

        public static void Enable()
        {
            while (_waitResume.TryDequeue(out string file)) Lock(file);
        }

    }
}
