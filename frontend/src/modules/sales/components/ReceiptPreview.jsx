import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Printer, Loader2 } from 'lucide-react';
import printService from '../../../services/printService';
import useAuthStore from '../../../stores/authStore';
import usePrintAgentStore from '../../../stores/printAgentStore';
import usePostSaleHardwareStore, {
  isBlockingPostSalePrint,
  isUnresolvedPostSalePrint,
} from '../../../stores/postSaleHardwareStore';
import { thermalReceiptStyles, openPrintWindow } from './printStyles';

const fmt = (v, currency) => {
  if (v == null || isNaN(v)) return '-';
  const amount = Number(v).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
  return `${String(currency || '').trim()} ${amount}`.trim();
};

const formatReceiptDate = (value, timezone, options) => new Intl.DateTimeFormat('es-CO', {
  ...options,
  timeZone: timezone,
}).format(new Date(value));

const escapeHtml = (value) => String(value ?? '')
  .replaceAll('&', '&amp;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;')
  .replaceAll('"', '&quot;')
  .replaceAll("'", '&#39;');

const PAYMENT_LABELS = {
  cash: 'Efectivo',
  card: 'Tarjeta',
  transfer: 'Transferencia',
  nequi: 'Nequi',
};

export default function ReceiptPreview({ orderId, onClose }) {
  const companyId = useAuthStore((state) => state.tenantId);
  const branchId = useAuthStore((state) => state.branchId);
  const {
    status: agentStatus,
    activeCommand,
    initializeContext,
    checkHealth,
    printReceipt,
    retryReceipt,
    acknowledgeReceiptAttempt,
    pendingReceiptCommand,
  } = usePrintAgentStore();
  const {
    getIntent: getPostSaleIntent,
    retryPostSalePrint,
    acknowledgePostSalePrint,
    persistenceFailed: postSalePersistenceFailed,
    storageWarning: postSaleStorageWarning,
  } = usePostSaleHardwareStore();
  const [directPrintResult, setDirectPrintResult] = useState(null);

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['receipt', orderId],
    queryFn: () => printService.getReceipt(orderId),
    enabled: !!orderId,
  });

  const receipt = data?.data;
  const postSaleIntent = getPostSaleIntent(companyId, branchId, orderId);
  const unresolvedPostSalePrint = isUnresolvedPostSalePrint(postSaleIntent);
  const blockingPostSalePrint = isBlockingPostSalePrint(postSaleIntent);
  const canRetryPostSalePrint = unresolvedPostSalePrint &&
    Boolean(postSaleIntent?.receiptJobId) &&
    Boolean(postSaleIntent?.printFingerprint);
  const directPrintEligible = receipt?.status === 'completed' && !receipt?.refundStatus;
  const pendingMatchesReceipt = pendingReceiptCommand?.orderId === receipt?.orderId;
  const creditTitle = receipt?.creditStatus === 'cancelled'
    ? 'Crédito cancelado'
    : receipt?.creditStatus === 'paid'
      ? 'Crédito saldado'
      : 'Crédito vigente';
  const creditBalanceLabel = receipt?.creditStatus === 'cancelled'
    ? 'Saldo no vigente'
    : 'Saldo crédito';

  useEffect(() => {
    initializeContext({ companyId, branchId });
    checkHealth();
  }, [companyId, branchId, initializeContext, checkHealth]);

  const buildReceiptHtml = () => {
    if (!receipt) return '';
    const saleTotal = Number(receipt.subtotal) - Number(receipt.discountAmount);
    const itemsRows = receipt.items
      .map(
        (i) =>
          `<tr><td>${escapeHtml(i.productName)}</td><td>${Number(i.quantity)}</td><td>${fmt(i.subtotal, receipt.currency)}</td></tr>`
      )
      .join('');

    const paymentsRows = receipt.payments
      .map(
        (p) =>
          `<div class="receipt-total-row"><span>${escapeHtml(PAYMENT_LABELS[p.method] || p.method)}</span><span>${fmt(p.amount, receipt.currency)}</span></div>`
      )
      .join('');

    const dateStr = formatReceiptDate(receipt.createdAt, receipt.timezone, {
      day: '2-digit', month: '2-digit', year: 'numeric',
    });
    const timeStr = formatReceiptDate(receipt.createdAt, receipt.timezone, {
      hour: '2-digit', minute: '2-digit',
    });

    return `
      <div class="receipt-header">
        <h2>${escapeHtml(receipt.companyName)}</h2>
        ${receipt.companyLegalName ? `<p>${escapeHtml(receipt.companyLegalName)}</p>` : ''}
        ${receipt.companyTaxId ? `<p>NIT: ${escapeHtml(receipt.companyTaxId)}</p>` : ''}
        ${receipt.companyAddress ? `<p>${escapeHtml(receipt.companyAddress)}</p>` : ''}
        ${receipt.companyPhone ? `<p>Tel: ${escapeHtml(receipt.companyPhone)}</p>` : ''}
      </div>
      <div class="receipt-divider"></div>
      <div class="receipt-meta">
        <div class="receipt-meta-row"><span>Mesa: ${escapeHtml(receipt.tableName)}</span><span>Cajero: ${escapeHtml(receipt.cashierName)}</span></div>
        <div class="receipt-meta-row"><span>${dateStr} ${timeStr}</span><span>${escapeHtml(receipt.orderNumber)}</span></div>
      </div>
      <div class="receipt-divider"></div>
      <table class="receipt-items">
        <thead><tr><th>Producto</th><th>Cant</th><th>Subt.</th></tr></thead>
        <tbody>${itemsRows}</tbody>
      </table>
      <div class="receipt-divider"></div>
      <div class="receipt-totals">
        <div class="receipt-total-row"><span>Subtotal</span><span>${fmt(receipt.subtotal, receipt.currency)}</span></div>
        ${receipt.discountAmount > 0 ? `<div class="receipt-total-row"><span>Descuento</span><span>-${fmt(receipt.discountAmount, receipt.currency)}</span></div>` : ''}
        <div class="receipt-total-row grand"><span>Total venta</span><span>${fmt(saleTotal, receipt.currency)}</span></div>
        <div class="receipt-total-row"><span>Pagado venta</span><span>${fmt(receipt.finalTotalPaid, receipt.currency)}</span></div>
        ${receipt.tipAmount > 0 ? `<div class="receipt-total-row"><span>${receipt.tipIncluded ? 'Propina incluida' : 'Propina no incluida'}</span><span>${fmt(receipt.tipAmount, receipt.currency)}</span></div>` : ''}
      </div>
      <div class="receipt-divider"></div>
      <div class="receipt-payments">
        ${paymentsRows}
      </div>
      ${receipt.hasCredit ? `<div class="receipt-divider"></div><div class="receipt-meta"><strong>${creditTitle}</strong><div class="receipt-total-row"><span>Crédito original</span><span>${fmt(Number(receipt.creditOriginalTotal) - Number(receipt.finalTotalPaid), receipt.currency)}</span></div><div class="receipt-total-row"><span>${creditBalanceLabel}</span><span>${fmt(receipt.creditAmount, receipt.currency)}</span></div>${receipt.creditStatus === 'cancelled' ? '<div>NO VIGENTE / NO EXIGIBLE</div>' : ''}<div>Cliente: ${escapeHtml(receipt.creditCustomerName || '-')}</div></div>` : ''}
      <div class="receipt-divider"></div>
      <div class="receipt-footer">
        ${receipt.splitCount > 1 ? `<p>Cuenta dividida: ${receipt.splitCount} personas</p>` : ''}
      </div>
    `;
  };

  const handleBrowserPrint = () => {
    if (!receipt || !directPrintEligible || pendingReceiptCommand || blockingPostSalePrint || postSalePersistenceFailed) return;
    openPrintWindow(buildReceiptHtml(), thermalReceiptStyles);
  };

  const showAgentResult = (result) => {
    const replayed = result?.status === 'replayed';
    const completed = result?.status === 'completed' && result?.executed !== false;
    setDirectPrintResult({
      type: completed ? 'success' : 'warning',
      message: replayed
        ? 'Este trabajo ya habia sido procesado; no se imprimio nuevamente.'
        : completed
          ? 'Recibo impreso correctamente con Walos.'
          : `El agente reporto el estado ${result?.status || 'desconocido'}; no crees otro trabajo ni uses fallback automatico.`,
    });
  };

  const showAgentError = (printError) => {
    const conflict = printError?.status === 409 || printError?.code === 'job_id_conflict';
    const receiptChanged = printError?.code === 'receipt_changed';
    const uncertain = !printError?.status;
    setDirectPrintResult({
      type: 'error',
      message: receiptChanged
        ? printError.message
        : conflict
        ? 'El jobId ya pertenece a otro documento. No se imprimio.'
        : uncertain
          ? 'Resultado incierto. No crees otro trabajo ni uses fallback: reintenta el mismo jobId.'
          : printError?.message || 'No fue posible imprimir con Walos Print Agent.',
    });
  };

  const validateAgentAvailability = async () => {
    setDirectPrintResult({ type: 'pending', message: 'Enviando recibo al agente...' });

    const health = await checkHealth();
    if (!health) {
      setDirectPrintResult({
        type: 'warning',
        message: 'Walos Print Agent no esta disponible. Podes usar la impresion del navegador.',
      });
      return false;
    }

    if (!health.paired) {
      setDirectPrintResult({
        type: 'warning',
        message: 'Walos Print Agent esta disponible, pero requiere vinculacion en Configuracion - Dispositivos.',
      });
      return false;
    }

    return true;
  };

  const handleDirectPrint = async () => {
    if (!receipt || !directPrintEligible || pendingReceiptCommand || blockingPostSalePrint || postSalePersistenceFailed) return;
    if (!await validateAgentAvailability()) return;

    try {
      const result = await printReceipt(receipt);
      showAgentResult(result);
    } catch (printError) {
      showAgentError(printError);
    }
  };

  const handleRetryReceipt = async () => {
    if (!pendingMatchesReceipt || !await validateAgentAvailability()) return;

    try {
      const refreshed = await refetch();
      const currentReceipt = refreshed.data?.data;
      if (!currentReceipt) throw new Error('No se pudo recargar el recibo persistido antes del reintento.');
      showAgentResult(await retryReceipt(currentReceipt));
    } catch (printError) {
      showAgentError(printError);
    }
  };

  const handleAcknowledgeReceiptAttempt = () => {
    if (!pendingReceiptCommand) return;
    const confirmed = globalThis.confirm(
      'Solo marcá este intento como revisado si verificaste físicamente la impresora. Esto no vuelve a imprimir.'
    );
    if (confirmed) acknowledgeReceiptAttempt();
  };

  const handleRetryPostSalePrint = async () => {
    if (postSalePersistenceFailed || !unresolvedPostSalePrint || !await validateAgentAvailability()) return;
    try {
      showAgentResult(await retryPostSalePrint({ companyId, branchId, orderId }));
    } catch (printError) {
      showAgentError(printError);
    }
  };

  const handleAcknowledgePostSalePrint = () => {
    if (!unresolvedPostSalePrint) return;
    const confirmed = globalThis.confirm(
      'Solo marcá este intento postventa como revisado si verificaste físicamente la impresora. Esto no vuelve a imprimir.'
    );
    if (confirmed) acknowledgePostSalePrint({ companyId, branchId, orderId });
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-lg font-semibold text-gray-900">Vista previa del recibo</h3>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded-lg">
            <X size={20} />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-4">
          {isLoading && (
            <div className="flex items-center justify-center h-40">
              <Loader2 className="animate-spin text-primary-600" size={32} />
            </div>
          )}

          {error && (
            <div className="text-red-600 text-center py-8">
              Error al cargar recibo
            </div>
          )}

          {receipt && (
            <div className="font-mono text-xs space-y-2 border border-gray-200 rounded-lg p-3 bg-gray-50">
              {/* Company */}
              <div className="text-center">
                <p className="font-bold text-sm">{receipt.companyName}</p>
                {receipt.companyLegalName && <p className="text-gray-500">{receipt.companyLegalName}</p>}
                {receipt.companyTaxId && <p className="text-gray-500">NIT: {receipt.companyTaxId}</p>}
                {receipt.companyAddress && <p className="text-gray-500">{receipt.companyAddress}</p>}
                {receipt.companyPhone && <p className="text-gray-500">Tel: {receipt.companyPhone}</p>}
              </div>
              <div className="border-t border-dashed border-gray-300" />

              {/* Meta */}
              <div className="flex justify-between">
                <span>Mesa: {receipt.tableName}</span>
                <span>{receipt.cashierName}</span>
              </div>
              <div className="flex justify-between">
                <span>{formatReceiptDate(receipt.createdAt, receipt.timezone, { dateStyle: 'short', timeStyle: 'short' })}</span>
                <span>{receipt.orderNumber}</span>
              </div>
              <div className="border-t border-dashed border-gray-300" />

              {/* Items */}
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-gray-300">
                    <th className="text-left py-1">Producto</th>
                    <th className="text-center w-8">Cant</th>
                    <th className="text-right">Subt.</th>
                  </tr>
                </thead>
                <tbody>
                  {receipt.items.map((item, i) => (
                    <tr key={i}>
                      <td className="py-0.5">{item.productName}</td>
                      <td className="text-center">{Number(item.quantity)}</td>
                      <td className="text-right">{fmt(item.subtotal, receipt.currency)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="border-t border-dashed border-gray-300" />

              {/* Totals */}
              <div className="space-y-0.5">
                <div className="flex justify-between"><span>Subtotal</span><span>{fmt(receipt.subtotal, receipt.currency)}</span></div>
                {receipt.discountAmount > 0 && (
                  <div className="flex justify-between text-red-600">
                    <span>Descuento</span><span>-{fmt(receipt.discountAmount, receipt.currency)}</span>
                  </div>
                )}
                <div className="flex justify-between font-bold text-sm border-t border-dashed border-gray-300 pt-1">
                  <span>Total venta</span><span>{fmt(Number(receipt.subtotal) - Number(receipt.discountAmount), receipt.currency)}</span>
                </div>
                <div className="flex justify-between">
                  <span>Pagado venta</span><span>{fmt(receipt.finalTotalPaid, receipt.currency)}</span>
                </div>
                {receipt.tipAmount > 0 && (
                  <div className="flex justify-between">
                    <span>{receipt.tipIncluded ? 'Propina incluida' : 'Propina no incluida'}</span>
                    <span>{fmt(receipt.tipAmount, receipt.currency)}</span>
                  </div>
                )}
              </div>
              <div className="border-t border-dashed border-gray-300" />

              {/* Payments */}
              <div className="space-y-0.5">
                {receipt.payments.map((p, i) => (
                  <div key={i} className="flex justify-between">
                    <span>{PAYMENT_LABELS[p.method] || p.method}</span>
                    <span>{fmt(p.amount, receipt.currency)}</span>
                  </div>
                ))}
              </div>

              {receipt.hasCredit && (
                <>
                  <div className="border-t border-dashed border-gray-300" />
                  <div className="space-y-0.5">
                    <div className="font-semibold">{creditTitle}</div>
                    <div className="flex justify-between">
                      <span>Crédito original</span>
                      <span>{fmt(Number(receipt.creditOriginalTotal) - Number(receipt.finalTotalPaid), receipt.currency)}</span>
                    </div>
                    <div className="flex justify-between">
                      <span>{creditBalanceLabel}</span><span>{fmt(receipt.creditAmount, receipt.currency)}</span>
                    </div>
                    {receipt.creditStatus === 'cancelled' && <div>NO VIGENTE / NO EXIGIBLE</div>}
                    <div>Cliente: {receipt.creditCustomerName || '-'}</div>
                  </div>
                </>
              )}

              <div className="border-t border-dashed border-gray-300" />
              {receipt.splitCount > 1 && (
                <p className="text-center text-gray-500">Cuenta dividida: {receipt.splitCount} personas</p>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t space-y-2">
          {directPrintResult && (
            <p
              role="status"
              className={`text-xs rounded-lg px-3 py-2 ${
                directPrintResult.type === 'success'
                  ? 'bg-green-50 text-green-700'
                  : directPrintResult.type === 'error'
                    ? 'bg-red-50 text-red-700'
                    : 'bg-amber-50 text-amber-700'
              }`}
            >
              {directPrintResult.message}
            </p>
          )}
          {receipt && !directPrintEligible && (
            <p role="status" className="text-xs rounded-lg px-3 py-2 bg-amber-50 text-amber-700">
              La impresion directa solo esta disponible para ordenes completadas sin devoluciones.
            </p>
          )}
          {pendingReceiptCommand && !pendingMatchesReceipt && (
            <div className="space-y-2 rounded-lg bg-amber-50 px-3 py-2">
              <p role="status" className="text-xs text-amber-800">
                Hay un trabajo incierto de la orden {pendingReceiptCommand.orderId}. Abrí ese recibo para reintentar el mismo jobId o revisá físicamente el intento antes de descartarlo.
              </p>
              <button
                onClick={handleAcknowledgeReceiptAttempt}
                disabled={activeCommand === 'print-receipt'}
                className="w-full px-4 py-2 border border-amber-400 text-amber-900 rounded-lg hover:bg-amber-100 disabled:opacity-50 text-sm font-medium"
              >
                Marcar intento como revisado
              </button>
            </div>
          )}
          {pendingMatchesReceipt && (
            <div className="space-y-2 rounded-lg bg-amber-50 px-3 py-2">
              <p role="status" className="text-xs text-amber-800">
                El resultado físico es incierto. No uses impresión del navegador ni crees otro trabajo hasta reconciliar este jobId.
              </p>
              <button
                onClick={handleRetryReceipt}
                disabled={activeCommand === 'print-receipt'}
                className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 text-sm font-medium"
              >
                {activeCommand === 'print-receipt' ? <Loader2 className="animate-spin" size={16} /> : <Printer size={16} />}
                Reintentar mismo trabajo
              </button>
              <button
                onClick={handleAcknowledgeReceiptAttempt}
                disabled={activeCommand === 'print-receipt'}
                className="w-full px-4 py-2 border border-amber-400 text-amber-900 rounded-lg hover:bg-amber-100 disabled:opacity-50 text-sm font-medium"
              >
                Marcar intento como revisado
              </button>
            </div>
          )}
          {unresolvedPostSalePrint && (
            <div className="space-y-2 rounded-lg bg-amber-50 px-3 py-2">
              <p role="status" className="text-xs text-amber-800">
                La impresión automática postventa quedó sin reconciliar. No crees un trabajo nuevo ni uses impresión del navegador.
              </p>
              {canRetryPostSalePrint && (
                <button
                  onClick={handleRetryPostSalePrint}
                  disabled={Boolean(activeCommand)}
                  className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 text-sm font-medium"
                >
                  {activeCommand ? <Loader2 className="animate-spin" size={16} /> : <Printer size={16} />}
                  Reintentar impresión postventa
                </button>
              )}
              <button
                onClick={handleAcknowledgePostSalePrint}
                disabled={Boolean(activeCommand)}
                className="w-full px-4 py-2 border border-amber-400 text-amber-900 rounded-lg hover:bg-amber-100 disabled:opacity-50 text-sm font-medium"
              >
                Marcar intento postventa como revisado
              </button>
            </div>
          )}
          {postSalePersistenceFailed && (
            <p role="alert" className="text-xs rounded-lg px-3 py-2 bg-red-50 text-red-800">
              {postSaleStorageWarning || 'El almacenamiento local no esta disponible. La impresion permanece bloqueada por seguridad.'}
            </p>
          )}
          {blockingPostSalePrint && !unresolvedPostSalePrint && (
            <p role="status" className="text-xs rounded-lg px-3 py-2 bg-amber-50 text-amber-700">
              La impresión automática postventa está en proceso. Esperá su resultado antes de reimprimir.
            </p>
          )}
          <button
            onClick={handleDirectPrint}
            disabled={!receipt || !directPrintEligible || Boolean(pendingReceiptCommand) || blockingPostSalePrint || postSalePersistenceFailed || Boolean(activeCommand) || directPrintResult?.type === 'pending'}
            className="w-full flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 text-sm font-medium"
          >
            {activeCommand === 'print-receipt' ? <Loader2 className="animate-spin" size={16} /> : <Printer size={16} />}
            Imprimir con Walos
          </button>
          <div className="flex gap-2">
            <button
              onClick={onClose}
              className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 text-sm font-medium"
            >
              Cerrar
            </button>
            <button
              onClick={handleBrowserPrint}
              disabled={!receipt || !directPrintEligible || Boolean(pendingReceiptCommand) || blockingPostSalePrint || postSalePersistenceFailed}
              className="flex-1 flex items-center justify-center gap-2 px-4 py-2 border border-primary-300 text-primary-700 rounded-lg hover:bg-primary-50 disabled:opacity-50 text-sm font-medium"
            >
              <Printer size={16} /> Imprimir con dialogo
            </button>
          </div>
          {agentStatus === 'disconnected' && (
            <p className="text-xs text-center text-gray-500">
              Configura o vincula el agente en Configuracion - Dispositivos.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
