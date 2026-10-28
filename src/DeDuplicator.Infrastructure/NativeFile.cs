using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using DeDuplicator.Core;

namespace DeDuplicator.Infrastructure;

public sealed class NativeFile : IDisposable
{
    public FileStream Stream { get; }
    public FileRecord Record { get; }
    public string Security { get; }
    private NativeFile(string path, SafeFileHandle handle)
    {
        Stream = new FileStream(handle, FileAccess.Read, 131072, false);
        Record = ReadRecord(path, handle);
        Security = ReadSecurity(path);
    }
    public static NativeFile Open(string path, bool forAction = false)
    {
        var handle = CreateFileW(path, 0x80000000u | (forAction ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x00200000u | 0x08000000u, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        try { return new NativeFile(path, handle); } catch { handle.Dispose(); throw; }
    }
    public void CheckStable()
    {
        if (ReadRecord(Record.Path, Stream.SafeFileHandle) != Record) throw new IOException("File changed during reading.");
    }
    public static bool SameSnapshot(FileRecord a, FileRecord b) => a.Identity == b.Identity && a.Length == b.Length && a.ModifiedUtc == b.ModifiedUtc && a.Attributes == b.Attributes && a.Links == b.Links;
    public static async Task<bool> EqualAsync(NativeFile a, NativeFile b, CancellationToken token)
    {
        if (a.Record.Length != b.Record.Length) return false;
        a.Stream.Position = b.Stream.Position = 0;
        byte[] left = new byte[131072], right = new byte[131072];
        while (true)
        {
            int n = await a.Stream.ReadAsync(left, token);
            if (n == 0) break;
            await b.Stream.ReadExactlyAsync(right.AsMemory(0, n), token);
            if (!left.AsSpan(0, n).SequenceEqual(right.AsSpan(0, n))) return false;
        }
        if (b.Stream.ReadByte() != -1) return false;
        a.CheckStable(); b.CheckStable();
        return true;
    }
    public void DeleteByHandle()
    {
        int value = 1;
        if (!SetFileInformationByHandle(Stream.SafeFileHandle, 4, ref value, 4)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void RenameByHandle(string destination)
    {
        // FILE_RENAME_INFO has pointer alignment, followed by a UTF-16 name. ReplaceIfExists remains false.
        byte[] name = System.Text.Encoding.Unicode.GetBytes(System.IO.Path.GetFullPath(destination));
        int offset = IntPtr.Size == 8 ? 20 : 12;
        int bufferSize = Math.Max(32, offset + name.Length + 2);
        IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            Marshal.Copy(new byte[bufferSize], 0, buffer, bufferSize);
            Marshal.WriteInt32(buffer, IntPtr.Size == 8 ? 16 : 8, name.Length);
            Marshal.Copy(name, 0, buffer + offset, name.Length);
            if (!SetFileInformationByHandle(Stream.SafeFileHandle, 3, buffer, (uint)bufferSize)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static FileRecord ReadRecord(string path, SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var attributes = (FileAttributes)info.Attributes;
        bool review = info.Links > 1 || (attributes & (FileAttributes.ReparsePoint | FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.SparseFile | FileAttributes.Compressed | FileAttributes.Encrypted)) != 0 || HasExtraStreams(path);
        return new(path, ((long)info.SizeHigh << 32) | info.SizeLow, DateTime.FromFileTimeUtc(((long)info.WriteHigh << 32) | info.WriteLow), $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}", info.Links, attributes, review);
    }
    private static bool HasExtraStreams(string path)
    {
        var handle = FindFirstStreamW(path, 0, out var data, 0);
        if (handle == new IntPtr(-1))
        {
            int error = Marshal.GetLastWin32Error();
            // Unsupported stream enumeration cannot establish interchangeability.
            return error != 38;
        }
        try { do { if (data.Name != "::$DATA") return true; } while (FindNextStreamW(handle, out data)); return Marshal.GetLastWin32Error() != 38; }
        finally { FindClose(handle); }
    }
    private static string ReadSecurity(string path)
    {
        GetFileSecurityW(path, 7, null, 0, out uint length);
        if (length == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        byte[] descriptor = new byte[length];
        if (!GetFileSecurityW(path, 7, descriptor, length, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return Convert.ToHexString(SHA256.HashData(descriptor));
    }
    public void Dispose() => Stream.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, ref int data, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, IntPtr data, uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr FindFirstStreamW(string path, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool FindNextStreamW(IntPtr handle, out StreamData data);
    [DllImport("kernel32.dll")] private static extern bool FindClose(IntPtr handle);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool GetFileSecurityW(string path, uint info, byte[]? descriptor, uint length, out uint required);
}
