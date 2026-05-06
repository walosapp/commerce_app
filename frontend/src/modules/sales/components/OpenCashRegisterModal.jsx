/**
 * Modal Apertura de Caja
 * ¿Qué es? Modal para abrir un turno de caja
 * ¿Para qué? Registrar monto base de efectivo al iniciar turno
 */

import { useEffect, useState } from 'react';
import { DoorOpen } from 'lucide-react';

const OpenCashRegisterModal = ({ isOpen, onClose, onConfirm }) => {
  const [openingAmount, setOpeningAmount] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setOpeningAmount('');
      setNotes('');
    }
  }, [isOpen]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const amount = Number(openingAmount);
    if (isNaN(amount) || amount < 0) return;

    setSaving(true);
    try {
      await onConfirm({ openingAmount: amount, notes: notes || null });
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
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-100">
            <DoorOpen className="h-5 w-5 text-green-600" />
          </div>
          <div>
            <h3 className="text-lg font-bold text-gray-900">Abrir Caja</h3>
            <p className="text-sm text-gray-500">Registra el efectivo inicial del turno</p>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Monto base (efectivo)
            </label>
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 text-sm">$</span>
              <input
                type="number"
                min="0"
                step="100"
                value={openingAmount}
                onChange={(e) => setOpeningAmount(e.target.value)}
                placeholder="0"
                className="w-full rounded-lg border border-gray-300 pl-7 pr-4 py-2.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
                autoFocus
              />
            </div>
            <p className="mt-1 text-xs text-gray-400">Cuenta el efectivo antes de empezar el turno</p>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">
              Notas (opcional)
            </label>
            <input
              type="text"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Ej: Turno mañana"
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
              disabled={saving}
              className="flex-1 rounded-lg bg-green-600 py-2.5 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50 transition-colors"
            >
              {saving ? 'Abriendo...' : 'Abrir Caja'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default OpenCashRegisterModal;
