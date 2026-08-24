/**
 * Modal de Devolucion / Anulacion
 * Que es? Modal para crear devoluciones totales o parciales de ordenes
 * Para que? Revertir ventas con motivo obligatorio, opcion parcial por item
 */

import { useEffect, useRef, useState } from 'react';
import { X, RotateCcw, AlertTriangle } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const RefundModal = ({ isOpen, onClose, onConfirm, order }) => {
  const [refundType, setRefundType] = useState('full');
  const [reason, setReason] = useState('');
  const [selectedItems, setSelectedItems] = useState({});
  const [saving, setSaving] = useState(false);
  const idempotencyKeyRef = useRef(null);

  useEffect(() => {
    idempotencyKeyRef.current = null;
  }, [order?.id]);

  if (!isOpen || !order) return null;

  const items = order.items || [];

  const toggleItem = (itemId) => {
    setSelectedItems(prev => {
      const copy = { ...prev };
      if (copy[itemId]) {
        delete copy[itemId];
      } else {
        const item = items.find(i => i.id === itemId);
        if (item) copy[itemId] = item.quantity;
      }
      return copy;
    });
  };

  const updateItemQty = (itemId, qty) => {
    const item = items.find(i => i.id === itemId);
    if (!item) return;
    const clamped = Math.max(0.01, Math.min(qty, item.quantity));
    setSelectedItems(prev => ({ ...prev, [itemId]: clamped }));
  };

  const reasonValid = reason.trim().length >= 10;
  const partialValid = refundType === 'partial' ? Object.keys(selectedItems).length > 0 : true;
  const canSubmit = reasonValid && partialValid;

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setSaving(true);
    try {
      const payload = {
        orderId: order.id,
        refundType,
        reason: reason.trim(),
        idempotencyKey: idempotencyKeyRef.current ??= crypto.randomUUID(),
      };

      if (refundType === 'partial') {
        payload.items = Object.entries(selectedItems).map(([itemId, qty]) => ({
          orderItemId: Number(itemId),
          quantity: qty,
        }));
      }

      await onConfirm(payload);
      onClose();
    } catch {
      // error handled by parent
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="w-full max-w-lg rounded-xl bg-white shadow-2xl m-4 flex flex-col max-h-[90vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b px-6 py-4 flex-shrink-0">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-red-100">
              <RotateCcw className="h-5 w-5 text-red-600" />
            </div>
            <div>
              <h3 className="text-lg font-bold text-gray-900">Devolucion</h3>
              <p className="text-sm text-gray-500">Orden {order.orderNumber || `#${order.id}`}</p>
            </div>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-gray-400 hover:bg-gray-100 transition-colors">
            <X size={20} />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
          {/* Tipo */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Tipo de devolucion</label>
            <div className="grid grid-cols-2 gap-2">
              <button
                type="button"
                onClick={() => { setRefundType('full'); setSelectedItems({}); }}
                className={`rounded-lg border px-4 py-3 text-sm font-medium transition-colors ${
                  refundType === 'full'
                    ? 'border-red-300 bg-red-50 text-red-700'
                    : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                }`}
              >
                Total
                <p className="text-xs font-normal mt-0.5 opacity-70">Anula toda la orden</p>
              </button>
              <button
                type="button"
                onClick={() => setRefundType('partial')}
                className={`rounded-lg border px-4 py-3 text-sm font-medium transition-colors ${
                  refundType === 'partial'
                    ? 'border-orange-300 bg-orange-50 text-orange-700'
                    : 'border-gray-200 text-gray-600 hover:bg-gray-50'
                }`}
              >
                Parcial
                <p className="text-xs font-normal mt-0.5 opacity-70">Selecciona items</p>
              </button>
            </div>
          </div>

          {/* Items (solo en parcial) */}
          {refundType === 'partial' && items.length > 0 && (
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Items a devolver</label>
              <div className="rounded-lg border border-gray-200 divide-y divide-gray-100">
                {items.map((item) => {
                  const isSelected = item.id in selectedItems;
                  return (
                    <div key={item.id} className="flex items-center gap-3 px-3 py-2.5">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => toggleItem(item.id)}
                        className="h-4 w-4 rounded border-gray-300 text-red-500 focus:ring-red-400"
                      />
                      <div className="flex-1 min-w-0">
                        <p className={`text-sm font-medium ${isSelected ? 'text-gray-900' : 'text-gray-400'}`}>
                          {item.productName}
                        </p>
                        <p className="text-xs text-gray-400">
                          {item.quantity} x {formatCurrency(item.unitPrice)}
                        </p>
                      </div>
                      {isSelected && (
                        <div className="flex items-center gap-2">
                          <label className="text-xs text-gray-500">Cant:</label>
                          <input
                            type="number"
                            min="0.01"
                            max={item.quantity}
                            step="1"
                            value={selectedItems[item.id] || ''}
                            onChange={(e) => updateItemQty(item.id, Number(e.target.value))}
                            className="w-16 rounded border border-gray-300 px-2 py-1 text-xs text-center focus:border-red-400 focus:ring-1 focus:ring-red-400"
                          />
                        </div>
                      )}
                      <span className="text-sm font-medium text-gray-600 w-20 text-right">
                        {formatCurrency(item.quantity * item.unitPrice)}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* Motivo */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Motivo <span className="text-red-500">*</span>
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Describe el motivo de la devolucion (minimo 10 caracteres)"
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-red-400 focus:ring-1 focus:ring-red-400 resize-none"
            />
            {reason.length > 0 && !reasonValid && (
              <p className="text-xs text-red-500 mt-1">Minimo 10 caracteres ({reason.trim().length}/10)</p>
            )}
          </div>

          {/* Resumen */}
          <div className="rounded-lg bg-red-50 border border-red-200 p-4">
            <div className="flex items-center gap-2 mb-2">
              <AlertTriangle className="h-4 w-4 text-red-500" />
              <span className="text-sm font-medium text-red-700">Resumen de devolucion</span>
            </div>
            <p className="text-sm text-red-700">
              El valor neto sera calculado por el backend con los datos de la venta.
            </p>
            {Number(order.discountAmount) > 0 && (
              <p className="text-xs text-red-600 mt-2">
                Descuento registrado: {formatCurrency(order.discountAmount)}. Se distribuira entre los items de forma deterministica.
              </p>
            )}
            {order.hasCredit && (
              <p className="text-xs text-red-600 mt-2">
                El valor se aplicara primero al credito pendiente. Solo el excedente se devolvera al cliente.
              </p>
            )}
            <p className="text-xs text-red-600 mt-2">
              La reduccion de deuda y el dinero realmente devuelto se confirmaran al procesar la operacion.
            </p>
            {refundType === 'partial' && Object.keys(selectedItems).length > 0 && (
              <p className="text-xs text-red-500 mt-1">
                {Object.keys(selectedItems).length} item{Object.keys(selectedItems).length > 1 ? 's' : ''} seleccionado{Object.keys(selectedItems).length > 1 ? 's' : ''}
              </p>
            )}
            <p className="text-xs text-red-500 mt-2">Esta accion revertira el stock y no se puede deshacer.</p>
          </div>
        </div>

        {/* Footer */}
        <div className="border-t px-6 py-4 flex gap-3 flex-shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-lg border border-gray-300 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={handleSubmit}
            disabled={saving || !canSubmit}
            className="flex-1 rounded-lg bg-red-600 py-2.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
          >
            {saving ? 'Procesando...' : 'Confirmar Devolucion'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default RefundModal;
