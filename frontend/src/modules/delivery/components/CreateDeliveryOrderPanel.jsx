import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Trash2, Loader2, Search, Bike, PackagePlus, Plus, Minus } from 'lucide-react';
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

const SOURCE_CLS = {
  manual: 'bg-gray-100 text-gray-600',
  whatsapp: 'bg-green-100 text-green-700',
  web: 'bg-blue-100 text-blue-700',
  rappi: 'bg-orange-100 text-orange-700',
  didi_food: 'bg-red-100 text-red-700',
  uber_eats: 'bg-emerald-100 text-emerald-700',
};

const TYPE_LABEL = { simple: 'Simple', prepared: 'Preparado', service: 'Servicio', supply: 'Insumo' };
const TYPE_CLS   = { simple: 'bg-gray-100 text-gray-600', prepared: 'bg-orange-100 text-orange-700', service: 'bg-purple-100 text-purple-700', supply: 'bg-blue-100 text-blue-700' };

const emptyForm = () => ({
  customerName: '', customerPhone: '', customerAddress: '',
  notes: '', deliveryFee: '', discountAmount: '', source: 'manual',
});

const CreateDeliveryOrderPanel = ({ isOpen, onClose, onCreated }) => {
  const { branchId, tenantId } = useAuthStore();
  const [form, setForm]           = useState(emptyForm());
  const [items, setItems]         = useState([]);
  const [productSearch, setProductSearch] = useState('');
  const [saving, setSaving]       = useState(false);
  const [error, setError]         = useState('');

  const { data: stockData, isLoading: loadingProducts } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: isOpen && !!branchId,
  });

  // Only show products available for sale (not supplies)
  const products = (stockData?.data ?? [])
    .filter(p =>
      p.productType !== 'supply' &&
      (!productSearch.trim() || p.productName?.toLowerCase().includes(productSearch.toLowerCase()))
    )
    .sort((a, b) => (a.productName ?? '').localeCompare(b.productName ?? ''));

  const addItem = (product) => {
    setItems(prev => {
      const existing = prev.find(i => i.productId === product.productId);
      if (existing) {
        return prev.map(i => i.productId === product.productId ? { ...i, quantity: i.quantity + 1 } : i);
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

  const updateQty = (productId, delta) => {
    setItems(prev => prev
      .map(i => i.productId === productId ? { ...i, quantity: Math.max(1, i.quantity + delta) } : i)
    );
  };

  const updateItem  = (productId, field, value) =>
    setItems(prev => prev.map(i => i.productId === productId ? { ...i, [field]: value } : i));

  const removeItem  = (productId) =>
    setItems(prev => prev.filter(i => i.productId !== productId));

  const subtotal = items.reduce((s, i) => s + Number(i.quantity) * Number(i.unitPrice), 0);
  const total    = subtotal + Number(form.deliveryFee || 0) - Number(form.discountAmount || 0);

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
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-5xl h-[88vh] flex flex-col">

        {/* ── Header ── */}
        <div className="flex items-center justify-between px-6 py-4 border-b shrink-0">
          <div className="flex items-center gap-2">
            <Bike size={18} className="text-orange-500" />
            <h2 className="text-base font-semibold text-gray-900">Nuevo Pedido / Domicilio</h2>
          </div>
          <button onClick={handleClose} className="p-1 rounded-lg hover:bg-gray-100 transition-colors">
            <X size={18} />
          </button>
        </div>

        {/* ── Body: two columns ── */}
        <div className="flex flex-1 overflow-hidden">

          {/* LEFT — product catalog */}
          <div className="w-72 shrink-0 border-r flex flex-col bg-gray-50">
            <div className="p-3 border-b bg-white">
              <p className="text-xs font-semibold text-gray-500 uppercase mb-2">Menú / Productos</p>
              <div className="relative">
                <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
                <input
                  className="w-full pl-7 pr-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-orange-400"
                  placeholder="Buscar producto..."
                  value={productSearch}
                  onChange={e => setProductSearch(e.target.value)}
                />
              </div>
            </div>

            <div className="flex-1 overflow-y-auto">
              {loadingProducts ? (
                <div className="flex items-center justify-center py-10 text-gray-400 text-sm gap-2">
                  <Loader2 size={14} className="animate-spin" /> Cargando...
                </div>
              ) : products.length === 0 ? (
                <div className="text-center py-10 text-gray-400 text-sm">Sin resultados</div>
              ) : products.map(p => {
                const inOrder = items.find(i => i.productId === p.productId);
                return (
                  <button
                    key={p.productId}
                    type="button"
                    onClick={() => addItem(p)}
                    className="w-full text-left px-3 py-2.5 border-b border-gray-100 transition-colors flex flex-col gap-0.5 hover:bg-orange-50 cursor-pointer"
                  >
                    <div className="flex items-start justify-between gap-1">
                      <span className="text-sm font-medium text-gray-800 leading-tight line-clamp-2 flex-1">{p.productName}</span>
                      {inOrder && (
                        <span className="shrink-0 text-xs bg-orange-100 text-orange-700 rounded-full px-1.5 py-0.5 font-semibold">
                          ×{inOrder.quantity}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-2 mt-0.5">
                      {p.productType && (
                        <span className={`text-xs px-1.5 py-0.5 rounded-full font-medium ${TYPE_CLS[p.productType] ?? 'bg-gray-100 text-gray-600'}`}>
                          {TYPE_LABEL[p.productType] ?? p.productType}
                        </span>
                      )}
                      <span className="text-xs font-semibold text-gray-600">
                        ${Number(p.salePrice ?? 0).toLocaleString('es-CO')}
                      </span>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* RIGHT — order form */}
          <div className="flex-1 flex flex-col overflow-hidden">
            <div className="flex-1 overflow-y-auto p-5 space-y-4">

              {/* Cliente + fuente */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Nombre del cliente</label>
                  <input
                    type="text"
                    value={form.customerName}
                    onChange={e => setForm(p => ({ ...p, customerName: e.target.value }))}
                    placeholder="Juan García"
                    className="input text-sm"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Fuente</label>
                  <select
                    value={form.source}
                    onChange={e => setForm(p => ({ ...p, source: e.target.value }))}
                    className="input text-sm"
                  >
                    {SOURCES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Teléfono</label>
                  <input
                    type="text"
                    value={form.customerPhone}
                    onChange={e => setForm(p => ({ ...p, customerPhone: e.target.value }))}
                    placeholder="+57 300 000 0000"
                    className="input text-sm"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Dirección</label>
                  <input
                    type="text"
                    value={form.customerAddress}
                    onChange={e => setForm(p => ({ ...p, customerAddress: e.target.value }))}
                    placeholder="Calle 123 #45-67"
                    className="input text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Notas del pedido</label>
                <textarea
                  rows={2}
                  value={form.notes}
                  onChange={e => setForm(p => ({ ...p, notes: e.target.value }))}
                  placeholder="Sin cebolla, entregar en portería..."
                  className="input text-sm resize-none"
                />
              </div>

              {/* Items */}
              <div>
                <p className="text-xs font-semibold text-gray-500 uppercase mb-2">
                  Items del pedido
                  {items.length > 0 && <span className="ml-2 bg-orange-100 text-orange-700 rounded-full px-1.5">{items.length}</span>}
                </p>

                {items.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-10 border-2 border-dashed border-gray-200 rounded-xl text-gray-400">
                    <PackagePlus size={28} className="mb-2 opacity-40" />
                    <p className="text-sm">Haz clic en un producto del menú para agregarlo</p>
                  </div>
                ) : (
                  <div className="space-y-1.5">
                    <div className="grid grid-cols-12 gap-2 text-xs font-semibold text-gray-400 uppercase px-2">
                      <span className="col-span-5">Producto</span>
                      <span className="col-span-3 text-center">Cantidad</span>
                      <span className="col-span-2 text-right">Precio unit.</span>
                      <span className="col-span-1 text-right">Total</span>
                      <span className="col-span-1" />
                    </div>
                    {items.map(item => (
                      <div key={item.productId} className="grid grid-cols-12 gap-2 items-center bg-white border border-gray-200 rounded-xl px-2 py-2">
                        <div className="col-span-5 text-sm font-medium text-gray-800 truncate pl-1">{item.productName}</div>
                        <div className="col-span-3 flex items-center justify-center gap-1">
                          <button
                            type="button"
                            onClick={() => updateQty(item.productId, -1)}
                            className="w-6 h-6 flex items-center justify-center rounded-md border border-gray-200 hover:bg-gray-100 text-gray-500"
                          >
                            <Minus size={11} />
                          </button>
                          <input
                            type="number" min="1"
                            className="w-10 text-center border border-gray-200 rounded-lg px-1 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-orange-400"
                            value={item.quantity}
                            onChange={e => updateItem(item.productId, 'quantity', Math.max(1, Number(e.target.value)))}
                          />
                          <button
                            type="button"
                            onClick={() => updateQty(item.productId, 1)}
                            className="w-6 h-6 flex items-center justify-center rounded-md border border-gray-200 hover:bg-gray-100 text-gray-500"
                          >
                            <Plus size={11} />
                          </button>
                        </div>
                        <div className="col-span-2 text-right text-xs text-gray-500 pr-1">
                          ${Number(item.unitPrice).toLocaleString('es-CO')}
                        </div>
                        <div className="col-span-1 text-right text-sm font-semibold text-gray-800">
                          ${(Number(item.quantity) * Number(item.unitPrice)).toLocaleString('es-CO')}
                        </div>
                        <button type="button" onClick={() => removeItem(item.productId)} className="col-span-1 flex justify-center text-red-400 hover:text-red-600">
                          <Trash2 size={13} />
                        </button>
                      </div>
                    ))}

                    {/* Subtotal / domicilio / descuento / total */}
                    <div className="mt-3 pt-3 border-t border-gray-100 space-y-2">
                      <div className="flex justify-between text-sm text-gray-500">
                        <span>Subtotal</span>
                        <span>${subtotal.toLocaleString('es-CO')}</span>
                      </div>
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <label className="block text-xs text-gray-500 mb-1">Costo domicilio</label>
                          <input
                            type="number" min="0"
                            value={form.deliveryFee}
                            onChange={e => setForm(p => ({ ...p, deliveryFee: e.target.value }))}
                            className="input text-sm py-1.5"
                            placeholder="0"
                          />
                        </div>
                        <div>
                          <label className="block text-xs text-gray-500 mb-1">Descuento</label>
                          <input
                            type="number" min="0"
                            value={form.discountAmount}
                            onChange={e => setForm(p => ({ ...p, discountAmount: e.target.value }))}
                            className="input text-sm py-1.5"
                            placeholder="0"
                          />
                        </div>
                      </div>
                      <div className="flex justify-between items-center pt-1 border-t border-gray-200">
                        <span className="text-sm font-semibold text-gray-700">Total</span>
                        <span className="text-xl font-bold text-gray-900">${total.toLocaleString('es-CO')}</span>
                      </div>
                    </div>
                  </div>
                )}
              </div>

              {error && <p className="text-sm text-red-600 bg-red-50 rounded-lg px-3 py-2">{error}</p>}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end gap-3 px-5 py-3 border-t bg-gray-50 shrink-0">
              <button type="button" onClick={handleClose} className="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg transition-colors">
                Cancelar
              </button>
              <button
                type="button"
                onClick={handleSubmit}
                disabled={saving}
                className="flex items-center gap-2 px-5 py-2 text-sm font-medium text-white bg-orange-500 hover:bg-orange-600 disabled:opacity-50 rounded-lg transition-colors"
              >
                {saving ? <Loader2 size={14} className="animate-spin" /> : <Bike size={14} />}
                {saving ? 'Creando...' : `Crear Pedido${items.length > 0 ? ` (${items.length})` : ''}`}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CreateDeliveryOrderPanel;
