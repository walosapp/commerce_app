import { useEffect, useState } from 'react';
import { X, Building2, Save, Loader2, KeyRound, Eye, EyeOff } from 'lucide-react';
import toast from 'react-hot-toast';
import adminService from '../../../services/adminService';

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
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
  </div>
);

const EditTenantModal = ({ tenant, onClose, onSaved }) => {
  const [form, setForm] = useState({});
  const [saving, setSaving]         = useState(false);
  const [error, setError]           = useState(null);
  const [newPassword, setNewPassword] = useState('');
  const [showPwd, setShowPwd]       = useState(false);
  const [savingPwd, setSavingPwd]   = useState(false);

  useEffect(() => {
    if (tenant) {
      setForm({
        name:     tenant.name     ?? '',
        legalName: tenant.legalName ?? '',
        taxId:    tenant.taxId    ?? '',
        email:    tenant.email    ?? '',
        phone:    tenant.phone    ?? '',
        city:     tenant.city     ?? '',
        country:  tenant.country  ?? 'CO',
        currency: tenant.currency ?? 'COP',
        language: tenant.language ?? 'es',
      });
      setError(null);
      setNewPassword('');
    }
  }, [tenant]);

  if (!tenant) return null;

  const handleChange = e => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleResetPassword = async () => {
    if (newPassword.length < 6) { toast.error('Mínimo 6 caracteres'); return; }
    setSavingPwd(true);
    try {
      await adminService.resetTenantPassword(tenant.id, newPassword);
      toast.success('Contraseña del administrador actualizada');
      setNewPassword('');
    } catch (err) {
      toast.error(err?.response?.data?.message || 'Error cambiando contraseña');
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
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg flex flex-col max-h-[90vh]">

        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <div className="flex items-center gap-2">
            <Building2 size={20} className="text-indigo-600" />
            <h2 className="text-lg font-semibold text-gray-900">Editar comercio</h2>
          </div>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 transition-colors">
            <X size={20} />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-6 py-5 grid grid-cols-1 gap-4">
          <Field label="Nombre del comercio *" name="name"      value={form.name}      onChange={handleChange} placeholder="Mi Bar SAS" />
          <Field label="Razón social"           name="legalName" value={form.legalName} onChange={handleChange} placeholder="Mi Bar S.A.S." />
          <Field label="NIT / RUT"              name="taxId"     value={form.taxId}     onChange={handleChange} placeholder="900123456-1" />
          <div className="grid grid-cols-2 gap-3">
            <Field label="Email"    name="email" type="email" value={form.email} onChange={handleChange} placeholder="contacto@mibar.com" />
            <Field label="Teléfono" name="phone"              value={form.phone} onChange={handleChange} placeholder="+57 300 000 0000" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <Field label="Ciudad" name="city" value={form.city} onChange={handleChange} placeholder="Bogotá" />
            <Select label="País" name="country" value={form.country} onChange={handleChange} options={[
              { value: 'CO', label: 'Colombia' },
              { value: 'MX', label: 'México' },
              { value: 'AR', label: 'Argentina' },
              { value: 'CL', label: 'Chile' },
              { value: 'PE', label: 'Perú' },
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
              { value: 'es', label: 'Español' },
              { value: 'en', label: 'English' },
              { value: 'pt', label: 'Português' },
            ]} />
          </div>

          {/* Cambio de contraseña del admin */}
          <div className="border border-dashed border-gray-300 rounded-xl p-4 space-y-2">
            <div className="flex items-center gap-2 text-sm font-semibold text-gray-700">
              <KeyRound size={15} className="text-indigo-500" />
              Contraseña del administrador
            </div>
            <p className="text-xs text-gray-400">Cambia la contraseña del usuario super_admin de este comercio</p>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <input
                  type={showPwd ? 'text' : 'password'}
                  value={newPassword}
                  onChange={e => setNewPassword(e.target.value)}
                  placeholder="Nueva contraseña (mín. 6 caracteres)"
                  className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm pr-9 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                />
                <button type="button" onClick={() => setShowPwd(v => !v)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600">
                  {showPwd ? <EyeOff size={15} /> : <Eye size={15} />}
                </button>
              </div>
              <button
                onClick={handleResetPassword}
                disabled={savingPwd || newPassword.length < 6}
                className="flex items-center gap-1.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-40 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors whitespace-nowrap"
              >
                {savingPwd ? <Loader2 size={14} className="animate-spin" /> : <KeyRound size={14} />}
                {savingPwd ? '...' : 'Cambiar'}
              </button>
            </div>
          </div>

          {error && (
            <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-600">
              {error}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t flex items-center justify-end gap-3">
          <button onClick={onClose} className="text-sm text-gray-500 hover:text-gray-700 px-4 py-2 rounded-lg hover:bg-gray-100 transition-colors">
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={saving || !form.name?.trim()}
            className="flex items-center gap-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
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
