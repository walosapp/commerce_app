import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PrinterSettings from '../../../modules/settings/components/PrinterSettings';

const { actions, printAgentState, postSaleState } = vi.hoisted(() => {
  const storeActions = {
    initializeContext: vi.fn(),
    checkHealth: vi.fn(),
    pair: vi.fn(),
    loadPrinters: vi.fn(),
    updateConfig: vi.fn(),
    saveConfig: vi.fn(),
    testPrint: vi.fn(),
    openDrawer: vi.fn(),
    printPostSaleReceipt: vi.fn(),
  };

  return {
    actions: storeActions,
    printAgentState: {
      ...storeActions,
      status: 'connected',
      isChecking: false,
      isPairing: false,
      isLoadingPrinters: false,
      isSaving: false,
      activeCommand: null,
      version: '1.0.0',
      token: 'agent-token',
      printers: [
        { name: 'Digital POS DIG-58IIA', isDefault: true },
        { name: 'Otra impresora', isDefault: false },
      ],
      config: {
        companyId: 25,
        branchId: 7,
        workstationId: 'puesto-caja-1',
        printerName: '',
        drawerPin: 0,
        drawerOnTimeMs: 120,
        drawerOffTimeMs: 240,
      },
      error: null,
      configurationSaved: false,
    },
    postSaleState: {
      policy: { autoPrintReceipt: false, autoOpenCashDrawer: false },
      persistenceFailed: false,
      storageWarning: null,
      intents: {},
      getPolicy: vi.fn(() => postSaleState.policy),
      updatePolicy: vi.fn(),
      retryPostSaleDrawer: vi.fn(),
      acknowledgePostSaleDrawer: vi.fn(),
    },
  };
});

vi.mock('../../../stores/authStore', () => ({
  default: (selector) => selector({ tenantId: 25, branchId: 7 }),
}));

vi.mock('../../../stores/printAgentStore', () => ({
  default: () => printAgentState,
}));

vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector(postSaleState),
  isUnresolvedPostSaleDrawer: (intent) => (
    ['failed', 'uncertain', 'stale'].includes(intent?.drawerStatus) && !intent?.drawerReviewed
  ),
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

describe('PrinterSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv('VITE_WALOS_AGENT_DOWNLOAD_URL', '');
    printAgentState.status = 'connected';
    printAgentState.isChecking = false;
    printAgentState.version = '1.0.0';
    printAgentState.token = 'agent-token';
    printAgentState.config.printerName = '';
    printAgentState.configurationSaved = false;
    actions.checkHealth.mockResolvedValue({ status: 'ok' });
    actions.saveConfig.mockResolvedValue({});
    postSaleState.policy.autoPrintReceipt = false;
    postSaleState.policy.autoOpenCashDrawer = false;
    postSaleState.storageWarning = null;
    postSaleState.persistenceFailed = false;
    postSaleState.intents = {};
    postSaleState.retryPostSaleDrawer.mockResolvedValue({ status: 'replayed', executed: false });
  });

  it('detecta el agente al abrir la configuracion', async () => {
    render(<PrinterSettings />);

    await waitFor(() => expect(actions.checkHealth).toHaveBeenCalledOnce());
    expect(screen.getByText('Agente conectado')).toBeInTheDocument();
    expect(screen.getByText('Versión instalada: 1.0.0')).toBeInTheDocument();
    expect(screen.queryByText('agent-token')).not.toBeInTheDocument();
    expect(actions.initializeContext).toHaveBeenCalledWith({ companyId: 25, branchId: 7 });
  });

  it('ofrece la descarga configurada cuando el agente no esta disponible', () => {
    vi.stubEnv(
      'VITE_WALOS_AGENT_DOWNLOAD_URL',
      'https://github.com/walosapp/commerce_app/releases/latest/download/Walos.PrintAgent.Setup.exe'
    );
    printAgentState.status = 'disconnected';
    printAgentState.version = null;
    printAgentState.token = null;

    render(<PrinterSettings />);

    expect(screen.getByText('Agente no detectado')).toBeInTheDocument();
    expect(screen.getByText('Versión instalada: No disponible')).toBeInTheDocument();
    const download = screen.getByRole('link', { name: 'Descargar Walos Agent' });
    expect(download).toHaveAttribute(
      'href',
      'https://github.com/walosapp/commerce_app/releases/latest/download/Walos.PrintAgent.Setup.exe'
    );
    expect(download).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('deshabilita la descarga cuando el entorno no configura una URL segura', () => {
    printAgentState.status = 'disconnected';
    printAgentState.version = null;
    printAgentState.token = null;

    render(<PrinterSettings />);

    expect(screen.getByRole('button', { name: 'Descargar Walos Agent' })).toBeDisabled();
    expect(screen.getByText(/La descarga no está configurada para este entorno/i)).toBeInTheDocument();
  });

  it('no convierte esquemas ejecutables en enlaces de descarga', () => {
    vi.stubEnv('VITE_WALOS_AGENT_DOWNLOAD_URL', 'javascript:alert(1)');
    printAgentState.status = 'disconnected';
    printAgentState.version = null;
    printAgentState.token = null;

    render(<PrinterSettings />);

    expect(screen.queryByRole('link', { name: 'Descargar Walos Agent' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar Walos Agent' })).toBeDisabled();
  });

  it('permite seleccionar la DIG-58IIA y guardar la configuracion local', async () => {
    const { rerender } = render(<PrinterSettings />);

    fireEvent.change(screen.getByLabelText('Impresora instalada'), {
      target: { value: 'Digital POS DIG-58IIA' },
    });
    expect(actions.updateConfig).toHaveBeenCalledWith({ printerName: 'Digital POS DIG-58IIA' });

    // Simula el siguiente render producido por Zustand despues de seleccionar.
    printAgentState.config.printerName = 'Digital POS DIG-58IIA';
    rerender(<PrinterSettings />);
    fireEvent.click(screen.getByRole('button', { name: 'Guardar configuracion' }));

    await waitFor(() => expect(actions.saveConfig).toHaveBeenCalledOnce());
  });

  it('mantiene opt-in local desactivado y permite habilitarlo con configuracion guardada', () => {
    printAgentState.config.printerName = 'Digital POS DIG-58IIA';
    printAgentState.configurationSaved = true;
    render(<PrinterSettings />);

    const autoPrint = screen.getByRole('checkbox', { name: /Imprimir recibo automaticamente/i });
    const autoDrawer = screen.getByRole('checkbox', { name: /Abrir cajon con pagos en efectivo/i });
    expect(autoPrint).not.toBeChecked();
    expect(autoDrawer).not.toBeChecked();

    fireEvent.click(autoPrint);
    fireEvent.click(autoDrawer);

    expect(postSaleState.updatePolicy).toHaveBeenCalledWith(
      { companyId: 25, branchId: 7 },
      { autoPrintReceipt: true }
    );
    expect(postSaleState.updatePolicy).toHaveBeenCalledWith(
      { companyId: 25, branchId: 7 },
      { autoOpenCashDrawer: true }
    );
  });

  it('permite desactivar politicas activas aunque el agente este desconectado', () => {
    printAgentState.status = 'disconnected';
    printAgentState.token = null;
    printAgentState.configurationSaved = false;
    postSaleState.policy.autoPrintReceipt = true;
    postSaleState.policy.autoOpenCashDrawer = true;
    render(<PrinterSettings />);

    const autoPrint = screen.getByRole('checkbox', { name: /Imprimir recibo automaticamente/i });
    const autoDrawer = screen.getByRole('checkbox', { name: /Abrir cajon con pagos en efectivo/i });
    expect(autoPrint).toBeEnabled();
    expect(autoDrawer).toBeEnabled();

    fireEvent.click(autoPrint);
    fireEvent.click(autoDrawer);

    expect(postSaleState.updatePolicy).toHaveBeenCalledWith(
      { companyId: 25, branchId: 7 },
      { autoPrintReceipt: false }
    );
    expect(postSaleState.updatePolicy).toHaveBeenCalledWith(
      { companyId: 25, branchId: 7 },
      { autoOpenCashDrawer: false }
    );
  });

  it('no permite habilitar politicas con el agente desconectado', () => {
    printAgentState.status = 'disconnected';
    printAgentState.token = null;
    printAgentState.configurationSaved = false;
    render(<PrinterSettings />);

    expect(screen.getByRole('checkbox', { name: /Imprimir recibo automaticamente/i })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: /Abrir cajon con pagos en efectivo/i })).toBeDisabled();
  });

  it('permite reconciliar una apertura incierta con el mismo contexto sin imprimir', async () => {
    printAgentState.config.printerName = 'Digital POS DIG-58IIA';
    printAgentState.configurationSaved = true;
    postSaleState.intents = {
      'c25:b7:o123': {
        companyId: 25,
        branchId: 7,
        orderId: 123,
        drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
        printStatus: 'completed',
        drawerStatus: 'uncertain',
        drawerReviewed: false,
      },
      'c99:b7:o456': {
        companyId: 99,
        branchId: 7,
        orderId: 456,
        drawerStatus: 'failed',
        drawerReviewed: false,
      },
    };
    render(<PrinterSettings />);

    expect(screen.getByText('Comercio 25 · Sede 7 · Orden 123')).toBeInTheDocument();
    expect(screen.queryByText(/Orden 456/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Reintentar apertura' }));

    await waitFor(() => expect(postSaleState.retryPostSaleDrawer).toHaveBeenCalledWith({
      companyId: 25,
      branchId: 7,
      orderId: 123,
    }));
    expect(actions.printPostSaleReceipt).not.toHaveBeenCalled();
  });

  it('permite marcar una apertura incierta como resuelta con el mismo contexto', () => {
    postSaleState.intents = {
      'c25:b7:o123': {
        companyId: 25,
        branchId: 7,
        orderId: 123,
        drawerStatus: 'stale',
        drawerReviewed: false,
      },
    };
    render(<PrinterSettings />);

    fireEvent.click(screen.getByRole('button', { name: 'Marcar como resuelto' }));

    expect(postSaleState.acknowledgePostSaleDrawer).toHaveBeenCalledWith({
      companyId: 25,
      branchId: 7,
      orderId: 123,
    });
    expect(actions.printPostSaleReceipt).not.toHaveBeenCalled();
  });

  it('alerta sin ocultar intentos cuando hay demasiadas acciones fisicas sin reconciliar', () => {
    postSaleState.storageWarning = 'Hay mas de 100 acciones fisicas postventa sin reconciliar.';

    render(<PrinterSettings />);

    expect(screen.getByRole('alert')).toHaveTextContent(/mas de 100 acciones fisicas postventa/i);
  });
});
