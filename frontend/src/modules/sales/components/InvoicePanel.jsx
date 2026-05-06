/**
 * Panel de Facturacion
 * Que es? Modal con resumen de cuenta, descuentos y division
 * Para que? Facturar una mesa con reglas operativas configurables
 */

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Receipt, Users, Printer, Percent, DollarSign, ShieldAlert, CreditCard, Banknote, ArrowRightLeft, Plus, Trash2, Heart } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatCurrency } from '../../../utils/formatCurrency';
import companyService from '../../../services/companyService';
import useAuthStore from '../../../stores/authStore';

const PAYMENT_METHODS = [
  { key: 'cash',     label: 'Efectivo',      icon: Banknote,        active: 'bg-green-100 border-green-300 text-green-700' },
  { key: 'card',     label: 'Tarjeta',       icon: CreditCard,      active: 'bg-blue-100 border-blue-300 text-blue-700' },
  { key: 'transfer', label: 'Transferencia', icon: ArrowRightLeft,  active: 'bg-purple-100 border-purple-300 text-purple-700' },
  { key: 'nequi',    label: 'Nequi',         icon: ArrowRightLeft,  active: 'bg-pink-100 border-pink-300 text-pink-700' },
];

const PaymentMethodsSection = ({ payments, setPayments, amountToPay, paymentsDiff }) => {
  const updatePayment = (idx, field, value) => {
    setPayments(prev => prev.map((p, i) => i === idx ? { ...p, [field]: value } : p));
  };

  const addPayment = () => {
    setPayments(prev => [...prev, { method: 'cash', amount: '', reference: '' }]);
  };

  const removePayment = (idx) => {
    if (payments.length <= 1) return;
    setPayments(prev => prev.filter((_, i) => i !== idx));
  };

  const fillRemaining = (idx) => {
    const otherSum = payments.reduce((s, p, i) => i === idx ? s : s + (Number(p.amount) || 0), 0);
    const remaining = Math.max(0, Math.round((amountToPay - otherSum) * 100) / 100);
    updatePayment(idx, 'amount', remaining.toString());
  };

  return (
    <div className="rounded-lg border border-gray-200 p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Banknote className="h-4 w-4 text-primary-600" />
          <span className="text-sm font-medium text-gray-700">Metodo de pago</span>
        </div>
        <span className="text-xs text-gray-400">
          A cobrar: <span className="font-semibold text-gray-700">{formatCurrency(amountToPay)}</span>
        </span>
      </div>

      <div className="space-y-3">
        {payments.map((payment, idx) => {
          const showRef = payment.method && payment.method !== 'cash';
          return (
            <div key={idx} className="space-y-2">
              <div className="flex items-center gap-2">
                {/* Method pills */}
                <div className="flex flex-wrap gap-1 flex-1">
                  {PAYMENT_METHODS.map(({ key, label, icon: Icon, active }) => (
                    <button
                      key={key}
                      type="button"
                      onClick={() => updatePayment(idx, 'method', key)}
                      className={`flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium border transition-colors ${
                        payment.method === key
                          ? active
                          : 'bg-gray-50 border-gray-200 text-gray-500 hover:bg-gray-100'
                      }`}
                    >
                      <Icon size={12} /> {label}
                    </button>
                  ))}
                </div>
                {payments.length > 1 && (
                  <button
                    type="button"
                    onClick={() => removePayment(idx)}
                    className="rounded p-1 text-gray-400 hover:text-red-500 hover:bg-red-50 transition-colors"
                  >
                    <Trash2 size={14} />
                  </button>
                )}
              </div>
              <div className="flex items-center gap-2">
                <div className="relative flex-1">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">$</span>
                  <input
                    type="number"
                    min="0"
                    step="100"
                    value={payment.amount}
                    onChange={(e) => updatePayment(idx, 'amount', e.target.value)}
                    onFocus={() => {
                      if (!payment.amount && payments.length === 1) fillRemaining(idx);
                    }}
                    placeholder="0"
                    className="w-full rounded-lg border border-gray-300 pl-7 pr-3 py-2 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                  />
                </div>
                <button
                  type="button"
                  onClick={() => fillRemaining(idx)}
                  title="Completar restante"
                  className="rounded-lg border border-gray-300 px-2 py-2 text-xs font-medium text-gray-500 hover:bg-gray-50 transition-colors whitespace-nowrap"
                >
                  Resto
                </button>
              </div>
              {showRef && (
                <input
                  type="text"
                  value={payment.reference}
                  onChange={(e) => updatePayment(idx, 'reference', e.target.value)}
                  placeholder="Referencia / aprobacion"
                  className="w-full rounded-lg border border-gray-300 px-3 py-1.5 text-xs focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                />
              )}
              {idx < payments.length - 1 && <hr className="border-gray-100" />}
            </div>
          );
        })}
      </div>

      <button
        type="button"
        onClick={addPayment}
        className="mt-3 flex items-center gap-1.5 text-xs font-medium text-primary-600 hover:text-primary-700 transition-colors"
      >
        <Plus size={14} /> Agregar otro metodo
      </button>

      {/* Validation indicator */}
      {amountToPay > 0 && payments.some(p => Number(p.amount) > 0) && (
        <div className={`mt-3 rounded-lg px-3 py-2 text-xs font-medium ${
          Math.abs(paymentsDiff) <= 1
            ? 'bg-green-50 text-green-700'
            : 'bg-red-50 text-red-700'
        }`}>
          {Math.abs(paymentsDiff) <= 1
            ? 'Los pagos cuadran correctamente'
            : paymentsDiff > 0
              ? `Sobran ${formatCurrency(paymentsDiff)}`
              : `Faltan ${formatCurrency(Math.abs(paymentsDiff))}`
          }
        </div>
      )}
    </div>
  );
};

const InvoicePanel = ({ isOpen, onClose, onConfirm, table }) => {
  const { tenantId } = useAuthStore();
  const [splitCount, setSplitCount]           = useState(1);
  const [invoicing, setInvoicing]             = useState(false);
  const [discountType, setDiscountType]       = useState('none');
  const [discountValue, setDiscountValue]     = useState('');
  const [overrideConfirmed, setOverrideConfirmed] = useState(false);

  // Pagos
  const [payments, setPayments]               = useState([{ method: 'cash', amount: '', reference: '' }]);

  // Propina
  const [tipIncluded, setTipIncluded]         = useState(false);
  const [tipAmount, setTipAmount]             = useState('');

  // Credito
  const [hasCredit, setHasCredit]             = useState(false);
  const [creditAmountPaid, setCreditAmountPaid] = useState('');
  const [creditCustomerName, setCreditCustomerName] = useState('');
  const [creditNotes, setCreditNotes]         = useState('');

  const { data: operationsData } = useQuery({
    queryKey: ['company-operations-settings', tenantId],
    queryFn: () => companyService.getOperationsSettings(),
    enabled: isOpen && !!tenantId,
  });

  useEffect(() => {
    if (!isOpen) {
      setSplitCount(1);
      setDiscountType('none');
      setDiscountValue('');
      setOverrideConfirmed(false);
      setPayments([{ method: 'cash', amount: '', reference: '' }]);
      setTipIncluded(false);
      setTipAmount('');
      setHasCredit(false);
      setCreditAmountPaid('');
      setCreditCustomerName('');
      setCreditNotes('');
    } else if (table?.name && table.name !== '') {
      setCreditCustomerName(table.name);
    }
  }, [isOpen, table?.id]);

  if (!isOpen || !table) return null;

  const settings        = operationsData?.data;
  const items           = table.items || [];
  const subtotal        = table.total || items.reduce((s, i) => s + i.quantity * i.unitPrice, 0);
  const rawDiscountValue = Number(discountValue || 0);
  const discountAmount  = discountType === 'percentage'
    ? Math.max(0, subtotal * (rawDiscountValue / 100))
    : discountType === 'fixed'
      ? Math.max(0, rawDiscountValue)
      : 0;
  const discountPercent = subtotal > 0 ? (discountAmount / subtotal) * 100 : 0;
  const finalTotal      = Math.max(0, subtotal - discountAmount);
  const perPerson       = splitCount > 1 ? finalTotal / splitCount : null;

  const tipNum          = Number(tipAmount || 0);
  const suggestedTip     = Math.round(finalTotal * 0.10);

  const creditPaid      = Number(creditAmountPaid || 0);
  const creditRemaining = hasCredit && creditPaid < finalTotal
    ? Math.round((finalTotal - creditPaid) * 100) / 100
    : 0;

  // Monto que se debe pagar con metodos de pago (incluye propina)
  const totalWithTip = finalTotal + (tipIncluded ? tipNum : 0);
  const amountToPay = hasCredit && creditPaid >= 0 && creditPaid < finalTotal
    ? Math.round(creditPaid * 100) / 100
    : totalWithTip;
  const paymentsSum = payments.reduce((s, p) => s + (Number(p.amount) || 0), 0);
  const paymentsDiff = Math.round((paymentsSum - amountToPay) * 100) / 100;

  let validationMessage = '';
  if (discountType !== 'none') {
    if (!settings?.manualDiscountEnabled) {
      validationMessage = 'Los descuentos manuales estan deshabilitados en configuracion.';
    } else if (discountType === 'percentage' && rawDiscountValue > Number(settings?.maxDiscountPercent || 0)) {
      validationMessage = `El descuento no puede superar ${settings?.maxDiscountPercent}%`;
    } else if (discountType === 'fixed' && rawDiscountValue > Number(settings?.maxDiscountAmount || 0)) {
      validationMessage = `El descuento no puede superar ${formatCurrency(settings?.maxDiscountAmount || 0)}`;
    } else if (discountAmount > subtotal) {
      validationMessage = 'El descuento no puede ser mayor al subtotal.';
    }
  }
  if (hasCredit) {
    if (creditPaid < 0) validationMessage = 'El monto no puede ser negativo.';
    else if (creditPaid >= finalTotal) validationMessage = 'El monto a pagar debe ser menor al total. Si paga todo, desmarca el credito.';
  }
  if (!validationMessage && amountToPay > 0 && Math.abs(paymentsDiff) > 1) {
    validationMessage = `La suma de pagos (${formatCurrency(paymentsSum)}) no coincide con el total a cobrar (${formatCurrency(amountToPay)})`;
  }
  if (!validationMessage && payments.some(p => !p.method)) {
    validationMessage = 'Selecciona un metodo de pago para cada linea.';
  }

  const requiresOverride =
    settings?.discountRequiresOverride &&
    discountType !== 'none' &&
    discountPercent >= Number(settings?.discountOverrideThresholdPercent || 0);

  const canSubmit = !validationMessage && (!requiresOverride || overrideConfirmed);

  const handleInvoice = async () => {
    if (!canSubmit) {
      toast.error(validationMessage || 'Confirma el descuento antes de facturar');
      return;
    }

    setInvoicing(true);
    try {
      const paymentLines = payments
        .filter(p => Number(p.amount) > 0)
        .map(p => ({ method: p.method, amount: Number(p.amount), reference: p.reference || null }));

      await onConfirm(table.id, {
        discountType,
        discountValue: discountType === 'none' ? 0 : rawDiscountValue,
        discountAmount,
        finalTotalPaid: finalTotal,
        splitCount,
        overrideConfirmed,
        hasCredit,
        creditAmountPaid: hasCredit ? creditPaid : 0,
        creditCustomerName: hasCredit ? creditCustomerName : null,
        creditNotes: hasCredit ? creditNotes : null,
        payments: paymentLines,
        tipAmount: tipIncluded ? tipNum : 0,
        tipIncluded,
      });
      onClose();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Error facturando');
    } finally {
      setInvoicing(false);
    }
  };

  const handlePrint = () => {
    const printWindow = window.open('', '_blank', 'width=400,height=600');
    const itemsHtml = items.map((i) =>
      `<tr>
        <td style="padding:4px 0">${i.productName}</td>
        <td style="text-align:center">${i.quantity}</td>
        <td style="text-align:right">${formatCurrency(i.unitPrice)}</td>
        <td style="text-align:right;font-weight:600">${formatCurrency(i.quantity * i.unitPrice)}</td>
      </tr>`
    ).join('');

    printWindow.document.write(`
      <html><head><title>Factura Mesa ${table.tableNumber}</title>
      <style>body{font-family:monospace;padding:20px;max-width:380px;margin:0 auto}
      table{width:100%;border-collapse:collapse}th,td{padding:6px 4px;font-size:13px}
      th{border-bottom:1px dashed #333;text-align:left}
      .total{font-size:18px;font-weight:bold;border-top:2px solid #333;padding-top:8px;margin-top:8px}</style></head>
      <body>
        <h2 style="text-align:center;margin-bottom:4px">WALOS</h2>
        <p style="text-align:center;font-size:12px;color:#666">Mesa ${table.tableNumber}${table.name && table.name !== `Mesa ${table.tableNumber}` ? ' - ' + table.name : ''}</p>
        <hr/>
        <table><thead><tr><th>Producto</th><th style="text-align:center">Cant</th><th style="text-align:right">Precio</th><th style="text-align:right">Subtotal</th></tr></thead>
        <tbody>${itemsHtml}</tbody></table>
        <p style="text-align:right">Subtotal: ${formatCurrency(subtotal)}</p>
        ${discountAmount > 0 ? `<p style="text-align:right;color:#dc2626">Descuento: -${formatCurrency(discountAmount)}</p>` : ''}
        <p class="total" style="text-align:right">Total: ${formatCurrency(finalTotal)}</p>
        ${tipIncluded && tipNum > 0 ? `<p style="text-align:right;font-size:13px;color:#ec4899">Propina: +${formatCurrency(tipNum)}</p><p style="text-align:right;font-weight:600">Total a cobrar: ${formatCurrency(totalWithTip)}</p>` : ''}
        ${hasCredit && creditRemaining > 0 ? `<p style="text-align:right;color:#dc2626;font-size:13px">Pago ahora: ${formatCurrency(creditPaid)}</p><p style="text-align:right;color:#dc2626;font-size:13px">Credito pendiente: ${formatCurrency(creditRemaining)}</p>` : ''}
        ${splitCount > 1 ? `<p style="text-align:right;font-size:13px">Por persona (${splitCount}): ${formatCurrency(perPerson)}</p>` : ''}
        <p style="text-align:center;font-size:11px;color:#999;margin-top:16px">Gracias por su visita</p>
      </body></html>
    `);
    printWindow.document.close();
    printWindow.print();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/50 sm:items-center">
      <div className="w-full max-w-md rounded-t-2xl bg-white shadow-xl sm:rounded-2xl flex flex-col max-h-[90vh]">

        <div className="flex items-center justify-between border-b px-6 py-4 flex-shrink-0">
          <div>
            <h2 className="text-lg font-bold text-gray-900">Facturar Mesa</h2>
            <p className="text-sm text-gray-500">
              {table.name && table.name !== `Mesa ${table.tableNumber}` ? table.name : `Mesa ${table.tableNumber}`}
            </p>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-gray-400 hover:bg-gray-100 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">

          {/* Items */}
          <div className="rounded-lg border border-gray-200 divide-y divide-gray-100">
            {items.map((item, idx) => (
              <div key={item.id || idx} className="flex items-center justify-between px-3 py-2.5">
                <div>
                  <p className="font-medium text-gray-900">{item.productName}</p>
                  <p className="text-xs text-gray-500">{item.quantity} x {formatCurrency(item.unitPrice)}</p>
                </div>
                <span className="font-semibold text-gray-900">{formatCurrency(item.quantity * item.unitPrice)}</span>
              </div>
            ))}
          </div>

          {/* Descuento */}
          <div className="rounded-lg border border-gray-200 p-4">
            <div className="mb-3 flex items-center gap-2">
              <Percent className="h-4 w-4 text-primary-600" />
              <span className="text-sm font-medium text-gray-700">Descuento</span>
            </div>
            <div className="grid grid-cols-3 gap-2">
              {['none','fixed','percentage'].map(t => (
                <button key={t} onClick={() => setDiscountType(t)}
                  className={`rounded-lg border px-3 py-2 text-sm font-medium transition-colors ${discountType === t ? 'border-primary-300 bg-primary-50 text-primary-700' : 'border-gray-200 text-gray-600 hover:bg-gray-50'}`}>
                  {t === 'none' ? 'Ninguno' : t === 'fixed' ? 'Fijo' : '%'}
                </button>
              ))}
            </div>
            {discountType !== 'none' && (
              <div className="mt-3 space-y-3">
                <div className="relative">
                  {discountType === 'fixed'
                    ? <DollarSign className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
                    : <Percent className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />}
                  <input type="number" min="0" step="0.01" value={discountValue}
                    onChange={(e) => setDiscountValue(e.target.value)}
                    className="input pl-10"
                    placeholder={discountType === 'fixed' ? 'Ej: 5000' : 'Ej: 7'} />
                </div>
                {requiresOverride && (
                  <label className="flex items-start gap-3 rounded-lg border border-yellow-200 bg-yellow-50 px-3 py-3 cursor-pointer">
                    <input type="checkbox" checked={overrideConfirmed} onChange={(e) => setOverrideConfirmed(e.target.checked)}
                      className="mt-1 h-4 w-4 rounded border-gray-300 text-primary-600" />
                    <div>
                      <p className="flex items-center gap-2 text-sm font-medium text-yellow-800">
                        <ShieldAlert className="h-4 w-4" /> Confirmacion adicional requerida
                      </p>
                      <p className="mt-1 text-xs text-yellow-700">
                        Este descuento supera el umbral de {settings?.discountOverrideThresholdPercent}%
                      </p>
                    </div>
                  </label>
                )}
                {validationMessage && !hasCredit && (
                  <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{validationMessage}</div>
                )}
              </div>
            )}
          </div>

          {/* Totales */}
          <div className="rounded-lg bg-gray-50 p-4 space-y-2">
            <div className="flex items-center justify-between text-sm">
              <span className="text-gray-500">Subtotal</span>
              <span className="font-medium text-gray-900">{formatCurrency(subtotal)}</span>
            </div>
            {discountAmount > 0 && (
              <div className="flex items-center justify-between text-sm">
                <span className="text-gray-500">Descuento</span>
                <span className="font-medium text-red-600">-{formatCurrency(discountAmount)}</span>
              </div>
            )}
            <div className="flex items-center justify-between border-t pt-2">
              <span className="text-sm text-gray-500">Total final</span>
              <span className="text-2xl font-bold text-gray-900">{formatCurrency(finalTotal)}</span>
            </div>
            {tipIncluded && tipNum > 0 && (
              <div className="flex items-center justify-between text-sm">
                <span className="text-pink-500">Propina</span>
                <span className="font-medium text-pink-600">+{formatCurrency(tipNum)}</span>
              </div>
            )}
            {tipIncluded && tipNum > 0 && (
              <div className="flex items-center justify-between text-sm font-semibold">
                <span className="text-gray-500">Total a cobrar</span>
                <span className="text-gray-900">{formatCurrency(totalWithTip)}</span>
              </div>
            )}
          </div>

          {/* Credito */}
          <div className={`rounded-lg border p-4 transition-colors ${hasCredit ? 'border-orange-300 bg-orange-50' : 'border-gray-200'}`}>
            <label className="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" checked={hasCredit} onChange={(e) => setHasCredit(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-orange-500 focus:ring-orange-400" />
              <div className="flex items-center gap-2">
                <CreditCard className="h-4 w-4 text-orange-600" />
                <span className="text-sm font-medium text-gray-700">Pago con credito (pago parcial)</span>
              </div>
            </label>
            {hasCredit && (
              <div className="mt-3 space-y-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Nombre del cliente</label>
                  <input type="text" value={creditCustomerName} onChange={(e) => setCreditCustomerName(e.target.value)}
                    placeholder="Nombre del cliente" maxLength={200}
                    className="input w-full" />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Monto que paga ahora</label>
                  <div className="relative">
                    <DollarSign className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
                    <input type="number" min="0" step="100" value={creditAmountPaid}
                      onChange={(e) => setCreditAmountPaid(e.target.value)}
                      placeholder="0"
                      className="input pl-10 w-full" />
                  </div>
                </div>
                {creditPaid >= 0 && creditPaid < finalTotal && (
                  <div className="rounded-lg bg-orange-100 border border-orange-200 px-3 py-2.5 flex items-center justify-between">
                    <span className="text-sm text-orange-700 font-medium">Queda como credito:</span>
                    <span className="text-lg font-bold text-orange-700">{formatCurrency(creditRemaining)}</span>
                  </div>
                )}
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Notas (opcional)</label>
                  <input type="text" value={creditNotes} onChange={(e) => setCreditNotes(e.target.value)}
                    placeholder="Observaciones del credito" maxLength={300}
                    className="input w-full" />
                </div>
                {validationMessage && (
                  <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{validationMessage}</div>
                )}
              </div>
            )}
          </div>

          {/* Propina */}
          <div className={`rounded-lg border p-4 transition-colors ${tipIncluded ? 'border-pink-300 bg-pink-50' : 'border-gray-200'}`}>
            <label className="flex items-center justify-between cursor-pointer">
              <div className="flex items-center gap-3">
                <input type="checkbox" checked={tipIncluded} onChange={(e) => {
                  setTipIncluded(e.target.checked);
                  if (e.target.checked && !tipAmount) setTipAmount(suggestedTip.toString());
                }}
                  className="h-4 w-4 rounded border-gray-300 text-pink-500 focus:ring-pink-400" />
                <div className="flex items-center gap-2">
                  <Heart className="h-4 w-4 text-pink-500" />
                  <span className="text-sm font-medium text-gray-700">Propina voluntaria</span>
                </div>
              </div>
              {!tipIncluded && (
                <span className="text-xs text-gray-400">Sugerido: {formatCurrency(suggestedTip)} (10%)</span>
              )}
            </label>
            {tipIncluded && (
              <div className="mt-3 space-y-2">
                <div className="flex items-center gap-2">
                  <button type="button" onClick={() => setTipAmount(suggestedTip.toString())}
                    className={`rounded-full px-3 py-1 text-xs font-medium border transition-colors ${
                      tipNum === suggestedTip ? 'bg-pink-100 border-pink-300 text-pink-700' : 'bg-gray-50 border-gray-200 text-gray-500 hover:bg-gray-100'
                    }`}>
                    10% ({formatCurrency(suggestedTip)})
                  </button>
                  <button type="button" onClick={() => setTipAmount(Math.round(finalTotal * 0.15).toString())}
                    className={`rounded-full px-3 py-1 text-xs font-medium border transition-colors ${
                      tipNum === Math.round(finalTotal * 0.15) ? 'bg-pink-100 border-pink-300 text-pink-700' : 'bg-gray-50 border-gray-200 text-gray-500 hover:bg-gray-100'
                    }`}>
                    15% ({formatCurrency(Math.round(finalTotal * 0.15))})
                  </button>
                  <button type="button" onClick={() => setTipAmount('')}
                    className="rounded-full px-3 py-1 text-xs font-medium border bg-gray-50 border-gray-200 text-gray-500 hover:bg-gray-100 transition-colors">
                    Otro
                  </button>
                </div>
                <div className="relative">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">$</span>
                  <input type="number" min="0" step="100" value={tipAmount}
                    onChange={(e) => setTipAmount(e.target.value)}
                    placeholder="Monto propina"
                    className="w-full rounded-lg border border-gray-300 pl-7 pr-3 py-2 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500" />
                </div>
              </div>
            )}
          </div>

          {/* Metodos de pago */}
          <PaymentMethodsSection
            payments={payments}
            setPayments={setPayments}
            amountToPay={amountToPay}
            paymentsDiff={paymentsDiff}
          />

          {/* Dividir cuenta */}
          <div className="rounded-lg border border-gray-200 p-4">
            <div className="mb-3 flex items-center gap-2">
              <Users className="h-4 w-4 text-gray-500" />
              <span className="text-sm font-medium text-gray-700">Dividir cuenta</span>
            </div>
            <div className="flex items-center gap-3">
              <input type="number" min="1" max="20" value={splitCount}
                onChange={(e) => setSplitCount(Math.max(1, parseInt(e.target.value, 10) || 1))}
                className="input w-20 text-center" />
              <span className="text-sm text-gray-500">personas</span>
            </div>
            {splitCount > 1 && (
              <p className="mt-2 text-sm font-semibold text-primary-600">
                {formatCurrency(perPerson)} por persona
              </p>
            )}
          </div>

        </div>

        <div className="border-t px-6 py-4 space-y-2 flex-shrink-0">
          <button onClick={handleInvoice} disabled={invoicing || !canSubmit}
            className="w-full flex items-center justify-center gap-2 rounded-lg bg-green-600 px-4 py-3 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50 transition-colors">
            <Receipt className="h-4 w-4" />
            {invoicing ? 'Facturando...' : 'Confirmar Facturacion'}
          </button>
          <button onClick={handlePrint}
            className="w-full flex items-center justify-center gap-2 rounded-lg border border-gray-300 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
            <Printer className="h-4 w-4" />
            Imprimir Factura
          </button>
        </div>

      </div>
    </div>
  );
};

export default InvoicePanel;
