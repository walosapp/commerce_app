using Walos.PrintAgent.Security;

namespace Walos.PrintAgent.Tray;

public sealed class PrintAgentTrayContext : ApplicationContext
{
    private readonly WebApplication _application;
    private readonly PairingService _pairing;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripItem _pairingCodeItem;

    public PrintAgentTrayContext(WebApplication application, PairingService pairing)
    {
        _application = application;
        _pairing = pairing;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Walos Print Agent — conectado").Enabled = false;
        _pairingCodeItem = menu.Items.Add($"Código de vinculación: {_pairing.CurrentPairingCode}", null, CopyPairingCode);
        menu.Items.Add("Copiar código", null, CopyPairingCode);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, Exit);
        menu.Opening += (_, _) =>
            _pairingCodeItem.Text = $"Código de vinculación: {_pairing.CurrentPairingCode}";

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Walos Print Agent",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.ShowBalloonTip(
            3000,
            "Walos Print Agent",
            $"Activo en 127.0.0.1:{Api.PrintAgentApi.Port}. Código: {_pairing.CurrentPairingCode}",
            ToolTipIcon.Info);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void CopyPairingCode(object? sender, EventArgs e)
    {
        Clipboard.SetText(_pairing.CurrentPairingCode);
    }

    private async void Exit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        await _application.StopAsync(TimeSpan.FromSeconds(5));
        ExitThread();
    }
}
