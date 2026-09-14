export const RECEIPT_DOCUMENT_VERSION = 1;

const requiredText = (value, field) => {
  const text = String(value ?? '').trim();
  if (!text) throw new Error(`${field} es obligatorio para imprimir el recibo.`);
  return text;
};

const optionalText = (value) => {
  if (value == null) return null;
  const text = String(value).trim();
  return text || null;
};

const finiteNumber = (value, field) => {
  const number = Number(value);
  if (!Number.isFinite(number)) throw new Error(`${field} debe ser un numero valido.`);
  return number;
};

const integer = (value, field, { allowZero = false } = {}) => {
  const number = finiteNumber(value, field);
  if (!Number.isSafeInteger(number) || (allowZero ? number < 0 : number <= 0)) {
    throw new Error(`${field} debe ser un entero seguro ${allowZero ? 'no negativo' : 'positivo'}.`);
  }
  return number;
};

/**
 * Convierte exclusivamente el ReceiptData persistido del backend al contrato H2.
 * No agrega datos del carrito, HTML ni comandos ESC/POS.
 */
export const mapReceiptDataToDocument = (receiptData) => {
  if (!receiptData || typeof receiptData !== 'object') {
    throw new Error('El recibo persistido es obligatorio.');
  }

  const orderId = integer(receiptData.orderId, 'orderId');
  const createdAt = new Date(receiptData.createdAt);
  if (Number.isNaN(createdAt.getTime())) throw new Error('createdAt debe ser una fecha valida.');

  return {
    documentVersion: RECEIPT_DOCUMENT_VERSION,
    companyId: integer(receiptData.companyId, 'companyId'),
    branchId: integer(receiptData.branchId, 'branchId'),
    orderId,
    receipt: {
      companyName: requiredText(receiptData.companyName, 'companyName'),
      companyLegalName: optionalText(receiptData.companyLegalName),
      companyPhone: optionalText(receiptData.companyPhone),
      companyTaxId: optionalText(receiptData.companyTaxId),
      companyAddress: optionalText(receiptData.companyAddress),
      currency: requiredText(receiptData.currency, 'currency'),
      timezone: requiredText(receiptData.timezone, 'timezone'),
      orderId,
      orderNumber: requiredText(receiptData.orderNumber, 'orderNumber'),
      status: requiredText(receiptData.status, 'status'),
      refundStatus: optionalText(receiptData.refundStatus),
      tableName: requiredText(receiptData.tableName, 'tableName'),
      tableNumber: integer(receiptData.tableNumber, 'tableNumber', { allowZero: true }),
      createdAt: createdAt.toISOString(),
      cashierName: requiredText(receiptData.cashierName, 'cashierName'),
      items: (receiptData.items || []).map((item) => ({
        productName: requiredText(item.productName, 'items.productName'),
        quantity: finiteNumber(item.quantity, 'items.quantity'),
        unitPrice: finiteNumber(item.unitPrice, 'items.unitPrice'),
        subtotal: finiteNumber(item.subtotal, 'items.subtotal'),
      })),
      subtotal: finiteNumber(receiptData.subtotal, 'subtotal'),
      discountType: optionalText(receiptData.discountType),
      discountValue: finiteNumber(receiptData.discountValue, 'discountValue'),
      discountAmount: finiteNumber(receiptData.discountAmount, 'discountAmount'),
      finalTotalPaid: finiteNumber(receiptData.finalTotalPaid, 'finalTotalPaid'),
      tipAmount: finiteNumber(receiptData.tipAmount, 'tipAmount'),
      tipIncluded: Boolean(receiptData.tipIncluded),
      splitCount: integer(receiptData.splitCount, 'splitCount'),
      payments: (receiptData.payments || []).map((payment) => ({
        method: requiredText(payment.method, 'payments.method'),
        amount: finiteNumber(payment.amount, 'payments.amount'),
        reference: optionalText(payment.reference),
      })),
      hasCredit: Boolean(receiptData.hasCredit),
      creditStatus: optionalText(receiptData.creditStatus),
      creditOriginalTotal: receiptData.creditOriginalTotal == null
        ? null
        : finiteNumber(receiptData.creditOriginalTotal, 'creditOriginalTotal'),
      creditAmountPaid: receiptData.creditAmountPaid == null
        ? null
        : finiteNumber(receiptData.creditAmountPaid, 'creditAmountPaid'),
      creditAmount: receiptData.creditAmount == null
        ? null
        : finiteNumber(receiptData.creditAmount, 'creditAmount'),
      creditCustomerName: optionalText(receiptData.creditCustomerName),
    },
  };
};

export const canonicalStringify = (value) => {
  if (Array.isArray(value)) {
    return `[${value.map(canonicalStringify).join(',')}]`;
  }

  if (value && typeof value === 'object') {
    return `{${Object.keys(value)
      .sort()
      .map((key) => `${JSON.stringify(key)}:${canonicalStringify(value[key])}`)
      .join(',')}}`;
  }

  if (typeof value === 'string') {
    return JSON.stringify(value.normalize('NFC'));
  }

  return JSON.stringify(value);
};

export const fingerprintReceiptDocument = async (document) => {
  if (!globalThis.crypto?.subtle) {
    throw new Error('Este navegador no soporta SHA-256 para impresion segura.');
  }

  const bytes = new TextEncoder().encode(canonicalStringify(document));
  const digest = await globalThis.crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, '0')).join('');
};

export const createPrintReceiptCommand = async (receiptData, jobId) => {
  const document = mapReceiptDataToDocument(receiptData);
  return {
    ...document,
    jobId: requiredText(jobId, 'jobId'),
    fingerprint: await fingerprintReceiptDocument(document),
  };
};
