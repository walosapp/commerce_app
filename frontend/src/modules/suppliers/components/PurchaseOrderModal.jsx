import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Plus, Trash2, Loader2, Search, ShoppingCart, PackagePlus, MessageCircle, Mail, Send } from 'lucide-react';
import ContactActions from './ContactActions';
import toast from 'react-hot-toast';
import purchaseOrderService from '../../../services/purchaseOrderService';
import inventoryService from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const TYPE_LABEL = { supply: 'Insumo', simple: 'Simple', prepared: 'Preparado', service: 'Servicio' };
const TYPE_CLS   = { supply: 'bg-blue-100 text-blue-700', simple: 'bg-gray-100 text-gray-600', prepared: 'bg-orange-100 text-orange-700', service: 'bg-purple-100 text-purple-700' };

const PurchaseOrderModal = ({ isOpen, onClose, onSaved, suppliers }) => {
  const { branchId, tenantId } = useAuthStore();
  const [form, setForm]       = useState({ supplierId: '', notes: '', expectedDate: '' });
  const [items, setItems]     = useState([]);
  const [search, setSearch]   = useState('');
  const [saving, setSaving]   = useState(false);

  const { data: stockData, isLoading: loadingProducts } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId && !!isOpen,
  });

  const products = (stockData?.data ?? [])
    .filter(p => !search.trim() || p.productName?.toLowerCase().includes(search.toLowerCase()))
    .sort((a, b) => {
      const aS = a.productType === 'supply' ? 0 : 1;
      const bS = b.productType === 'supply' ? 0 : 1;
      return aS - bS || (a.productName ?? '').localeCompare(b.productName ?? '');
    });

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
  };

  const removeItem  = (productId) => setItems(prev => prev.filter(i => i.productId !== productId));
  const updateItem  = (productId, field, val) =>
    setItems(prev => prev.map(i => i.productId === productId ? { ...i, [field]: val } : i));

  const total            = items.reduce((s, i) => s + (Number(i.quantity) || 0) * (Number(i.unitCost) || 0), 0);
  const selectedSupplier = (suppliers ?? []).find(s => String(s.id) === String(form.supplierId));
  const canShare         = !!selectedSupplier && items.length > 0;

  const handleSubmit = async () => {
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
      toast.success('Pedido creado exitosamente');
      onSaved();
      onClose();
      setForm({ supplierId: '', notes: '', expectedDate: '' });
      setItems([]);
      setSearch('');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al crear pedido');
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-5xl h-[88vh] flex flex-col">

        {/* ── Header ── */}
        <div className="flex items-center justify-between px-6 py-4 border-b shrink-0">
          <div className="flex items-center gap-2">
            <ShoppingCart size={18} className="text-primary-600" />
            <h2 className="text-base font-semibold text-gray-900">Nuevo Pedido a Proveedor</h2>
          </div>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100 transition-colors">
            <X size={18} />
          </button>
        </div>

        {/* ── Body: two columns ── */}
        <div className="flex flex-1 overflow-hidden">

          {/* LEFT — product catalog */}
          <div className="w-72 shrink-0 border-r flex flex-col bg-gray-50">
            <div className="p-3 border-b bg-white">
              <p className="text-xs font-semibold text-gray-500 uppercase mb-2">Catálogo de productos</p>
              <div className="relative">
                <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
                <input
                  className="w-full pl-7 pr-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-primary-400"
                  placeholder="Buscar producto..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
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
                const inOrder = !!items.find(i => i.productId === p.productId);
                return (
                  <button
                    key={p.productId}
                    type="button"
                    onClick={() => addProduct(p)}
                    disabled={inOrder}
                    className={`w-full text-left px-3 py-2.5 border-b border-gray-100 transition-colors flex flex-col gap-0.5
                      ${inOrder ? 'opacity-40 cursor-not-allowed bg-gray-50' : 'hover:bg-primary-50 cursor-pointer'}`}
                  >
                    <span className="text-sm font-medium text-gray-800 leading-tight line-clamp-2">{p.productName}</span>
                    <div className="flex items-center gap-2 mt-0.5">
                      {p.productType && (
                        <span className={`text-xs px-1.5 py-0.5 rounded-full font-medium ${TYPE_CLS[p.productType] ?? 'bg-gray-100 text-gray-600'}`}>
                          {TYPE_LABEL[p.productType] ?? p.productType}
                        </span>
                      )}
                      <span className="text-xs text-gray-400">
                        ${Number(p.costPrice ?? 0).toLocaleString('es-CO')}
                      </span>
                      {inOrder && <span className="text-xs text-primary-600 font-medium ml-auto">✓ Agregado</span>}
                    </div>
                  </button>
                );
              })}
            </div>
          </div>

          {/* RIGHT — order form */}
          <div className="flex-1 flex flex-col overflow-hidden">
            <div className="flex-1 overflow-y-auto p-5 space-y-4">

              {/* Proveedor + fecha + notas */}
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-600 mb-1">Proveedor *</label>
                  <select
                    className="input text-sm"
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
                  <label className="block text-xs font-medium text-gray-600 mb-1">Fecha esperada</label>
                  <input
                    type="date"
                    className="input text-sm"
                    value={form.expectedDate}
                    onChange={e => setForm(f => ({ ...f, expectedDate: e.target.value }))}
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Notas</label>
                <textarea
                  className="input text-sm min-h-[52px] resize-none"
                  placeholder="Instrucciones, condiciones, etc."
                  value={form.notes}
                  onChange={e => setForm(f => ({ ...f, notes: e.target.value }))}
                />
              </div>

              {/* Items del pedido */}
              <div>
                <p className="text-xs font-semibold text-gray-500 uppercase mb-2">
                  Productos en el pedido
                  {items.length > 0 && <span className="ml-2 bg-primary-100 text-primary-700 rounded-full px-1.5">{items.length}</span>}
                </p>

                {items.length === 0 ? (
                  <div className="flex flex-col items-center justify-center py-12 border-2 border-dashed border-gray-200 rounded-xl text-gray-400">
                    <PackagePlus size={32} className="mb-2 opacity-40" />
                    <p className="text-sm">Haz clic en un producto del catálogo para agregarlo</p>
                  </div>
                ) : (
                  <div className="space-y-1.5">
                    {/* Header */}
                    <div className="grid grid-cols-12 gap-2 text-xs font-semibold text-gray-400 uppercase px-2">
                      <span className="col-span-4">Producto</span>
                      <span className="col-span-2 text-center">Cant.</span>
                      <span className="col-span-3 text-center">Costo unit.</span>
                      <span className="col-span-2 text-right">Subtotal</span>
                      <span className="col-span-1" />
                    </div>
                    {items.map(item => (
                      <div key={item.productId} className="grid grid-cols-12 gap-2 items-center bg-white border border-gray-200 rounded-xl px-2 py-2">
                        <div className="col-span-4 text-sm font-medium text-gray-800 truncate pl-1">{item.productName}</div>
                        <div className="col-span-2">
                          <input
                            type="number" min="0.001" step="0.001"
                            className="w-full text-center border border-gray-200 rounded-lg px-1 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
                            value={item.quantity}
                            onChange={e => updateItem(item.productId, 'quantity', e.target.value)}
                          />
                        </div>
                        <div className="col-span-3">
                          <input
                            type="number" min="0" step="0.01"
                            className="w-full text-center border border-gray-200 rounded-lg px-1 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
                            value={item.unitCost}
                            onChange={e => updateItem(item.productId, 'unitCost', e.target.value)}
                          />
                        </div>
                        <div className="col-span-2 text-right text-sm font-semibold text-gray-700 pr-1">
                          ${((Number(item.quantity)||0)*(Number(item.unitCost)||0)).toLocaleString('es-CO',{maximumFractionDigits:0})}
                        </div>
                        <button type="button" onClick={() => removeItem(item.productId)} className="col-span-1 flex justify-center text-red-400 hover:text-red-600">
                          <Trash2 size={13} />
                        </button>
                      </div>
                    ))}

                    {/* Total */}
                    <div className="flex justify-end pt-2 border-t border-gray-100 mt-2">
                      <span className="text-sm font-bold text-gray-900">
                        Total: ${total.toLocaleString('es-CO', {maximumFractionDigits:0})}
                      </span>
                    </div>
                  </div>
                )}
              </div>
            </div>

            {/* Footer */}
            <div className="flex items-center justify-between gap-3 px-5 py-3 border-t bg-gray-50 shrink-0">
              {/* WhatsApp / Email — solo si hay proveedor e ítems */}
              <div className="flex items-center gap-2">
                {canShare ? (
                  <ContactActions
                    supplier={selectedSupplier}
                    orderItems={items}
                    mode="order"
                  />
                ) : (
                  <span className="text-xs text-gray-400">
                    {!selectedSupplier ? 'Selecciona proveedor para compartir' : 'Agrega productos para compartir'}
                  </span>
                )}
              </div>
              <div className="flex items-center gap-3">
                <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg transition-colors">
                  Cancelar
                </button>
                <button
                  type="button"
                  onClick={handleSubmit}
                  disabled={saving}
                  className="flex items-center gap-2 px-5 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 rounded-lg transition-colors"
                >
                  {saving ? <Loader2 size={14} className="animate-spin" /> : <ShoppingCart size={14} />}
                  {saving ? 'Creando...' : `Crear Pedido${items.length > 0 ? ` (${items.length})` : ''}`}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default PurchaseOrderModal;
