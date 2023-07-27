using System;

namespace FFF.IO
{
    public sealed class LegacyPaths
    {
        public static string LocalAppData() =>
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public static string AppData() =>
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string UserProfile() =>
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
