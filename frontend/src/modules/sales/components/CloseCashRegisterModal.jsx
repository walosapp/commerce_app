/**
 * Modal Cierre de Caja (Arqueo)
 * ¿Qué es? Modal para cerrar turno con conteo de efectivo
 * ¿Para qué? Registrar arqueo, calcular diferencia y generar reporte Z
 */

import { useEffect, useState } from 'react';
import { DoorClosed, AlertTriangle, CheckCircle2 } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';
import { calculateExpectedCash } from '../../../utils/cashRegister';

const CloseCashRegisterModal = ({ isOpen, onClose, onConfirm, register }) => {
  const [closingAmount, setClosingAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setClosingAmount('');
      setNotes('');
    }
  }, [isOpen]);

  if (!isOpen || !register) return null;

  const expectedCash = calculateExpectedCash(register);

  const closingNum = Number(closingAmount) || 0;
  const difference = closingNum - expectedCash;
  const hasDifference = closingAmount !== '' && Math.abs(difference) > 1;

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (closingAmount === '') return;

    if (hasDifference && !window.confirm(
      `Hay una diferencia de ${formatCurrency(difference)}. ¿Deseas cerrar la caja de todas formas?`
    )) return;

    setSaving(true);
    try {
      await onConfirm(register.id, {
        closingAmount: closingNum,
        notes: notes || null,
      });
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
        className="w-full max-w-lg rounded-xl bg-white shadow-2xl m-4 max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="p-6">
          <div className="flex items-center gap-3 mb-6">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-red-100">
              <DoorClosed className="h-5 w-5 text-red-600" />
            </div>
            <div>
              <h3 className="text-lg font-bold text-gray-900">Cerrar Caja</h3>
              <p className="text-sm text-gray-500">Arqueo y cierre de turno</p>
            </div>
          </div>

          {/* Resumen del turno */}
          <div className="rounded-lg bg-gray-50 p-4 mb-5 space-y-2 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-500">Monto apertura</span>
              <span className="font-medium">{formatCurrency(register.openingAmount)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Ventas totales</span>
              <span className="font-medium">{formatCurrency(register.totalSales)}</span>
            </div>
            <div className="flex justify-between pl-4 text-xs text-gray-400">
              <span>Efectivo</span>
              <span>{formatCurrency(register.totalCashSales)}</span>
            </div>
            <div className="flex justify-between pl-4 text-xs text-gray-400">
              <span>Tarjeta</span>
              <span>{formatCurrency(register.totalCardSales)}</span>
            </div>
            <div className="flex justify-between pl-4 text-xs text-gray-400">
              <span>Transferencia</span>
              <span>{formatCurrency(register.totalTransferSales)}</span>
            </div>
            {register.totalOtherSales > 0 && (
              <div className="flex justify-between pl-4 text-xs text-gray-400">
                <span>Otros</span>
                <span>{formatCurrency(register.totalOtherSales)}</span>
              </div>
            )}
            <div className="flex justify-between">
              <span className="text-gray-500">Entradas manuales</span>
              <span className="font-medium text-green-600">+{formatCurrency(register.cashIn)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-500">Salidas manuales</span>
              <span className="font-medium text-red-600">-{formatCurrency(register.cashOut)}</span>
            </div>
            {register.totalDiscounts > 0 && (
              <div className="flex justify-between">
                <span className="text-gray-500">Descuentos</span>
                <span className="font-medium text-orange-600">-{formatCurrency(register.totalDiscounts)}</span>
              </div>
            )}
            {register.totalCredits > 0 && (
              <div className="flex justify-between">
                <span className="text-gray-500">Créditos otorgados (informativo)</span>
                <span className="font-medium text-gray-500">{formatCurrency(register.totalCredits)}</span>
              </div>
            )}
            {register.totalTips > 0 && (
              <div className="flex justify-between">
                <span className="text-gray-500">Propinas</span>
                <span className="font-medium">{formatCurrency(register.totalTips)}</span>
              </div>
            )}
            <hr className="my-1 border-gray-200" />
            <div className="flex justify-between font-semibold text-gray-800">
              <span>Efectivo esperado en caja</span>
              <span>{formatCurrency(expectedCash)}</span>
            </div>
            <div className="flex justify-between text-xs text-gray-400">
              <span>Órdenes facturadas</span>
              <span>{register.orderCount}</span>
            </div>
          </div>

          {/* Formulario de arqueo */}
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Conteo real de efectivo
              </label>
              <div className="relative">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">$</span>
                <input
                  type="number"
                  min="0"
                  step="100"
                  value={closingAmount}
                  onChange={(e) => setClosingAmount(e.target.value)}
                  placeholder="Cuenta el efectivo"
                  className="w-full rounded-lg border border-gray-300 pl-7 pr-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                  autoFocus
                />
              </div>
            </div>

            {/* Diferencia */}
            {closingAmount !== '' && (
              <div className={`rounded-lg p-3 flex items-center gap-2 ${
                hasDifference
                  ? Math.abs(difference) > 5000 ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700'
                  : 'bg-green-50 text-green-700'
              }`}>
                {hasDifference ? (
                  <AlertTriangle size={16} />
                ) : (
                  <CheckCircle2 size={16} />
                )}
                <span className="text-sm font-medium">
                  {hasDifference
                    ? `Diferencia: ${formatCurrency(difference)} (${difference > 0 ? 'sobrante' : 'faltante'})`
                    : 'Cuadra perfectamente'}
                </span>
              </div>
            )}

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                Notas de cierre (opcional)
              </label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                placeholder="Observaciones del turno..."
                rows={2}
                className="w-full rounded-lg border border-gray-300 px-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500 resize-none"
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
                disabled={saving || closingAmount === ''}
                className="flex-1 rounded-lg bg-red-600 py-2.5 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50 transition-colors"
              >
                {saving ? 'Cerrando...' : 'Cerrar Caja'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
};

export default CloseCashRegisterModal;
