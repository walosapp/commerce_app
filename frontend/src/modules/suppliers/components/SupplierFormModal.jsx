import { useState, useEffect } from 'react';
import { X, Loader2, Truck } from 'lucide-react';

const emptyForm = () => ({
  name: '', contactName: '', phone: '', email: '', address: '', notes: '',
});

const Field = ({ label, value, onChange, placeholder, type = 'text', required }) => (
  <div>
    <label className="block text-sm font-medium text-gray-700 mb-1">
      {label}{required && <span className="text-red-500 ml-1">*</span>}
    </label>
    <input
      type={type}
      value={value}
      onChange={e => onChange(e.target.value)}
      placeholder={placeholder}
      className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
    />
  </div>
);

const SupplierFormModal = ({ supplier, onSave, onClose }) => {
  const [form, setForm] = useState(emptyForm());
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const isEdit = !!supplier;

  useEffect(() => {
    if (supplier) {
      setForm({
        name: supplier.name ?? '',
        contactName: supplier.contactName ?? '',
        phone: supplier.phone ?? '',
        email: supplier.email ?? '',
        address: supplier.address ?? '',
        notes: supplier.notes ?? '',
      });
    } else {
      setForm(emptyForm());
    }
  }, [supplier]);

  const set = (field) => (value) => setForm(p => ({ ...p, [field]: value }));

  const handleSubmit = async () => {
    if (!form.name.trim()) { setError('El nombre es requerido'); return; }
    setSaving(true);
    setError('');
    try {
      await onSave(form);
      onClose();
    } catch (e) {
      setError(e?.response?.data?.message || 'Error al guardar proveedor');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg">
        <div className="flex items-center gap-3 px-6 py-4 border-b">
          <Truck size={20} className="text-primary-600" />
          <h2 className="text-base font-semibold text-gray-900">
            {isEdit ? 'Editar proveedor' : 'Nuevo proveedor'}
          </h2>
          <button onClick={onClose} className="ml-auto text-gray-400 hover:text-gray-600"><X size={18} /></button>
        </div>
        <div className="px-6 py-5 space-y-4">
          <Field label="Nombre" value={form.name} onChange={set('name')} placeholder="Distribuidora XYZ" required />
          <div className="grid grid-cols-2 gap-4">
            <Field label="Contacto" value={form.contactName} onChange={set('contactName')} placeholder="Carlos López" />
            <Field label="Teléfono" value={form.phone} onChange={set('phone')} placeholder="+57 300 000 0000" />
          </div>
          <Field label="Email" value={form.email} onChange={set('email')} placeholder="ventas@proveedor.com" type="email" />
          <Field label="Dirección" value={form.address} onChange={set('address')} placeholder="Calle 123 #45-67" />
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notas</label>
            <textarea
              rows={2}
              value={form.notes}
              onChange={e => setForm(p => ({ ...p, notes: e.target.value }))}
              placeholder="Condiciones, descuentos, observaciones..."
              className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>
        <div className="flex justify-end gap-3 px-6 pb-5">
          <button onClick={onClose} className="text-sm text-gray-500 hover:text-gray-700 px-4 py-2">Cancelar</button>
          <button
            onClick={handleSubmit}
            disabled={saving}
            className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 disabled:opacity-50 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors"
          >
            {saving && <Loader2 size={14} className="animate-spin" />}
            {isEdit ? 'Guardar cambios' : 'Crear proveedor'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default SupplierFormModal;
