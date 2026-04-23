import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Users, Search, RefreshCw, UserCheck, UserX,
  KeyRound, Building2, ChevronDown
} from 'lucide-react';
import toast from 'react-hot-toast';
import userService from '../../services/userService';
import adminService from '../../services/adminService';

const ROLE_COLORS = {
  admin:   'bg-purple-100 text-purple-700',
  manager: 'bg-blue-100 text-blue-700',
  cashier: 'bg-green-100 text-green-700',
  waiter:  'bg-orange-100 text-orange-700',
  default: 'bg-gray-100 text-gray-600',
};

const ResetPasswordModal = ({ user, onClose, onSave }) => {
  const [pw, setPw] = useState('');
  const [saving, setSaving] = useState(false);

  const handleSave = async () => {
    if (pw.length < 6) { toast.error('Mínimo 6 caracteres'); return; }
    setSaving(true);
    try {
      await onSave(pw);
      onClose();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl">
        <h2 className="text-base font-bold text-gray-900 mb-1">Resetear contraseña</h2>
        <p className="text-sm text-gray-500 mb-4">
          {user.firstName} {user.lastName} — <span className="font-medium">{user.companyName}</span>
        </p>
        <input
          type="password"
          placeholder="Nueva contraseña (mín. 6 caracteres)"
          value={pw}
          onChange={e => setPw(e.target.value)}
          className="input w-full mb-4"
        />
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

const AdminUsersPage = () => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [filterCompanyId, setFilterCompanyId] = useState('');
  const [resetModal, setResetModal] = useState(null);

  const { data: tenantsData } = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: () => adminService.getTenants(),
  });

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['admin-users', filterCompanyId],
    queryFn: () => userService.adminGetAll(filterCompanyId || undefined),
  });

  const companies = tenantsData?.data ?? [];
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

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin-users'] });

  const handleToggleStatus = async (user) => {
    try {
      await userService.adminSetStatus(user.id, user.companyId, !user.isActive);
      toast.success(user.isActive ? 'Usuario desactivado' : 'Usuario activado');
      invalidate();
    } catch (e) {
      toast.error(e?.response?.data?.message || 'Error al cambiar estado');
    }
  };

  const handleResetPassword = async (user, newPassword) => {
    await userService.adminResetPassword(user.id, user.companyId, newPassword);
    toast.success('Contraseña actualizada');
  };

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
            <h1 className="text-xl font-bold text-gray-900">Usuarios — Todos los comercios</h1>
            <p className="text-sm text-gray-500">{users.length} usuario{users.length !== 1 ? 's' : ''}</p>
          </div>
        </div>

        <div className="flex items-center gap-2 flex-wrap">
          <div className="relative">
            <Building2 size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <select
              value={filterCompanyId}
              onChange={e => setFilterCompanyId(e.target.value)}
              className="pl-8 pr-8 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500 appearance-none bg-white"
            >
              <option value="">Todos los comercios</option>
              {companies.map(c => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
            <ChevronDown size={14} className="absolute right-2 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none" />
          </div>

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

          <button onClick={() => refetch()} className="p-2 rounded-lg border border-gray-300 hover:bg-gray-50 text-gray-500">
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      <div className="flex-1 overflow-auto">
        {isLoading ? (
          <div className="flex items-center justify-center h-40 text-gray-400 text-sm">Cargando usuarios...</div>
        ) : users.length === 0 ? (
          <div className="flex items-center justify-center h-40 text-gray-400 text-sm">Sin resultados</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50 text-xs text-gray-500 uppercase tracking-wide sticky top-0">
              <tr>
                <th className="text-left px-4 py-3">Usuario</th>
                <th className="text-left px-4 py-3">Comercio</th>
                <th className="text-left px-4 py-3">Rol</th>
                <th className="text-left px-4 py-3">Sucursal</th>
                <th className="text-left px-4 py-3">Estado</th>
                <th className="text-right px-4 py-3">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {users.map(u => (
                <tr key={u.id} className="hover:bg-gray-50 transition-colors">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{u.firstName} {u.lastName}</div>
                    <div className="text-xs text-gray-500">{u.email}</div>
                  </td>
                  <td className="px-4 py-3 text-gray-700 font-medium">{u.companyName}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${ROLE_COLORS[u.roleCode] || ROLE_COLORS.default}`}>
                      {u.roleName}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500">{u.branchName || '—'}</td>
                  <td className="px-4 py-3">
                    <span className={`inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-semibold ${
                      u.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'
                    }`}>
                      {u.isActive ? <UserCheck size={11} /> : <UserX size={11} />}
                      {u.isActive ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        onClick={() => handleToggleStatus(u)}
                        title={u.isActive ? 'Desactivar' : 'Activar'}
                        className="p-1.5 rounded-lg hover:bg-gray-100 text-gray-500"
                      >
                        {u.isActive ? <UserX size={15} /> : <UserCheck size={15} />}
                      </button>
                      <button
                        onClick={() => setResetModal(u)}
                        title="Resetear contraseña"
                        className="p-1.5 rounded-lg hover:bg-blue-50 text-blue-500"
                      >
                        <KeyRound size={15} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
};

export default AdminUsersPage;
