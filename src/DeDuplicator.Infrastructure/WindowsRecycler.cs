using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DeDuplicator.Core;

namespace DeDuplicator.Infrastructure;

public sealed record RecycleReceipt(string ParsingName, string FilePath, int FormatVersion = 1);

public interface IFileRecycler
{
    Task<RecycleReceipt> RecycleAsync(FileRecord staged, NativeFile keeper, CancellationToken token);
    Task RestoreAsync(RecycleReceipt receipt, string destination, string expectedHash, long expectedLength, CancellationToken token);
}

public sealed class WindowsRecycler : IFileRecycler
{
    public Task<RecycleReceipt> RecycleAsync(FileRecord staged, NativeFile keeper, CancellationToken token)
        => OnSta(() => Recycle(staged, keeper, token));

    private static Task<T> OnSta<T>(Func<T> operation)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(operation()); }
            catch (Exception error) { completion.SetException(error); }
        }) { IsBackground = true, Name = "DeDuplicator Recycle" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }

    private static RecycleReceipt Recycle(FileRecord staged, NativeFile keeper, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Scanner.EnsureNoReparseAncestors(staged.Path);
        // Shell needs rename/delete sharing. Contents stay write-denied, and the unpredictable
        // staging name prevents it from ever targeting a replacement at the original user path.
        using var source = NativeFile.Open(staged.Path, true, allowDeleteSharing: true);
        if (!NativeFile.SameSnapshot(staged, source.Record) || source.Record.ReviewRequired || source.Security != keeper.Security || source.Record.Attributes != keeper.Record.Attributes || !NativeFile.EqualAsync(source, keeper, token).GetAwaiter().GetResult())
            throw new IOException("Recycle staging content changed; retained for recovery.");
        var objects = new List<object>();
        try
        {
            Guid shellItem = typeof(IShellItem).GUID;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(staged.Path, IntPtr.Zero, ref shellItem, out var item)); objects.Add(item);
            item.GetParent(out var folder); objects.Add(folder);
            Guid transferHandler = new("D5E346A1-F753-4932-B403-4574800E2498"), transferInterface = typeof(ITransferSource).GUID;
            folder.BindToHandler(IntPtr.Zero, ref transferHandler, ref transferInterface, out var handler); objects.Add(handler);
            Guid recycleFolder = new("B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC");
            Marshal.ThrowExceptionForHR(SHGetKnownFolderItem(ref recycleFolder, 0, IntPtr.Zero, ref shellItem, out var bin)); objects.Add(bin);
            token.ThrowIfCancellationRequested();
            // RecycleItem is a distinct COM method. RemoveItem/permanent deletion is never called.
            int result = ((ITransferSource)handler).RecycleItem(item, bin, 0, out var recycled);
            Marshal.ThrowExceptionForHR(result);
            if (recycled is null || File.Exists(staged.Path)) throw new IOException("Recycling was not confirmed; inspect the staged file and journal. Choose a new action explicitly.");
            objects.Add(recycled);
            return new(Name(recycled, 0x80028000), Name(recycled, 0x80058000));
        }
        catch (COMException ex) { throw new IOException("Windows could not recycle this location; the staged file is retained when available. No permanent-delete fallback was used.", ex); }
        finally
        {
            foreach (var value in objects.AsEnumerable().Reverse().Distinct(ReferenceEqualityComparer.Instance))
                if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
    }

    public async Task RestoreAsync(RecycleReceipt receipt, string destination, string expectedHash, long expectedLength, CancellationToken token)
    {
        await OnSta(() =>
        {
            token.ThrowIfCancellationRequested();
            if (receipt.FormatVersion != 1) throw new IOException("Unsupported recycle receipt version.");
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Restore destination already exists.");
            Scanner.EnsureNoReparseAncestors(System.IO.Path.GetDirectoryName(destination)!);
            using var content = NativeFile.Open(receipt.FilePath, true, allowDeleteSharing: true);
            if (content.Record.ReviewRequired || content.Record.Length != expectedLength || Convert.ToHexString(SHA256.HashData(content.Stream)) != expectedHash) throw new IOException("Recycled content changed; manual recovery required.");
            content.CheckStable();
            var objects = new List<object>();
            try
            {
                Guid iid = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(receipt.ParsingName, IntPtr.Zero, ref iid, out var item)); objects.Add(item);
                item.GetParent(out var parent); objects.Add(parent);
                Guid bhid = new("D5E346A1-F753-4932-B403-4574800E2498"), transfer = typeof(ITransferSource).GUID;
                parent.BindToHandler(IntPtr.Zero, ref bhid, ref transfer, out var handler); objects.Add(handler);
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(System.IO.Path.GetDirectoryName(destination)!, IntPtr.Zero, ref iid, out var folder)); objects.Add(folder);
                token.ThrowIfCancellationRequested();
                ((ITransferSource)handler).MoveItem(item, folder, System.IO.Path.GetFileName(destination), 0, out var restored); objects.Add(restored);
                if (!File.Exists(destination)) throw new IOException("Restore not confirmed; inspect both recorded paths.");
                return true;
            }
            catch (COMException ex) { throw new IOException("Windows could not restore this recycled item without overwrite.", ex); }
            finally { foreach (var value in objects.AsEnumerable().Reverse().Distinct(ReferenceEqualityComparer.Instance)) if (Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
        });
    }

    private static string Name(IShellItem item, uint kind)
    {
        item.GetDisplayName(kind, out IntPtr name);
        try { return Marshal.PtrToStringUni(name) ?? throw new IOException("Windows returned no recycle recovery name."); }
        finally { Marshal.FreeCoTaskMem(name); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetKnownFolderItem(ref Guid folder, uint flags, IntPtr token, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hints, out int order);
    }

    [ComImport, Guid("00ADB003-BDE9-45C6-8E29-D09F9353E108"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITransferSource
    {
        void Advise(IntPtr sink, out uint cookie);
        void Unadvise(uint cookie);
        void SetProperties(IntPtr properties);
        void OpenItem(IShellItem item, uint flags, ref Guid iid, out IntPtr result);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out IShellItem result);
        [PreserveSig] int RecycleItem(IShellItem source, IShellItem destination, uint flags, [MarshalAs(UnmanagedType.Interface)] out IShellItem? result);
    }
}
