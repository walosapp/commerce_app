using Walos.PrintAgent.Api;
using Walos.PrintAgent.Printing;
using Walos.PrintAgent.Storage;

namespace Walos.PrintAgent.Commands;

public sealed record PreparedPrintJob(string PrinterName, string DocumentName, byte[] Data);

public sealed class PrintCommandService(
    AgentStateStore state,
    IPrinterCatalog printers,
    IRawPrinter rawPrinter,
    EscPos58Encoder encoder)
{
    public PreparedPrintJob PrepareTestTicket()
    {
        var configuration = GetValidConfiguration();
        return new PreparedPrintJob(
            configuration.PrinterName,
            "Walos - ticket de prueba",
            encoder.EncodeTestTicket(configuration));
    }

    public PreparedPrintJob PrepareDrawerPulse()
    {
        var configuration = GetValidConfiguration();
        return new PreparedPrintJob(
            configuration.PrinterName,
            "Walos - abrir cajón",
            encoder.EncodeDrawerPulse(
                configuration.DrawerPin,
                configuration.DrawerOnTimeMs,
                configuration.DrawerOffTimeMs));
    }

    public PreparedPrintJob PrepareReceipt(ReceiptDocument receipt)
    {
        var configuration = GetValidConfiguration();
        return new PreparedPrintJob(
            configuration.PrinterName,
            $"Walos - recibo {EscPos58Encoder.Sanitize(receipt.OrderNumber)}",
            encoder.EncodeReceipt(receipt));
    }

    public PreparedPrintJob PrepareCashClose(CashCloseDocument cashClose)
    {
        var configuration = GetValidConfiguration();
        return new PreparedPrintJob(
            configuration.PrinterName,
            $"Walos - cierre caja {cashClose.CashRegisterId}",
            encoder.EncodeCashClose(cashClose));
    }

    public Task SendAsync(PreparedPrintJob job, CancellationToken ct) =>
        rawPrinter.PrintAsync(job.PrinterName, job.DocumentName, job.Data, ct);

    private Api.PrinterConfiguration GetValidConfiguration()
    {
        var configuration = state.GetPrinterConfiguration()
            ?? throw new PrinterNotConfiguredException("No hay una impresora configurada.");

        if (!state.MatchesPairedIdentity(
                configuration.CompanyId,
                configuration.BranchId,
                configuration.WorkstationId))
        {
            throw new PrinterNotConfiguredException(
                "La impresora configurada no pertenece a la vinculacion activa.");
        }

        if (!printers.Exists(configuration.PrinterName))
        {
            throw new PrinterNotFoundException(configuration.PrinterName);
        }

        return configuration;
    }
}
