using System;
using System.Runtime.InteropServices;

namespace BotwUnpacker;

public static class Utilities
{
    public static void OpenDirectory(string path)
    {
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                // Windows
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer",
                    Arguments = path,
                    UseShellExecute = true
                });
                break;
            case PlatformID.Unix:
                // Linux
                System.Diagnostics.Process.Start("xdg-open", path);
                break;
            case PlatformID.MacOSX:
                // macOS
                System.Diagnostics.Process.Start("open", path);
                break;
            default:
                throw new NotSupportedException("Unsupported platform");
        }
    }
}