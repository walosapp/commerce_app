import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PlusCircle, Trash2, Loader2, Search, ShoppingCart, X, Check } from 'lucide-react';
import toast from 'react-hot-toast';
import supplierService from '../../../services/supplierService';
import { inventoryService } from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const TYPE_CLS = {
  supply:   'bg-blue-100 text-blue-700',
  simple:   'bg-gray-100 text-gray-600',
  prepared: 'bg-orange-100 text-orange-700',
  service:  'bg-purple-100 text-purple-700',
};
const TYPE_LABEL = { supply: 'Insumo', simple: 'Simple', prepared: 'Preparado', service: 'Servicio' };

const SupplierProductsManager = ({ supplierId, products, onNewOrder, supplier }) => {
  const { branchId, tenantId } = useAuthStore();
  const queryClient = useQueryClient();
  const [showAdd, setShowAdd]   = useState(false);
  const [search, setSearch]     = useState('');
  const [pending, setPending]   = useState(null); // producto seleccionado para confirmar datos
  const [form, setForm]         = useState({ supplierSku: '', unitCost: '', leadTimeDays: '' });
  const [saving, setSaving]     = useState(false);

  const { data: stockData, isLoading: loadingStock } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: showAdd && !!branchId,
  });

  const alreadyIds = new Set((products ?? []).map(p => p.productId));
  const available  = (stockData?.data ?? []).filter(p =>
    !alreadyIds.has(p.productId) &&
    (!search.trim() || p.productName?.toLowerCase().includes(search.toLowerCase()))
  ).sort((a, b) => (a.productName ?? '').localeCompare(b.productName ?? ''));

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['supplier', supplierId] });

  const selectProduct = (p) => {
    setPending(p);
    setForm({ supplierSku: '', unitCost: p.costPrice ? String(p.costPrice) : '', leadTimeDays: '' });
  };

  const handleAdd = async () => {
    if (!pending) return;
    setSaving(true);
    try {
      await supplierService.addProduct(supplierId, {
        productId:    Number(pending.productId),
        supplierSku:  form.supplierSku || undefined,
        unitCost:     form.unitCost    ? Number(form.unitCost)    : undefined,
        leadTimeDays: form.leadTimeDays ? Number(form.leadTimeDays) : undefined,
      });
      toast.success('Producto asociado');
      setPending(null);
      setForm({ supplierSku: '', unitCost: '', leadTimeDays: '' });
      setSearch('');
      invalidate();
    } catch {
      toast.error('Error al asociar producto');
    } finally {
      setSaving(false);
    }
  };

  const handleRemove = async (productId) => {
    try {
      await supplierService.removeProduct(supplierId, productId);
      toast.success('Producto desasociado');
      invalidate();
    } catch {
      toast.error('Error al desasociar producto');
    }
  };

  return (
    <div className="space-y-3">

      {/* Header */}
      <div className="flex items-center justify-between">
        <p className="text-sm font-semibold text-gray-700">Productos ({products?.length ?? 0})</p>
        <div className="flex items-center gap-2">
          {(products ?? []).length > 0 && onNewOrder && (
            <button
              onClick={() => onNewOrder(supplier)}
              className="flex items-center gap-1 text-xs text-green-600 hover:text-green-800 font-medium bg-green-50 hover:bg-green-100 px-2 py-1 rounded-lg transition-colors"
            >
              <ShoppingCart size={12} /> Generar pedido
            </button>
          )}
          <button
            onClick={() => { setShowAdd(v => !v); setPending(null); setSearch(''); }}
            className="flex items-center gap-1 text-xs text-primary-600 hover:text-primary-800 font-medium"
          >
            <PlusCircle size={14} /> Asociar
          </button>
        </div>
      </div>

      {/* Panel para asociar nuevo producto */}
      {showAdd && (
        <div className="border border-gray-200 rounded-xl overflow-hidden">

          {/* Buscador */}
          <div className="p-3 bg-gray-50 border-b">
            <div className="relative">
              <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
              <input
                autoFocus
                placeholder="Buscar producto del catálogo..."
                value={search}
                onChange={e => { setSearch(e.target.value); setPending(null); }}
                className="w-full pl-7 pr-3 py-1.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
              />
            </div>
          </div>

          {/* Lista de productos */}
          <div className="max-h-52 overflow-y-auto bg-white">
            {loadingStock ? (
              <div className="flex items-center justify-center py-6 text-gray-400 gap-2 text-sm">
                <Loader2 size={14} className="animate-spin" /> Cargando...
              </div>
            ) : available.length === 0 ? (
              <p className="text-xs text-gray-400 text-center py-6">
                {search ? 'Sin resultados' : 'Todos los productos ya están asociados'}
              </p>
            ) : (
              available.map(p => (
                <button
                  key={p.productId}
                  type="button"
                  onClick={() => selectProduct(p)}
                  className={`w-full text-left px-3 py-2.5 border-b border-gray-100 last:border-0 flex items-center gap-2 transition-colors
                    ${pending?.productId === p.productId ? 'bg-primary-50' : 'hover:bg-gray-50'}`}
                >
                  {pending?.productId === p.productId
                    ? <Check size={13} className="text-primary-600 shrink-0" />
                    : <div className="w-3.5 h-3.5 shrink-0" />}
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-800 truncate">{p.productName}</p>
                    <div className="flex items-center gap-2 mt-0.5">
                      {p.productType && (
                        <span className={`text-xs px-1.5 py-0.5 rounded-full font-medium ${TYPE_CLS[p.productType] ?? 'bg-gray-100 text-gray-600'}`}>
                          {TYPE_LABEL[p.productType] ?? p.productType}
                        </span>
                      )}
                      {p.costPrice > 0 && (
                        <span className="text-xs text-gray-400">${Number(p.costPrice).toLocaleString('es-CO')}</span>
                      )}
                    </div>
                  </div>
                </button>
              ))
            )}
          </div>

          {/* Formulario adicional al seleccionar */}
          {pending && (
            <div className="p-3 bg-indigo-50 border-t border-indigo-100 space-y-2">
              <p className="text-xs font-semibold text-indigo-700 truncate">Configurar: {pending.productName}</p>
              <div className="grid grid-cols-3 gap-2">
                <input
                  placeholder="SKU proveedor"
                  value={form.supplierSku}
                  onChange={e => setForm(f => ({ ...f, supplierSku: e.target.value }))}
                  className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400 bg-white"
                />
                <input
                  placeholder="Costo unit."
                  type="number"
                  value={form.unitCost}
                  onChange={e => setForm(f => ({ ...f, unitCost: e.target.value }))}
                  className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400 bg-white"
                />
                <input
                  placeholder="Lead time (días)"
                  type="number"
                  value={form.leadTimeDays}
                  onChange={e => setForm(f => ({ ...f, leadTimeDays: e.target.value }))}
                  className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400 bg-white"
                />
              </div>
              <div className="flex gap-2">
                <button
                  onClick={handleAdd}
                  disabled={saving}
                  className="flex items-center gap-1.5 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
                >
                  {saving ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
                  Confirmar asociación
                </button>
                <button
                  onClick={() => setPending(null)}
                  className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 px-2 py-1.5 rounded-lg hover:bg-gray-100"
                >
                  <X size={12} /> Cancelar
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Lista de productos asociados */}
      <div className="space-y-1.5">
        {(products ?? []).map(sp => (
          <div key={sp.id} className="flex items-center justify-between bg-gray-50 rounded-lg px-3 py-2">
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-gray-800 truncate">{sp.productName}</p>
              <div className="flex gap-3 text-xs text-gray-500 mt-0.5">
                {sp.supplierSku  && <span>SKU: {sp.supplierSku}</span>}
                {sp.unitCost     && <span>${Number(sp.unitCost).toLocaleString('es-CO')}</span>}
                {sp.leadTimeDays && <span>{sp.leadTimeDays}d entrega</span>}
              </div>
            </div>
            <button onClick={() => handleRemove(sp.productId)} className="text-red-400 hover:text-red-600 transition-colors ml-2">
              <Trash2 size={14} />
            </button>
          </div>
        ))}
        {(!products || products.length === 0) && !showAdd && (
          <p className="text-xs text-gray-400 text-center py-3">Sin productos asociados</p>
        )}
      </div>
    </div>
  );
};

export default SupplierProductsManager;
