using System.Drawing.Printing;

namespace Walos.PrintAgent.Printing;

public sealed record PrinterDescriptor(string Name, bool IsDefault);

public interface IPrinterCatalog
{
    IReadOnlyList<PrinterDescriptor> GetInstalledPrinters();
    bool Exists(string printerName);
}

public sealed class WindowsPrinterCatalog : IPrinterCatalog
{
    public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters()
    {
        var defaultPrinter = new PrinterSettings().PrinterName;
        return PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Select(name => new PrinterDescriptor(
                name,
                name.Equals(defaultPrinter, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(printer => printer.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public bool Exists(string printerName) => GetInstalledPrinters()
        .Any(printer => printer.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));
}

public sealed class PrinterNotConfiguredException(string message) : Exception(message);

public sealed class PrinterNotFoundException(string printerName)
    : Exception($"La impresora '{printerName}' no está instalada.");

public sealed class PrintSpoolerException(string message, Exception? innerException = null)
    : Exception(message, innerException);
