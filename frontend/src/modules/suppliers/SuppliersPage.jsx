import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Truck, PlusCircle, Search, RefreshCw, Phone, Mail, Package, ShoppingCart, PackageCheck, XCircle, Clock, CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import supplierService from '../../services/supplierService';
import SupplierFormModal from './components/SupplierFormModal';
import SupplierDetailPanel from './components/SupplierDetailPanel';
import PurchaseOrderModal from './components/PurchaseOrderModal';
import ReceiveOrderModal from './components/ReceiveOrderModal';
import purchaseOrderService from '../../services/purchaseOrderService';

const SuppliersPage = () => {
  const { tenantId } = useAuthStore();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [selectedId, setSelectedId] = useState(null);
  const [formModal, setFormModal] = useState(null);
  const [activeTab, setActiveTab] = useState('suppliers'); // 'suppliers' | 'orders'
  const [showOrderModal, setShowOrderModal] = useState(false);
  const [receiveOrder, setReceiveOrder] = useState(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['suppliers', tenantId],
    queryFn: () => supplierService.getAll(),
    enabled: !!tenantId,
  });

  const suppliers = (data?.data ?? []).filter(s =>
    !search.trim() ||
    s.name?.toLowerCase().includes(search.toLowerCase()) ||
    s.contactName?.toLowerCase().includes(search.toLowerCase()) ||
    s.phone?.includes(search) ||
    s.email?.toLowerCase().includes(search.toLowerCase())
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['suppliers', tenantId] });

  const handleSave = async (form) => {
    if (formModal?.supplier) {
      await supplierService.update(formModal.supplier.id, form);
      toast.success('Proveedor actualizado');
      queryClient.invalidateQueries({ queryKey: ['supplier', formModal.supplier.id] });
    } else {
      await supplierService.create(form);
      toast.success('Proveedor creado');
    }
    invalidate();
  };

  const handleDelete = async (supplier) => {
    if (!window.confirm(`¿Eliminar a "${supplier.name}"?`)) return;
    try {
      await supplierService.delete(supplier.id);
      toast.success('Proveedor eliminado');
      setSelectedId(null);
      invalidate();
    } catch {
      toast.error('Error al eliminar proveedor');
    }
  };

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)]">
      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-blue-100 flex items-center justify-center">
            <Truck size={22} className="text-blue-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Proveedores</h1>
            <p className="text-sm text-gray-500">
              {suppliers.length} proveedor{suppliers.length !== 1 ? 'es' : ''}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              placeholder="Buscar..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-8 pr-4 py-2 border border-gray-300 rounded-lg text-sm w-52 focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <button onClick={() => refetch()} className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors" title="Actualizar">
            <RefreshCw size={18} />
          </button>
          {activeTab === 'suppliers' ? (
            <button onClick={() => setFormModal({})} className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              <PlusCircle size={18} /> Nuevo Proveedor
            </button>
          ) : (
            <button onClick={() => setShowOrderModal(true)} className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              <ShoppingCart size={18} /> Nuevo Pedido
            </button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b bg-white px-6">
        {[{k:'suppliers',l:'Proveedores',i:Truck},{k:'orders',l:'Pedidos',i:ShoppingCart}].map(({k,l,i:Icon}) => (
          <button key={k} onClick={() => setActiveTab(k)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${activeTab===k ? 'border-primary-600 text-primary-600' : 'border-transparent text-gray-500 hover:text-gray-700'}`}>
            <Icon size={16}/>{l}
            {k==='orders' && orders.length > 0 && <span className="ml-1 bg-primary-100 text-primary-700 text-xs rounded-full px-1.5">{orders.length}</span>}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-auto p-4 md:p-6">
        {isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {[...Array(6)].map((_, i) => (
              <div key={i} className="h-32 bg-gray-100 rounded-xl animate-pulse" />
            ))}
          </div>
        ) : suppliers.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-gray-400">
            <Truck size={48} className="mb-3 opacity-30" />
            <p className="text-lg font-medium">
              {search ? 'Sin resultados para tu búsqueda' : 'Sin proveedores registrados'}
            </p>
            {!search && (
              <button
                onClick={() => setFormModal({})}
                className="mt-4 text-primary-600 hover:text-primary-800 text-sm font-medium"
              >
                + Agregar primer proveedor
              </button>
            )}
          </div>
        ) : activeTab === 'suppliers' ? (
          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  {['Nombre', 'Contacto', 'Teléfono', 'Email', 'Productos', 'Acciones'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {suppliers.map(s => (
                  <tr
                    key={s.id}
                    onClick={() => setSelectedId(s.id)}
                    className={`hover:bg-primary-50 cursor-pointer transition-colors ${selectedId === s.id ? 'bg-primary-50' : ''}`}
                  >
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5">
                        <div className="w-8 h-8 rounded-lg bg-blue-100 flex items-center justify-center shrink-0">
                          <Truck size={15} className="text-blue-600" />
                        </div>
                        <span className="font-medium text-gray-900 text-sm">{s.name}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-sm text-gray-600">{s.contactName ?? '—'}</td>
                    <td className="px-4 py-3">
                      {s.phone
                        ? <div className="flex items-center gap-1 text-sm text-gray-600"><Phone size={12} />{s.phone}</div>
                        : <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      {s.email
                        ? <div className="flex items-center gap-1 text-sm text-gray-600"><Mail size={12} /><span className="truncate max-w-[160px]">{s.email}</span></div>
                        : <span className="text-gray-400">—</span>}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-1 text-sm text-gray-600">
                        <Package size={13} />
                        <span>{s.productCount ?? 0}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3" onClick={e => e.stopPropagation()}>
                      <button
                        onClick={() => setFormModal({ supplier: s })}
                        className="text-xs text-primary-600 hover:text-primary-800 font-medium mr-3"
                      >
                        Editar
                      </button>
                      <button
                        onClick={() => handleDelete(s)}
                        className="text-xs text-red-500 hover:text-red-700 font-medium"
                      >
                        Eliminar
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

        ) : (
          /* ORDERS TAB */
          <div className="space-y-3">
            {ordersLoading ? (
              <div className="flex items-center gap-2 text-gray-400 py-8 justify-center"><RefreshCw size={16} className="animate-spin" /> Cargando...</div>
            ) : orders.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-20 text-gray-400">
                <ShoppingCart size={48} className="mb-3 opacity-30" />
                <p className="text-lg font-medium">Sin pedidos registrados</p>
                <button onClick={() => setShowOrderModal(true)} className="mt-4 text-primary-600 hover:text-primary-800 text-sm font-medium">+ Crear primer pedido</button>
              </div>
            ) : (
              <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
                <table className="min-w-full divide-y divide-gray-200">
                  <thead className="bg-gray-50">
                    <tr>
                      {['#Pedido','Proveedor','Fecha esperada','Total','Estado','Acciones'].map(h => (
                        <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {orders.map(o => {
                      const badge = STATUS_BADGE[o.status] ?? STATUS_BADGE.pending;
                      const BadgeIcon = badge.icon;
                      return (
                        <tr key={o.id} className="hover:bg-gray-50 transition-colors">
                          <td className="px-4 py-3 text-sm font-mono font-medium text-gray-900">{o.orderNumber}</td>
                          <td className="px-4 py-3 text-sm text-gray-700">{o.supplierName}</td>
                          <td className="px-4 py-3 text-sm text-gray-500">{o.expectedDate ? new Date(o.expectedDate).toLocaleDateString('es-CO') : '—'}</td>
                          <td className="px-4 py-3 text-sm font-semibold text-gray-900">${o.total.toLocaleString('es-CO')}</td>
                          <td className="px-4 py-3">
                            <span className={`inline-flex items-center gap-1 text-xs font-medium px-2.5 py-1 rounded-full ${badge.cls}`}>
                              <BadgeIcon size={11}/>{badge.label}
                            </span>
                          </td>
                          <td className="px-4 py-3 flex items-center gap-2">
                            {o.status === 'pending' && (
                              <>
                                <button onClick={() => { purchaseOrderService.getById(o.id).then(r => setReceiveOrder(r.data)); }} className="text-xs text-green-600 hover:text-green-800 font-medium flex items-center gap-1"><PackageCheck size={13}/>Recibir</button>
                                <button onClick={() => handleCancelOrder(o.id)} className="text-xs text-red-500 hover:text-red-700 font-medium flex items-center gap-1"><XCircle size={13}/>Cancelar</button>
                              </>
                            )}
                            {o.status === 'received' && <span className="text-xs text-gray-400">Completado</span>}
                            {o.status === 'cancelled' && <span className="text-xs text-gray-400">Cancelado</span>}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </div>

      {selectedId && (
        <SupplierDetailPanel
          supplierId={selectedId}
          onClose={() => setSelectedId(null)}
          onEdit={(s) => { setFormModal({ supplier: s }); }}
          onDelete={handleDelete}
        />
      )}


      <PurchaseOrderModal
        isOpen={showOrderModal}
        onClose={() => setShowOrderModal(false)}
        onSaved={refetchOrders}
        suppliers={suppliersData?.data ?? []}
      />

      <ReceiveOrderModal
        isOpen={!!receiveOrder}
        order={receiveOrder}
        onClose={() => setReceiveOrder(null)}
        onSaved={() => { refetchOrders(); queryClient.invalidateQueries({ queryKey: ['stock'] }); }}
      />

      {formModal !== null && (
        <SupplierFormModal
          supplier={formModal.supplier ?? null}
          onSave={handleSave}
          onClose={() => setFormModal(null)}
        />
      )}
    </div>
  );
};

export default SuppliersPage;





