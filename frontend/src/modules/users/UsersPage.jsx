import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Users, PlusCircle, Search, RefreshCw,
  UserCheck, UserX, Trash2, Edit2, ShieldCheck
} from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import userService from '../../services/userService';
import UserFormModal from './components/UserFormModal';

const ROLE_COLORS = {
  admin:   'bg-purple-100 text-purple-700',
  manager: 'bg-blue-100 text-blue-700',
  cashier: 'bg-green-100 text-green-700',
  waiter:  'bg-orange-100 text-orange-700',
  default: 'bg-gray-100 text-gray-600',
};

const UsersPage = () => {
  const { tenantId, user: currentUser } = useAuthStore();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [formModal, setFormModal] = useState(null);

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['users', tenantId],
    queryFn: () => userService.getAll(),
    enabled: !!tenantId,
  });

  const users = (data?.data ?? []).filter(u =>
    !search.trim() ||
    `${u.firstName} ${u.lastName}`.toLowerCase().includes(search.toLowerCase()) ||
    u.email?.toLowerCase().includes(search.toLowerCase()) ||
    u.roleName?.toLowerCase().includes(search.toLowerCase())
  );

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['users', tenantId] });

  const handleSave = async (form) => {
    if (formModal?.user) {
      await userService.update(formModal.user.id, form);
      toast.success('Usuario actualizado');
    } else {
      await userService.create(form);
      toast.success('Usuario creado');
    }
    invalidate();
  };

  const handleToggleStatus = async (user) => {
    try {
      await userService.setStatus(user.id, !user.isActive);
      toast.success(user.isActive ? 'Usuario desactivado' : 'Usuario activado');
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al cambiar estado');
    }
  };

  const handleDelete = async (user) => {
    if (!window.confirm(`¿Eliminar a "${user.firstName} ${user.lastName}"? Esta acción no se puede deshacer.`)) return;
    try {
      await userService.delete(user.id);
      toast.success('Usuario eliminado');
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al eliminar usuario');
    }
  };

  const activeCount = users.filter(u => u.isActive).length;
  const inactiveCount = users.filter(u => !u.isActive).length;

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)]">
      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-indigo-100 flex items-center justify-center">
            <Users size={22} className="text-indigo-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Usuarios</h1>
            <p className="text-sm text-gray-500">
              {activeCount} activo{activeCount !== 1 ? 's' : ''}
              {inactiveCount > 0 && ` · ${inactiveCount} inactivo${inactiveCount !== 1 ? 's' : ''}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <div className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              placeholder="Buscar usuario..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-8 pr-4 py-2 border border-gray-300 rounded-lg text-sm w-52 focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <button onClick={() => refetch()} className="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors" title="Actualizar">
            <RefreshCw size={18} />
          </button>
          <button
            onClick={() => setFormModal({})}
            className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
          >
            <PlusCircle size={18} /> Nuevo usuario
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto p-4 md:p-6">
        {isLoading ? (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="h-14 bg-gray-100 rounded-xl animate-pulse" />
            ))}
          </div>
        ) : users.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-20 text-gray-400">
            <Users size={48} className="mb-3 opacity-30" />
            <p className="text-lg font-medium">
              {search ? 'Sin resultados' : 'Sin usuarios registrados'}
            </p>
            {!search && (
              <button onClick={() => setFormModal({})} className="mt-4 text-primary-600 hover:text-primary-800 text-sm font-medium">
                + Crear primer usuario
              </button>
            )}
          </div>
        ) : (
          <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  {['Usuario', 'Email', 'Rol', 'Estado', 'Último acceso', 'Acciones'].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {users.map(u => {
                  const isSelf = u.id === currentUser?.id || u.email === currentUser?.email;
                  const roleBadge = ROLE_COLORS[u.roleCode] ?? ROLE_COLORS.default;
                  return (
                    <tr key={u.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-3">
                          <div className="w-9 h-9 rounded-full bg-indigo-100 flex items-center justify-center shrink-0 text-indigo-700 font-semibold text-sm">
                            {u.firstName?.[0]}{u.lastName?.[0]}
                          </div>
                          <div>
                            <p className="font-medium text-gray-900 text-sm">
                              {u.firstName} {u.lastName}
                              {isSelf && <span className="ml-2 text-xs text-gray-400">(tú)</span>}
                            </p>
                            {u.branchName && <p className="text-xs text-gray-500">{u.branchName}</p>}
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-600">{u.email}</td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-1.5">
                          <ShieldCheck size={13} className="text-gray-400" />
                          <span className={`text-xs font-medium px-2 py-0.5 rounded-full ${roleBadge}`}>
                            {u.roleName ?? u.roleCode}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex items-center gap-1 text-xs font-medium px-2.5 py-1 rounded-full ${
                          u.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'
                        }`}>
                          {u.isActive ? <UserCheck size={12} /> : <UserX size={12} />}
                          {u.isActive ? 'Activo' : 'Inactivo'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {u.lastLoginAt
                          ? new Date(u.lastLoginAt).toLocaleDateString('es-CO')
                          : <span className="text-gray-300">Nunca</span>}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <button
                            onClick={() => setFormModal({ user: u })}
                            className="p-1.5 text-gray-400 hover:text-primary-600 hover:bg-primary-50 rounded-lg transition-colors"
                            title="Editar"
                          >
                            <Edit2 size={15} />
                          </button>
                          {!isSelf && (
                            <>
                              <button
                                onClick={() => handleToggleStatus(u)}
                                className={`p-1.5 rounded-lg transition-colors ${
                                  u.isActive
                                    ? 'text-gray-400 hover:text-orange-500 hover:bg-orange-50'
                                    : 'text-gray-400 hover:text-green-500 hover:bg-green-50'
                                }`}
                                title={u.isActive ? 'Desactivar' : 'Activar'}
                              >
                                {u.isActive ? <UserX size={15} /> : <UserCheck size={15} />}
                              </button>
                              <button
                                onClick={() => handleDelete(u)}
                                className="p-1.5 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors"
                                title="Eliminar"
                              >
                                <Trash2 size={15} />
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {formModal !== null && (
        <UserFormModal
          user={formModal.user ?? null}
          onSave={handleSave}
          onClose={() => setFormModal(null)}
        />
      )}
    </div>
  );
};

export default UsersPage;
