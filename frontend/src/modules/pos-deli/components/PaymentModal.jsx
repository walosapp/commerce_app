import { useEffect, useMemo, useState } from 'react';
import { Banknote, CreditCard, Smartphone, Landmark, X } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const METHODS = [
  { key: 'cash', label: 'Efectivo', icon: Banknote },
  { key: 'card', label: 'Tarjeta', icon: CreditCard },
  { key: 'nequi', label: 'Nequi', icon: Smartphone },
  { key: 'transfer', label: 'Transferencia', icon: Landmark },
];

const PaymentModal = ({ isOpen, total, onClose, onConfirm, loading }) => {
  const [method, setMethod] = useState('cash');
  const [cashReceived, setCashReceived] = useState('');
  const [reference, setReference] = useState('');

  useEffect(() => {
    if (!isOpen) return;
    setMethod('cash');
    setCashReceived('');
    setReference('');
  }, [isOpen]);

  const change = useMemo(() => {
    if (method !== 'cash') return 0;
    const received = Number(cashReceived || 0);
    return Math.max(0, received - total);
  }, [cashReceived, method, total]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="w-full max-w-xl rounded-3xl bg-white shadow-2xl">
        <div className="flex items-center justify-between border-b border-gray-200 px-6 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Cobro</p>
            <h3 className="text-2xl font-black text-gray-900">{formatCurrency(total)}</h3>
          </div>
          <button type="button" onClick={onClose} className="rounded-xl p-2 text-gray-400 hover:bg-gray-100">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-5 px-6 py-6">
          <div className="grid grid-cols-2 gap-3">
            {METHODS.map(({ key, label, icon: Icon }) => (
              <button
                key={key}
                type="button"
                onClick={() => setMethod(key)}
                className={`rounded-2xl border p-4 text-left transition-colors ${
                  method === key ? 'border-primary-500 bg-primary-50 text-primary-700' : 'border-gray-200 bg-white text-gray-700'
                }`}
              >
                <Icon className="mb-2 h-5 w-5" />
                <p className="font-bold">{label}</p>
              </button>
            ))}
          </div>

          {method === 'cash' ? (
            <div>
              <label className="mb-2 block text-sm font-semibold text-gray-700">Monto recibido</label>
              <input
                type="number"
                min="0"
                step="100"
                value={cashReceived}
                onChange={(e) => setCashReceived(e.target.value)}
                className="w-full rounded-2xl border border-gray-200 px-4 py-4 text-xl font-black outline-none focus:border-primary-500"
                placeholder="0"
              />
              <p className="mt-3 text-lg font-black text-green-600">Cambio: {formatCurrency(change)}</p>
            </div>
          ) : (
            <div>
              <label className="mb-2 block text-sm font-semibold text-gray-700">Referencia (opcional)</label>
              <input
                type="text"
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                className="w-full rounded-2xl border border-gray-200 px-4 py-4 text-base outline-none focus:border-primary-500"
                placeholder="Número de aprobación / referencia"
              />
            </div>
          )}
        </div>

        <div className="flex justify-end gap-3 border-t border-gray-200 px-6 py-4">
          <button type="button" onClick={onClose} className="rounded-2xl border border-gray-200 px-4 py-3 font-semibold text-gray-600">
            Cancelar
          </button>
          <button
            type="button"
            disabled={loading || (method === 'cash' && Number(cashReceived || 0) < total)}
            onClick={() => onConfirm({ method, cashReceived: Number(cashReceived || 0), reference })}
            className="rounded-2xl bg-primary-600 px-4 py-3 font-semibold text-white disabled:opacity-50"
          >
            {loading ? 'Procesando...' : 'Confirmar venta'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default PaymentModal;
