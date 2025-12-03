using System.Runtime.InteropServices;

namespace Infinity.Core
{
    internal class Platform
    {
        public static bool IsWindows
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            }
        }

        public static bool IsLinux
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            }
        }

        public static bool IsMacOS
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            }
        }

        public static bool IsFreeBSD
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
            }
        }
    }
}
