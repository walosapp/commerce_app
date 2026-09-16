import { canonicalStringify } from './receiptDocument';

export const CASH_CLOSE_DOCUMENT_VERSION = 1;

const text = (value, field) => {
  const result = String(value ?? '').trim();
  if (!result) throw new Error(`${field} es obligatorio para imprimir el cierre.`);
  return result;
};

const number = (value, field) => {
  const result = Number(value);
  if (!Number.isFinite(result)) throw new Error(`${field} debe ser un número válido.`);
  return result;
};

const id = (value, field) => {
  const result = number(value, field);
  if (!Number.isSafeInteger(result) || result <= 0) throw new Error(`${field} debe ser un entero positivo.`);
  return result;
};

const count = (value, field) => {
  const result = number(value, field);
  if (!Number.isSafeInteger(result) || result < 0) throw new Error(`${field} debe ser un entero no negativo.`);
  return result;
};

export const mapCashCloseDataToDocument = (report) => {
  if (!report || typeof report !== 'object') throw new Error('El cierre persistido es obligatorio.');
  const cashRegisterId = id(report.cashRegisterId, 'cashRegisterId');
  const openedAt = new Date(report.openedAt);
  const closedAt = new Date(report.closedAt);
  if (Number.isNaN(openedAt.getTime()) || Number.isNaN(closedAt.getTime())) {
    throw new Error('Las fechas del cierre no son válidas.');
  }

  return {
    documentVersion: CASH_CLOSE_DOCUMENT_VERSION,
    companyId: id(report.companyId, 'companyId'),
    branchId: id(report.branchId, 'branchId'),
    cashRegisterId,
    cashClose: {
      companyName: text(report.companyName, 'companyName'),
      branchName: text(report.branchName, 'branchName'),
      currency: text(report.currency, 'currency'),
      timezone: text(report.timezone, 'timezone'),
      cashRegisterId,
      openedAt: openedAt.toISOString(),
      closedAt: closedAt.toISOString(),
      userName: text(report.closedByName || report.openedByName, 'userName'),
      openingAmount: number(report.openingAmount, 'openingAmount'),
      orderCount: count(report.orderCount, 'orderCount'),
      totalSales: number(report.totalSales, 'totalSales'),
      cashSales: number(report.totalCashSales, 'totalCashSales'),
      cardSales: number(report.totalCardSales, 'totalCardSales'),
      transferSales: number(report.totalTransferSales, 'totalTransferSales'),
      otherSales: number(report.totalOtherSales, 'totalOtherSales'),
      refundTotal: number(report.refundTotal, 'refundTotal'),
      cashIn: number(report.cashIn, 'cashIn'),
      cashOut: number(report.manualCashOut, 'manualCashOut'),
      expectedCash: number(report.expectedCash, 'expectedCash'),
      countedCash: number(report.closingAmount, 'closingAmount'),
      difference: number(report.difference, 'difference'),
      notes: report.notes == null ? null : String(report.notes).trim() || null,
    },
  };
};

export const createPrintCashCloseCommand = async (report, jobId) => {
  const document = mapCashCloseDataToDocument(report);
  const bytes = new TextEncoder().encode(canonicalStringify(document));
  const digest = await globalThis.crypto.subtle.digest('SHA-256', bytes);
  return {
    ...document,
    jobId: text(jobId, 'jobId'),
    fingerprint: Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join(''),
  };
};
