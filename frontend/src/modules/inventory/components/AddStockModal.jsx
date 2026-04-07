/**
 * Modal de Agregar Stock Rápido
 * ¿Qué es? Mini-modal para agregar cantidad a un producto existente
 * ¿Para qué? Incrementar stock con recálculo de costo promedio ponderado
 */

import { useEffect, useState } from 'react';
import { Plus } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const AddStockModal = ({ isOpen, onClose, onConfirm, product }) => {
  const [quantity, setQuantity] = useState('');
  const [unitCost, setUnitCost] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!isOpen) {
      setQuantity('');
      setUnitCost('');
    }
  }, [isOpen, product?.productId]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      await onConfirm({
        quantity: Number(quantity),
        unitCost: unitCost ? Number(unitCost) : null,
      });
      onClose();
    } catch (err) {
      console.error('Error agregando stock:', err);
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen || !product) return null;

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="w-full max-w-sm rounded-xl bg-white p-6 shadow-2xl m-4"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center gap-3 mb-4">
          <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-100">
            <Plus className="h-5 w-5 text-green-600" />
          </div>
          <div>
            <h3 className="text-lg font-bold text-gray-900">Agregar Stock</h3>
            <p className="text-sm text-gray-500">{product.productName}</p>
          </div>
        </div>

        <div className="mb-4 rounded-lg bg-gray-50 p-3 text-sm">
          <div className="flex justify-between">
            <span className="text-gray-500">Stock actual:</span>
            <span className="font-semibold">{product.quantity}</span>
          </div>
          <div className="flex justify-between mt-1">
            <span className="text-gray-500">Costo actual:</span>
            <span className="font-semibold">{formatCurrency(product.costPrice)}</span>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Cantidad a agregar *</label>
            <input
              type="number"
              required
              min="1"
              step="0.01"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              className="input"
              placeholder="Ej: 10"
              autoFocus
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Costo unitario (opcional)</label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={unitCost}
              onChange={(e) => setUnitCost(e.target.value)}
              className="input"
              placeholder="Si cambia, recalcula promedio"
            />
            <p className="text-xs text-gray-400 mt-1">
              Si se ingresa, se recalculará el costo promedio ponderado.
            </p>
          </div>

          <div className="flex items-center justify-end gap-3 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving || !quantity}
              className="rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 disabled:opacity-50 transition-colors"
            >
              {saving ? 'Agregando...' : 'Agregar Stock'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default AddStockModal;
