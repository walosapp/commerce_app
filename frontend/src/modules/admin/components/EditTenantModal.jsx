import { useEffect, useState } from 'react';
import { X, Building2, Save, Loader2, KeyRound, Eye, EyeOff } from 'lucide-react';
import toast from 'react-hot-toast';
import adminService from '../../../services/adminService';
import { validatePassword } from '../../../utils/passwordPolicy';

const Field = ({ label, name, value, onChange, type = 'text', placeholder }) => (
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
    <input
      type={type}
      name={name}
      value={value ?? ''}
      onChange={onChange}
      placeholder={placeholder}
      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
    />
  </div>
);

const Select = ({ label, name, value, onChange, options }) => (
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">{label}</label>
    <select
      name={name}
      value={value ?? ''}
      onChange={onChange}
      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
    >
      {options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  </div>
);

const EditTenantModal = ({ tenant, onClose, onSaved }) => {
  const [form, setForm] = useState({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [newPassword, setNewPassword] = useState('');
  const [showPwd, setShowPwd] = useState(false);
  const [savingPwd, setSavingPwd] = useState(false);

  useEffect(() => {
    if (tenant) {
      setForm({
        name: tenant.name ?? '',
        legalName: tenant.legalName ?? '',
        taxId: tenant.taxId ?? '',
        email: tenant.email ?? '',
        adminEmail: tenant.adminEmail ?? '',
        phone: tenant.phone ?? '',
        city: tenant.city ?? '',
        country: tenant.country ?? 'CO',
        currency: tenant.currency ?? 'COP',
        language: tenant.language ?? 'es',
      });
      setError(null);
      setNewPassword('');
    }
  }, [tenant]);

  if (!tenant) return null;

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
  };

  const handleResetPassword = async () => {
    const policyError = validatePassword(newPassword);
    if (policyError) {
      toast.error(policyError);
      return;
    }

    setSavingPwd(true);
    try {
      await adminService.resetTenantPassword(tenant.id, newPassword);
      toast.success('Contrasena del administrador actualizada');
      setNewPassword('');
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Error cambiando contrasena');
    } finally {
      setSavingPwd(false);
    }
  };

  const handleSubmit = async () => {
    setSaving(true);
    setError(null);
    try {
      await onSaved(tenant.id, form);
      onClose();
    } catch (err) {
      setError(err?.response?.data?.message || 'Error al actualizar el comercio');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="flex max-h-[90vh] w-full max-w-lg flex-col rounded-2xl bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <div className="flex items-center gap-2">
            <Building2 size={20} className="text-indigo-600" />
            <h2 className="text-lg font-semibold text-gray-900">Editar comercio</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 transition-colors hover:text-gray-600">
            <X size={20} />
          </button>
        </div>

        <div className="grid flex-1 grid-cols-1 gap-4 overflow-y-auto px-6 py-5">
          <Field label="Nombre del comercio *" name="name" value={form.name} onChange={handleChange} placeholder="Mi Bar SAS" />
          <Field label="Razon social" name="legalName" value={form.legalName} onChange={handleChange} placeholder="Mi Bar S.A.S." />
          <Field label="NIT / RUT" name="taxId" value={form.taxId} onChange={handleChange} placeholder="900123456-1" />

          <div className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-3 text-xs text-blue-700">
            <p className="font-semibold">Credenciales separadas</p>
            <p className="mt-1">El correo del comercio es de contacto. El inicio de sesion usa el correo del administrador principal.</p>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Email del comercio" name="email" type="email" value={form.email} onChange={handleChange} placeholder="contacto@mibar.com" />
            <Field label="Email admin principal" name="adminEmail" type="email" value={form.adminEmail} onChange={handleChange} placeholder="admin@comercio.com" />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Telefono" name="phone" value={form.phone} onChange={handleChange} placeholder="+57 300 000 0000" />
            <div />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Field label="Ciudad" name="city" value={form.city} onChange={handleChange} placeholder="Bogota" />
            <Select label="Pais" name="country" value={form.country} onChange={handleChange} options={[
              { value: 'CO', label: 'Colombia' },
              { value: 'MX', label: 'Mexico' },
              { value: 'AR', label: 'Argentina' },
              { value: 'CL', label: 'Chile' },
              { value: 'PE', label: 'Peru' },
              { value: 'EC', label: 'Ecuador' },
            ]} />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <Select label="Moneda" name="currency" value={form.currency} onChange={handleChange} options={[
              { value: 'COP', label: 'COP' },
              { value: 'USD', label: 'USD' },
              { value: 'MXN', label: 'MXN' },
              { value: 'ARS', label: 'ARS' },
              { value: 'CLP', label: 'CLP' },
            ]} />
            <Select label="Idioma" name="language" value={form.language} onChange={handleChange} options={[
              { value: 'es', label: 'Espanol' },
              { value: 'en', label: 'English' },
              { value: 'pt', label: 'Portugues' },
            ]} />
          </div>

          <div className="space-y-2 rounded-xl border border-dashed border-gray-300 p-4">
            <div className="flex items-center gap-2 text-sm font-semibold text-gray-700">
              <KeyRound size={15} className="text-indigo-500" />
              Contrasena del administrador
            </div>
            <p className="text-xs text-gray-400">
              Cambia la contrasena del usuario super_admin de este comercio.
              {form.adminEmail ? ` Login actual: ${form.adminEmail}` : ''}
            </p>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <input
                  type={showPwd ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  placeholder="Nueva contraseña segura (mín. 8)"
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 pr-9 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <button
                  type="button"
                  onClick={() => setShowPwd((v) => !v)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPwd ? <EyeOff size={15} /> : <Eye size={15} />}
                </button>
              </div>
              <button
                onClick={handleResetPassword}
                disabled={savingPwd || newPassword.length < 8}
                className="whitespace-nowrap rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-indigo-700 disabled:opacity-40"
              >
                {savingPwd ? <Loader2 size={14} className="animate-spin" /> : 'Cambiar'}
              </button>
            </div>
          </div>

          {error && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-600">
              {error}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-3 border-t px-6 py-4">
          <button onClick={onClose} className="rounded-lg px-4 py-2 text-sm text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700">
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={saving || !form.name?.trim() || !form.adminEmail?.trim()}
            className="flex items-center gap-2 rounded-lg bg-indigo-600 px-5 py-2 text-sm font-medium text-white transition-colors hover:bg-indigo-700 disabled:opacity-50"
          >
            {saving ? <Loader2 size={16} className="animate-spin" /> : <Save size={16} />}
            {saving ? 'Guardando...' : 'Guardar cambios'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default EditTenantModal;
