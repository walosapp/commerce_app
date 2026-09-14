using System.Text.RegularExpressions;

namespace Walos.PrintAgent.Api;

internal static partial class RequestValidation
{
    public static string? Validate(PairRequest request)
    {
        if (!PairingCodeRegex().IsMatch(request.PairingCode ?? string.Empty))
        {
            return "pairingCode debe contener exactamente 6 dígitos.";
        }

        return ValidateIdentity(request.CompanyId, request.BranchId, request.WorkstationId);
    }

    public static string? Validate(PrinterConfiguration request)
    {
        var identityError = ValidateIdentity(request.CompanyId, request.BranchId, request.WorkstationId);
        if (identityError is not null)
        {
            return identityError;
        }

        if (string.IsNullOrWhiteSpace(request.PrinterName) || request.PrinterName.Length > 260)
        {
            return "printerName es obligatorio y no puede exceder 260 caracteres.";
        }

        if (request.DrawerPin is not (0 or 1))
        {
            return "drawerPin debe ser 0 o 1.";
        }

        if (request.DrawerOnTimeMs is < 10 or > 500 || request.DrawerOffTimeMs is < 10 or > 510)
        {
            return "Los tiempos del cajón están fuera del rango permitido.";
        }

        return null;
    }

    public static string? Validate(JobCommandRequest request)
    {
        return JobIdRegex().IsMatch(request.JobId ?? string.Empty)
            ? null
            : "jobId debe tener entre 1 y 100 caracteres seguros.";
    }

    private static string? ValidateIdentity(long companyId, long branchId, string workstationId)
    {
        if (companyId <= 0 || branchId <= 0)
        {
            return "companyId y branchId deben ser mayores que cero.";
        }

        if (!WorkstationIdRegex().IsMatch(workstationId ?? string.Empty))
        {
            return "workstationId debe tener entre 1 y 100 caracteres seguros.";
        }

        return null;
    }

    [GeneratedRegex("^[0-9]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex PairingCodeRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex JobIdRegex();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex WorkstationIdRegex();
}
