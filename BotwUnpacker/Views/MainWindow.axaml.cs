using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using BotwUnpacker.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace BotwUnpacker;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private CompareTool _compareTool;
    private PaddingTool _paddingTool;
    
    public MainWindow(IServiceProvider services, IConfiguration config, CompareTool compareTool, PaddingTool paddingTool)
    {
        _services = services;
        _config = config;
        _compareTool = compareTool;
        _paddingTool = paddingTool;

        InitializeComponent();

        TextFolderRoot.Text = _config["RootFolder"];
        if (!string.IsNullOrWhiteSpace(TextFolderRoot.Text))
        {
            ButtonBrowse.IsEnabled = true;
            ButtonOpenFolder.IsEnabled = true;
        }
        
        bool.TryParse(_config["LittleEndian"], out bool littleEndian);
        RadioButtonWiiU.IsChecked = !littleEndian;
        RadioButtonSwitch.IsChecked = littleEndian;

        CheckBoxDebugWriteSarcXml.IsVisible = CheckBoxDebugWriteYaz0Xml.IsVisible 
            = false;

        Footnote.Text = $"Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.Major + "." + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.Minor + "." + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.Build}\n" +
                        $"Made by Shadsterwolf\n" +
                        $"Heavily modified code from UWizard SARC\n" +
                        $"Port to Avalonia by Renny";
    }

    private async void ButtonBrowse_Click(object? sender, RoutedEventArgs e)
    {
        var opts = new FolderPickerOpenOptions
        {
            Title = "Select the root folder",
            AllowMultiple = false,
        };
        
        if (!string.IsNullOrWhiteSpace(TextFolderRoot.Text))
        {
            opts.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text));
        }
        
        var folders = await this.StorageProvider.OpenFolderPickerAsync(opts);
        if (!folders.Any())
            return;
        
        var folder = folders[0];
        TextFolderRoot.Text = folder.TryGetLocalPath();

        ButtonBrowse.IsEnabled = true;
        ButtonOpenFolder.IsEnabled = true;
        
        _config.AddOrUpdateSetting("RootFolder", TextFolderRoot.Text);
    }

    private async void ButtonOpenFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextFolderRoot.Text))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", "Please select a folder first", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }

        try
        {
            Utilities.OpenDirectory(TextFolderRoot.Text);
        }
        catch (Exception ex)
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to open folder: {ex.Message}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
        }
    }

    private async void UnpackSARC_Click(object? sender, RoutedEventArgs e)
    {
        var opts = new FilePickerOpenOptions
        {
            Title = "Select your packs",
            AllowMultiple = true,
        };
        if (!string.IsNullOrWhiteSpace(TextFolderRoot.Text))
            opts.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text));
        
        var files = await StorageProvider.OpenFilePickerAsync(opts);

        if (!files.Any())
            return;

        var filesUnpacked = 0;
        var filesDecoded = 0;
        var filesSkipped = 0;
        var filesErrored = 0;
        
        foreach (var file in files)
        {
            var fi = new FileInfo(file.Path.AbsolutePath);
            
            var folderName = Path.GetFileNameWithoutExtension(fi.FullName);
            var folderPath = Path.Combine(fi.DirectoryName, folderName);

            if (Directory.Exists(folderPath))
            {
                var overwritePrompt = await MessageBoxManager.GetMessageBoxStandard("Folder Exists",
                    $"The folder {folderName} already exists.\n\nDo you want to overwrite it?",
                    MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
                if (overwritePrompt is MsBox.Avalonia.Enums.ButtonResult.No or MsBox.Avalonia.Enums.ButtonResult.Cancel)
                {
                    filesSkipped++;
                    continue;
                }
            }

            var autoDecode = false;
            var nodeDecode = false;
            if (CheckBoxAutoDecode.IsChecked == true)
                nodeDecode = true;
            else
            {
                if (SARC.IsYaz0File(fi.FullName))
                {
                    var decodePrompt =await MessageBoxManager.GetMessageBoxStandard("Decode", $"The file {file.Name} is a Yaz0 file.\n\nDo you want to decode it and attempt to extract?", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Question).ShowAsync();
                    if (decodePrompt == ButtonResult.Yes)
                        autoDecode = true;
                    else
                    {
                        filesSkipped++;
                        continue;
                    }
                }
            }

            if (!SARC.Extract(fi.FullName, folderPath, autoDecode, nodeDecode))
            {
                await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to extract file: {fi.Name}\n\n{SARC.lerror}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                filesErrored++;
                continue;
            }

            if (autoDecode || nodeDecode)
                filesDecoded++;
            
            filesUnpacked++;
            
            if (CheckBoxDebugWriteSarcXml.IsChecked == true)
            {
                var debugFilePath = Path.Combine(fi.DirectoryName, $"{fi.Name}_SarcDebug.xml");
                if (File.Exists(debugFilePath))
                {
                    var overwritePrompt = await MessageBoxManager.GetMessageBoxStandard("File Exists",
                        $"The file {debugFilePath} already exists.\n\nDo you want to overwrite it?",
                        MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
                    
                    if (overwritePrompt is MsBox.Avalonia.Enums.ButtonResult.No or MsBox.Avalonia.Enums.ButtonResult.Cancel)
                        continue;
                }

                if (!DebugWriter.WriteSarcXml(fi.FullName, debugFilePath))
                {
                    await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to write debug file: {debugFilePath}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                    filesErrored++;
                }
            }
        }
        
        if (filesUnpacked > 0)
            Utilities.OpenDirectory(TextFolderRoot.Text);
        
    }

    private async void UnpackALL_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextFolderRoot.Text) || !Directory.Exists(TextFolderRoot.Text))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", "Please select a folder first", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }

        if (CheckBoxCompileAll.IsChecked == true)
        {
            var compileAllPrompt = await MessageBoxManager.GetMessageBoxStandard("Compile All", 
                "Extract all SARC data type files from default path?\n" + 
                TextFolderRoot.Text + "\n" + 
                "*This does not include subfolders\n\n" + 
                "You are choosing to compile all extracted data to ONE folder!\n" + 
                "You'll then select a folder where you want to place them all in.", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Question).ShowAsync();

            if (compileAllPrompt == ButtonResult.No)
                return;
        }
        else
        {
            var compilePrompt = await MessageBoxManager.GetMessageBoxStandard("Compile", 
                "Extract all SARC data type files from default path?\n" + 
                TextFolderRoot.Text + "\n" + 
                "*This does not include subfolders\n\n" + 
                "This will generate SEPARATE folders of every file it unpacks!", MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Question).ShowAsync();

            if (compilePrompt == ButtonResult.No)
                return;
        }

        var folder = new DirectoryInfo(TextFolderRoot.Text);

        string folderPath;
        int sarcCount = 0;
        
        bool autoDecode = false;
        bool nodeDecode = CheckBoxAutoDecode.IsChecked == true;

        if (CheckBoxCompileAll.IsChecked == true)
        {
            var opts = new FolderPickerOpenOptions
            {
                Title = "Select the root folder",
                AllowMultiple = false,
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text)),
            };
            
            var folders = await this.StorageProvider.OpenFolderPickerAsync(opts);
            if (!folders.Any())
                return;
            
            var selectedFolder = folders[0];
            var dirInfo = new DirectoryInfo(selectedFolder.Path.AbsolutePath);
            folderPath = dirInfo.FullName;
            
            foreach (var file in folder.GetFiles())
            {
                if (SARC.Extract(file.FullName, folderPath, autoDecode, nodeDecode))
                    sarcCount++;
            }
        }
        else
        {
            foreach (var file in folder.GetFiles())
            {
                var folderName = Path.GetFileNameWithoutExtension(file.FullName);
                folderPath = Path.Combine(file.DirectoryName, folderName);
                if (SARC.Extract(file.FullName, folderPath, autoDecode, nodeDecode))
                    sarcCount++;
            }
        }
        
        var messageBox = await MessageBoxManager.GetMessageBoxStandard("Unpack Complete", 
            $"Unpacked {sarcCount} files.\n\n" + 
            "You can now open the folders to view the unpacked files.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
    }

    private async void YazDecode_Click(object? sender, RoutedEventArgs e)
    {
        var opts = new FilePickerOpenOptions
        {
            Title = "Select your packs",
            AllowMultiple = true,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text)),
        };
        
        var files = await StorageProvider.OpenFilePickerAsync(opts);
        if (!files.Any())
            return;
        
        var filesDecoded = 0;
        foreach (var file in files)
        {
            var fi = new FileInfo(file.Path.AbsolutePath);
            var success = Yaz0.Decode(fi.FullName, Yaz0.DecodeOutputFileRename(fi.FullName));
            if (!success)
            {
                await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to decode file: {fi.Name}\n\n{Yaz0.lerror}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            } else 
            {
                filesDecoded++;
            }

            if (CheckBoxDebugWriteYaz0Xml.IsChecked == true)
            {
                var folderName = Path.GetFileNameWithoutExtension(fi.Name);
                var folderPath = Path.Combine(fi.DirectoryName, folderName);
                
                var debugFilePath = Path.Combine(folderPath, $"{folderName}_Yaz0Debug.xml");
                if (File.Exists(debugFilePath))
                {
                    var overwritePrompt = await MessageBoxManager.GetMessageBoxStandard("File Exists",
                        $"The file {debugFilePath} already exists.\n\nDo you want to overwrite it?",
                        MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
                    
                    if (overwritePrompt is MsBox.Avalonia.Enums.ButtonResult.No or MsBox.Avalonia.Enums.ButtonResult.Cancel)
                        continue;

                    if (!DebugWriter.WriteYaz0Xml(fi.FullName, debugFilePath))
                    {
                        await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to write debug file: {debugFilePath}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                    }
                }
            }
        }
        
        await MessageBoxManager.GetMessageBoxStandard("Decode Complete", 
            $"Decoded {filesDecoded} files.\n\n" + 
            "You can now open the folders to view the unpacked files.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
    }

    private async void BuildSARC_OnClick(object? sender, RoutedEventArgs e)
    {
        var validDataOffset = uint.TryParse(TextDataOffset.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint offset);

        if (CheckBoxSetDataOffset.IsChecked == true && !validDataOffset)
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", "Fixed Data Offset is not a hex value.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }
        
        var opts = new FolderPickerOpenOptions
        {
            Title = "Select the root folder",
            AllowMultiple = false,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text)),
        };
        
        var folderPicker = await StorageProvider.OpenFolderPickerAsync(opts);
        
        if (!folderPicker.Any())
            return;
        
        var dirInfo = new DirectoryInfo(folderPicker[0].Path.AbsolutePath);

        var fileCount = dirInfo.GetFiles("*", SearchOption.AllDirectories).Length;
        
        var saveOpts = new FilePickerSaveOptions
        {
            Title = "Select the output file",
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text)),
        };
        var saveFile = await StorageProvider.SaveFilePickerAsync(saveOpts);
        if (saveFile == null)
            return;
        
        var savePath = saveFile.Path.AbsolutePath;

        uint dataOffset = 0;
        if (CheckBoxSetDataOffset.IsChecked == true) 
            dataOffset = offset;

        if (!SARC.Build(dirInfo.FullName, savePath, dataOffset, RadioButtonSwitch.IsChecked == true))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to build file: {savePath}\n\n{SARC.lerror}", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
            return;
        }
        
        var messageBox = await MessageBoxManager.GetMessageBoxStandard("Build Complete", 
            $"Built {fileCount} files.\n\n" + 
            "You can now open the folder to view the packed files.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
    }
    
    private async void YazEncode_Click(object? sender, RoutedEventArgs e)
    {
        var opts = new FilePickerOpenOptions
        {
            Title = "Select your packs",
            AllowMultiple = true,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(new Uri(TextFolderRoot.Text)),
        };
        
        var files = await StorageProvider.OpenFilePickerAsync(opts);
        if (!files.Any())
            return;
        
        var filesPacked = 0;
        foreach (var file in files)
        {
            var fi = new FileInfo(file.Path.AbsolutePath);
            if (SARC.IsYaz0File(fi.FullName))
            {
                await MessageBoxManager.GetMessageBoxStandard("Error", $"The file {file.Name} is already a Yaz0 file.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error).ShowAsync();
                continue;
            }
            
            var outfile = Yaz0.EncodeOutputFileRename(fi.FullName);
            if (File.Exists(outfile))
            {
                var overwritePrompt = await MessageBoxManager.GetMessageBoxStandard("File Exists",
                    $"The file {outfile} already exists.\n\nDo you want to overwrite it?",
                    MsBox.Avalonia.Enums.ButtonEnum.YesNo, MsBox.Avalonia.Enums.Icon.Warning).ShowAsync();
                
                if (overwritePrompt is MsBox.Avalonia.Enums.ButtonResult.No or MsBox.Avalonia.Enums.ButtonResult.Cancel)
                    continue;
            }
            
            Yaz0.Encode(fi.FullName, outfile);
            filesPacked++;
        }
        
        await MessageBoxManager.GetMessageBoxStandard("Pack Complete", 
            $"Packed {filesPacked} files.\n\n" + 
            "You can now open the folders to view the packed files.", MsBox.Avalonia.Enums.ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Info).ShowAsync();
    }

    private void ButtonCompareTool_Click(object? sender, RoutedEventArgs e)
    {
        if (!_compareTool.IsVisible)
        {
            if (_compareTool.PlatformImpl == null)
                _compareTool = _services.GetService<CompareTool>();
            
            _compareTool.Show();
        }
        else
        {
            if (_compareTool.WindowState == WindowState.Minimized)
                _compareTool.WindowState = WindowState.Normal;
            else
                _compareTool.Activate();
        }
        
        _compareTool.WindowState = WindowState.Normal;
        _compareTool.Focus();
        _compareTool.BringIntoView();
    }

    private void ButtonPaddingTool_Click(object? sender, RoutedEventArgs e)
    {
        if (!_paddingTool.IsVisible)
        {
            if (_paddingTool.PlatformImpl == null)
                _paddingTool = _services.GetService<PaddingTool>();
            
            _paddingTool.Show();
        }
        else
        {
            if (_paddingTool.WindowState == WindowState.Minimized)
                _paddingTool.WindowState = WindowState.Normal;
            else
                _paddingTool.Activate();
        }
        
        _paddingTool.WindowState = WindowState.Normal;
        _paddingTool.Focus();
        _paddingTool.BringIntoView();
    }

    private void TextFolderRoot_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!Directory.Exists(TextFolderRoot.Text) && TextFolderRoot.Text != string.Empty)
            return;
        
        _config.AddOrUpdateSetting("RootFolder", TextFolderRoot.Text);
    }

    private void RadioButtonSwitch_CheckedChange(object? sender, RoutedEventArgs e)
    {
        _config.AddOrUpdateSetting("LittleEndian", RadioButtonSwitch.IsChecked == true ? "true" : "false");
    }
    
    private void CheckBoxSetDataOffset_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        TextDataOffset.IsEnabled = CheckBoxSetDataOffset.IsChecked == true;
    }
}