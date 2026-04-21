import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  X, Truck, Loader2, Phone, Mail, MapPin, FileText, Edit2, Trash2,
  ShoppingCart, Clock, CheckCircle2, XCircle, ChevronRight
} from 'lucide-react';
import supplierService from '../../../services/supplierService';
import purchaseOrderService from '../../../services/purchaseOrderService';
import SupplierProductsManager from './SupplierProductsManager';
import SuggestedOrderPanel from './SuggestedOrderPanel';
import ContactActions from './ContactActions';

const STATUS = {
  pending:   { label: 'Pendiente', cls: 'bg-yellow-100 text-yellow-700', icon: Clock },
  ordered:   { label: 'Enviado',   cls: 'bg-blue-100 text-blue-700',     icon: ShoppingCart },
  received:  { label: 'Recibido',  cls: 'bg-green-100 text-green-700',   icon: CheckCircle2 },
  cancelled: { label: 'Cancelado', cls: 'bg-red-100 text-red-700',       icon: XCircle },
};

const SupplierDetailPanel = ({ supplierId, onClose, onEdit, onDelete, onOpenOrderDetail, onNewOrder }) => {
  const { data, isLoading } = useQuery({
    queryKey: ['supplier', supplierId],
    queryFn: () => supplierService.getById(supplierId),
    enabled: !!supplierId,
  });

  const { data: ordersData } = useQuery({
    queryKey: ['purchase-orders-supplier', supplierId],
    queryFn:  () => purchaseOrderService.getAll(supplierId),
    enabled:  !!supplierId,
  });

  const supplier = data?.data;
  const orders   = (ordersData?.data ?? []).slice(0, 20);

  return (
    <div className="fixed inset-y-0 right-0 z-40 w-full md:w-[820px] bg-white shadow-2xl border-l flex flex-col">

      {/* Header */}
      <div className="flex items-center gap-3 px-5 py-4 border-b flex-shrink-0">
        <Truck size={20} className="text-primary-600" />
        <h2 className="font-semibold text-gray-900 truncate flex-1">
          {supplier?.name ?? 'Proveedor'}
        </h2>
        <div className="flex items-center gap-1">
          {supplier && (
            <>
              <button onClick={() => onEdit(supplier)} className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-gray-100 rounded-lg transition-colors" title="Editar">
                <Edit2 size={16} />
              </button>
              <button onClick={() => onDelete(supplier)} className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors" title="Eliminar">
                <Trash2 size={16} />
              </button>
            </>
          )}
          <button onClick={onClose} className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors">
            <X size={18} />
          </button>
        </div>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center">
          <Loader2 size={22} className="animate-spin text-primary-500" />
        </div>
      ) : !supplier ? (
        <div className="flex-1 flex items-center justify-center text-gray-400 text-sm">Proveedor no encontrado</div>
      ) : (
        <div className="flex-1 overflow-hidden flex">

          {/* ── Columna izquierda: info + productos ── */}
          <div className="flex-1 overflow-y-auto border-r">
            <div className="px-5 py-4 space-y-5">

              {/* Info de contacto */}
              <div className="bg-gray-50 rounded-xl p-4 space-y-2">
                {supplier.contactName && (
                  <div className="flex items-center gap-2 text-sm text-gray-700">
                    <span className="font-medium w-20 shrink-0 text-gray-500">Contacto</span>
                    <span>{supplier.contactName}</span>
                  </div>
                )}
                {supplier.phone && (
                  <div className="flex items-center gap-2 text-sm text-gray-700">
                    <Phone size={13} className="text-gray-400 shrink-0" />
                    <span>{supplier.phone}</span>
                  </div>
                )}
                {supplier.email && (
                  <div className="flex items-center gap-2 text-sm text-gray-700">
                    <Mail size={13} className="text-gray-400 shrink-0" />
                    <span className="truncate">{supplier.email}</span>
                  </div>
                )}
                {supplier.address && (
                  <div className="flex items-center gap-2 text-sm text-gray-700">
                    <MapPin size={13} className="text-gray-400 shrink-0" />
                    <span>{supplier.address}</span>
                  </div>
                )}
                {supplier.notes && (
                  <div className="flex items-start gap-2 text-sm text-gray-600 pt-1">
                    <FileText size={13} className="text-gray-400 shrink-0 mt-0.5" />
                    <span className="italic">{supplier.notes}</span>
                  </div>
                )}
              </div>

              <div>
                <p className="text-sm font-semibold text-gray-700 mb-2">Contactar</p>
                <ContactActions supplier={supplier} />
              </div>

              <SuggestedOrderPanel supplier={supplier} onNewOrder={onNewOrder} />

              <SupplierProductsManager
                supplierId={supplierId}
                products={supplier.products ?? []}
                onNewOrder={onNewOrder}
                supplier={supplier}
              />
            </div>
          </div>

          {/* ── Columna derecha: pedidos del proveedor ── */}
          <div className="w-72 flex-shrink-0 flex flex-col bg-gray-50">
            <div className="flex items-center justify-between px-4 py-3 border-b bg-white">
              <div className="flex items-center gap-2">
                <ShoppingCart size={15} className="text-primary-600" />
                <p className="text-sm font-semibold text-gray-800">Pedidos</p>
                {orders.length > 0 && (
                  <span className="text-xs bg-primary-100 text-primary-700 rounded-full px-1.5">{orders.length}</span>
                )}
              </div>
              <button
                onClick={() => onNewOrder && onNewOrder(supplier)}
                className="flex items-center gap-1 text-xs text-primary-600 hover:text-primary-800 font-medium"
              >
                + Nuevo
              </button>
            </div>

            <div className="flex-1 overflow-y-auto">
              {orders.length === 0 ? (
                <div className="flex flex-col items-center justify-center py-12 text-gray-400 px-4 text-center">
                  <ShoppingCart size={28} className="mb-2 opacity-30" />
                  <p className="text-xs">Sin pedidos para este proveedor</p>
                  <button
                    onClick={() => onNewOrder && onNewOrder(supplier)}
                    className="mt-3 text-xs text-primary-600 hover:text-primary-800 font-medium"
                  >
                    + Crear primer pedido
                  </button>
                </div>
              ) : (
                <div className="divide-y divide-gray-100">
                  {orders.map(o => {
                    const badge    = STATUS[o.status] ?? STATUS.pending;
                    const BadgeIcon = badge.icon;
                    return (
                      <button
                        key={o.id}
                        onClick={() => onOpenOrderDetail && onOpenOrderDetail(o.id)}
                        className="w-full text-left px-4 py-3 hover:bg-white transition-colors flex items-start gap-2 group"
                      >
                        <div className="flex-1 min-w-0">
                          <p className="text-xs font-mono font-semibold text-gray-700 truncate">{o.orderNumber}</p>
                          <p className="text-xs text-gray-400 mt-0.5">
                            {new Date(o.createdAt).toLocaleDateString('es-CO')}
                          </p>
                          <div className="flex items-center gap-1.5 mt-1">
                            <span className={`inline-flex items-center gap-0.5 text-xs font-medium px-1.5 py-0.5 rounded-full ${badge.cls}`}>
                              <BadgeIcon size={9} /> {badge.label}
                            </span>
                            <span className="text-xs font-semibold text-gray-700">
                              ${Number(o.total).toLocaleString('es-CO')}
                            </span>
                          </div>
                        </div>
                        <ChevronRight size={14} className="text-gray-300 group-hover:text-gray-500 mt-1 shrink-0" />
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

        </div>
      )}
    </div>
  );
};

export default SupplierDetailPanel;
