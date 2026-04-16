import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PlusCircle, Trash2, Loader2, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import supplierService from '../../../services/supplierService';
import { inventoryService } from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const SupplierProductsManager = ({ supplierId, products }) => {
  const { branchId, tenantId } = useAuthStore();
  const queryClient = useQueryClient();
  const [showAdd, setShowAdd] = useState(false);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState({ productId: '', supplierSku: '', unitCost: '', leadTimeDays: '' });
  const [saving, setSaving] = useState(false);

  const { data: stockData } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: showAdd && !!branchId,
  });

  const allProducts = (stockData?.data ?? []).filter(p =>
    !products?.find(sp => sp.productId === p.productId) &&
    (!search.trim() || p.productName?.toLowerCase().includes(search.toLowerCase()))
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['supplier', supplierId] });

  const handleAdd = async () => {
    if (!form.productId) { toast.error('Selecciona un producto'); return; }
    setSaving(true);
    try {
      await supplierService.addProduct(supplierId, {
        productId: Number(form.productId),
        supplierSku: form.supplierSku || undefined,
        unitCost: form.unitCost ? Number(form.unitCost) : undefined,
        leadTimeDays: form.leadTimeDays ? Number(form.leadTimeDays) : undefined,
      });
      toast.success('Producto asociado');
      setForm({ productId: '', supplierSku: '', unitCost: '', leadTimeDays: '' });
      setShowAdd(false);
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
      <div className="flex items-center justify-between">
        <p className="text-sm font-semibold text-gray-700">Productos ({products?.length ?? 0})</p>
        <button
          onClick={() => setShowAdd(v => !v)}
          className="flex items-center gap-1 text-xs text-primary-600 hover:text-primary-800 font-medium"
        >
          <PlusCircle size={14} /> Asociar
        </button>
      </div>

      {showAdd && (
        <div className="bg-gray-50 rounded-xl p-3 space-y-2 border border-gray-200">
          <div className="relative">
            <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              placeholder="Buscar producto..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full pl-7 pr-3 py-1.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-1 focus:ring-primary-400"
            />
          </div>
          {search && allProducts.length > 0 && (
            <div className="max-h-32 overflow-y-auto border border-gray-200 rounded-lg bg-white">
              {allProducts.slice(0, 20).map(p => (
                <button
                  key={p.productId}
                  onClick={() => { setForm(f => ({ ...f, productId: p.productId })); setSearch(p.productName); }}
                  className="w-full text-left px-3 py-2 text-sm hover:bg-primary-50 border-b border-gray-100 last:border-b-0"
                >
                  {p.productName}
                </button>
              ))}
            </div>
          )}
          <div className="grid grid-cols-3 gap-2">
            <input
              placeholder="SKU proveedor"
              value={form.supplierSku}
              onChange={e => setForm(f => ({ ...f, supplierSku: e.target.value }))}
              className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400"
            />
            <input
              placeholder="Costo unit."
              type="number"
              value={form.unitCost}
              onChange={e => setForm(f => ({ ...f, unitCost: e.target.value }))}
              className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400"
            />
            <input
              placeholder="Lead time (días)"
              type="number"
              value={form.leadTimeDays}
              onChange={e => setForm(f => ({ ...f, leadTimeDays: e.target.value }))}
              className="border border-gray-300 rounded-lg px-2 py-1.5 text-xs focus:outline-none focus:ring-1 focus:ring-primary-400"
            />
          </div>
          <button
            onClick={handleAdd}
            disabled={saving || !form.productId}
            className="flex items-center gap-1.5 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
          >
            {saving && <Loader2 size={12} className="animate-spin" />}
            Asociar producto
          </button>
        </div>
      )}

      <div className="space-y-1.5">
        {(products ?? []).map(sp => (
          <div key={sp.id} className="flex items-center justify-between bg-gray-50 rounded-lg px-3 py-2">
            <div>
              <p className="text-sm font-medium text-gray-800">{sp.productName}</p>
              <div className="flex gap-3 text-xs text-gray-500 mt-0.5">
                {sp.supplierSku && <span>SKU: {sp.supplierSku}</span>}
                {sp.unitCost && <span>${Number(sp.unitCost).toLocaleString('es-CO')}</span>}
                {sp.leadTimeDays && <span>{sp.leadTimeDays}d entrega</span>}
              </div>
            </div>
            <button
              onClick={() => handleRemove(sp.productId)}
              className="text-red-400 hover:text-red-600 transition-colors"
            >
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
