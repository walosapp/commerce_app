import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Sparkles, Loader2, ChevronDown, ChevronUp } from 'lucide-react';
import supplierService from '../../../services/supplierService';
import ContactActions from './ContactActions';

const SuggestedOrderPanel = ({ supplier }) => {
  const [open, setOpen] = useState(false);
  const [editedItems, setEditedItems] = useState(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['suggested-order', supplier.id],
    queryFn: () => supplierService.getSuggestedOrder(supplier.id),
    enabled: open,
    staleTime: 0,
  });

  const suggestion = data?.data;
  const items = editedItems ?? suggestion?.items ?? [];

  const handleOpen = () => {
    setOpen(v => {
      if (!v) { setEditedItems(null); refetch(); }
      return !v;
    });
  };

  const updateQty = (productId, qty) => {
    const base = suggestion?.items ?? [];
    setEditedItems((editedItems ?? base).map(i =>
      i.productId === productId ? { ...i, suggestedQty: Number(qty), estimatedCost: i.unitCost ? i.unitCost * Number(qty) : null } : i
    ));
  };

  const total = items.reduce((s, i) => s + (i.estimatedCost ?? 0), 0);

  return (
    <div className="border border-gray-200 rounded-xl overflow-hidden">
      <button
        onClick={handleOpen}
        className="w-full flex items-center justify-between px-4 py-3 bg-gradient-to-r from-indigo-50 to-purple-50 hover:from-indigo-100 hover:to-purple-100 transition-colors"
      >
        <div className="flex items-center gap-2">
          <Sparkles size={16} className="text-indigo-600" />
          <span className="text-sm font-semibold text-indigo-700">Pedido sugerido por IA</span>
          {suggestion && items.length > 0 && (
            <span className="text-xs bg-indigo-600 text-white px-2 py-0.5 rounded-full">
              {items.length} producto{items.length !== 1 ? 's' : ''}
            </span>
          )}
        </div>
        {open ? <ChevronUp size={16} className="text-indigo-500" /> : <ChevronDown size={16} className="text-indigo-500" />}
      </button>

      {open && (
        <div className="p-4 space-y-3">
          {isLoading ? (
            <div className="flex items-center justify-center py-4">
              <Loader2 size={20} className="animate-spin text-indigo-500" />
            </div>
          ) : items.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-3">
              Todos los productos de este proveedor tienen stock suficiente.
            </p>
          ) : (
            <>
              <p className="text-xs text-gray-500">
                Productos con stock bajo o agotado. Ajusta las cantidades antes de enviar.
              </p>
              <div className="space-y-2">
                {items.map(item => (
                  <div key={item.productId} className="flex items-center gap-2 bg-gray-50 rounded-lg px-3 py-2">
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-gray-800 truncate">{item.productName}</p>
                      <p className="text-xs text-gray-500">
                        Stock: <span className={item.currentStock <= 0 ? 'text-red-500 font-semibold' : 'text-orange-500 font-semibold'}>{item.currentStock}</span>
                        {item.reorderPoint > 0 && ` / min: ${item.reorderPoint}`}
                      </p>
                    </div>
                    <div className="flex items-center gap-1">
                      <input
                        type="number"
                        min="1"
                        value={item.suggestedQty}
                        onChange={e => updateQty(item.productId, e.target.value)}
                        className="w-16 border border-gray-300 rounded-lg px-2 py-1 text-sm text-center focus:outline-none focus:ring-1 focus:ring-indigo-400"
                      />
                    </div>
                    {item.estimatedCost != null && (
                      <span className="text-xs font-medium text-gray-700 w-20 text-right">
                        ${Number(item.estimatedCost).toLocaleString('es-CO')}
                      </span>
                    )}
                  </div>
                ))}
              </div>

              {total > 0 && (
                <div className="flex justify-between items-center pt-2 border-t border-gray-200">
                  <span className="text-sm text-gray-600">Total estimado</span>
                  <span className="text-base font-bold text-gray-900">${total.toLocaleString('es-CO')}</span>
                </div>
              )}

              <div className="pt-2">
                <p className="text-xs text-gray-500 mb-2">Enviar pedido:</p>
                <ContactActions supplier={supplier} suggestedItems={items} />
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
};

export default SuggestedOrderPanel;
