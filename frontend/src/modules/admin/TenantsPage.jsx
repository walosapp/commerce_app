import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Building2, PlusCircle, Search, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import adminService from '../../services/adminService';
import TenantCard from './components/TenantCard';
import CreateTenantModal from './components/CreateTenantModal';
import EditTenantModal from './components/EditTenantModal';

const TenantsPage = () => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [editingTenant, setEditingTenant] = useState(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: () => adminService.getTenants(),
  });

  const tenants = (data?.data ?? []).filter(t => {
    if (!search.trim()) return true;
    const q = search.toLowerCase();
    return (
      t.name?.toLowerCase().includes(q) ||
      t.taxId?.toLowerCase().includes(q) ||
      t.email?.toLowerCase().includes(q)
    );
  });

  const handleCreate = async (formData) => {
    await adminService.createTenant(formData);
    toast.success(`Comercio "${formData.companyName}" creado exitosamente`);
    queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
  };

  const handleUpdate = async (id, formData) => {
    await adminService.updateTenant(id, formData);
    toast.success('Comercio actualizado');
    queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
  };

  const handleToggleStatus = async (tenant) => {
    try {
      await adminService.setTenantStatus(tenant.id, !tenant.isActive);
      toast.success(tenant.isActive ? 'Comercio desactivado' : 'Comercio activado');
      queryClient.invalidateQueries({ queryKey: ['admin-tenants'] });
    } catch {
      toast.error('No se pudo cambiar el estado del comercio');
    }
  };

  return (
    <div className="p-4 md:p-6 space-y-5">
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-indigo-100 flex items-center justify-center">
            <Building2 size={22} className="text-indigo-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Comercios</h1>
            <p className="text-sm text-gray-500">Gestión de tenants del sistema</p>
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
            onClick={() => setModalOpen(true)}
            className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
          >
            <PlusCircle size={18} />
            Nuevo Comercio
          </button>
        </div>
      </div>

      <div className="relative max-w-sm">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          placeholder="Buscar por nombre, NIT o email..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
        />
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[...Array(3)].map((_, i) => (
            <div key={i} className="bg-white rounded-xl border shadow-sm p-5 animate-pulse h-44" />
          ))}
        </div>
      ) : tenants.length === 0 ? (
        <div className="text-center py-16 text-gray-400">
          <Building2 size={48} className="mx-auto mb-3 opacity-30" />
          <p className="text-sm">{search ? 'Sin resultados para la búsqueda' : 'No hay comercios registrados'}</p>
          {!search && (
            <button
              onClick={() => setModalOpen(true)}
              className="mt-4 text-sm text-indigo-600 hover:underline"
            >
              Crear el primer comercio
            </button>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {tenants.map(tenant => (
            <TenantCard
              key={tenant.id}
              tenant={tenant}
              onToggleStatus={handleToggleStatus}
              onEdit={setEditingTenant}
            />
          ))}
        </div>
      )}

      <CreateTenantModal
        isOpen={modalOpen}
        onClose={() => setModalOpen(false)}
        onCreated={handleCreate}
      />

      <EditTenantModal
        tenant={editingTenant}
        onClose={() => setEditingTenant(null)}
        onSaved={handleUpdate}
      />
    </div>
  );
};

export default TenantsPage;
