using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ToolboxWeb.Web.Infrastructure.Files;

/// <summary>
/// Windows parts of "Files &amp; folders" for the desktop copy, which runs as the signed-in
/// Windows user: the folder and "Save as" dialogs (the shell's IFileOpenDialog / IFileSaveDialog,
/// on their own STA thread, so the app needs neither WinForms nor a Windows-only build), the
/// Recycle Bin, "Show in Explorer" and "Open with the default program". Callers check
/// <see cref="IsAvailable"/>; nothing here is used on the IIS site.
/// </summary>
public static class WindowsShell
{
    public static bool IsAvailable => OperatingSystem.IsWindows();

    /// <summary>Shows the folder dialog; null when cancelled.</summary>
    [SupportedOSPlatform("windows")]
    public static Task<string?> PickFolderAsync(string title, string? startFolder) =>
        RunSta(() => ShowDialog(open: true, title, startFolder, fileName: null));

    /// <summary>Shows the "Save as" dialog; null when cancelled.</summary>
    [SupportedOSPlatform("windows")]
    public static Task<string?> PickSaveFileAsync(string title, string? startFolder, string fileName) =>
        RunSta(() => ShowDialog(open: false, title, startFolder, fileName));

    [SupportedOSPlatform("windows")]
    public static void MoveToRecycleBin(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(fullPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
        else
        {
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fullPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }
    }

    [SupportedOSPlatform("windows")]
    public static void RevealInExplorer(string fullPath)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add("/select," + fullPath);
        Process.Start(start)?.Dispose();
    }

    [SupportedOSPlatform("windows")]
    public static void OpenWithDefaultProgram(string fullPath)
    {
        Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true })?.Dispose();
    }

    private static Task<string?> RunSta(Func<string?> work)
    {
        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                done.SetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Toolkit file dialog"
        };
        if (OperatingSystem.IsWindows())
        {
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
        return done.Task;
    }

    [SupportedOSPlatform("windows")]
    private static string? ShowDialog(bool open, string title, string? startFolder, string? fileName)
    {
        var dialog = open ? (IFileDialog)new FileOpenDialogCom() : (IFileDialog)new FileSaveDialogCom();
        try
        {
            dialog.GetOptions(out var options);
            options |= FosForceFileSystem | FosPathMustExist;
            options |= open ? FosPickFolders : FosOverwritePrompt;
            dialog.SetOptions(options);
            dialog.SetTitle(title);
            if (!string.IsNullOrEmpty(fileName))
            {
                dialog.SetFileName(fileName);
            }

            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
            {
                var iid = typeof(IShellItem).GUID;
                if (SHCreateItemFromParsingName(startFolder, IntPtr.Zero, ref iid, out var folder) == 0)
                {
                    dialog.SetFolder(folder);
                    Marshal.ReleaseComObject(folder);
                }
            }

            // Owned by the app window (in front of it), which is the foreground window when the user clicked.
            var hr = dialog.Show(GetForegroundWindow());
            if (hr != 0)
            {
                return null;
            }

            dialog.GetResult(out var item);
            try
            {
                item.GetDisplayName(SigdnFileSysPath, out var path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(item);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    private const uint FosOverwritePrompt = 0x2;
    private const uint FosPickFolders = 0x20;
    private const uint FosForceFileSystem = 0x40;
    private const uint FosPathMustExist = 0x800;
    private const uint SigdnFileSysPath = 0x80058000;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem item);

    [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private class FileOpenDialogCom;

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialogCom;

    [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint count, IntPtr filterSpec);
        void SetFileTypeIndex(uint index);
        void GetFileTypeIndex(out uint index);
        void Advise(IntPtr events, out uint cookie);
        void Unadvise(uint cookie);
        void SetOptions(uint options);
        void GetOptions(out uint options);
        void SetDefaultFolder(IShellItem item);
        void SetFolder(IShellItem item);
        void GetFolder(out IShellItem item);
        void GetCurrentSelection(out IShellItem item);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
        void GetResult(out IShellItem item);
        void AddPlace(IShellItem item, int place);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr filter);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindContext, ref Guid handler, ref Guid riid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint sigdn, [MarshalAs(UnmanagedType.LPWStr)] out string name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }
}
