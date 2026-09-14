using Walos.PrintAgent.Printing;

namespace Walos.PrintAgent.Api;

public sealed record PairRequest(
    string PairingCode,
    long CompanyId,
    long BranchId,
    string WorkstationId);

public sealed record PairResponse(
    string Token,
    string TokenType,
    Guid AgentId);

public sealed record PrinterConfiguration(
    long CompanyId,
    long BranchId,
    string WorkstationId,
    string PrinterName,
    int DrawerPin,
    int DrawerOnTimeMs,
    int DrawerOffTimeMs);

public sealed record JobCommandRequest(string JobId);

public sealed record JobCommandResponse(string JobId, string Status, bool Executed);

public sealed record PrintersResponse(
    IReadOnlyList<PrinterDescriptor> Printers,
    string? SelectedPrinter);

public sealed record ErrorResponse(string Code, string Message);
