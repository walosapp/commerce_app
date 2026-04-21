import { useQuery } from '@tanstack/react-query';
import { ShoppingBag, Loader2 } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';
import api from '../../../config/api';

const getOrderItems = (orderId) =>
  api.get(`/sales/orders/${orderId}/items`).then(r => r.data);

const OrderItemsList = ({ orderId }) => {
  const { data, isLoading } = useQuery({
    queryKey: ['order-items', orderId],
    queryFn:  () => getOrderItems(orderId),
    enabled:  !!orderId,
    staleTime: 60_000,
  });

  const items = data?.data ?? [];

  if (isLoading) {
    return (
      <div className="flex items-center gap-2 py-2 text-xs text-gray-400">
        <Loader2 size={12} className="animate-spin" /> Cargando productos...
      </div>
    );
  }

  if (!items.length) {
    return (
      <p className="text-xs text-gray-400 py-1">Sin productos registrados</p>
    );
  }

  return (
    <div>
      <div className="flex items-center gap-1.5 mb-2">
        <ShoppingBag size={12} className="text-gray-400" />
        <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">Productos del pedido</p>
      </div>
      <div className="rounded-lg border border-gray-200 overflow-hidden">
        {items.map((item, idx) => (
          <div
            key={item.id}
            className={`flex items-center gap-3 px-3 py-2 text-sm ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50'}`}
          >
            <span className="w-6 h-6 rounded-full bg-gray-100 text-gray-600 text-xs font-bold flex items-center justify-center shrink-0">
              {item.quantity % 1 === 0 ? item.quantity : item.quantity.toFixed(1)}
            </span>
            <span className="flex-1 font-medium text-gray-800 truncate">{item.productName}</span>
            {item.notes && (
              <span className="text-xs text-gray-400 italic truncate max-w-[100px]">{item.notes}</span>
            )}
            <div className="text-right shrink-0">
              <p className="font-semibold text-gray-900">{formatCurrency(item.subtotal)}</p>
              <p className="text-xs text-gray-400">{formatCurrency(item.unitPrice)} c/u</p>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default OrderItemsList;
