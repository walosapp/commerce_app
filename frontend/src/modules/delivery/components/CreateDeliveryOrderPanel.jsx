import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Plus, Trash2, Loader2, Search } from 'lucide-react';
import { inventoryService } from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const SOURCES = [
  { value: 'manual',    label: 'Manual' },
  { value: 'whatsapp',  label: 'WhatsApp' },
  { value: 'web',       label: 'Web' },
  { value: 'rappi',     label: 'Rappi' },
  { value: 'didi_food', label: 'Didi Food' },
  { value: 'uber_eats', label: 'Uber Eats' },
];

const emptyForm = () => ({
  customerName: '',
  customerPhone: '',
  customerAddress: '',
  notes: '',
  deliveryFee: '',
  discountAmount: '',
  source: 'manual',
});

const CreateDeliveryOrderPanel = ({ isOpen, onClose, onCreated }) => {
  const { branchId, tenantId } = useAuthStore();
  const [form, setForm] = useState(emptyForm());
  const [items, setItems] = useState([]);
  const [productSearch, setProductSearch] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const { data: stockData } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: isOpen && !!branchId,
  });

  const products = (stockData?.data ?? []).filter(p =>
    !productSearch.trim() ||
    p.productName?.toLowerCase().includes(productSearch.toLowerCase())
  );

  const addItem = (product) => {
    setItems(prev => {
      const existing = prev.find(i => i.productId === product.productId);
      if (existing) {
        return prev.map(i =>
          i.productId === product.productId ? { ...i, quantity: i.quantity + 1 } : i
        );
      }
      return [...prev, {
        productId: product.productId,
        productName: product.productName,
        quantity: 1,
        unitPrice: product.salePrice ?? 0,
        notes: '',
      }];
    });
  };

  const updateItem = (productId, field, value) => {
    setItems(prev => prev.map(i => i.productId === productId ? { ...i, [field]: value } : i));
  };

  const removeItem = (productId) => {
    setItems(prev => prev.filter(i => i.productId !== productId));
  };

  const subtotal = items.reduce((s, i) => s + (Number(i.quantity) * Number(i.unitPrice)), 0);
  const total = subtotal + Number(form.deliveryFee || 0) - Number(form.discountAmount || 0);

  const handleClose = () => {
    setForm(emptyForm());
    setItems([]);
    setProductSearch('');
    setError('');
    onClose();
  };

  const handleSubmit = async () => {
    if (items.length === 0) { setError('Agrega al menos un producto'); return; }
    setSaving(true);
    setError('');
    try {
      await onCreated({
        ...form,
        deliveryFee: Number(form.deliveryFee || 0),
        discountAmount: Number(form.discountAmount || 0),
        items,
      });
      handleClose();
    } catch (e) {
      setError(e?.response?.data?.message || 'Error al crear el pedido');
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 bg-black/50 z-40 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-2xl max-h-[92vh] flex flex-col">
        <div className="flex items-center gap-3 px-6 py-4 border-b">
          <h2 className="text-lg font-semibold text-gray-900">Nuevo Pedido</h2>
          <button onClick={handleClose} className="ml-auto text-gray-400 hover:text-gray-600"><X size={20} /></button>
        </div>

        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-5">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Fuente</label>
              <select
                value={form.source}
                onChange={e => setForm(p => ({ ...p, source: e.target.value }))}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                {SOURCES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Nombre del cliente</label>
              <input
                type="text"
                value={form.customerName}
                onChange={e => setForm(p => ({ ...p, customerName: e.target.value }))}
                placeholder="Juan García"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Teléfono</label>
              <input
                type="text"
                value={form.customerPhone}
                onChange={e => setForm(p => ({ ...p, customerPhone: e.target.value }))}
                placeholder="+57 300 000 0000"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Dirección</label>
              <input
                type="text"
                value={form.customerAddress}
                onChange={e => setForm(p => ({ ...p, customerAddress: e.target.value }))}
                placeholder="Calle 123 #45-67"
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div className="sm:col-span-2">
              <label className="block text-sm font-medium text-gray-700 mb-1">Notas</label>
              <textarea
                rows={2}
                value={form.notes}
                onChange={e => setForm(p => ({ ...p, notes: e.target.value }))}
                placeholder="Sin cebolla, entregar en portería..."
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-700 mb-2">Buscar productos</p>
            <div className="relative mb-3">
              <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                placeholder="Buscar..."
                value={productSearch}
                onChange={e => setProductSearch(e.target.value)}
                className="w-full pl-8 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
              />
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-2 max-h-36 overflow-y-auto">
              {products.slice(0, 30).map(p => (
                <button
                  key={p.productId}
                  onClick={() => addItem(p)}
                  className="text-left border border-gray-200 rounded-lg px-3 py-2 text-xs hover:border-indigo-400 hover:bg-indigo-50 transition-colors"
                >
                  <p className="font-medium text-gray-800 truncate">{p.productName}</p>
                  <p className="text-gray-500">${Number(p.salePrice).toLocaleString('es-CO')}</p>
                </button>
              ))}
            </div>
          </div>

          {items.length > 0 && (
            <div>
              <p className="text-sm font-medium text-gray-700 mb-2">Items del pedido</p>
              <div className="space-y-2">
                {items.map(item => (
                  <div key={item.productId} className="flex items-center gap-2 bg-gray-50 rounded-lg px-3 py-2">
                    <p className="flex-1 text-sm font-medium text-gray-800 truncate">{item.productName}</p>
                    <input
                      type="number"
                      min="1"
                      value={item.quantity}
                      onChange={e => updateItem(item.productId, 'quantity', Number(e.target.value))}
                      className="w-14 border border-gray-300 rounded px-2 py-1 text-sm text-center focus:outline-none focus:ring-1 focus:ring-indigo-400"
                    />
                    <span className="text-sm text-gray-600 w-20 text-right">
                      ${(Number(item.quantity) * Number(item.unitPrice)).toLocaleString('es-CO')}
                    </span>
                    <button onClick={() => removeItem(item.productId)} className="text-red-400 hover:text-red-600">
                      <Trash2 size={14} />
                    </button>
                  </div>
                ))}
              </div>
              <div className="mt-3 pt-3 border-t space-y-1 text-sm">
                <div className="flex gap-3">
                  <div className="flex-1">
                    <label className="block text-xs text-gray-500 mb-1">Costo domicilio</label>
                    <input
                      type="number"
                      min="0"
                      value={form.deliveryFee}
                      onChange={e => setForm(p => ({ ...p, deliveryFee: e.target.value }))}
                      className="w-full border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo-400"
                    />
                  </div>
                  <div className="flex-1">
                    <label className="block text-xs text-gray-500 mb-1">Descuento</label>
                    <input
                      type="number"
                      min="0"
                      value={form.discountAmount}
                      onChange={e => setForm(p => ({ ...p, discountAmount: e.target.value }))}
                      className="w-full border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo-400"
                    />
                  </div>
                  <div className="flex-1 flex flex-col justify-end">
                    <p className="text-xs text-gray-500">Total</p>
                    <p className="text-lg font-bold text-gray-900">${total.toLocaleString('es-CO')}</p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>

        <div className="px-6 py-4 border-t flex justify-end gap-3">
          <button onClick={handleClose} className="text-sm text-gray-500 hover:text-gray-700 px-4 py-2">
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={saving}
            className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
          >
            {saving ? <Loader2 size={15} className="animate-spin" /> : <Plus size={15} />}
            Crear Pedido
          </button>
        </div>
      </div>
    </div>
  );
};

export default CreateDeliveryOrderPanel;
