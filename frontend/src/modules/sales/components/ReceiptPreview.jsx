import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Printer, Loader2 } from 'lucide-react';
import printService from '../../../services/printService';
import { thermalReceiptStyles, openPrintWindow } from './printStyles';

const fmt = (v) => {
  if (v == null || isNaN(v)) return '-';
  return `$${Number(v).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`;
};

const PAYMENT_LABELS = {
  cash: 'Efectivo',
  card: 'Tarjeta',
  transfer: 'Transferencia',
  nequi: 'Nequi',
};

export default function ReceiptPreview({ orderId, onClose }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['receipt', orderId],
    queryFn: () => printService.getReceipt(orderId),
    enabled: !!orderId,
  });

  const receipt = data?.data;

  const buildReceiptHtml = () => {
    if (!receipt) return '';
    const itemsRows = receipt.items
      .map(
        (i) =>
          `<tr><td>${i.productName}</td><td>${Number(i.quantity)}</td><td>${fmt(i.subtotal)}</td></tr>`
      )
      .join('');

    const paymentsRows = receipt.payments
      .map(
        (p) =>
          `<div class="receipt-total-row"><span>${PAYMENT_LABELS[p.method] || p.method}</span><span>${fmt(p.amount)}</span></div>`
      )
      .join('');

    const date = new Date(receipt.createdAt);
    const dateStr = date.toLocaleDateString('es-CO', { day: '2-digit', month: '2-digit', year: 'numeric' });
    const timeStr = date.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });

    return `
      <div class="receipt-header">
        <h2>${receipt.companyName}</h2>
        ${receipt.companyLegalName ? `<p>${receipt.companyLegalName}</p>` : ''}
        ${receipt.companyPhone ? `<p>Tel: ${receipt.companyPhone}</p>` : ''}
      </div>
      <div class="receipt-divider"></div>
      <div class="receipt-meta">
        <div class="receipt-meta-row"><span>Mesa: ${receipt.tableName}</span><span>Cajero: ${receipt.cashierName}</span></div>
        <div class="receipt-meta-row"><span>${dateStr} ${timeStr}</span><span>${receipt.orderNumber}</span></div>
      </div>
      <div class="receipt-divider"></div>
      <table class="receipt-items">
        <thead><tr><th>Producto</th><th>Cant</th><th>Subt.</th></tr></thead>
        <tbody>${itemsRows}</tbody>
      </table>
      <div class="receipt-divider"></div>
      <div class="receipt-totals">
        <div class="receipt-total-row"><span>Subtotal</span><span>${fmt(receipt.subtotal)}</span></div>
        ${receipt.discountAmount > 0 ? `<div class="receipt-total-row"><span>Descuento</span><span>-${fmt(receipt.discountAmount)}</span></div>` : ''}
        <div class="receipt-total-row grand"><span>Total</span><span>${fmt(receipt.finalTotalPaid)}</span></div>
        ${receipt.tipIncluded && receipt.tipAmount > 0 ? `<div class="receipt-total-row"><span>Propina</span><span>${fmt(receipt.tipAmount)}</span></div>` : ''}
        ${receipt.tipIncluded && receipt.tipAmount > 0 ? `<div class="receipt-total-row grand"><span>Total c/ propina</span><span>${fmt(receipt.finalTotalPaid + receipt.tipAmount)}</span></div>` : ''}
      </div>
      <div class="receipt-divider"></div>
      <div class="receipt-payments">
        ${paymentsRows}
      </div>
      ${receipt.hasCredit ? `<div class="receipt-divider"></div><div class="receipt-meta"><div class="receipt-total-row"><span>Crédito pendiente</span><span>${fmt(receipt.creditAmount)}</span></div><div>Cliente: ${receipt.creditCustomerName || '-'}</div></div>` : ''}
      <div class="receipt-divider"></div>
      <div class="receipt-footer">
        <p>¡Gracias por su visita!</p>
        ${receipt.splitCount > 1 ? `<p>Cuenta dividida: ${receipt.splitCount} personas</p>` : ''}
      </div>
    `;
  };

  const handlePrint = () => {
    openPrintWindow(buildReceiptHtml(), thermalReceiptStyles);
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
                {receipt.companyPhone && <p className="text-gray-500">Tel: {receipt.companyPhone}</p>}
              </div>
              <div className="border-t border-dashed border-gray-300" />

              {/* Meta */}
              <div className="flex justify-between">
                <span>Mesa: {receipt.tableName}</span>
                <span>{receipt.cashierName}</span>
              </div>
              <div className="flex justify-between">
                <span>{new Date(receipt.createdAt).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' })}</span>
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
                      <td className="text-right">{fmt(item.subtotal)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="border-t border-dashed border-gray-300" />

              {/* Totals */}
              <div className="space-y-0.5">
                <div className="flex justify-between"><span>Subtotal</span><span>{fmt(receipt.subtotal)}</span></div>
                {receipt.discountAmount > 0 && (
                  <div className="flex justify-between text-red-600">
                    <span>Descuento</span><span>-{fmt(receipt.discountAmount)}</span>
                  </div>
                )}
                <div className="flex justify-between font-bold text-sm border-t border-dashed border-gray-300 pt-1">
                  <span>Total</span><span>{fmt(receipt.finalTotalPaid)}</span>
                </div>
                {receipt.tipIncluded && receipt.tipAmount > 0 && (
                  <>
                    <div className="flex justify-between"><span>Propina</span><span>{fmt(receipt.tipAmount)}</span></div>
                    <div className="flex justify-between font-bold text-sm">
                      <span>Total c/ propina</span><span>{fmt(receipt.finalTotalPaid + receipt.tipAmount)}</span>
                    </div>
                  </>
                )}
              </div>
              <div className="border-t border-dashed border-gray-300" />

              {/* Payments */}
              <div className="space-y-0.5">
                {receipt.payments.map((p, i) => (
                  <div key={i} className="flex justify-between">
                    <span>{PAYMENT_LABELS[p.method] || p.method}</span>
                    <span>{fmt(p.amount)}</span>
                  </div>
                ))}
              </div>

              {receipt.hasCredit && (
                <>
                  <div className="border-t border-dashed border-gray-300" />
                  <div className="space-y-0.5">
                    <div className="flex justify-between">
                      <span>Crédito pendiente</span><span>{fmt(receipt.creditAmount)}</span>
                    </div>
                    <div>Cliente: {receipt.creditCustomerName || '-'}</div>
                  </div>
                </>
              )}

              <div className="border-t border-dashed border-gray-300" />
              <p className="text-center text-gray-500">¡Gracias por su visita!</p>
              {receipt.splitCount > 1 && (
                <p className="text-center text-gray-500">Cuenta dividida: {receipt.splitCount} personas</p>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 text-sm font-medium"
          >
            Cerrar
          </button>
          <button
            onClick={handlePrint}
            disabled={!receipt}
            className="flex-1 flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 text-sm font-medium"
          >
            <Printer size={16} /> Imprimir
          </button>
        </div>
      </div>
    </div>
  );
}
