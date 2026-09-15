import { createStore } from 'zustand/vanilla';
import { createJSONStorage, persist } from 'zustand/middleware';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  createPostSaleHardwareState,
  createSafePostSaleStorage,
  DEFAULT_POST_SALE_POLICY,
  POST_SALE_PERSISTENCE_WARNING,
  postSaleStorageWarningFor,
  probePostSaleStorage,
  retainSafePostSaleIntents,
  selectPersistedPostSaleState,
} from '../../stores/postSaleHardwareStore';

const receipt = (orderId) => ({
  companyId: 25,
  branchId: 7,
  orderId,
  status: 'completed',
  refundStatus: null,
  companyName: 'Dato que no debe persistirse',
  companyLegalName: null,
  companyPhone: null,
  companyTaxId: null,
  companyAddress: null,
  currency: 'COP',
  timezone: 'America/Bogota',
  orderNumber: `ORD-${orderId}`,
  tableName: 'Mostrador',
  tableNumber: 1,
  createdAt: '2026-09-14T17:30:00.000Z',
  cashierName: 'Maria',
  items: [{ productName: 'Cafe', quantity: 1, unitPrice: 1000, subtotal: 1000 }],
  subtotal: 1000,
  discountType: null,
  discountValue: 0,
  discountAmount: 0,
  finalTotalPaid: 1000,
  tipAmount: 0,
  tipIncluded: false,
  splitCount: 1,
  creditCustomerName: 'Cliente privado',
  hasCredit: false,
  creditStatus: null,
  creditOriginalTotal: null,
  creditAmountPaid: null,
  creditAmount: null,
  payments: [{ method: 'cash', amount: 1000 }],
});

const createDependencies = () => ({
  getReceipt: vi.fn(async (orderId) => ({ data: receipt(orderId) })),
  printReceipt: vi.fn().mockResolvedValue({ status: 'completed', executed: true }),
  openDrawer: vi.fn().mockResolvedValue({ status: 'completed', executed: true }),
  notify: vi.fn(),
});

const createPersistedFailureStore = (dependencies, shouldFail) => {
  let store;
  let reporting = false;
  let writeCount = 0;
  const rawStorage = {
    getItem: vi.fn(() => null),
    setItem: vi.fn((_, value) => {
      writeCount += 1;
      if (shouldFail(value, writeCount, store?.getState())) throw new Error('storage write failed');
    }),
    removeItem: vi.fn(),
  };
  const safeStorage = createSafePostSaleStorage(rawStorage, () => {
    if (reporting) return;
    reporting = true;
    store.getState().reportPersistenceFailure();
  });
  store = createStore(persist(createPostSaleHardwareState(dependencies), {
    name: 'post-sale-test',
    storage: createJSONStorage(() => safeStorage),
    partialize: selectPersistedPostSaleState,
  }));
  return store;
};

describe('postSaleHardwareStore', () => {
  let dependencies;
  let store;

  beforeEach(() => {
    dependencies = createDependencies();
    store = createStore(createPostSaleHardwareState(dependencies));
  });

  const updatePolicy = (partial, companyId = 25, branchId = 7) =>
    store.getState().updatePolicy({ companyId, branchId }, partial);

  it('mantiene ambas preferencias desactivadas por defecto', () => {
    expect(store.getState().getPolicy(25, 7)).toEqual(DEFAULT_POST_SALE_POLICY);
  });

  it('aísla la política por empresa y sucursal canónicas del recibo', () => {
    updatePolicy({ autoOpenCashDrawer: true }, 25, 7);

    expect(store.getState().getPolicy(25, 7).autoOpenCashDrawer).toBe(true);
    expect(store.getState().getPolicy(99, 7)).toEqual(DEFAULT_POST_SALE_POLICY);
    expect(store.getState().getPolicy(25, 8)).toEqual(DEFAULT_POST_SALE_POLICY);
  });

  it('no aplica a un recibo la politica habilitada de otro tenant', async () => {
    updatePolicy({ autoPrintReceipt: true, autoOpenCashDrawer: true }, 99, 7);

    const outcome = await store.getState().enqueuePostSale({ orderId: 123 });

    expect(outcome).toMatchObject({
      companyId: 25,
      branchId: 7,
      printStatus: 'skipped_disabled',
      drawerStatus: 'skipped_disabled',
    });
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('serializa ventas rapidas y conserva el orden de procesamiento', async () => {
    const events = [];
    dependencies.getReceipt.mockImplementation(async (orderId) => {
      events.push(`fetch-${orderId}`);
      return { data: receipt(orderId) };
    });
    dependencies.printReceipt.mockImplementation(async (value) => {
      events.push(`print-${value.orderId}`);
      return { status: 'completed', executed: true };
    });
    updatePolicy({ autoPrintReceipt: true });

    await Promise.all([
      store.getState().enqueuePostSale({ orderId: 101 }),
      store.getState().enqueuePostSale({ orderId: 102 }),
    ]);

    expect(events).toEqual(['fetch-101', 'print-101', 'fetch-102', 'print-102']);
  });

  it('deduplica en vuelo y replays posteriores conservan IDs deterministas', async () => {
    updatePolicy({ autoPrintReceipt: true, autoOpenCashDrawer: true });

    const first = store.getState().enqueuePostSale({ orderId: 123 });
    const duplicate = store.getState().enqueuePostSale({ orderId: 123 });
    expect(duplicate).toBe(first);
    const firstOutcome = await first;

    dependencies.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    const replay = await store.getState().enqueuePostSale({ orderId: 123 });

    expect(replay.receiptJobId).toBe(firstOutcome.receiptJobId);
    expect(replay.drawerJobId).toBe(firstOutcome.drawerJobId);
    expect(replay).toMatchObject({ printStatus: 'replayed', drawerStatus: 'skipped_print_replay' });
    expect(dependencies.openDrawer).toHaveBeenCalledTimes(1);
  });

  it('persiste solo metadata y nunca ReceiptDocument, pagos ni PII', async () => {
    updatePolicy({ autoPrintReceipt: true, autoOpenCashDrawer: true });
    await store.getState().enqueuePostSale({ orderId: 123 });

    const persisted = {
      policies: store.getState().policies,
      intents: store.getState().intents,
      lastOutcome: store.getState().lastOutcome,
    };
    const json = JSON.stringify(persisted);

    expect(json).not.toContain('Dato que no debe persistirse');
    expect(json).not.toContain('Cliente privado');
    expect(json).not.toContain('payments');
    expect(json).toContain('post-sale.v1.c25.b7.o123.receipt');
    expect(Object.keys(store.getState().intents)).toEqual(['c25:b7:o123']);
  });

  it('conserva los mismos jobIds al rehidratar y reejecutar la misma orden', async () => {
    updatePolicy({ autoPrintReceipt: true, autoOpenCashDrawer: true });
    const first = await store.getState().enqueuePostSale({ orderId: 123 });
    const snapshot = {
      policies: store.getState().policies,
      intents: store.getState().intents,
      lastOutcome: store.getState().lastOutcome,
    };

    const rehydratedDependencies = createDependencies();
    rehydratedDependencies.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    const rehydrated = createStore(createPostSaleHardwareState(rehydratedDependencies));
    rehydrated.setState(snapshot);

    const replay = await rehydrated.getState().enqueuePostSale({ orderId: 123 });

    expect(replay.receiptJobId).toBe(first.receiptJobId);
    expect(replay.drawerJobId).toBe(first.drawerJobId);
    expect(replay).toMatchObject({ printStatus: 'replayed', drawerStatus: 'skipped_print_replay' });
    expect(rehydratedDependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('no invoca window.print como fallback automatico', async () => {
    const browserPrint = vi.spyOn(window, 'print').mockImplementation(() => {});
    dependencies.printReceipt.mockRejectedValue(new Error('offline'));
    updatePolicy({ autoPrintReceipt: true });

    await store.getState().enqueuePostSale({ orderId: 123 });

    expect(browserPrint).not.toHaveBeenCalled();
    expect(dependencies.notify).toHaveBeenCalledOnce();
  });

  it('rehidrata un timeout H3 y reintenta exactamente el mismo printJob sin cajon', async () => {
    const timeout = Object.assign(new Error('timeout'), { status: 503, code: 'spooler_error' });
    dependencies.printReceipt.mockRejectedValue(timeout);
    updatePolicy({ autoPrintReceipt: true });
    await store.getState().enqueuePostSale({ orderId: 123 });
    const original = store.getState().getIntent(25, 7, 123);

    expect(original).toMatchObject({
      printStatus: 'uncertain',
      receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
      printFingerprint: expect.stringMatching(/^[a-f0-9]{64}$/),
    });

    const retryDependencies = createDependencies();
    retryDependencies.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    const rehydrated = createStore(createPostSaleHardwareState(retryDependencies));
    rehydrated.setState({
      policies: store.getState().policies,
      intents: store.getState().intents,
      lastOutcome: store.getState().lastOutcome,
    });

    const result = await rehydrated.getState().retryPostSalePrint({ companyId: 25, branchId: 7, orderId: 123 });

    expect(result).toMatchObject({ status: 'replayed', executed: false });
    expect(retryDependencies.printReceipt).toHaveBeenCalledWith(
      expect.objectContaining({ orderId: 123 }),
      { jobId: original.receiptJobId }
    );
    expect(retryDependencies.openDrawer).not.toHaveBeenCalled();
    expect(rehydrated.getState().getIntent(25, 7, 123).printStatus).toBe('replayed');
  });

  it('bloquea el retry del mismo job si cambio el recibo persistido', async () => {
    dependencies.printReceipt.mockRejectedValue(Object.assign(new Error('timeout'), { status: 503 }));
    updatePolicy({ autoPrintReceipt: true });
    await store.getState().enqueuePostSale({ orderId: 123 });
    dependencies.printReceipt.mockClear();
    dependencies.getReceipt.mockResolvedValue({
      data: {
        ...receipt(123),
        subtotal: 1200,
        finalTotalPaid: 1200,
        items: [{ productName: 'Cafe', quantity: 1, unitPrice: 1200, subtotal: 1200 }],
        payments: [{ method: 'cash', amount: 1200 }],
      },
    });

    await expect(store.getState().retryPostSalePrint({ companyId: 25, branchId: 7, orderId: 123 }))
      .rejects.toMatchObject({ code: 'receipt_changed' });

    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
    expect(store.getState().getIntent(25, 7, 123)).toMatchObject({
      printStatus: 'stale',
      printReviewed: false,
    });
  });

  it('solo libera un intent H3 incierto mediante acknowledgement explicito', async () => {
    dependencies.printReceipt.mockRejectedValue(Object.assign(new Error('timeout'), { status: 503 }));
    updatePolicy({ autoPrintReceipt: true });
    await store.getState().enqueuePostSale({ orderId: 123 });

    store.getState().acknowledgePostSalePrint({ companyId: 25, branchId: 7, orderId: 123 });

    expect(store.getState().getIntent(25, 7, 123)).toMatchObject({
      printStatus: 'uncertain',
      printReviewed: true,
    });
  });

  it('convierte un trabajo activo interrumpido en incierto al persistir para reload', () => {
    const snapshot = selectPersistedPostSaleState({
      policies: {},
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
          printFingerprint: 'a'.repeat(64),
          printStatus: 'queued',
        },
      },
      lastOutcome: null,
    });

    expect(snapshot.intents['c25:b7:o123']).toMatchObject({
      printStatus: 'uncertain',
      printErrorCode: 'interrupted',
    });
  });

  it('convierte tambien un cajon activo interrumpido en incierto al persistir', () => {
    const snapshot = selectPersistedPostSaleState({
      policies: {},
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
          drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
          printStatus: 'completed',
          drawerStatus: 'queued',
        },
      },
      lastOutcome: null,
    });

    expect(snapshot.intents['c25:b7:o123']).toMatchObject({
      printStatus: 'completed',
      drawerStatus: 'uncertain',
      drawerErrorCode: 'interrupted',
    });
  });

  it('conserva un cajon incierto aunque la impresion ya haya terminado', () => {
    const intents = Object.fromEntries([
      ...Array.from({ length: 101 }, (_, index) => [
        `c25:b7:o${index + 1}`,
        {
          companyId: 25,
          branchId: 7,
          orderId: index + 1,
          printStatus: 'completed',
          drawerStatus: 'completed',
        },
      ]),
      ['c25:b7:o999', {
        companyId: 25,
        branchId: 7,
        orderId: 999,
        drawerJobId: 'post-sale.v1.c25.b7.o999.drawer',
        printStatus: 'completed',
        drawerStatus: 'uncertain',
        drawerReviewed: false,
      }],
    ]);

    const retained = retainSafePostSaleIntents(intents);

    expect(Object.keys(retained)).toHaveLength(101);
    expect(retained['c25:b7:o1']).toBeUndefined();
    expect(retained['c25:b7:o2']).toBeDefined();
    expect(retained['c25:b7:o999']).toMatchObject({ drawerStatus: 'uncertain' });
  });

  it('reconcilia solo el cajon con el mismo drawerJob y nunca reimprime', async () => {
    store.setState({
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
          drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
          printStatus: 'completed',
          drawerStatus: 'uncertain',
          drawerReviewed: false,
        },
      },
    });
    dependencies.openDrawer.mockResolvedValue({ status: 'replayed', executed: false });

    const result = await store.getState().retryPostSaleDrawer({ companyId: 25, branchId: 7, orderId: 123 });

    expect(result).toMatchObject({ status: 'replayed', executed: false });
    expect(dependencies.getReceipt).toHaveBeenCalledWith(123);
    expect(dependencies.openDrawer).toHaveBeenCalledWith({
      jobId: 'post-sale.v1.c25.b7.o123.drawer',
      companyId: 25,
      branchId: 7,
    });
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(store.getState().getIntent(25, 7, 123)).toMatchObject({
      printStatus: 'completed',
      drawerStatus: 'replayed',
    });
  });

  it('no abre el cajon al reconciliar si el recibo ya no es elegible', async () => {
    store.setState({
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
          printStatus: 'completed',
          drawerStatus: 'uncertain',
          drawerReviewed: false,
        },
      },
    });
    dependencies.getReceipt.mockResolvedValue({ data: { ...receipt(123), refundStatus: 'refunded' } });

    await expect(store.getState().retryPostSaleDrawer({ companyId: 25, branchId: 7, orderId: 123 }))
      .rejects.toMatchObject({ code: 'receipt_ineligible' });

    expect(dependencies.openDrawer).not.toHaveBeenCalled();
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(store.getState().getIntent(25, 7, 123)).toMatchObject({
      drawerStatus: 'stale',
      drawerReviewed: false,
    });
  });

  it('solo libera un cajon incierto mediante acknowledgement explicito', () => {
    store.setState({
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
          printStatus: 'completed',
          drawerStatus: 'uncertain',
          drawerReviewed: false,
        },
      },
    });

    store.getState().acknowledgePostSaleDrawer({ companyId: 25, branchId: 7, orderId: 123 });

    expect(store.getState().getIntent(25, 7, 123)).toMatchObject({
      printStatus: 'completed',
      drawerStatus: 'uncertain',
      drawerReviewed: true,
    });
  });

  it('limita solo terminales y nunca expulsa un intento incierto', () => {
    const intents = Object.fromEntries([
      ...Array.from({ length: 105 }, (_, index) => [
        `c25:b7:o${index + 1}`,
        { companyId: 25, branchId: 7, orderId: index + 1, printStatus: 'completed' },
      ]),
      ['c25:b7:o999', {
        companyId: 25,
        branchId: 7,
        orderId: 999,
        printStatus: 'uncertain',
        printReviewed: false,
      }],
    ]);

    const retained = retainSafePostSaleIntents(intents);

    expect(Object.keys(retained)).toHaveLength(101);
    expect(retained['c25:b7:o1']).toBeUndefined();
    expect(retained['c25:b7:o6']).toBeDefined();
    expect(retained['c25:b7:o999']).toMatchObject({ printStatus: 'uncertain' });
  });

  it('conserva mas de 100 intentos no reconciliados y activa alerta fail-closed', () => {
    const intents = Object.fromEntries(Array.from({ length: 101 }, (_, index) => [
      `c25:b7:o${index + 1}`,
      {
        companyId: 25,
        branchId: 7,
        orderId: index + 1,
        printStatus: 'uncertain',
        printReviewed: false,
      },
    ]));

    const retained = retainSafePostSaleIntents(intents);

    expect(Object.keys(retained)).toHaveLength(101);
    expect(postSaleStorageWarningFor(retained)).toMatch(/mas de 100 acciones fisicas postventa/i);
  });

  it('no sobrescribe un intent incierto de la misma orden aunque cambie la politica', async () => {
    dependencies.printReceipt.mockRejectedValue(Object.assign(new Error('timeout'), { status: 503 }));
    updatePolicy({ autoPrintReceipt: true });
    const unresolved = await store.getState().enqueuePostSale({ orderId: 123 });
    expect(unresolved.printStatus).toBe('uncertain');

    updatePolicy({ autoPrintReceipt: false, autoOpenCashDrawer: false });
    const replay = await store.getState().enqueuePostSale({ orderId: 123 });

    expect(replay).toBe(unresolved);
    expect(replay.printStatus).toBe('uncertain');
    expect(dependencies.getReceipt).toHaveBeenCalledOnce();
    expect(dependencies.printReceipt).toHaveBeenCalledOnce();
    expect(store.getState().getIntent(25, 7, 123)).toBe(unresolved);
  });

  it('no reejecuta la venta si solo el cajon sigue sin reconciliar', async () => {
    const pendingDrawer = {
      companyId: 25,
      branchId: 7,
      orderId: 123,
      receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
      drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
      printStatus: 'completed',
      drawerStatus: 'uncertain',
      drawerReviewed: false,
    };
    store.setState({ intents: { 'c25:b7:o123': pendingDrawer } });

    const outcome = await store.getState().enqueuePostSale({ orderId: 123 });

    expect(outcome).toBe(pendingDrawer);
    expect(dependencies.getReceipt).not.toHaveBeenCalled();
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('detecta storage inaccesible y arranca fail-closed tambien despues de reload', async () => {
    const inaccessibleStorage = {
      getItem: vi.fn(() => { throw new Error('storage denied'); }),
      setItem: vi.fn(() => { throw new Error('storage denied'); }),
      removeItem: vi.fn(() => { throw new Error('storage denied'); }),
    };
    const runtime = createStore(createPostSaleHardwareState(dependencies));
    const safeStorage = createSafePostSaleStorage(
      inaccessibleStorage,
      runtime.getState().reportPersistenceFailure
    );

    expect(probePostSaleStorage(inaccessibleStorage)).toBe(false);
    expect(safeStorage.getItem('state')).toBeNull();
    safeStorage.setItem('state', '{}');
    safeStorage.removeItem('state');
    expect(runtime.getState()).toMatchObject({
      persistenceFailed: true,
      storageWarning: POST_SALE_PERSISTENCE_WARNING,
    });
    const runtimeOutcome = await runtime.getState().enqueuePostSale({ orderId: 123 });
    expect(runtimeOutcome.status).toBe('persistence_failed');

    const reloaded = createStore(createPostSaleHardwareState(dependencies, {
      persistenceFailed: !probePostSaleStorage(inaccessibleStorage),
    }));
    const outcome = await reloaded.getState().enqueuePostSale({ orderId: 123 });

    expect(reloaded.getState()).toMatchObject({
      persistenceFailed: true,
      storageWarning: POST_SALE_PERSISTENCE_WARNING,
    });
    expect(outcome).toMatchObject({ status: 'persistence_failed' });
    expect(dependencies.getReceipt).not.toHaveBeenCalled();
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('aborta antes del fetch si falla exactamente la escritura queued', async () => {
    const persisted = createPersistedFailureStore(
      dependencies,
      (_, writeCount) => writeCount === 1
    );

    const outcome = await persisted.getState().enqueuePostSale({ orderId: 123 });

    expect(outcome.status).toBe('persistence_failed');
    expect(persisted.getState().persistenceFailed).toBe(true);
    expect(persisted.getState().intents['pending:o123']).toMatchObject({ printStatus: 'pending_policy' });
    expect(dependencies.getReceipt).not.toHaveBeenCalled();
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('aborta retry antes del fetch si falla exactamente la escritura retrying', async () => {
    const persisted = createPersistedFailureStore(
      dependencies,
      (_, writeCount) => writeCount === 2
    );
    persisted.setState({
      intents: {
        'c25:b7:o123': {
          companyId: 25,
          branchId: 7,
          orderId: 123,
          receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
          printFingerprint: 'a'.repeat(64),
          printStatus: 'uncertain',
          printReviewed: false,
        },
      },
    });

    await expect(persisted.getState().retryPostSalePrint({ companyId: 25, branchId: 7, orderId: 123 }))
      .rejects.toMatchObject({ code: 'persistence_unavailable' });

    expect(persisted.getState().persistenceFailed).toBe(true);
    expect(persisted.getState().getIntent(25, 7, 123)).toMatchObject({ printStatus: 'retrying' });
    expect(dependencies.getReceipt).not.toHaveBeenCalled();
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });
});
