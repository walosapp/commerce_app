import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createStore } from 'zustand/vanilla';
import { createPrintAgentState } from '../../stores/printAgentStore';

const createService = () => ({
  health: vi.fn(),
  pair: vi.fn(),
  getPrinters: vi.fn(),
  savePrinterConfig: vi.fn(),
  testPrint: vi.fn(),
  openDrawer: vi.fn(),
});

describe('printAgentStore', () => {
  let service;
  let store;

  beforeEach(() => {
    service = createService();
    store = createStore(createPrintAgentState(service));
  });

  it('marca el agente como desconectado cuando localhost no esta disponible', async () => {
    service.health.mockRejectedValue(new Error('Walos Print Agent no esta disponible en este equipo.'));

    const result = await store.getState().checkHealth();

    expect(result).toBeNull();
    expect(store.getState()).toMatchObject({
      status: 'disconnected',
      isChecking: false,
      printers: [],
      error: 'Walos Print Agent no esta disponible en este equipo.',
    });
  });

  it('vincula, selecciona una impresora y guarda solo la configuracion tipada', async () => {
    service.pair.mockResolvedValue({ token: 'agent-token', tokenType: 'Bearer', agentId: 'agent-1' });
    service.getPrinters.mockResolvedValue({
      printers: [{ name: 'Digital POS DIG-58IIA', isDefault: true }],
      selectedPrinter: null,
    });
    service.savePrinterConfig.mockImplementation(async (_token, config) => config);

    store.getState().initializeContext({ companyId: 25, branchId: 7 });
    await store.getState().pair('123456');
    await store.getState().loadPrinters();
    store.getState().updateConfig({
      printerName: 'Digital POS DIG-58IIA',
      drawerPin: 1,
      drawerOnTimeMs: 100,
      drawerOffTimeMs: 300,
    });
    await store.getState().saveConfig();

    expect(service.pair).toHaveBeenCalledWith(expect.objectContaining({
      pairingCode: '123456',
      companyId: 25,
      branchId: 7,
      workstationId: expect.any(String),
    }));
    expect(service.savePrinterConfig).toHaveBeenCalledWith('agent-token', {
      companyId: 25,
      branchId: 7,
      workstationId: expect.any(String),
      printerName: 'Digital POS DIG-58IIA',
      drawerPin: 1,
      drawerOnTimeMs: 100,
      drawerOffTimeMs: 300,
    });
  });

  it('reutiliza el mismo jobId al reintentar una respuesta incierta', async () => {
    const transientError = new Error('timeout');
    service.testPrint
      .mockRejectedValueOnce(transientError)
      .mockResolvedValueOnce({ jobId: 'server-echo', status: 'replayed', executed: false });

    store.setState({ token: 'agent-token', configurationSaved: true });
    await store.getState().testPrint();

    expect(service.testPrint).toHaveBeenCalledTimes(2);
    expect(service.testPrint.mock.calls[0][1]).toBe(service.testPrint.mock.calls[1][1]);
    expect(store.getState().lastCommand).toMatchObject({ status: 'replayed', executed: false });
  });

  it('limpia un token rechazado para permitir una nueva vinculacion', async () => {
    const unauthorized = Object.assign(new Error('token invalido'), { status: 401 });
    service.getPrinters.mockRejectedValue(unauthorized);
    store.setState({
      token: 'stale-token',
      agentId: 'old-agent',
      agentPaired: true,
      pairedContext: { companyId: 25, branchId: 7, workstationId: 'POS-1' },
      configurationSaved: true,
    });

    await expect(store.getState().loadPrinters()).rejects.toBe(unauthorized);

    expect(store.getState()).toMatchObject({
      token: null,
      agentId: null,
      agentPaired: false,
      pairedContext: null,
      printers: [],
      configurationSaved: false,
    });
  });

  it('requiere reconciliar y guardar la configuracion despues de cada pairing', async () => {
    service.pair.mockResolvedValue({ token: 'new-token', tokenType: 'Bearer', agentId: 'agent-2' });
    store.getState().initializeContext({ companyId: 25, branchId: 7 });
    store.setState({ configurationSaved: true });

    await store.getState().pair('123456');

    expect(store.getState()).toMatchObject({
      token: 'new-token',
      configurationSaved: false,
    });
  });

  it('no permite comandos fisicos mientras la configuracion tenga cambios sin guardar', async () => {
    store.setState({ token: 'agent-token', configurationSaved: false });

    await expect(store.getState().openDrawer()).rejects.toThrow('Guarda la configuracion');
    expect(service.openDrawer).not.toHaveBeenCalled();
  });
});
