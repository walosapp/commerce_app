import DeliveryOrderCard from './DeliveryOrderCard';

const COLUMNS = [
  { status: 'new',                label: 'Nuevos',         color: 'border-blue-400',   badge: 'bg-blue-100 text-blue-700' },
  { status: 'accepted',           label: 'Aceptados',      color: 'border-indigo-400', badge: 'bg-indigo-100 text-indigo-700' },
  { status: 'preparing',          label: 'En preparacion', color: 'border-yellow-400', badge: 'bg-yellow-100 text-yellow-700' },
  { status: 'ready_for_dispatch', label: 'Listos',         color: 'border-purple-400', badge: 'bg-purple-100 text-purple-700' },
  { status: 'out_for_delivery',   label: 'En camino',      color: 'border-orange-400', badge: 'bg-orange-100 text-orange-700' },
];

const DeliveryBoard = ({ orders, onOrderClick, onAction }) => {
  return (
    <div className="flex gap-4 overflow-x-auto pb-4 min-h-[400px]">
      {COLUMNS.map(col => {
        const colOrders = orders.filter(o => o.status === col.status);
        return (
          <div key={col.status} className={`flex-shrink-0 w-64 bg-gray-50 rounded-xl border-t-4 ${col.color}`}>
            <div className="px-3 py-2.5 flex items-center justify-between">
              <span className="text-sm font-semibold text-gray-700">{col.label}</span>
              <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${col.badge}`}>
                {colOrders.length}
              </span>
            </div>
            <div className="px-2 pb-3 space-y-2 min-h-[200px]">
              {colOrders.map(order => (
                <DeliveryOrderCard
                  key={order.id}
                  order={order}
                  onClick={onOrderClick}
                  onAction={onAction}
                />
              ))}
              {colOrders.length === 0 && (
                <div className="text-center py-8 text-xs text-gray-400">Sin pedidos</div>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default DeliveryBoard;
