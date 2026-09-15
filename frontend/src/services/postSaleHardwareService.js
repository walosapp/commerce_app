import { createPrintReceiptCommand } from './receiptDocument';

const JOB_PREFIX = 'post-sale.v1';

const positiveSafeInteger = (value, field) => {
  const number = Number(value);
  if (!Number.isSafeInteger(number) || number <= 0) {
    throw new Error(`${field} debe ser un entero positivo seguro.`);
  }
  return number;
};

export const hasCashPayment = (payments) => Array.isArray(payments) && payments.some(
  (payment) => payment?.method === 'cash' &&
    typeof payment.amount === 'number' &&
    Number.isFinite(payment.amount) &&
    payment.amount > 0
);

export const createPostSaleJobIds = ({ companyId, branchId, orderId }) => {
  const company = positiveSafeInteger(companyId, 'companyId');
  const branch = positiveSafeInteger(branchId, 'branchId');
  const order = positiveSafeInteger(orderId, 'orderId');
  const base = `${JOB_PREFIX}.c${company}.b${branch}.o${order}`;

  return {
    receiptJobId: `${base}.receipt`,
    drawerJobId: `${base}.drawer`,
  };
};

const resultStatus = (result) => {
  if (['completed', 'replayed', 'failed', 'uncertain'].includes(result?.status)) {
    return result.status;
  }
  return result?.executed === true ? 'completed' : 'uncertain';
};

const errorStatus = (error) => {
  const status = Number(error?.status);
  return Number.isInteger(status) && status >= 400 && status < 500 ? 'failed' : 'uncertain';
};

const errorCode = (error) => String(error?.code || 'hardware_error').slice(0, 100);

export const createPostSaleHardwareRunner = ({ getReceipt, printReceipt, openDrawer }) => {
  if (!getReceipt || !printReceipt || !openDrawer) {
    throw new Error('Las dependencias del orquestador postventa son obligatorias.');
  }

  return async ({ orderId, policy, resolvePolicy, onProgress = () => {} }) => {
    const persistedOrderId = positiveSafeInteger(orderId, 'orderId');
    let outcome = {
      orderId: persistedOrderId,
      companyId: null,
      branchId: null,
      receiptJobId: null,
      drawerJobId: null,
      status: 'running',
      printStatus: 'pending_policy',
      drawerStatus: 'pending_policy',
      printErrorCode: null,
      drawerErrorCode: null,
    };
    onProgress(outcome);

    let receipt;
    try {
      const response = await getReceipt(persistedOrderId);
      receipt = response?.data;
      if (!receipt || Number(receipt.orderId) !== persistedOrderId) {
        throw new Error('El backend no devolvio el recibo persistido solicitado.');
      }
    } catch (error) {
      outcome = {
        ...outcome,
        status: 'fetch_failed',
        printStatus: 'skipped_receipt_unavailable',
        drawerStatus: 'skipped_receipt_unavailable',
        fetchErrorCode: String(error?.code || 'receipt_unavailable').slice(0, 100),
      };
      onProgress(outcome);
      return outcome;
    }

    let companyId;
    let branchId;
    let receiptJobId;
    let drawerJobId;
    try {
      companyId = positiveSafeInteger(receipt.companyId, 'receipt.companyId');
      branchId = positiveSafeInteger(receipt.branchId, 'receipt.branchId');
      ({ receiptJobId, drawerJobId } = createPostSaleJobIds({ companyId, branchId, orderId: persistedOrderId }));
    } catch {
      outcome = {
        ...outcome,
        status: 'fetch_failed',
        printStatus: 'skipped_receipt_unavailable',
        drawerStatus: 'skipped_receipt_unavailable',
        fetchErrorCode: 'invalid_receipt_context',
      };
      onProgress(outcome);
      return outcome;
    }
    const effectivePolicy = typeof resolvePolicy === 'function'
      ? resolvePolicy(companyId, branchId)
      : policy;
    const autoPrintReceipt = Boolean(effectivePolicy?.autoPrintReceipt);
    const autoOpenCashDrawer = Boolean(effectivePolicy?.autoOpenCashDrawer);
    const shouldOpenDrawer = autoOpenCashDrawer && hasCashPayment(receipt.payments);

    outcome = {
      ...outcome,
      companyId,
      branchId,
      receiptJobId,
      drawerJobId,
      printStatus: autoPrintReceipt ? 'queued' : 'skipped_disabled',
      drawerStatus: autoOpenCashDrawer
        ? (shouldOpenDrawer ? 'queued' : 'skipped_no_cash')
        : 'skipped_disabled',
    };
    onProgress(outcome);

    if (!autoPrintReceipt && !autoOpenCashDrawer) {
      outcome = { ...outcome, status: 'completed' };
      onProgress(outcome);
      return outcome;
    }

    // El recibo persistido también gobierna la elegibilidad del efecto físico.
    // Esto evita que una cola demorada abra el cajón de una orden que ya fue
    // cancelada o devuelta antes de que el agente pudiera procesarla.
    if (receipt.status !== 'completed' || (typeof receipt.refundStatus === 'string' && receipt.refundStatus.trim())) {
      outcome = {
        ...outcome,
        status: 'receipt_ineligible',
        printStatus: autoPrintReceipt ? 'skipped_receipt_ineligible' : 'skipped_disabled',
        drawerStatus: autoOpenCashDrawer ? 'skipped_receipt_ineligible' : 'skipped_disabled',
      };
      onProgress(outcome);
      return outcome;
    }

    if (autoPrintReceipt) {
      let receiptCommand;
      try {
        receiptCommand = await createPrintReceiptCommand(receipt, receiptJobId);
        outcome = { ...outcome, printFingerprint: receiptCommand.fingerprint };
        onProgress(outcome);
        const result = await printReceipt(receipt, { jobId: receiptJobId });
        outcome = { ...outcome, printStatus: resultStatus(result) };
      } catch (error) {
        outcome = {
          ...outcome,
          printStatus: errorStatus(error),
          printErrorCode: errorCode(error),
          ...(receiptCommand ? { printFingerprint: receiptCommand.fingerprint } : {}),
        };
      }
      onProgress(outcome);
    }

    if (outcome.printStatus === 'replayed' && shouldOpenDrawer) {
      outcome = { ...outcome, drawerStatus: 'skipped_print_replay' };
      onProgress(outcome);
    }

    // El cajon tiene su propio limite de error: una falla de impresion no lo cancela.
    if (shouldOpenDrawer && outcome.printStatus !== 'replayed') {
      try {
        const result = await openDrawer({ jobId: drawerJobId, companyId, branchId });
        outcome = { ...outcome, drawerStatus: resultStatus(result) };
      } catch (error) {
        outcome = {
          ...outcome,
          drawerStatus: errorStatus(error),
          drawerErrorCode: errorCode(error),
        };
      }
      onProgress(outcome);
    }

    const actionStatuses = [outcome.printStatus, outcome.drawerStatus];
    const hasErrors = actionStatuses.some((status) => status === 'failed' || status === 'uncertain');
    outcome = { ...outcome, status: hasErrors ? 'completed_with_errors' : 'completed' };
    onProgress(outcome);
    return outcome;
  };
};
