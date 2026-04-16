import { useQuery } from '@tanstack/react-query';
import { X, Clock, Phone, MapPin, Package, History, Loader2 } from 'lucide-react';
import deliveryService from '../../../services/deliveryService';

const STATUS_LABELS = {
  new:                { label: 'Nuevo',           color: 'bg-blue-100 text-blue-700' },
  accepted:           { label: 'Aceptado',         color: 'bg-indigo-100 text-indigo-700' },
  preparing:          { label: 'En preparacion',   color: 'bg-yellow-100 text-yellow-700' },
  ready_for_dispatch: { label: 'Listo',            color: 'bg-purple-100 text-purple-700' },
  out_for_delivery:   { label: 'En camino',        color: 'bg-orange-100 text-orange-700' },
  delivered:          { label: 'Entregado',        color: 'bg-green-100 text-green-700' },
  rejected:           { label: 'Rechazado',        color: 'bg-red-100 text-red-600' },
  cancelled:          { label: 'Cancelado',        color: 'bg-gray-100 text-gray-500' },
  returned:           { label: 'Devuelto',         color: 'bg-orange-100 text-orange-600' },
};

const DeliveryOrderDetailsPanel = ({ orderId, onClose, onAction }) => {
  const { data, isLoading } = useQuery({
    queryKey: ['delivery-order', orderId],
    queryFn: () => deliveryService.getOrder(orderId),
    enabled: !!orderId,
    refetchInterval: 15000,
  });

  const order = data?.data;
  const statusMeta = order ? (STATUS_LABELS[order.status] ?? { label: order.status, color: 'bg-gray-100 text-gray-600' }) : null;

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full sm:w-[400px] bg-white shadow-xl border-l flex flex-col">
      <div className="flex items-center gap-3 px-5 py-4 border-b">
        <Package size={20} className="text-indigo-600" />
        <h2 className="font-semibold text-gray-900">
          {order ? `Pedido #${order.orderNumber}` : 'Detalle de pedido'}
        </h2>
        <button onClick={onClose} className="ml-auto text-gray-400 hover:text-gray-600"><X size={20} /></button>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 size={24} className="animate-spin text-indigo-500" />
        </div>
      ) : !order ? (
        <div className="flex-1 flex items-center justify-center text-gray-400 text-sm">
          Pedido no encontrado
        </div>
      ) : (
        <div className="flex-1 overflow-y-auto">
          <div className="px-5 py-4 space-y-4">
            <div className="flex items-center gap-2 flex-wrap">
              <span className={`text-xs font-semibold px-2.5 py-1 rounded-full ${statusMeta.color}`}>
                {statusMeta.label}
              </span>
              <span className="text-xs text-gray-400">
                {new Date(order.createdAt).toLocaleString('es-CO')}
              </span>
            </div>

            {(order.customerName || order.customerPhone || order.customerAddress) && (
              <div className="bg-gray-50 rounded-xl p-4 space-y-2 text-sm">
                <p className="font-medium text-gray-700">Cliente</p>
                {order.customerName && <p className="text-gray-800 font-semibold">{order.customerName}</p>}
                {order.customerPhone && (
                  <div className="flex items-center gap-2 text-gray-600">
                    <Phone size={13} /><span>{order.customerPhone}</span>
                  </div>
                )}
                {order.customerAddress && (
                  <div className="flex items-center gap-2 text-gray-600">
                    <MapPin size={13} /><span>{order.customerAddress}</span>
                  </div>
                )}
              </div>
            )}

            {order.notes && (
              <div className="bg-yellow-50 border border-yellow-200 rounded-lg p-3 text-sm text-yellow-800">
                {order.notes}
              </div>
            )}

            {order.items && order.items.length > 0 && (
              <div>
                <p className="text-sm font-medium text-gray-700 mb-2">Items</p>
                <div className="space-y-2">
                  {order.items.map(item => (
                    <div key={item.id} className="flex items-center justify-between text-sm">
                      <div>
                        <span className="font-medium text-gray-800">{item.productName}</span>
                        {item.notes && <p className="text-xs text-gray-400">{item.notes}</p>}
                      </div>
                      <div className="text-right text-gray-600">
                        <p>{item.quantity} × ${Number(item.unitPrice).toLocaleString('es-CO')}</p>
                        <p className="font-semibold text-gray-800">${Number(item.subtotal).toLocaleString('es-CO')}</p>
                      </div>
                    </div>
                  ))}
                </div>
                <div className="mt-3 pt-3 border-t space-y-1 text-sm">
                  <div className="flex justify-between text-gray-500">
                    <span>Subtotal</span><span>${Number(order.subtotal).toLocaleString('es-CO')}</span>
                  </div>
                  {order.deliveryFee > 0 && (
                    <div className="flex justify-between text-gray-500">
                      <span>Domicilio</span><span>${Number(order.deliveryFee).toLocaleString('es-CO')}</span>
                    </div>
                  )}
                  {order.discountAmount > 0 && (
                    <div className="flex justify-between text-green-600">
                      <span>Descuento</span><span>-${Number(order.discountAmount).toLocaleString('es-CO')}</span>
                    </div>
                  )}
                  <div className="flex justify-between font-bold text-gray-900 text-base pt-1">
                    <span>Total</span><span>${Number(order.total).toLocaleString('es-CO')}</span>
                  </div>
                </div>
              </div>
            )}

            {order.rejectedReason && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-700">
                <strong>Rechazado:</strong> {order.rejectedReason}
              </div>
            )}
            {order.returnedReason && (
              <div className="bg-orange-50 border border-orange-200 rounded-lg p-3 text-sm text-orange-700">
                <strong>Devuelto:</strong> {order.returnedReason}
              </div>
            )}

            {order.statusHistory && order.statusHistory.length > 0 && (
              <div>
                <div className="flex items-center gap-2 mb-2">
                  <History size={15} className="text-gray-400" />
                  <p className="text-sm font-medium text-gray-700">Historial</p>
                </div>
                <div className="space-y-2">
                  {order.statusHistory.map(h => (
                    <div key={h.id} className="flex gap-3 text-xs">
                      <div className="pt-0.5 flex-shrink-0">
                        <div className="w-2 h-2 rounded-full bg-indigo-400 mt-1" />
                      </div>
                      <div>
                        <span className="font-medium text-gray-700">{h.toStatus}</span>
                        {h.comment && <p className="text-gray-500">{h.comment}</p>}
                        <p className="text-gray-400">{new Date(h.createdAt).toLocaleString('es-CO')}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {order && !['delivered', 'rejected', 'cancelled', 'returned'].includes(order.status) && (
        <div className="px-5 py-4 border-t flex gap-2 flex-wrap">
          {order.status === 'out_for_delivery' ? (
            <>
              <button onClick={() => onAction(order, 'deliver')} className="flex-1 bg-green-600 hover:bg-green-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">Entregado</button>
              <button onClick={() => onAction(order, 'return')} className="flex-1 bg-orange-500 hover:bg-orange-600 text-white text-sm font-medium py-2 rounded-lg transition-colors">Devolver</button>
            </>
          ) : order.status === 'ready_for_dispatch' ? (
            <>
              <button onClick={() => onAction(order, 'dispatch')} className="flex-1 bg-purple-600 hover:bg-purple-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">Despachar</button>
              <button onClick={() => onAction(order, 'cancel')} className="flex-1 bg-red-100 hover:bg-red-200 text-red-600 text-sm font-medium py-2 rounded-lg transition-colors">Cancelar</button>
            </>
          ) : order.status === 'preparing' ? (
            <>
              <button onClick={() => onAction(order, 'ready')} className="flex-1 bg-yellow-500 hover:bg-yellow-600 text-white text-sm font-medium py-2 rounded-lg transition-colors">Marcar Listo</button>
              <button onClick={() => onAction(order, 'cancel')} className="flex-1 bg-red-100 hover:bg-red-200 text-red-600 text-sm font-medium py-2 rounded-lg transition-colors">Cancelar</button>
            </>
          ) : order.status === 'accepted' ? (
            <>
              <button onClick={() => onAction(order, 'prepare')} className="flex-1 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">Preparar</button>
              <button onClick={() => onAction(order, 'reject')} className="flex-1 bg-red-100 hover:bg-red-200 text-red-600 text-sm font-medium py-2 rounded-lg transition-colors">Rechazar</button>
            </>
          ) : (
            <>
              <button onClick={() => onAction(order, 'accept')} className="flex-1 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">Aceptar</button>
              <button onClick={() => onAction(order, 'reject')} className="flex-1 bg-red-100 hover:bg-red-200 text-red-600 text-sm font-medium py-2 rounded-lg transition-colors">Rechazar</button>
            </>
          )}
        </div>
      )}
    </div>
  );
};

export default DeliveryOrderDetailsPanel;
