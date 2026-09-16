import { describe, expect, it, vi } from 'vitest';
import { createPrintCashCloseCommand, mapCashCloseDataToDocument } from '../../services/cashCloseDocument';

const report = {
  cashRegisterId: 15,
  companyId: 10,
  branchId: 20,
  companyName: 'Comercio',
  branchName: 'Centro',
  currency: 'COP',
  timezone: 'America/Bogota',
  openedAt: '2026-09-16T10:00:00Z',
  closedAt: '2026-09-16T18:00:00Z',
  closedByName: 'Ana',
  openingAmount: 100,
  orderCount: 4,
  totalSales: 900,
  totalCashSales: 500,
  totalCardSales: 300,
  totalTransferSales: 100,
  totalOtherSales: 0,
  refundTotal: 50,
  cashIn: 20,
  manualCashOut: 10,
  expectedCash: 630,
  closingAmount: 625,
  difference: -5,
  notes: 'Cierre normal',
};

describe('cash close print document', () => {
  it('maps only persisted backend summary fields', () => {
    const document = mapCashCloseDataToDocument(report);

    expect(document.cashClose).toMatchObject({
      cashRegisterId: 15,
      companyName: 'Comercio',
      branchName: 'Centro',
      refundTotal: 50,
      cashOut: 10,
      countedCash: 625,
      difference: -5,
    });
    expect(document).not.toHaveProperty('html');
    expect(document).not.toHaveProperty('openDrawer');
  });

  it('creates a stable fingerprint for the same persisted close', async () => {
    const digest = new Uint8Array(32).fill(7).buffer;
    vi.spyOn(globalThis.crypto.subtle, 'digest').mockResolvedValue(digest);

    const command = await createPrintCashCloseCommand(report, 'cash-close-job');

    expect(command.jobId).toBe('cash-close-job');
    expect(command.fingerprint).toBe('07'.repeat(32));
  });
});
