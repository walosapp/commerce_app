import { useQuery } from '@tanstack/react-query';
import {
  X, ShoppingCart, Truck, Calendar, FileText,
  Clock, CheckCircle2, XCircle, Package, Loader2,
  MessageCircle, Mail
} from 'lucide-react';
import purchaseOrderService from '../../../services/purchaseOrderService';
import ContactActions from './ContactActions';

const STATUS = {
  pending:   { label: 'Pendiente',  cls: 'bg-yellow-100 text-yellow-700', icon: Clock },
  ordered:   { label: 'Enviado',    cls: 'bg-blue-100 text-blue-700',     icon: ShoppingCart },
  received:  { label: 'Recibido',   cls: 'bg-green-100 text-green-700',   icon: CheckCircle2 },
  cancelled: { label: 'Cancelado',  cls: 'bg-red-100 text-red-700',       icon: XCircle },
};

const fmt = (n) => Number(n ?? 0).toLocaleString('es-CO', { minimumFractionDigits: 0 });

const PurchaseOrderDetailPanel = ({ orderId, onClose, onReceive, onCancel }) => {
  const { data, isLoading } = useQuery({
    queryKey: ['purchase-order-detail', orderId],
    queryFn:  () => purchaseOrderService.getById(orderId),
    enabled:  !!orderId,
  });

  const order = data?.data;
  const badge = order ? (STATUS[order.status] ?? STATUS.pending) : null;
  const BadgeIcon = badge?.icon;

  const supplierForContact = order
    ? { name: order.supplierName, contactName: order.supplierName, phone: order.supplierPhone, email: order.supplierEmail }
    : null;

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full sm:w-[460px] bg-white shadow-xl border-l flex flex-col">

      {/* Header */}
      <div className="flex items-center gap-3 px-5 py-4 border-b flex-shrink-0">
        <ShoppingCart size={18} className="text-primary-600" />
        <div className="flex-1 min-w-0">
          <p className="font-semibold text-gray-900 truncate">
            {isLoading ? 'Cargando...' : (order?.orderNumber ?? 'Pedido')}
          </p>
          {order && (
            <p className="text-xs text-gray-400">
              {new Date(order.createdAt).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })}
            </p>
          )}
        </div>
        {badge && (
          <span className={`inline-flex items-center gap-1 text-xs font-medium px-2.5 py-1 rounded-full flex-shrink-0 ${badge.cls}`}>
            <BadgeIcon size={11} /> {badge.label}
          </span>
        )}
        <button onClick={onClose} className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors">
          <X size={18} />
        </button>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 size={22} className="animate-spin text-primary-500" />
        </div>
      ) : !order ? (
        <div className="flex-1 flex items-center justify-center text-gray-400 text-sm">Pedido no encontrado</div>
      ) : (
        <>
          <div className="flex-1 overflow-y-auto">

            {/* Info del proveedor */}
            <div className="px-5 py-4 border-b space-y-2">
              <div className="flex items-center gap-2 text-sm text-gray-700">
                <Truck size={15} className="text-gray-400 shrink-0" />
                <span className="font-medium">{order.supplierName}</span>
              </div>
              {order.expectedDate && (
                <div className="flex items-center gap-2 text-sm text-gray-500">
                  <Calendar size={14} className="text-gray-400 shrink-0" />
                  <span>Entrega esperada: {new Date(order.expectedDate).toLocaleDateString('es-CO')}</span>
                </div>
              )}
              {order.receivedAt && (
                <div className="flex items-center gap-2 text-sm text-gray-500">
                  <CheckCircle2 size={14} className="text-green-500 shrink-0" />
                  <span>Recibido: {new Date(order.receivedAt).toLocaleString('es-CO', { dateStyle: 'medium', timeStyle: 'short' })}</span>
                </div>
              )}
              {order.notes && (
                <div className="flex items-start gap-2 text-sm text-gray-500">
                  <FileText size={14} className="text-gray-400 shrink-0 mt-0.5" />
                  <span className="italic">{order.notes}</span>
                </div>
              )}
            </div>

            {/* Ítems del pedido */}
            <div className="px-5 py-4">
              <div className="flex items-center gap-2 mb-3">
                <Package size={15} className="text-gray-400" />
                <p className="text-xs font-semibold text-gray-500 uppercase tracking-wide">
                  Productos ({order.items?.length ?? 0})
                </p>
              </div>

              {(!order.items || order.items.length === 0) ? (
                <p className="text-sm text-gray-400 text-center py-6">Sin productos</p>
              ) : (
                <div className="rounded-xl border border-gray-200 overflow-hidden">
                  {/* Cabecera */}
                  <div className="grid grid-cols-12 gap-2 px-3 py-2 bg-gray-50 text-xs font-semibold text-gray-400 uppercase">
                    <span className="col-span-5">Producto</span>
                    <span className="col-span-2 text-center">Cant.</span>
                    <span className="col-span-2 text-center">Costo</span>
                    <span className="col-span-3 text-right">Subtotal</span>
                  </div>
                  {order.items.map((item, idx) => {
                    const received = item.receivedQty ?? null;
                    const diffQty  = received !== null && received !== item.quantity;
                    return (
                      <div
                        key={item.id ?? idx}
                        className={`grid grid-cols-12 gap-2 px-3 py-2.5 text-sm items-center border-t border-gray-100 ${idx % 2 === 0 ? 'bg-white' : 'bg-gray-50/50'}`}
                      >
                        <div className="col-span-5 font-medium text-gray-800 truncate">{item.productName}</div>
                        <div className="col-span-2 text-center text-gray-600">
                          {item.quantity}
                          {diffQty && (
                            <span className="block text-xs text-green-600 font-medium">↳ {received} rec.</span>
                          )}
                        </div>
                        <div className="col-span-2 text-center text-gray-500 text-xs">${fmt(item.unitCost)}</div>
                        <div className="col-span-3 text-right font-semibold text-gray-900">${fmt(item.subtotal)}</div>
                      </div>
                    );
                  })}
                  {/* Total */}
                  <div className="grid grid-cols-12 gap-2 px-3 py-2.5 bg-gray-50 border-t border-gray-200">
                    <span className="col-span-9 text-sm font-semibold text-gray-700 text-right">Total</span>
                    <span className="col-span-3 text-right text-base font-bold text-gray-900">${fmt(order.total)}</span>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Footer con acciones */}
          <div className="px-5 py-4 border-t bg-gray-50 flex items-center justify-between gap-3 flex-shrink-0">
            {/* WhatsApp / Email */}
            <ContactActions
              supplier={{ name: order.supplierName, contactName: order.supplierName, phone: order.supplierPhone, email: order.supplierEmail }}
              orderItems={(order.items ?? []).map(i => ({ productName: i.productName, quantity: i.quantity, unitCost: i.unitCost }))}
              mode="order"
            />

            {/* Acciones del pedido */}
            {order.status === 'pending' && (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => onReceive(order)}
                  className="flex items-center gap-1.5 bg-green-600 hover:bg-green-700 text-white text-xs font-medium px-3 py-2 rounded-lg transition-colors"
                >
                  <CheckCircle2 size={13} /> Recibir
                </button>
                <button
                  onClick={() => onCancel(order.id)}
                  className="flex items-center gap-1.5 border border-red-300 text-red-600 hover:bg-red-50 text-xs font-medium px-3 py-2 rounded-lg transition-colors"
                >
                  <XCircle size={13} /> Cancelar
                </button>
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};

export default PurchaseOrderDetailPanel;
