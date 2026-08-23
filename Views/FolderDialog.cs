using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace WiFitool
{
    internal static class FolderDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct BROWSEINFO { public IntPtr hwndOwner; public IntPtr pidlRoot; public IntPtr pszDisplayName; public string lpszTitle; public uint ulFlags; public IntPtr lpfn; public IntPtr lParam; public int iImage; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool SHGetPathFromIDList(IntPtr pidl, StringBuilder path);
        [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr pv);
        public static bool TrySelect(Window owner, string title, out string path)
        {
            path = null; var display = Marshal.AllocHGlobal(260); try { var bi = new BROWSEINFO { hwndOwner = new System.Windows.Interop.WindowInteropHelper(owner).Handle, pszDisplayName = display, lpszTitle = title, ulFlags = 0x00000040 | 0x00000001 }; var pidl = SHBrowseForFolder(ref bi); if (pidl == IntPtr.Zero) return false; try { var builder = new StringBuilder(1024); if (!SHGetPathFromIDList(pidl, builder)) return false; path = builder.ToString(); return path.Length > 0; } finally { CoTaskMemFree(pidl); } } finally { Marshal.FreeHGlobal(display); }
        }
    }
}
