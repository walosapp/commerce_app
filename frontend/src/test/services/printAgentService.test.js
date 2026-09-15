import { beforeEach, describe, expect, it, vi } from 'vitest';
import printAgentService from '../../services/printAgentService';

describe('printAgentService H2', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      json: async () => ({ status: 'completed', executed: true }),
    }));
  });

  it('envia Bearer y solo el contrato tipado de print-receipt', async () => {
    const command = {
      documentVersion: 1,
      jobId: 'job-123',
      companyId: 25,
      branchId: 7,
      orderId: 123,
      fingerprint: 'a'.repeat(64),
      html: '<script>malicioso</script>',
      bytes: [27, 112],
      receipt: {
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
        createdAt: '2026-09-14T17:30:00.000Z',
        cashierName: 'Maria',
        items: [{ productName: 'Cafe', quantity: 1, unitPrice: 5000, subtotal: 5000, bytes: [27] }],
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
      },
    };

    await printAgentService.printReceipt('agent-token', command);

    expect(fetch).toHaveBeenCalledOnce();
    const [url, options] = fetch.mock.calls[0];
    const sent = JSON.parse(options.body);
    expect(url).toBe('http://127.0.0.1:17831/v1/commands/print-receipt');
    expect(options.headers.Authorization).toBe('Bearer agent-token');
    expect(sent).toMatchObject({ documentVersion: 1, jobId: 'job-123', orderId: 123 });
    expect(sent).not.toHaveProperty('html');
    expect(sent).not.toHaveProperty('bytes');
    expect(sent.receipt.items[0]).not.toHaveProperty('bytes');
  });

  it('envia contexto vinculado en open-drawer y no acepta un payload jobId-only', async () => {
    await printAgentService.openDrawer('agent-token', {
      jobId: 'drawer-job-1',
      companyId: 25,
      branchId: 7,
    });

    const [url, options] = fetch.mock.calls[0];
    expect(url).toBe('http://127.0.0.1:17831/v1/commands/open-drawer');
    expect(JSON.parse(options.body)).toEqual({
      jobId: 'drawer-job-1',
      companyId: 25,
      branchId: 7,
    });
  });
});
