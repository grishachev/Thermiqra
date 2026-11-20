using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PCHardwareMonitor;

internal static class ProgramRecycleBin
{
    internal static Task SendAsync(string path)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread worker = new(() =>
        {
            IShellItem? item = null; IOperation? operation = null;
            try
            {
                ProgramLeftoverCleanupPolicy.CheckAncestors(path);
                Guid iid = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item));
                operation = (IOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"), true)!)!;
                // Recycle only; stop on errors. The sink rejects non-recycle deletes.
                operation.SetOperationFlags(0x00080000 | 0x00100000 | 0x20000000 | 0x4000 | 0x0400 | 0x0004);
                Sink sink = new();
                operation.DeleteItem(item, sink);
                operation.PerformOperations();
                operation.GetAnyOperationsAborted(out bool aborted);
                if (aborted || !sink.Recycled) throw new IOException(SettingsService.L(
                    "Windows не подтвердил перенос в Корзину. Безвозвратное удаление не выполняется.",
                    "Windows did not confirm recycling. Permanent deletion is not performed."));
                completion.SetResult();
            }
            catch (Exception ex) { completion.SetException(ex); }
            finally
            {
                Release(item); Release(operation);
            }
        }) { IsBackground = true };
        worker.SetApartmentState(ApartmentState.STA); worker.Start();
        return completion.Task;
    }

    private static void Release(object? value)
    {
        try { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
        catch (InvalidComObjectException) { }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid,
        out IShellItem item);

    // The native API expects IShellItem*, not an object's default IUnknown*.
    // Typed interface marshaling performs QueryInterface for the correct vtable.
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
        void GetParent(out IShellItem parent);
        void GetDisplayName(uint kind, out IntPtr name);
        void GetAttributes(uint mask, out uint attributes);
        void Compare(IShellItem other, uint hint, out int order);
    }

    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOperation
    {
        void Advise(IntPtr sink, out uint cookie); void Unadvise(uint cookie);
        void SetOperationFlags(uint flags); void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog(IntPtr dialog); void SetProperties(IntPtr properties); void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IntPtr item); void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IntPtr item, IntPtr name, IntPtr sink); void RenameItems(IntPtr items, IntPtr name);
        void MoveItem(IntPtr item, IntPtr destination, IntPtr name, IntPtr sink); void MoveItems(IntPtr items, IntPtr destination);
        void CopyItem(IntPtr item, IntPtr destination, IntPtr name, IntPtr sink); void CopyItems(IntPtr items, IntPtr destination);
        void DeleteItem(IShellItem item, IProgressSink sink);
        void DeleteItems(IntPtr items); void NewItem(IntPtr destination, uint attributes, IntPtr name, IntPtr template, IntPtr sink);
        void PerformOperations(); void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }

    [ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IProgressSink
    {
        [PreserveSig] int StartOperations(); [PreserveSig] int FinishOperations(int result);
        [PreserveSig] int PreRenameItem(uint flags, IntPtr item, IntPtr name);
        [PreserveSig] int PostRenameItem(uint flags, IntPtr item, IntPtr name, int result, IntPtr created);
        [PreserveSig] int PreMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr name);
        [PreserveSig] int PostMoveItem(uint flags, IntPtr item, IntPtr destination, IntPtr name, int result, IntPtr created);
        [PreserveSig] int PreCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr name);
        [PreserveSig] int PostCopyItem(uint flags, IntPtr item, IntPtr destination, IntPtr name, int result, IntPtr created);
        [PreserveSig] int PreDeleteItem(uint flags, IntPtr item);
        [PreserveSig] int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr created);
        [PreserveSig] int PreNewItem(uint flags, IntPtr destination, IntPtr name);
        [PreserveSig] int PostNewItem(uint flags, IntPtr destination, IntPtr name, IntPtr template, uint attributes, int result, IntPtr created);
        [PreserveSig] int UpdateProgress(uint total, uint completed); [PreserveSig] int ResetTimer();
        [PreserveSig] int PauseTimer(); [PreserveSig] int ResumeTimer();
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Sink : IProgressSink
    {
        internal bool Recycled;
        public int PreDeleteItem(uint flags, IntPtr item) => (flags & 0x80) != 0 ? 0 : unchecked((int)0x80004004);
        public int PostDeleteItem(uint flags, IntPtr item, int result, IntPtr created)
        { if (result >= 0 && created != IntPtr.Zero) Recycled = true; return result; }
        public int StartOperations() => 0; public int FinishOperations(int result) => result;
        public int PreRenameItem(uint f, IntPtr i, IntPtr n) => unchecked((int)0x80004004);
        public int PostRenameItem(uint f, IntPtr i, IntPtr n, int r, IntPtr c) => 0;
        public int PreMoveItem(uint f, IntPtr i, IntPtr d, IntPtr n) => unchecked((int)0x80004004);
        public int PostMoveItem(uint f, IntPtr i, IntPtr d, IntPtr n, int r, IntPtr c) => 0;
        public int PreCopyItem(uint f, IntPtr i, IntPtr d, IntPtr n) => unchecked((int)0x80004004);
        public int PostCopyItem(uint f, IntPtr i, IntPtr d, IntPtr n, int r, IntPtr c) => 0;
        public int PreNewItem(uint f, IntPtr d, IntPtr n) => unchecked((int)0x80004004);
        public int PostNewItem(uint f, IntPtr d, IntPtr n, IntPtr t, uint a, int r, IntPtr c) => 0;
        public int UpdateProgress(uint t, uint c) => 0; public int ResetTimer() => 0;
        public int PauseTimer() => 0; public int ResumeTimer() => 0;
    }
}
