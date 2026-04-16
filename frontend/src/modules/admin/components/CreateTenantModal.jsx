import { useState } from 'react';
import { X, Building2, MapPin, User, CheckCircle, ChevronRight, ChevronLeft, Loader2 } from 'lucide-react';

const STEPS = [
  { id: 1, label: 'Negocio', icon: Building2 },
  { id: 2, label: 'Sucursal', icon: MapPin },
  { id: 3, label: 'Administrador', icon: User },
  { id: 4, label: 'Confirmar', icon: CheckCircle },
];

const BRANCH_TYPES = [
  { value: 'bar', label: 'Bar' },
  { value: 'restaurant', label: 'Restaurante' },
  { value: 'store', label: 'Tienda' },
  { value: 'warehouse', label: 'Bodega' },
];

const initialForm = {
  companyName: '',
  legalName: '',
  taxId: '',
  email: '',
  phone: '',
  address: '',
  city: '',
  country: 'CO',
  currency: 'COP',
  language: 'es',
  branchName: '',
  branchType: 'bar',
  adminFirstName: '',
  adminLastName: '',
  adminEmail: '',
  adminPassword: '',
};

const Field = ({ label, name, value, onChange, type = 'text', required, placeholder }) => (
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">
      {label} {required && <span className="text-red-500">*</span>}
    </label>
    <input
      type={type}
      name={name}
      value={value}
      onChange={onChange}
      placeholder={placeholder}
      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
    />
  </div>
);

const CreateTenantModal = ({ isOpen, onClose, onCreated }) => {
  const [step, setStep] = useState(1);
  const [form, setForm] = useState(initialForm);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  if (!isOpen) return null;

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm(prev => ({ ...prev, [name]: value }));
  };

  const handleClose = () => {
    setStep(1);
    setForm(initialForm);
    setError(null);
    onClose();
  };

  const handleSubmit = async () => {
    setSaving(true);
    setError(null);
    try {
      await onCreated(form);
      handleClose();
    } catch (err) {
      setError(err?.response?.data?.message || 'Error al crear el comercio');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg flex flex-col max-h-[90vh]">
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <h2 className="text-lg font-semibold text-gray-900">Nuevo Comercio</h2>
          <button onClick={handleClose} className="text-gray-400 hover:text-gray-600">
            <X size={20} />
          </button>
        </div>

        <div className="px-6 py-3 border-b bg-gray-50">
          <div className="flex items-center justify-between">
            {STEPS.map((s, i) => {
              const Icon = s.icon;
              const active = step === s.id;
              const done = step > s.id;
              return (
                <div key={s.id} className="flex items-center gap-1">
                  <div className={`flex items-center gap-1.5 text-xs font-medium transition-colors ${
                    active ? 'text-indigo-600' : done ? 'text-green-600' : 'text-gray-400'
                  }`}>
                    <Icon size={16} />
                    <span className="hidden sm:block">{s.label}</span>
                  </div>
                  {i < STEPS.length - 1 && (
                    <ChevronRight size={14} className="text-gray-300 ml-1" />
                  )}
                </div>
              );
            })}
          </div>
        </div>

        <div className="flex-1 overflow-y-auto px-6 py-5">
          {step === 1 && (
            <div className="grid grid-cols-1 gap-4">
              <Field label="Nombre del comercio" name="companyName" value={form.companyName} onChange={handleChange} required placeholder="Mi Bar SAS" />
              <Field label="Razón social" name="legalName" value={form.legalName} onChange={handleChange} placeholder="Mi Bar S.A.S." />
              <Field label="NIT / RUT" name="taxId" value={form.taxId} onChange={handleChange} placeholder="900123456-1" />
              <Field label="Email del negocio" name="email" value={form.email} onChange={handleChange} type="email" required placeholder="contacto@mibar.com" />
              <Field label="Teléfono" name="phone" value={form.phone} onChange={handleChange} placeholder="+57 300 000 0000" />
              <div className="grid grid-cols-2 gap-3">
                <Field label="Ciudad" name="city" value={form.city} onChange={handleChange} placeholder="Bogotá" />
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">País</label>
                  <select name="country" value={form.country} onChange={handleChange}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
                    <option value="CO">Colombia</option>
                    <option value="MX">México</option>
                    <option value="AR">Argentina</option>
                    <option value="CL">Chile</option>
                    <option value="PE">Perú</option>
                    <option value="EC">Ecuador</option>
                  </select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Moneda</label>
                  <select name="currency" value={form.currency} onChange={handleChange}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
                    <option value="COP">COP</option>
                    <option value="USD">USD</option>
                    <option value="MXN">MXN</option>
                    <option value="ARS">ARS</option>
                    <option value="CLP">CLP</option>
                  </select>
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Idioma</label>
                  <select name="language" value={form.language} onChange={handleChange}
                    className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500">
                    <option value="es">Español</option>
                    <option value="en">English</option>
                    <option value="pt">Português</option>
                  </select>
                </div>
              </div>
            </div>
          )}

          {step === 2 && (
            <div className="grid grid-cols-1 gap-4">
              <Field label="Nombre de la sucursal" name="branchName" value={form.branchName} onChange={handleChange} required placeholder="Sede Principal" />
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Tipo de negocio <span className="text-red-500">*</span></label>
                <div className="grid grid-cols-2 gap-2">
                  {BRANCH_TYPES.map(bt => (
                    <button
                      key={bt.value}
                      type="button"
                      onClick={() => setForm(prev => ({ ...prev, branchType: bt.value }))}
                      className={`py-2.5 px-4 rounded-lg border text-sm font-medium transition-colors ${
                        form.branchType === bt.value
                          ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
                          : 'border-gray-200 text-gray-600 hover:border-gray-300'
                      }`}
                    >
                      {bt.label}
                    </button>
                  ))}
                </div>
              </div>
              <Field label="Dirección" name="address" value={form.address} onChange={handleChange} placeholder="Calle 123 #45-67" />
            </div>
          )}

          {step === 3 && (
            <div className="grid grid-cols-1 gap-4">
              <div className="grid grid-cols-2 gap-3">
                <Field label="Nombre" name="adminFirstName" value={form.adminFirstName} onChange={handleChange} required placeholder="Juan" />
                <Field label="Apellido" name="adminLastName" value={form.adminLastName} onChange={handleChange} required placeholder="Pérez" />
              </div>
              <Field label="Email del administrador" name="adminEmail" value={form.adminEmail} onChange={handleChange} type="email" required placeholder="admin@mibar.com" />
              <Field label="Contraseña temporal" name="adminPassword" value={form.adminPassword} onChange={handleChange} type="password" required placeholder="mínimo 6 caracteres" />
              <p className="text-xs text-gray-500 bg-yellow-50 border border-yellow-200 rounded-lg p-3">
                El administrador podrá cambiar su contraseña después del primer login.
              </p>
            </div>
          )}

          {step === 4 && (
            <div className="space-y-4">
              <div className="bg-gray-50 rounded-xl p-4 space-y-2 text-sm">
                <p className="font-semibold text-gray-700 mb-3">Resumen del nuevo comercio</p>
                <div className="grid grid-cols-2 gap-y-2 gap-x-4 text-gray-600">
                  <span className="text-gray-400">Comercio</span><span className="font-medium">{form.companyName || '—'}</span>
                  <span className="text-gray-400">NIT</span><span>{form.taxId || '—'}</span>
                  <span className="text-gray-400">Email negocio</span><span className="truncate">{form.email || '—'}</span>
                  <span className="text-gray-400">Ciudad</span><span>{form.city || '—'}</span>
                  <span className="text-gray-400">Moneda</span><span>{form.currency}</span>
                  <span className="text-gray-400">Sucursal</span><span>{form.branchName || '—'} ({form.branchType})</span>
                  <span className="text-gray-400">Admin</span><span>{form.adminFirstName} {form.adminLastName}</span>
                  <span className="text-gray-400">Email admin</span><span className="truncate">{form.adminEmail || '—'}</span>
                </div>
              </div>
              {error && (
                <div className="bg-red-50 border border-red-200 rounded-lg p-3 text-sm text-red-600">
                  {error}
                </div>
              )}
              <p className="text-xs text-gray-500">
                Se creará la empresa, sucursal, roles base, usuario administrador, categorías de inventario y categorías financieras en una sola transacción.
              </p>
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t flex items-center justify-between gap-3">
          <button
            onClick={() => step > 1 ? setStep(s => s - 1) : handleClose()}
            className="flex items-center gap-1.5 text-sm text-gray-500 hover:text-gray-700 transition-colors"
          >
            <ChevronLeft size={16} />
            {step === 1 ? 'Cancelar' : 'Atrás'}
          </button>

          {step < 4 ? (
            <button
              onClick={() => setStep(s => s + 1)}
              className="flex items-center gap-1.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
            >
              Siguiente <ChevronRight size={16} />
            </button>
          ) : (
            <button
              onClick={handleSubmit}
              disabled={saving}
              className="flex items-center gap-2 bg-green-600 hover:bg-green-700 disabled:opacity-50 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
            >
              {saving ? <><Loader2 size={16} className="animate-spin" /> Creando...</> : <><CheckCircle size={16} /> Crear Comercio</>}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default CreateTenantModal;
