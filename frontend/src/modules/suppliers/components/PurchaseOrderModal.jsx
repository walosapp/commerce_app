import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Plus, Trash2, Loader2, Search, ShoppingCart } from 'lucide-react';
import toast from 'react-hot-toast';
import purchaseOrderService from '../../../services/purchaseOrderService';
import inventoryService from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const PurchaseOrderModal = ({ isOpen, onClose, onSaved, suppliers }) => {
  const { branchId, tenantId } = useAuthStore();
  const [form, setForm] = useState({ supplierId: '', notes: '', expectedDate: '' });
  const [items, setItems] = useState([]);
  const [search, setSearch] = useState('');
  const [showDrop, setShowDrop] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: stockData } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId,
  });

  const products = (stockData?.data ?? []).filter(p =>
    !search.trim() || p.productName?.toLowerCase().includes(search.toLowerCase())
  );

  const addProduct = (p) => {
    if (items.find(i => i.productId === p.productId)) {
      toast('Ya está en el pedido', { icon: '⚠️' });
      return;
    }
    setItems(prev => [...prev, {
      productId: p.productId,
      productName: p.productName,
      quantity: 1,
      unitCost: p.costPrice ?? 0,
    }]);
    setSearch('');
    setShowDrop(false);
  };

  const removeItem = (productId) => setItems(prev => prev.filter(i => i.productId !== productId));

  const updateItem = (productId, field, val) => {
    setItems(prev => prev.map(i => i.productId === productId ? { ...i, [field]: val } : i));
  };

  const total = items.reduce((s, i) => s + (Number(i.quantity) || 0) * (Number(i.unitCost) || 0), 0);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.supplierId) { toast.error('Selecciona un proveedor'); return; }
    if (items.length === 0) { toast.error('Agrega al menos un producto'); return; }
    setSaving(true);
    try {
      await purchaseOrderService.create({
        supplierId: Number(form.supplierId),
        branchId: Number(branchId),
        notes: form.notes || undefined,
        expectedDate: form.expectedDate || undefined,
        items: items.map(i => ({
          productId: i.productId,
          productName: i.productName,
          quantity: Number(i.quantity),
          unitCost: Number(i.unitCost),
        })),
      });
      toast.success('Pedido creado');
      onSaved();
      onClose();
      setForm({ supplierId: '', notes: '', expectedDate: '' });
      setItems([]);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al crear pedido');
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <div className="flex items-center gap-2">
            <ShoppingCart size={18} className="text-primary-600" />
            <h2 className="text-base font-semibold text-gray-900">Nuevo Pedido a Proveedor</h2>
          </div>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100">
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto p-6 space-y-5">
          {/* Proveedor + fecha */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Proveedor *</label>
              <select
                className="input"
                value={form.supplierId}
                onChange={e => setForm(f => ({ ...f, supplierId: e.target.value }))}
              >
                <option value="">Seleccionar...</option>
                {(suppliers ?? []).map(s => (
                  <option key={s.id} value={s.id}>{s.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Fecha esperada</label>
              <input
                type="date"
                className="input"
                value={form.expectedDate}
                onChange={e => setForm(f => ({ ...f, expectedDate: e.target.value }))}
              />
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notas</label>
            <textarea
              className="input min-h-[60px] resize-y text-sm"
              placeholder="Instrucciones, condiciones, etc."
              value={form.notes}
              onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
            />
          </div>

          {/* Buscar producto */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Agregar producto</label>
            <div className="relative">
              <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
              <input
                className="input pl-8"
                placeholder="Buscar en inventario..."
                value={search}
                onChange={e => { setSearch(e.target.value); setShowDrop(true); }}
                onFocus={() => setShowDrop(true)}
              />
              {showDrop && search && products.length > 0 && (
                <div className="absolute z-10 w-full mt-1 bg-white border border-gray-200 rounded-xl shadow-lg max-h-44 overflow-y-auto">
                  {products.slice(0, 15).map(p => (
                    <button
                      key={p.productId}
                      type="button"
                      onMouseDown={() => addProduct(p)}
                      className="w-full text-left px-4 py-2.5 text-sm hover:bg-primary-50 border-b border-gray-100 last:border-b-0 flex justify-between"
                    >
                      <span className="font-medium">{p.productName}</span>
                      <span className="text-gray-400 text-xs">stock: {p.quantity}</span>
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Items */}
          {items.length > 0 && (
            <div className="space-y-2">
              <div className="grid grid-cols-12 gap-2 text-xs font-semibold text-gray-500 uppercase px-1">
                <span className="col-span-5">Producto</span>
                <span className="col-span-2 text-center">Cantidad</span>
                <span className="col-span-3 text-center">Costo unit.</span>
                <span className="col-span-2 text-right">Subtotal</span>
              </div>
              {items.map(item => (
                <div key={item.productId} className="grid grid-cols-12 gap-2 items-center bg-gray-50 rounded-xl px-3 py-2 border border-gray-200">
                  <div className="col-span-5 text-sm font-medium text-gray-800 truncate">{item.productName}</div>
                  <div className="col-span-2">
                    <input
                      type="number" min="0.001" step="0.001"
                      className="w-full text-center border border-gray-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
                      value={item.quantity}
                      onChange={e => updateItem(item.productId, 'quantity', e.target.value)}
                    />
                  </div>
                  <div className="col-span-3">
                    <input
                      type="number" min="0" step="0.01"
                      className="w-full text-center border border-gray-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
                      value={item.unitCost}
                      onChange={e => updateItem(item.productId, 'unitCost', e.target.value)}
                    />
                  </div>
                  <div className="col-span-1 text-right text-sm font-semibold text-gray-700">
                    ${((Number(item.quantity)||0) * (Number(item.unitCost)||0)).toLocaleString('es-CO', {minimumFractionDigits:0})}
                  </div>
                  <button type="button" onClick={() => removeItem(item.productId)} className="col-span-1 text-red-400 hover:text-red-600 flex justify-end">
                    <Trash2 size={14} />
                  </button>
                </div>
              ))}
              <div className="flex justify-end pt-1">
                <span className="text-sm font-bold text-gray-900">
                  Total: ${total.toLocaleString('es-CO', {minimumFractionDigits:0})}
                </span>
              </div>
            </div>
          )}
        </form>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t bg-gray-50 rounded-b-2xl">
          <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg transition-colors">
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 rounded-lg transition-colors"
          >
            {saving ? <Loader2 size={14} className="animate-spin" /> : <Plus size={14} />}
            {saving ? 'Creando...' : 'Crear Pedido'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default PurchaseOrderModal;
