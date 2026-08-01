import { useEffect, useState } from 'react';

const WeightInputModal = ({ isOpen, product, onClose, onConfirm }) => {
  const [weight, setWeight] = useState('');

  useEffect(() => {
    if (isOpen) setWeight('');
  }, [isOpen]);

  if (!isOpen || !product) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-3xl bg-white p-6 shadow-2xl">
        <h3 className="text-xl font-black text-gray-900">Ingresar peso manual</h3>
        <p className="mt-1 text-sm text-gray-500">{product.name}</p>

        <div className="mt-5">
          <label className="mb-2 block text-sm font-semibold text-gray-700">Peso en kg</label>
          <input
            type="number"
            min="0"
            step="0.001"
            value={weight}
            onChange={(e) => setWeight(e.target.value)}
            className="w-full rounded-2xl border border-gray-200 px-4 py-3 text-lg font-bold outline-none focus:border-primary-500"
            placeholder="0.000"
          />
        </div>

        <div className="mt-6 flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-2xl border border-gray-200 px-4 py-3 text-sm font-semibold text-gray-600"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => onConfirm(Number(weight))}
            className="rounded-2xl bg-primary-600 px-4 py-3 text-sm font-semibold text-white"
          >
            Agregar
          </button>
        </div>
      </div>
    </div>
  );
};

export default WeightInputModal;
