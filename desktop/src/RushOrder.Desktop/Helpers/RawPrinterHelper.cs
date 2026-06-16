using System.Runtime.InteropServices;

namespace RushOrder.Desktop.Helpers;

public static class RawPrinterHelper
{
    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int Level, [In] ref DOCINFOA pDocInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
    }

    public static bool SendToDefaultPrinter(byte[] data, string docName = "RushOrder")
    {
        var printerName = GetDefaultPrinterName();
        return printerName is not null && SendToPrinter(printerName, data, docName);
    }

    public static bool SendToPrinter(string printerName, byte[] data, string docName = "RushOrder")
    {
        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero)) return false;

        var docInfo = new DOCINFOA
        {
            pDocName   = docName,
            pDataType  = "RAW",
            pOutputFile = null,
        };

        if (!StartDocPrinter(hPrinter, 1, ref docInfo))
        {
            ClosePrinter(hPrinter);
            return false;
        }

        StartPagePrinter(hPrinter);

        var ptr = Marshal.AllocCoTaskMem(data.Length);
        Marshal.Copy(data, 0, ptr, data.Length);
        WritePrinter(hPrinter, ptr, data.Length, out _);
        Marshal.FreeCoTaskMem(ptr);

        EndPagePrinter(hPrinter);
        EndDocPrinter(hPrinter);
        ClosePrinter(hPrinter);
        return true;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetDefaultPrinter(System.Text.StringBuilder pszBuffer, ref int pcchBuffer);

    public static string? GetDefaultPrinterName()
    {
        try
        {
            int size = 256;
            var sb   = new System.Text.StringBuilder(size);
            return GetDefaultPrinter(sb, ref size) ? sb.ToString() : null;
        }
        catch { return null; }
    }
}
