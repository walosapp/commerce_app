import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Bike, PlusCircle, RefreshCw, CheckCircle2, XCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import deliveryService from '../../services/deliveryService';
import DeliveryBoard from './components/DeliveryBoard';
import DeliveryOrderDetailsPanel from './components/DeliveryOrderDetailsPanel';
import CreateDeliveryOrderPanel from './components/CreateDeliveryOrderPanel';
import StatusActionModal from './components/StatusActionModal';

const IMMEDIATE_ACTIONS = ['accept', 'prepare', 'ready', 'dispatch', 'deliver'];
const COMMENT_ACTIONS   = ['reject', 'cancel', 'return'];

const DeliveryOrdersPage = () => {
  const { branchId, tenantId } = useAuthStore();
  const queryClient = useQueryClient();

  const [selectedOrderId, setSelectedOrderId] = useState(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [actionModal, setActionModal] = useState(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['delivery-orders', branchId, tenantId],
    queryFn: () => deliveryService.getOrders(),
    enabled: !!branchId && !!tenantId,
    refetchInterval: 30000,
  });

  const orders = data?.data ?? [];
  const activeOrders = orders.filter(o =>
    !['delivered', 'rejected', 'cancelled', 'returned'].includes(o.status)
  );
  const doneOrders = orders.filter(o =>
    ['delivered', 'rejected', 'cancelled', 'returned'].includes(o.status)
  );

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['delivery-orders', branchId, tenantId] });
    if (selectedOrderId) {
      queryClient.invalidateQueries({ queryKey: ['delivery-order', selectedOrderId] });
    }
  };

  const handleAction = async (order, action) => {
    if (COMMENT_ACTIONS.includes(action)) {
      setActionModal({ order, action });
      return;
    }
    try {
      await deliveryService[action](order.id);
      toast.success(`Pedido ${action === 'accept' ? 'aceptado' : action === 'prepare' ? 'en preparación' : action === 'ready' ? 'marcado listo' : action === 'dispatch' ? 'despachado' : 'entregado'}`);
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al actualizar el pedido');
    }
  };

  const handleModalConfirm = async (comment) => {
    const { order, action } = actionModal;
    await deliveryService[action](order.id, comment);
    toast.success('Estado actualizado');
    invalidate();
  };

  const handleCreate = async (formData) => {
    await deliveryService.createOrder(formData);
    toast.success('Pedido creado');
    invalidate();
  };

  return (
    <div className="flex flex-col -m-4 h-[calc(100%+2rem)] overflow-hidden">
      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-orange-100 flex items-center justify-center">
            <Bike size={22} className="text-orange-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Pedidos y Domicilios</h1>
            <p className="text-sm text-gray-500">
              {activeOrders.length} activo{activeOrders.length !== 1 ? 's' : ''}
              {doneOrders.length > 0 && ` · ${doneOrders.length} finalizado${doneOrders.length !== 1 ? 's' : ''} hoy`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => refetch()}
            className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
            title="Actualizar"
          >
            <RefreshCw size={18} />
          </button>
          <button
            onClick={() => setCreateOpen(true)}
            className="flex items-center gap-2 bg-orange-500 hover:bg-orange-600 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
          >
            <PlusCircle size={18} /> Nuevo Pedido
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-4 md:p-6">
        {isLoading ? (
          <div className="flex gap-4">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="flex-shrink-0 w-64 h-64 bg-gray-100 rounded-xl animate-pulse" />
            ))}
          </div>
        ) : (
          <DeliveryBoard
            orders={activeOrders}
            onOrderClick={o => setSelectedOrderId(o.id)}
            onAction={handleAction}
          />
        )}

        {doneOrders.length > 0 && !isLoading && (
          <div className="mt-6">
            <p className="text-sm font-medium text-gray-500 mb-3">Finalizados hoy</p>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3">
              {doneOrders.map(o => (
                <div
                  key={o.id}
                  onClick={() => setSelectedOrderId(o.id)}
                  className="bg-white border border-gray-200 rounded-xl p-3.5 cursor-pointer hover:shadow-sm transition-all flex items-center gap-3 opacity-70 hover:opacity-100"
                >
                  {o.status === 'delivered'
                    ? <CheckCircle2 size={18} className="text-green-500 shrink-0" />
                    : <XCircle size={18} className="text-red-400 shrink-0" />
                  }
                  <div className="min-w-0 flex-1">
                    <p className="text-sm font-semibold text-gray-800">#{o.orderNumber}</p>
                    <p className="text-xs text-gray-500 truncate">{o.customerName || '—'}</p>
                  </div>
                  <span className="text-sm font-bold text-gray-700">${Number(o.total).toLocaleString('es-CO')}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {selectedOrderId && (
        <DeliveryOrderDetailsPanel
          orderId={selectedOrderId}
          onClose={() => setSelectedOrderId(null)}
          onAction={(order, action) => {
            setSelectedOrderId(null);
            handleAction(order, action);
          }}
        />
      )}

      <CreateDeliveryOrderPanel
        isOpen={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={handleCreate}
      />

      <StatusActionModal
        action={actionModal?.action}
        onConfirm={handleModalConfirm}
        onClose={() => setActionModal(null)}
      />
    </div>
  );
};

export default DeliveryOrdersPage;
