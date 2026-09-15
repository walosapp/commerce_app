import { describe, expect, it, vi } from 'vitest';
import {
  createPostSaleHardwareRunner,
  createPostSaleJobIds,
  hasCashPayment,
} from '../../services/postSaleHardwareService';

const receipt = (overrides = {}) => ({
  companyId: 25,
  branchId: 7,
  orderId: 123,
  status: 'completed',
  refundStatus: null,
  companyName: 'Comercio',
  companyLegalName: null,
  companyPhone: null,
  companyTaxId: null,
  companyAddress: null,
  currency: 'COP',
  timezone: 'America/Bogota',
  orderNumber: 'ORD-123',
  tableName: 'Mostrador',
  tableNumber: 1,
  createdAt: '2026-09-14T17:30:00.000Z',
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
  ...overrides,
});

const setup = (persistedReceipt = receipt()) => {
  const dependencies = {
    getReceipt: vi.fn().mockResolvedValue({ data: persistedReceipt }),
    printReceipt: vi.fn().mockResolvedValue({ status: 'completed', executed: true }),
    openDrawer: vi.fn().mockResolvedValue({ status: 'completed', executed: true }),
  };
  return { dependencies, run: createPostSaleHardwareRunner(dependencies) };
};

const enabled = { autoPrintReceipt: true, autoOpenCashDrawer: true };

describe('postSaleHardwareService', () => {
  it.each([
    ['cash positivo', [{ method: 'cash', amount: 1 }], true],
    ['cash cero', [{ method: 'cash', amount: 0 }], false],
    ['Cash no canonico', [{ method: 'Cash', amount: 10 }], false],
    ['efectivo alias', [{ method: 'efectivo', amount: 10 }], false],
    ['card', [{ method: 'card', amount: 10 }], false],
    ['transfer', [{ method: 'transfer', amount: 10 }], false],
    ['mixed', [{ method: 'mixed', amount: 10 }], false],
    ['cash string', [{ method: 'cash', amount: '10' }], false],
  ])('clasifica %s sin inventar aliases', (_name, payments, expected) => {
    expect(hasCashPayment(payments)).toBe(expected);
  });

  it('genera IDs deterministas, distintos y validos para el agente', () => {
    const first = createPostSaleJobIds({ companyId: 25, branchId: 7, orderId: 123 });
    const second = createPostSaleJobIds({ companyId: 25, branchId: 7, orderId: 123 });

    expect(first).toEqual(second);
    expect(first).toEqual({
      receiptJobId: 'post-sale.v1.c25.b7.o123.receipt',
      drawerJobId: 'post-sale.v1.c25.b7.o123.drawer',
    });
    expect(first.receiptJobId).not.toBe(first.drawerJobId);
    expect(first.receiptJobId).toMatch(/^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$/);
  });

  it('obtiene el recibo canonico y ejecuta ambos trabajos con IDs separados', async () => {
    const { dependencies, run } = setup();

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(dependencies.getReceipt).toHaveBeenCalledWith(123);
    expect(dependencies.printReceipt).toHaveBeenCalledWith(expect.objectContaining({ orderId: 123 }), {
      jobId: 'post-sale.v1.c25.b7.o123.receipt',
    });
    expect(dependencies.openDrawer).toHaveBeenCalledWith({
      jobId: 'post-sale.v1.c25.b7.o123.drawer',
      companyId: 25,
      branchId: 7,
    });
    expect(outcome).toMatchObject({ status: 'completed', printStatus: 'completed', drawerStatus: 'completed' });
  });

  it('abre el cajon aunque falle la impresion', async () => {
    const { dependencies, run } = setup();
    dependencies.printReceipt.mockRejectedValue(Object.assign(new Error('printer'), { status: 404, code: 'printer_not_found' }));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(dependencies.openDrawer).toHaveBeenCalledOnce();
    expect(outcome).toMatchObject({ status: 'completed_with_errors', printStatus: 'failed', drawerStatus: 'completed' });
  });

  it('conserva venta/impresion completas si falla el cajon', async () => {
    const { dependencies, run } = setup();
    dependencies.openDrawer.mockRejectedValue(Object.assign(new Error('timeout'), { code: 'agent_timeout' }));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome).toMatchObject({ status: 'completed_with_errors', printStatus: 'completed', drawerStatus: 'uncertain' });
  });

  it('reporta ambas fallas sin lanzar ni repetir checkout', async () => {
    const { dependencies, run } = setup();
    dependencies.printReceipt.mockRejectedValue(new Error('offline'));
    dependencies.openDrawer.mockRejectedValue(new Error('offline'));

    await expect(run({ orderId: 123, policy: enabled })).resolves.toMatchObject({
      status: 'completed_with_errors',
      printStatus: 'uncertain',
      drawerStatus: 'uncertain',
    });
  });

  it.each(['card', 'transfer', 'mixed'])('no abre cajon para %s', async (method) => {
    const { dependencies, run } = setup(receipt({ payments: [{ method, amount: 5000 }] }));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome.drawerStatus).toBe('skipped_no_cash');
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('abre una sola vez para pagos mixtos que incluyen una linea cash positiva', async () => {
    const { dependencies, run } = setup(receipt({
      payments: [
        { method: 'cash', amount: 2000 },
        { method: 'card', amount: 3000 },
      ],
    }));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome.drawerStatus).toBe('completed');
    expect(dependencies.openDrawer).toHaveBeenCalledOnce();
  });

  it('no abre cajon para credito sin pago cash', async () => {
    const { dependencies, run } = setup(receipt({ payments: [], hasCredit: true, creditAmount: 5000 }));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome.drawerStatus).toBe('skipped_no_cash');
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('falla cerrado si no puede obtener el recibo persistido', async () => {
    const { dependencies, run } = setup();
    dependencies.getReceipt.mockRejectedValue(new Error('api offline'));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome.status).toBe('fetch_failed');
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it.each([
    ['cancelada', { status: 'cancelled' }],
    ['devuelta', { refundStatus: 'full' }],
  ])('no imprime ni abre cajon si la orden persistida esta %s', async (_name, overrides) => {
    const { dependencies, run } = setup(receipt(overrides));

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome).toMatchObject({
      status: 'receipt_ineligible',
      printStatus: 'skipped_receipt_ineligible',
      drawerStatus: 'skipped_receipt_ineligible',
    });
    expect(dependencies.printReceipt).not.toHaveBeenCalled();
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });

  it('un replay de impresion nunca dispara apertura de cajon', async () => {
    const { dependencies, run } = setup();
    dependencies.printReceipt.mockResolvedValue({ status: 'replayed', executed: false });
    dependencies.openDrawer.mockResolvedValue({ status: 'replayed', executed: false });

    const outcome = await run({ orderId: 123, policy: enabled });

    expect(outcome).toMatchObject({ status: 'completed', printStatus: 'replayed', drawerStatus: 'skipped_print_replay' });
    expect(dependencies.openDrawer).not.toHaveBeenCalled();
  });
});
