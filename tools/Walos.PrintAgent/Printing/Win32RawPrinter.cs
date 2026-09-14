using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Walos.PrintAgent.Printing;

public interface IRawPrinter
{
    Task PrintAsync(string printerName, string documentName, byte[] data, CancellationToken ct);
}

public sealed class Win32RawPrinter : IRawPrinter
{
    public Task PrintAsync(string printerName, string documentName, byte[] data, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            WriteRaw(printerName, documentName, data);
            return Task.CompletedTask;
        }
        catch (Win32Exception exception)
        {
            throw new PrintSpoolerException(
                $"Windows no pudo enviar el trabajo RAW a '{printerName}' (error {exception.NativeErrorCode}).",
                exception);
        }
    }

    private static unsafe void WriteRaw(string printerName, string documentName, byte[] data)
    {
        if (!NativeMethods.OpenPrinter(printerName, out var printer, IntPtr.Zero))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var documentStarted = false;
        var pageStarted = false;
        Exception? failure = null;

        try
        {
            var document = new NativeMethods.DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (NativeMethods.StartDocPrinter(printer, 1, ref document) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            documentStarted = true;

            if (!NativeMethods.StartPagePrinter(printer))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            pageStarted = true;

            fixed (byte* pointer = data)
            {
                if (!NativeMethods.WritePrinter(printer, (IntPtr)pointer, data.Length, out var written) ||
                    written != data.Length)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (pageStarted && !NativeMethods.EndPagePrinter(printer) && failure is null)
            {
                failure = new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (documentStarted && !NativeMethods.EndDocPrinter(printer) && failure is null)
            {
                failure = new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!NativeMethods.ClosePrinter(printer) && failure is null)
            {
                failure = new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DocInfo
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int StartDocPrinter(IntPtr printer, int level, ref DocInfo document);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StartPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WritePrinter(IntPtr printer, IntPtr bytes, int count, out int written);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EndDocPrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClosePrinter(IntPtr printer);
    }
}
