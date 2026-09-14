import { describe, expect, it } from 'vitest';
import {
  canonicalStringify,
  fingerprintReceiptDocument,
  mapReceiptDataToDocument,
} from '../../services/receiptDocument';

const receiptData = (overrides = {}) => ({
  companyId: 25,
  branchId: 7,
  companyName: 'Cafeteria El Nino',
  companyLegalName: 'Cafeteria El Nino SAS',
  companyPhone: '3001234567',
  companyTaxId: '900123456-1',
  companyAddress: 'Calle 1 # 2-3',
  currency: 'COP',
  timezone: 'America/Bogota',
  companyLogoUrl: 'https://example.test/logo.png',
  orderId: 123,
  orderNumber: 'ORD-00123',
  status: 'completed',
  refundStatus: null,
  tableName: 'Mesa 4',
  tableNumber: 4,
  createdAt: '2026-09-14T17:30:00Z',
  cashierName: 'Maria Pena',
  items: [
    { productName: 'Cafe pequeno', quantity: 2, unitPrice: 3500, subtotal: 7000 },
    { productName: 'Pan de queso', quantity: 1, unitPrice: 2500, subtotal: 2500 },
  ],
  subtotal: 9500,
  discountType: 'amount',
  discountValue: 500,
  discountAmount: 500,
  finalTotalPaid: 8000,
  tipAmount: 900,
  tipIncluded: true,
  splitCount: 1,
  payments: [
    { method: 'cash', amount: 5000, reference: null },
    { method: 'card', amount: 3900, reference: 'AUTH-1' },
  ],
  hasCredit: true,
  creditStatus: 'pending',
  creditOriginalTotal: 9000,
  creditAmountPaid: 8000,
  creditAmount: 1000,
  creditCustomerName: 'Jose Perez',
  ...overrides,
});

describe('ReceiptData -> ReceiptDocument', () => {
  it('mapea exclusivamente el recibo real persistido al contrato tipado versionado', () => {
    const source = receiptData();
    const document = mapReceiptDataToDocument(source);

    expect(document).toMatchObject({
      documentVersion: 1,
      companyId: 25,
      branchId: 7,
      orderId: 123,
      receipt: {
        companyName: 'Cafeteria El Nino',
        companyTaxId: '900123456-1',
        companyAddress: 'Calle 1 # 2-3',
        currency: 'COP',
        timezone: 'America/Bogota',
        status: 'completed',
        refundStatus: null,
        createdAt: '2026-09-14T17:30:00.000Z',
        finalTotalPaid: 8000,
        tipAmount: 900,
        hasCredit: true,
        creditAmount: 1000,
      },
    });
    expect(document.receipt.items).toHaveLength(2);
    expect(document.receipt.payments).toHaveLength(2);
    expect(document.receipt).not.toHaveProperty('companyLogoUrl');
    expect(document).not.toHaveProperty('html');
    expect(document).not.toHaveProperty('bytes');
  });

  it('canonicaliza propiedades en orden estable sin cambiar el orden de arrays', () => {
    expect(canonicalStringify({ z: 1, a: { y: 2, x: 3 }, items: ['b', 'a'] }))
      .toBe('{"a":{"x":3,"y":2},"items":["b","a"],"z":1}');
  });

  it('produce el mismo fingerprint para el mismo documento', async () => {
    const first = mapReceiptDataToDocument(receiptData());
    const second = mapReceiptDataToDocument(receiptData());

    expect(await fingerprintReceiptDocument(first)).toBe(await fingerprintReceiptDocument(second));
  });

  it('normaliza Unicode NFC antes de calcular el fingerprint', async () => {
    const composed = mapReceiptDataToDocument(receiptData({ companyName: 'Café' }));
    const decomposed = mapReceiptDataToDocument(receiptData({ companyName: 'Cafe\u0301' }));

    expect(await fingerprintReceiptDocument(composed))
      .toBe(await fingerprintReceiptDocument(decomposed));
  });

  it('preserva el pago historico canonico reconstruido por el backend', () => {
    const document = mapReceiptDataToDocument(receiptData({
      payments: [{ method: 'cash', amount: 8000, reference: null }],
      tipAmount: 0,
      tipIncluded: false,
    }));

    expect(document.receipt.payments).toEqual([
      { method: 'cash', amount: 8000, reference: null },
    ]);
  });

  it('preserva estado y montos reales de un credito cancelado', () => {
    const document = mapReceiptDataToDocument(receiptData({
      subtotal: 20000,
      discountValue: 0,
      discountAmount: 0,
      finalTotalPaid: 5000,
      tipAmount: 0,
      tipIncluded: false,
      payments: [{ method: 'cash', amount: 5000, reference: null }],
      hasCredit: true,
      creditStatus: 'cancelled',
      creditOriginalTotal: 20000,
      creditAmountPaid: 5000,
      creditAmount: 15000,
      creditCustomerName: 'Cliente cancelado',
    }));

    expect(document.receipt).toMatchObject({
      hasCredit: true,
      creditStatus: 'cancelled',
      creditOriginalTotal: 20000,
      creditAmountPaid: 5000,
      creditAmount: 15000,
    });
  });

  it('mantiene el vector SHA-256 compartido con el Print Agent', async () => {
    const document = mapReceiptDataToDocument(receiptData({
      companyId: 10,
      branchId: 20,
      companyName: 'Café Español',
      companyLegalName: 'Café Español S.A.S.',
      companyPhone: '+57 300 000 0000',
      companyTaxId: '900123456-7',
      companyAddress: 'Calle 10 # 20-30',
      orderId: 300,
      orderNumber: 'ORD-00300',
      tableName: 'Mesa principal de terraza con nombre largo',
      tableNumber: 7,
      createdAt: '2026-09-14T17:05:06.000Z',
      cashierName: 'María Muñoz',
      items: [
        { productName: 'Café con leche y porción de torta de chocolate', quantity: 2, unitPrice: 6250.5, subtotal: 12501 },
        { productName: 'Jugo de maracuyá', quantity: 1.5, unitPrice: 4000, subtotal: 6000 },
      ],
      subtotal: 18501,
      discountType: 'Porcentaje',
      discountValue: 10,
      discountAmount: 1850.1,
      finalTotalPaid: 14800.9,
      tipAmount: 1850,
      tipIncluded: true,
      splitCount: 2,
      payments: [
        { method: 'Efectivo', amount: 10000, reference: null },
        { method: 'Tarjeta', amount: 5000, reference: 'APROB-123' },
        { method: 'Transferencia', amount: 1650.9, reference: 'TRX-Ñ' },
      ],
      hasCredit: true,
      creditStatus: 'pending',
      creditOriginalTotal: 16650.9,
      creditAmountPaid: 14800.9,
      creditAmount: 1850,
      creditCustomerName: 'José Pérez',
    }));

    expect(await fingerprintReceiptDocument(document))
      .toBe('39d63b78c43df93f77e2a0500647e76fb829acfb28a504425a2a73a9ba928d02');
  });

  it('cambia el fingerprint cuando cambia el total persistido', async () => {
    const original = mapReceiptDataToDocument(receiptData());
    const changed = mapReceiptDataToDocument(receiptData({ finalTotalPaid: 8001 }));

    expect(await fingerprintReceiptDocument(original)).not.toBe(await fingerprintReceiptDocument(changed));
  });

  it('cambia el fingerprint cuando cambian los items persistidos', async () => {
    const original = mapReceiptDataToDocument(receiptData());
    const changed = mapReceiptDataToDocument(receiptData({
      items: [{ productName: 'Cafe pequeno', quantity: 3, unitPrice: 3500, subtotal: 10500 }],
    }));

    expect(await fingerprintReceiptDocument(original)).not.toBe(await fingerprintReceiptDocument(changed));
  });

  it('rechaza IDs bigint que JavaScript no puede representar sin perdida', () => {
    expect(() => mapReceiptDataToDocument(receiptData({ orderId: Number.MAX_SAFE_INTEGER + 1 })))
      .toThrow(/entero seguro/i);
  });
});
