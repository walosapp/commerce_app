import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createStore } from 'zustand/vanilla';
import {
  createPrintAgentState,
  selectPersistedPrintAgentState,
} from '../../stores/printAgentStore';
import { createPrintReceiptCommand } from '../../services/receiptDocument';

const createService = () => ({
  health: vi.fn(),
  pair: vi.fn(),
  getPrinters: vi.fn(),
  savePrinterConfig: vi.fn(),
  testPrint: vi.fn(),
  openDrawer: vi.fn(),
  printReceipt: vi.fn(),
  printCashClose: vi.fn(),
});

const persistedReceipt = {
  companyId: 25,
  branchId: 7,
  companyName: 'Comercio',
  companyLegalName: null,
  companyPhone: null,
  companyTaxId: null,
  companyAddress: null,
  currency: 'COP',
  timezone: 'America/Bogota',
  orderId: 123,
  orderNumber: 'ORD-123',
  status: 'completed',
  refundStatus: null,
  tableName: 'Mostrador',
  tableNumber: 1,
  createdAt: '2026-09-14T17:30:00Z',
  cashierName: 'Maria',
  items: [{ productName: 'Cafe', quantity: 1, unitPrice: 5000, subtotal: 5000 }],
  subtotal: 5000,
  discountType: null,
  discountValue: 0,
  discountAmount: 0,
  finalTotalPaid: 5000,
  tipAmount: 0,
  tipIncluded: false,
  splitCount: 1,
  payments: [{ method: 'cash', amount: 5000, reference: null }],
  hasCredit: false,
  creditStatus: null,
  creditOriginalTotal: null,
  creditAmountPaid: null,
  creditAmount: null,
  creditCustomerName: null,
};

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

    store.setState({
      token: 'agent-token',
      configurationSaved: true,
      config: { ...store.getState().config, companyId: 25, branchId: 7 },
    });
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

  it('reintenta un recibo con el mismo jobId y fingerprint sin abrir el cajon', async () => {
    service.printReceipt
      .mockRejectedValueOnce(new Error('timeout'))
      .mockResolvedValueOnce({ status: 'replayed', executed: false });
    store.setState({ token: 'agent-token', configurationSaved: true });

    await store.getState().printReceipt(persistedReceipt);

    expect(service.printReceipt).toHaveBeenCalledTimes(2);
    expect(service.printReceipt.mock.calls[0][1]).toEqual(service.printReceipt.mock.calls[1][1]);
    expect(service.printReceipt.mock.calls[0][1]).toMatchObject({
      documentVersion: 1,
      orderId: 123,
      jobId: expect.any(String),
      fingerprint: expect.stringMatching(/^[a-f0-9]{64}$/),
    });
    expect(service.openDrawer).not.toHaveBeenCalled();
  });

  it('no reintenta un conflicto de jobId informado por el agente', async () => {
    const conflict = Object.assign(new Error('job_id_conflict'), { status: 409 });
    service.printReceipt.mockRejectedValue(conflict);
    store.setState({ token: 'agent-token', configurationSaved: true });

    await expect(store.getState().printReceipt(persistedReceipt)).rejects.toBe(conflict);

    expect(service.printReceipt).toHaveBeenCalledOnce();
    expect(store.getState().pendingReceiptCommand).toBeNull();
    expect(service.openDrawer).not.toHaveBeenCalled();
  });

  it('conserva un 503 tras hidratar y reintenta exactamente el mismo jobId', async () => {
    const spoolerError = Object.assign(new Error('spooler_error'), { status: 503 });
    service.printReceipt.mockRejectedValue(spoolerError);
    store.setState({ token: 'agent-token', configurationSaved: true });

    await expect(store.getState().printReceipt(persistedReceipt)).rejects.toBe(spoolerError);

    const pending = selectPersistedPrintAgentState(store.getState()).pendingReceiptCommand;
    expect(pending).toMatchObject({
      orderId: 123,
      jobId: expect.any(String),
      fingerprint: expect.stringMatching(/^[a-f0-9]{64}$/),
    });

    const rehydratedService = createService();
    rehydratedService.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    const rehydrated = createStore(createPrintAgentState(rehydratedService));
    rehydrated.setState({
      token: 'agent-token',
      configurationSaved: true,
      pendingReceiptCommand: pending,
      lastCommand: store.getState().lastCommand,
    });

    await rehydrated.getState().retryReceipt(persistedReceipt);

    expect(rehydratedService.printReceipt).toHaveBeenCalledWith(
      'agent-token',
      expect.objectContaining(pending)
    );
    expect(rehydrated.getState().pendingReceiptCommand).toBeNull();
    expect(rehydratedService.openDrawer).not.toHaveBeenCalled();
  });

  it('solo libera un intento fallido o incierto mediante reconocimiento explicito', () => {
    const pending = { orderId: 123, jobId: 'pending-job', fingerprint: 'a'.repeat(64) };
    store.setState({
      pendingReceiptCommand: pending,
      lastCommand: { command: 'print-receipt', jobId: pending.jobId, status: 'failed' },
    });

    expect(store.getState().pendingReceiptCommand).toBe(pending);

    store.getState().acknowledgeReceiptAttempt();

    expect(store.getState().pendingReceiptCommand).toBeNull();
    expect(store.getState().lastCommand).toMatchObject({ reviewed: true });
    expect(service.printReceipt).not.toHaveBeenCalled();
  });

  it('no pierde un recibo incierto al cambiar de contexto de empresa o sucursal', () => {
    const pending = { orderId: 123, jobId: 'pending-job', fingerprint: 'a'.repeat(64) };
    store.setState({
      pairedContext: { companyId: 25, branchId: 7, workstationId: 'POS-1' },
      pendingReceiptCommand: pending,
      lastCommand: { command: 'print-receipt', jobId: pending.jobId, status: 'uncertain' },
    });

    store.getState().initializeContext({ companyId: 99, branchId: 8 });

    expect(store.getState().pendingReceiptCommand).toBe(pending);
    expect(store.getState().lastCommand).toMatchObject({ jobId: pending.jobId, status: 'uncertain' });
  });

  it.each([
    ['devolucion', { refundStatus: 'full_refund' }],
    ['contexto', { companyId: 26 }],
  ])('bloquea retry del payload viejo cuando cambia %s', async (_case, changes) => {
    const pending = await createPrintReceiptCommand(persistedReceipt, 'same-job');
    store.setState({
      token: 'agent-token',
      configurationSaved: true,
      pendingReceiptCommand: pending,
      lastCommand: { command: 'print-receipt', jobId: pending.jobId, status: 'uncertain' },
    });

    const retry = store.getState().retryReceipt({ ...persistedReceipt, ...changes });

    await expect(retry).rejects.toMatchObject({ code: 'receipt_changed' });
    expect(service.printReceipt).not.toHaveBeenCalled();
    expect(store.getState().pendingReceiptCommand).toEqual(pending);
    expect(store.getState().lastCommand).toMatchObject({ jobId: 'same-job', status: 'stale' });
  });

  it('bloquea retry cuando cambia el saldo persistido del credito', async () => {
    const creditReceipt = {
      ...persistedReceipt,
      finalTotalPaid: 4000,
      payments: [{ method: 'cash', amount: 4000, reference: null }],
      hasCredit: true,
      creditStatus: 'pending',
      creditOriginalTotal: 5000,
      creditAmountPaid: 4000,
      creditAmount: 1000,
      creditCustomerName: 'Cliente',
    };
    const pending = await createPrintReceiptCommand(creditReceipt, 'credit-job');
    store.setState({
      token: 'agent-token',
      configurationSaved: true,
      pendingReceiptCommand: pending,
    });

    const retry = store.getState().retryReceipt({
      ...creditReceipt,
      creditStatus: 'partial',
      creditAmountPaid: 4500,
      creditAmount: 500,
    });

    await expect(retry).rejects.toMatchObject({ code: 'receipt_changed' });
    expect(service.printReceipt).not.toHaveBeenCalled();
    expect(store.getState().pendingReceiptCommand).toEqual(pending);
  });

  it('conserva el comando tras dos timeouts y lo reintenta con el mismo job despues de hidratar', async () => {
    service.printReceipt
      .mockRejectedValueOnce(new Error('timeout 1'))
      .mockRejectedValueOnce(new Error('timeout 2'));
    store.setState({ token: 'agent-token', configurationSaved: true });

    await expect(store.getState().printReceipt(persistedReceipt)).rejects.toThrow('timeout 2');

    const pending = store.getState().pendingReceiptCommand;
    expect(pending).toMatchObject({
      jobId: expect.any(String),
      fingerprint: expect.stringMatching(/^[a-f0-9]{64}$/),
    });
    expect(store.getState().lastCommand.status).toBe('uncertain');
    expect(selectPersistedPrintAgentState(store.getState())).toMatchObject({
      pendingReceiptCommand: pending,
      lastCommand: { jobId: pending.jobId, status: 'uncertain' },
    });

    const rehydratedService = createService();
    rehydratedService.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    const rehydrated = createStore(createPrintAgentState(rehydratedService));
    rehydrated.setState({
      token: 'agent-token',
      configurationSaved: true,
      pendingReceiptCommand: pending,
      lastCommand: store.getState().lastCommand,
    });

    await rehydrated.getState().retryReceipt(persistedReceipt);

    expect(rehydratedService.printReceipt).toHaveBeenCalledWith(
      'agent-token',
      expect.objectContaining(pending)
    );
    expect(rehydrated.getState().pendingReceiptCommand).toBeNull();
    expect(rehydrated.getState().lastCommand).toMatchObject({
      jobId: pending.jobId,
      fingerprint: pending.fingerprint,
      status: 'replayed',
      executed: false,
    });
    expect(rehydratedService.openDrawer).not.toHaveBeenCalled();
  });

  it('acepta jobIds H3 explicitos sin cambiar los IDs aleatorios de comandos manuales', async () => {
    service.printReceipt.mockResolvedValue({ status: 'completed', executed: true });
    service.openDrawer.mockResolvedValue({ status: 'completed', executed: true });
    store.setState({
      token: 'agent-token',
      configurationSaved: true,
      config: { ...store.getState().config, companyId: 25, branchId: 7 },
    });

    await store.getState().printReceipt(persistedReceipt, { jobId: 'post-sale.v1.c25.b7.o123.receipt' });
    await store.getState().openDrawer({ jobId: 'post-sale.v1.c25.b7.o123.drawer' });

    expect(service.printReceipt).toHaveBeenCalledWith(
      'agent-token',
      expect.objectContaining({ jobId: 'post-sale.v1.c25.b7.o123.receipt' })
    );
    expect(service.openDrawer).toHaveBeenCalledWith('agent-token', {
      jobId: 'post-sale.v1.c25.b7.o123.drawer',
      companyId: 25,
      branchId: 7,
    });
  });

  it('persiste solo metadata del recibo incierto y no datos de cliente o items', async () => {
    service.printReceipt.mockRejectedValue(Object.assign(new Error('spooler'), { status: 503 }));
    store.setState({ token: 'agent-token', configurationSaved: true });

    await expect(store.getState().printReceipt(persistedReceipt)).rejects.toThrow('spooler');

    const persisted = JSON.stringify(selectPersistedPrintAgentState(store.getState()));
    expect(persisted).not.toContain('Comercio');
    expect(persisted).not.toContain('Cafe');
    expect(persisted).not.toContain('payments');
    expect(store.getState().pendingReceiptCommand).toEqual({
      documentVersion: 1,
      jobId: expect.any(String),
      companyId: 25,
      branchId: 7,
      orderId: 123,
      fingerprint: expect.stringMatching(/^[a-f0-9]{64}$/),
    });
  });

  it('un intento H3 no sobrescribe ni libera el pendiente manual H2', async () => {
    const spoolerError = Object.assign(new Error('spooler'), { status: 503 });
    service.printReceipt
      .mockRejectedValueOnce(spoolerError)
      .mockResolvedValueOnce({ status: 'completed', executed: true });
    store.setState({ token: 'agent-token', configurationSaved: true });

    await expect(store.getState().printReceipt(persistedReceipt)).rejects.toBe(spoolerError);
    const manualPending = store.getState().pendingReceiptCommand;

    await store.getState().printPostSaleReceipt(
      { ...persistedReceipt, orderId: 124, orderNumber: 'ORD-124' },
      { jobId: 'post-sale.v1.c25.b7.o124.receipt' }
    );

    expect(store.getState().pendingReceiptCommand).toEqual(manualPending);
    expect(store.getState().pendingReceiptCommand.orderId).toBe(123);
    expect(service.printReceipt).toHaveBeenLastCalledWith(
      'agent-token',
      expect.objectContaining({ orderId: 124, jobId: 'post-sale.v1.c25.b7.o124.receipt' })
    );
  });

  it('serializa globalmente impresion manual H2 y postventa H3', async () => {
    let releaseManual;
    const manualGate = new Promise((resolve) => { releaseManual = resolve; });
    const executionOrder = [];
    service.printReceipt
      .mockImplementationOnce(async (_token, command) => {
        executionOrder.push(`start-${command.orderId}`);
        await manualGate;
        executionOrder.push(`end-${command.orderId}`);
        return { status: 'completed', executed: true };
      })
      .mockImplementationOnce(async (_token, command) => {
        executionOrder.push(`start-${command.orderId}`);
        return { status: 'completed', executed: true };
      });
    store.setState({ token: 'agent-token', configurationSaved: true });

    const manual = store.getState().printReceipt(persistedReceipt);
    await vi.waitFor(() => expect(service.printReceipt).toHaveBeenCalledTimes(1));
    const automatic = store.getState().printPostSaleReceipt(
      { ...persistedReceipt, orderId: 124, orderNumber: 'ORD-124' },
      { jobId: 'post-sale.v1.c25.b7.o124.receipt' }
    );
    await Promise.resolve();
    expect(service.printReceipt).toHaveBeenCalledTimes(1);

    releaseManual();
    await Promise.all([manual, automatic]);

    expect(executionOrder).toEqual(['start-123', 'end-123', 'start-124']);
  });

  it('imprime cierre con retry estable y nunca invoca cajon', async () => {
    service.printCashClose
      .mockRejectedValueOnce(new Error('timeout'))
      .mockResolvedValueOnce({ status: 'completed', executed: true });
    store.setState({ token: 'agent-token', configurationSaved: true });
    const report = {
      cashRegisterId: 15, companyId: 25, branchId: 7, companyName: 'Comercio',
      branchName: 'Centro', currency: 'COP', timezone: 'America/Bogota',
      openedAt: '2026-09-16T10:00:00Z', closedAt: '2026-09-16T18:00:00Z',
      closedByName: 'Ana', openingAmount: 100, orderCount: 1, totalSales: 500,
      totalCashSales: 500, totalCardSales: 0, totalTransferSales: 0,
      totalOtherSales: 0, refundTotal: 0, cashIn: 0, manualCashOut: 0,
      expectedCash: 600, closingAmount: 600, difference: 0, notes: null,
    };

    await store.getState().printCashClose(report, { jobId: 'cash-close-job' });

    expect(service.printCashClose).toHaveBeenCalledTimes(2);
    expect(service.printCashClose.mock.calls[0][1]).toEqual(service.printCashClose.mock.calls[1][1]);
    expect(service.openDrawer).not.toHaveBeenCalled();
  });
});
