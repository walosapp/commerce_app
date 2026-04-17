import { useState } from 'react';
import { X, PackageCheck, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import purchaseOrderService from '../../../services/purchaseOrderService';

const ReceiveOrderModal = ({ isOpen, order, onClose, onSaved }) => {
  const [items, setItems] = useState([]);
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);

  // Init items from order when it opens
  useState(() => {
    if (order?.items) {
      setItems(order.items.map(i => ({ orderItemId: i.id, receivedQty: i.quantity, productName: i.productName, ordered: i.quantity })));
    }
  }, [order]);

  if (!isOpen || !order) return null;

  const displayItems = order.items.map(i => {
    const local = items.find(x => x.orderItemId === i.id);
    return { ...i, receivedQty: local?.receivedQty ?? i.quantity };
  });

  const setQty = (id, val) => {
    setItems(prev => {
      const existing = prev.find(x => x.orderItemId === id);
      if (existing) return prev.map(x => x.orderItemId === id ? { ...x, receivedQty: val } : x);
      return [...prev, { orderItemId: id, receivedQty: val }];
    });
  };

  const handleReceive = async () => {
    setSaving(true);
    try {
      await purchaseOrderService.receive(order.id, {
        notes,
        items: displayItems.map(i => ({ orderItemId: i.id, receivedQty: Number(i.receivedQty) })),
      });
      toast.success('Pedido recibido. Stock actualizado.');
      onSaved();
      onClose();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al recibir pedido');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg flex flex-col max-h-[85vh]">
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <div className="flex items-center gap-2">
            <PackageCheck size={18} className="text-green-600" />
            <h2 className="text-base font-semibold text-gray-900">Recibir Pedido #{order.orderNumber}</h2>
          </div>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-gray-100"><X size={18} /></button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          <p className="text-sm text-gray-500">
            Confirma las cantidades recibidas. Se descontará del proveedor y se sumará al stock de tu sucursal.
          </p>

          <div className="space-y-2">
            <div className="grid grid-cols-12 gap-2 text-xs font-semibold text-gray-400 uppercase px-1">
              <span className="col-span-6">Producto</span>
              <span className="col-span-3 text-center">Pedido</span>
              <span className="col-span-3 text-center">Recibido</span>
            </div>
            {order.items.map(item => {
              const rcv = displayItems.find(d => d.id === item.id)?.receivedQty ?? item.quantity;
              return (
                <div key={item.id} className="grid grid-cols-12 gap-2 items-center bg-gray-50 border border-gray-200 rounded-xl px-3 py-2.5">
                  <span className="col-span-6 text-sm font-medium text-gray-800 truncate">{item.productName}</span>
                  <span className="col-span-3 text-center text-sm text-gray-500">{item.quantity}</span>
                  <div className="col-span-3">
                    <input
                      type="number" min="0" step="0.001"
                      className="w-full text-center border border-gray-300 rounded-lg px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-green-400"
                      value={rcv}
                      onChange={e => setQty(item.id, e.target.value)}
                    />
                  </div>
                </div>
              );
            })}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notas de recepcion</label>
            <textarea
              className="input min-h-[60px] resize-y text-sm"
              placeholder="Ej: llegaron 2 productos danados..."
              value={notes}
              onChange={e => setNotes(e.target.value)}
            />
          </div>
        </div>

        <div className="flex justify-end gap-3 px-6 py-4 border-t bg-gray-50 rounded-b-2xl">
          <button type="button" onClick={onClose} className="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg">
            Cancelar
          </button>
          <button
            onClick={handleReceive}
            disabled={saving}
            className="flex items-center gap-2 px-5 py-2 text-sm font-medium text-white bg-green-600 hover:bg-green-700 disabled:opacity-50 rounded-lg transition-colors"
          >
            {saving ? <Loader2 size={14} className="animate-spin" /> : <PackageCheck size={14} />}
            {saving ? 'Procesando...' : 'Confirmar Recepcion'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ReceiveOrderModal;
