/**
 * Modal Movimiento de Caja
 * ¿Qué es? Modal para registrar entradas y salidas manuales de efectivo
 * ¿Para qué? Registrar retiros parciales, cambio, pagos a proveedores, etc.
 */

import { useEffect, useState } from 'react';
import { ArrowDownCircle, ArrowUpCircle } from 'lucide-react';

const REASONS = {
  in: ['Cambio', 'Préstamo', 'Otro ingreso'],
  out: ['Pago a proveedor', 'Retiro parcial', 'Gasto operativo', 'Otro egreso'],
};

const CashMovementModal = ({ isOpen, onClose, onConfirm, registerId, type = 'in' }) => {
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [customReason, setCustomReason] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  const isIn = type === 'in';
  const reasons = REASONS[type] || [];

  useEffect(() => {
    if (!isOpen) {
      setAmount('');
      setReason('');
      setCustomReason('');
      setNotes('');
    }
  }, [isOpen]);

  const finalReason = reason === '_custom' ? customReason : reason;

  const handleSubmit = async (e) => {
    e.preventDefault();
    const num = Number(amount);
    if (isNaN(num) || num <= 0 || !finalReason.trim()) return;

    setSaving(true);
    try {
      await onConfirm(registerId, {
        type,
        amount: num,
        reason: finalReason.trim(),
        notes: notes || null,
      });
      onClose();
    } catch {
      // error handled by parent
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="w-full max-w-md rounded-xl bg-white p-6 shadow-2xl m-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-3 mb-6">
          <div className={`flex h-10 w-10 items-center justify-center rounded-full ${
            isIn ? 'bg-green-100' : 'bg-red-100'
          }`}>
            {isIn
              ? <ArrowDownCircle className="h-5 w-5 text-green-600" />
              : <ArrowUpCircle className="h-5 w-5 text-red-600" />
            }
          </div>
          <div>
            <h3 className="text-lg font-bold text-gray-900">
              {isIn ? 'Entrada de Efectivo' : 'Salida de Efectivo'}
            </h3>
            <p className="text-sm text-gray-500">
              {isIn ? 'Registrar ingreso manual a caja' : 'Registrar retiro manual de caja'}
            </p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Monto</label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">$</span>
              <input
                type="number"
                min="1"
                step="100"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="0"
                className="w-full rounded-lg border border-gray-300 pl-7 pr-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                autoFocus
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Motivo</label>
            <div className="flex flex-wrap gap-2 mb-2">
              {reasons.map((r) => (
                <button
                  key={r}
                  type="button"
                  onClick={() => setReason(r)}
                  className={`rounded-full px-3 py-1 text-xs font-medium border transition-colors ${
                    reason === r
                      ? isIn ? 'bg-green-100 border-green-300 text-green-700' : 'bg-red-100 border-red-300 text-red-700'
                      : 'bg-gray-50 border-gray-200 text-gray-600 hover:bg-gray-100'
                  }`}
                >
                  {r}
                </button>
              ))}
              <button
                type="button"
                onClick={() => setReason('_custom')}
                className={`rounded-full px-3 py-1 text-xs font-medium border transition-colors ${
                  reason === '_custom'
                    ? 'bg-blue-100 border-blue-300 text-blue-700'
                    : 'bg-gray-50 border-gray-200 text-gray-600 hover:bg-gray-100'
                }`}
              >
                Otro...
              </button>
            </div>
            {reason === '_custom' && (
              <input
                type="text"
                value={customReason}
                onChange={(e) => setCustomReason(e.target.value)}
                placeholder="Describe el motivo"
                className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
              />
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Notas (opcional)
            </label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Detalle adicional"
              className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
            />
          </div>

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-lg border border-gray-300 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving || !amount || !finalReason.trim()}
              className={`flex-1 rounded-lg py-2.5 text-sm font-medium text-white disabled:opacity-50 transition-colors ${
                isIn ? 'bg-green-600 hover:bg-green-700' : 'bg-red-600 hover:bg-red-700'
              }`}
            >
              {saving ? 'Guardando...' : isIn ? 'Registrar Entrada' : 'Registrar Salida'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default CashMovementModal;
