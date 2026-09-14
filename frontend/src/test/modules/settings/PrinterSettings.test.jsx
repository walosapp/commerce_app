import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PrinterSettings from '../../../modules/settings/components/PrinterSettings';

const { actions, printAgentState } = vi.hoisted(() => {
  const storeActions = {
    initializeContext: vi.fn(),
    checkHealth: vi.fn(),
    pair: vi.fn(),
    loadPrinters: vi.fn(),
    updateConfig: vi.fn(),
    saveConfig: vi.fn(),
    testPrint: vi.fn(),
    openDrawer: vi.fn(),
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
    },
  };
});

vi.mock('../../../stores/authStore', () => ({
  default: (selector) => selector({ tenantId: 25, branchId: 7 }),
}));

vi.mock('../../../stores/printAgentStore', () => ({
  default: () => printAgentState,
}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

describe('PrinterSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    printAgentState.config.printerName = '';
    actions.checkHealth.mockResolvedValue({ status: 'ok' });
    actions.saveConfig.mockResolvedValue({});
  });

  it('detecta el agente al abrir la configuracion', async () => {
    render(<PrinterSettings />);

    await waitFor(() => expect(actions.checkHealth).toHaveBeenCalledOnce());
    expect(screen.getByText('Walos Print Agent conectado')).toBeInTheDocument();
    expect(actions.initializeContext).toHaveBeenCalledWith({ companyId: 25, branchId: 7 });
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
});
