import { useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Loader2, UserPlus, Eye, EyeOff } from 'lucide-react';
import userService from '../../../services/userService';
import adminService from '../../../services/adminService';
import useAuthStore from '../../../stores/authStore';

const Field = ({ label, value, onChange, placeholder, type = 'text', required, children }) => (
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">
      {label}{required && <span className="text-red-500 ml-1">*</span>}
    </label>
    {children ?? (
      <input
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
      />
    )}
  </div>
);

const empty = () => ({ firstName: '', lastName: '', email: '', password: '', phone: '', roleId: '', branchId: '', companyId: '' });

const UserFormModal = ({ user, onSave, onClose, companies = [] }) => {
  const { tenantId, user: currentUser } = useAuthStore();
  const isDev = currentUser?.role === 'dev';
  const isEdit = !!user;
  const [form, setForm] = useState(empty());
  const [showPassword, setShowPassword] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const selectedCompanyId = form.companyId ? Number(form.companyId) : (isDev ? null : Number(tenantId));

  const { data: rolesData } = useQuery({
    queryKey: ['user-roles', selectedCompanyId ?? tenantId],
    queryFn: () => isDev && selectedCompanyId
      ? userService.adminGetRolesForCompany(selectedCompanyId)
      : userService.getRoles(),
    enabled: isDev ? !!selectedCompanyId : !!tenantId,
    staleTime: 2 * 60 * 1000,
  });
  const roles = rolesData?.data ?? [];

  useEffect(() => {
    if (user) {
      setForm({
        firstName: user.firstName ?? '',
        lastName: user.lastName ?? '',
        email: user.email ?? '',
        password: '',
        phone: user.phone ?? '',
        roleId: String(user.roleId ?? ''),
        branchId: String(user.branchId ?? ''),
        companyId: String(user.companyId ?? ''),
      });
    } else {
      setForm(empty());
    }
    setError('');
  }, [user]);

  const set = f => v => setForm(p => ({ ...p, [f]: v }));

  const handleSubmit = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) { setError('Nombre y apellido requeridos'); return; }
    if (!form.email.trim()) { setError('El email es requerido'); return; }
    if (!isEdit && (!form.password || form.password.length < 6)) { setError('La contraseña debe tener al menos 6 caracteres'); return; }
    if (!form.roleId) { setError('Selecciona un rol'); return; }
    if (isDev && !isEdit && !form.companyId) { setError('Selecciona un comercio'); return; }

    setSaving(true);
    setError('');
    try {
      const payload = {
        firstName: form.firstName,
        lastName: form.lastName,
        email: form.email,
        phone: form.phone || undefined,
        roleId: Number(form.roleId),
        branchId: form.branchId ? Number(form.branchId) : undefined,
        ...(!isEdit && { password: form.password }),
        ...(isDev && !isEdit && { companyId: Number(form.companyId) }),
      };
      await onSave(payload);
      onClose();
    } catch (e) {
      setError(e?.response?.data?.message || 'Error al guardar usuario');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg">
        <div className="flex items-center gap-3 px-6 py-4 border-b">
          <UserPlus size={20} className="text-primary-600" />
          <h2 className="text-base font-semibold text-gray-900">
            {isEdit ? 'Editar usuario' : 'Nuevo usuario'}
          </h2>
          <button onClick={onClose} className="ml-auto text-gray-400 hover:text-gray-600"><X size={18} /></button>
        </div>

        <div className="px-6 py-5 space-y-4">
          {isDev && !isEdit && (
            <Field label="Comercio" required>
              <select
                value={form.companyId}
                onChange={e => setForm(p => ({ ...p, companyId: e.target.value, roleId: '' }))}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
              >
                <option value="">Seleccionar comercio...</option>
                {companies.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </Field>
          )}

          <div className="grid grid-cols-2 gap-4">
            <Field label="Nombre" value={form.firstName} onChange={set('firstName')} placeholder="Carlos" required />
            <Field label="Apellido" value={form.lastName} onChange={set('lastName')} placeholder="López" required />
          </div>
          <Field label="Email" value={form.email} onChange={set('email')} placeholder="usuario@comercio.com" type="email" required />

          {!isEdit && (
            <Field label="Contraseña" required>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={form.password}
                  onChange={e => setForm(p => ({ ...p, password: e.target.value }))}
                  placeholder="Mínimo 6 caracteres"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 pr-10 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(v => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </Field>
          )}

          <div className="grid grid-cols-2 gap-4">
            <Field label="Teléfono" value={form.phone} onChange={set('phone')} placeholder="+57 300 000 0000" />
            <Field label="Rol" required>
              <select
                value={form.roleId}
                onChange={e => setForm(p => ({ ...p, roleId: e.target.value }))}
                className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
                disabled={isDev && !isEdit && !form.companyId}
              >
                <option value="">{isDev && !isEdit && !form.companyId ? 'Primero selecciona un comercio' : 'Seleccionar rol...'}</option>
                {roles.map(r => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
            </Field>
          </div>

          {error && <p className="text-sm text-red-600 bg-red-50 px-3 py-2 rounded-lg">{error}</p>}
        </div>

        <div className="flex justify-end gap-3 px-6 pb-5">
          <button onClick={onClose} className="text-sm text-gray-500 hover:text-gray-700 px-4 py-2">Cancelar</button>
          <button
            onClick={handleSubmit}
            disabled={saving}
            className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
          >
            {saving && <Loader2 size={14} className="animate-spin" />}
            {isEdit ? 'Guardar cambios' : 'Crear usuario'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default UserFormModal;
