using Avalonia;
using System;
using System.IO;

namespace BotwUnpacker;

class Program
{
    #if WINDOWS
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);
    #endif
    
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0) //Determine if to utilize application or pass console arguments
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        else if (args.Length > 0 && (!args[0].StartsWith("/"))) //Drag n' drop (no slash command executed)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (File.Exists(args[i])) 
                    ConsoleHandler.DragAndDropFile(args[i]);
                else if (Directory.Exists(args[i]))
                    ConsoleHandler.DragAndDropFolder(args[i]);
            }
        }
        else
        {
            #if WINDOWS
            AttachConsole(-1); //Pass to parent console that sent the arguments
            #endif
            ConsoleHandler.Commands(args);
        }
    }
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}