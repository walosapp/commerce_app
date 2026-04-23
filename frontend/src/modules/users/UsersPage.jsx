import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Users, PlusCircle, Search, RefreshCw,
  UserCheck, UserX, Trash2, Edit2, ShieldCheck,
  Building2, ChevronDown, KeyRound
} from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import userService from '../../services/userService';
import adminService from '../../services/adminService';
import UserFormModal from './components/UserFormModal';

const ROLE_COLORS = {
  super_admin: 'bg-red-100 text-red-700',
  admin:       'bg-purple-100 text-purple-700',
  manager:     'bg-blue-100 text-blue-700',
  cashier:     'bg-green-100 text-green-700',
  waiter:      'bg-orange-100 text-orange-700',
  delivery:    'bg-cyan-100 text-cyan-700',
  default:     'bg-gray-100 text-gray-600',
};

const ResetPasswordModal = ({ user, onClose, onSave }) => {
  const [pw, setPw] = useState('');
  const [saving, setSaving] = useState(false);
  const handleSave = async () => {
    if (pw.length < 6) { toast.error('Mínimo 6 caracteres'); return; }
    setSaving(true);
    try { await onSave(pw); onClose(); } finally { setSaving(false); }
  };
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl">
        <h2 className="text-base font-bold text-gray-900 mb-1">Resetear contraseña</h2>
        <p className="text-sm text-gray-500 mb-4">{user.firstName} {user.lastName}</p>
        <input type="password" placeholder="Nueva contraseña (mín. 6 caracteres)" value={pw}
          onChange={e => setPw(e.target.value)} className="input w-full mb-4" />
        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-secondary text-sm">Cancelar</button>
          <button onClick={handleSave} disabled={saving} className="btn-primary text-sm">
            {saving ? 'Guardando...' : 'Guardar'}
          </button>
        </div>
      </div>
    </div>
  );
};

const UsersPage = () => {
  const { tenantId, user: currentUser } = useAuthStore();
  const isDev = currentUser?.role === 'dev';
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [filterCompanyId, setFilterCompanyId] = useState('');
  const [formModal, setFormModal] = useState(null);
  const [resetModal, setResetModal] = useState(null);

  const { data: tenantsData } = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: () => adminService.getTenants(),
    enabled: isDev,
  });
  const companies = tenantsData?.data ?? [];

  const { data, isLoading, refetch } = useQuery({
    queryKey: isDev ? ['admin-users', filterCompanyId] : ['users', tenantId],
    queryFn: isDev
      ? () => userService.adminGetAll(filterCompanyId || undefined)
      : () => userService.getAll(),
    enabled: !!tenantId,
  });

  const allUsers = data?.data ?? [];
  const users = allUsers.filter(u => {
    if (!search.trim()) return true;
    const term = search.toLowerCase();
    return (
      `${u.firstName} ${u.lastName}`.toLowerCase().includes(term) ||
      u.email?.toLowerCase().includes(term) ||
      u.roleName?.toLowerCase().includes(term) ||
      u.companyName?.toLowerCase().includes(term)
    );
  });

  const invalidate = () => queryClient.invalidateQueries({
    queryKey: isDev ? ['admin-users'] : ['users', tenantId]
  });

  const handleSave = async (form) => {
    if (formModal?.user) {
      if (isDev) {
        await userService.adminUpdate(formModal.user.id, formModal.user.companyId, form);
      } else {
        await userService.update(formModal.user.id, form);
      }
      toast.success('Usuario actualizado');
    } else {
      if (isDev) {
        await userService.adminCreate(form);
      } else {
        await userService.create(form);
      }
      toast.success('Usuario creado');
    }
    invalidate();
  };

  const handleToggleStatus = async (u) => {
    try {
      if (isDev) {
        await userService.adminSetStatus(u.id, u.companyId, !u.isActive);
      } else {
        await userService.setStatus(u.id, !u.isActive);
      }
      toast.success(u.isActive ? 'Usuario desactivado' : 'Usuario activado');
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al cambiar estado');
    }
  };

  const handleDelete = async (u) => {
    if (!window.confirm(`¿Eliminar a "${u.firstName} ${u.lastName}"? Esta acción no se puede deshacer.`)) return;
    try {
      if (isDev) {
        await userService.adminDelete(u.id, u.companyId);
      } else {
        await userService.delete(u.id);
      }
      toast.success('Usuario eliminado');
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al eliminar usuario');
    }
  };

  const handleResetPassword = async (u, newPassword) => {
    await userService.adminResetPassword(u.id, u.companyId, newPassword);
    toast.success('Contraseña actualizada');
  };

  const activeCount = users.filter(u => u.isActive).length;
  const inactiveCount = users.filter(u => !u.isActive).length;

  return (
    <div className="flex flex-col -m-4 h-[calc(100%+2rem)] overflow-hidden">
      {resetModal && (
        <ResetPasswordModal
          user={resetModal}
          onClose={() => setResetModal(null)}
          onSave={(pw) => handleResetPassword(resetModal, pw)}
        />
      )}

      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-indigo-100 flex items-center justify-center">
            <Users size={22} className="text-indigo-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">
              {isDev ? 'Usuarios — Todos los comercios' : 'Usuarios'}
            </h1>
            <p className="text-sm text-gray-500">
              {activeCount} activo{activeCount !== 1 ? 's' : ''}
              {inactiveCount > 0 && ` · ${inactiveCount} inactivo${inactiveCount !== 1 ? 's' : ''}`}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          {isDev && (
            <div className="relative">
              <Building2 size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
              <select
                value={filterCompanyId}
                onChange={e => setFilterCompanyId(e.target.value)}
                className="pl-8 pr-7 py-2 border border-gray-300 rounded-lg text-sm appearance-none bg-white focus:outline-none focus:ring-2 focus:ring-primary-500"
              >
                <option value="">Todos los comercios</option>
                {companies.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
              <ChevronDown size={13} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
            </div>
          )}
          <div className="relative">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              placeholder="Buscar usuario..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="pl-8 pr-4 py-2 border border-gray-300 rounded-lg text-sm w-48 focus:outline-none focus:ring-2 focus:ring-primary-500"
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
                  {['Usuario', ...(isDev ? ['Comercio'] : []), 'Email', 'Rol', 'Estado', 'Último acceso', 'Acciones'].map(h => (
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
                      {isDev && <td className="px-4 py-3 text-sm font-medium text-gray-700">{u.companyName}</td>}
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
                                onClick={() => setResetModal(u)}
                                className="p-1.5 text-blue-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg transition-colors"
                                title="Resetear contraseña"
                              >
                                <KeyRound size={15} />
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
          companies={companies}
          onSave={handleSave}
          onClose={() => setFormModal(null)}
        />
      )}
    </div>
  );
};

export default UsersPage;
