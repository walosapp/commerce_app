import React from 'react';
import { Repeat, Trash2 } from 'lucide-react';

const typeOptions = [
  { value: 'expense', label: 'Gasto' },
  { value: 'income', label: 'Ingreso' },
];

const natureOptions = [
  { value: 'fixed', label: 'fixed' },
  { value: 'variable', label: 'variable' },
  { value: 'unique', label: 'unique' },
];

const frequencyOptions = [
  { value: 'weekly', label: 'weekly' },
  { value: 'biweekly', label: 'biweekly' },
  { value: 'monthly', label: 'monthly' },
  { value: 'unique', label: 'unique' },
];

const getInitialForm = (category) => ({
  name: category?.name || '',
  type: category?.type || 'expense',
  colorHex: category?.colorHex || '#0EA5E9',
  defaultAmount: category?.defaultAmount ?? '',
  nature: category?.nature || 'fixed',
  frequency: category?.frequency || 'monthly',
  dayOfMonth: category?.dayOfMonth ?? 1,
  biweeklyDay1: category?.biweeklyDay1 ?? 1,
  biweeklyDay2: category?.biweeklyDay2 ?? 15,
  autoIncludeInMonth: category?.autoIncludeInMonth !== false,
  isActive: category?.isActive !== false,
});

const FinancialCategoryModal = ({ isOpen, onClose, onSave, onDelete, category }) => {
  const [form, setForm] = React.useState(getInitialForm(category));

  React.useEffect(() => {
    if (!isOpen) return;
    setForm(getInitialForm(category));
  }, [category, isOpen]);

  if (!isOpen) return null;

  const submit = async (e) => {
    e.preventDefault();
    await onSave({
      ...form,
      defaultAmount: Number(form.defaultAmount || 0),
      dayOfMonth: Number(form.dayOfMonth || 1),
      biweeklyDay1: form.frequency === 'biweekly' ? Number(form.biweeklyDay1 || 1) : null,
      biweeklyDay2: form.frequency === 'biweekly' ? Number(form.biweeklyDay2 || 15) : null,
    });
  };

  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-2xl rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <form onSubmit={submit} className="space-y-4 p-6">
          <div className="flex items-center gap-2">
            <Repeat className="h-5 w-5 text-primary-600" />
            <h2 className="text-lg font-bold text-gray-900">{category ? 'Editar item financiero' : 'Nuevo item financiero'}</h2>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="md:col-span-2">
              <label className="mb-1 block text-sm font-medium text-gray-700">Nombre del item</label>
              <input className="input" value={form.name} onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))} required />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Tipo</label>
              <select className="input" value={form.type} onChange={(e) => setForm((prev) => ({ ...prev, type: e.target.value }))}>
                {typeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Color</label>
              <input type="color" className="h-10 w-full rounded-lg border border-gray-300 bg-white px-2" value={form.colorHex} onChange={(e) => setForm((prev) => ({ ...prev, colorHex: e.target.value }))} />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Monto por defecto</label>
              <input type="number" min="0" step="0.01" className="input" value={form.defaultAmount} onChange={(e) => setForm((prev) => ({ ...prev, defaultAmount: e.target.value }))} required />
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Naturaleza</label>
              <select className="input" value={form.nature} onChange={(e) => setForm((prev) => ({ ...prev, nature: e.target.value }))}>
                {natureOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </div>

            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Periodicidad</label>
              <select className="input" value={form.frequency} onChange={(e) => setForm((prev) => ({ ...prev, frequency: e.target.value }))}>
                {frequencyOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </div>

            {form.frequency === 'monthly' && (
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Dia del mes</label>
                <input type="number" min="1" max="31" className="input" value={form.dayOfMonth} onChange={(e) => setForm((prev) => ({ ...prev, dayOfMonth: e.target.value }))} />
              </div>
            )}

            {form.frequency === 'biweekly' && (
              <>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Dia 1</label>
                  <input type="number" min="1" max="31" className="input" value={form.biweeklyDay1} onChange={(e) => setForm((prev) => ({ ...prev, biweeklyDay1: e.target.value }))} />
                </div>
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Dia 2</label>
                  <input type="number" min="1" max="31" className="input" value={form.biweeklyDay2} onChange={(e) => setForm((prev) => ({ ...prev, biweeklyDay2: e.target.value }))} />
                </div>
              </>
            )}

            <div className="md:col-span-2">
              <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
                <input type="checkbox" checked={form.autoIncludeInMonth} onChange={(e) => setForm((prev) => ({ ...prev, autoIncludeInMonth: e.target.checked }))} />
                Incluir este item automaticamente al iniciar el mes
              </label>
            </div>

            <div className="md:col-span-2">
              <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
                <input type="checkbox" checked={form.isActive} onChange={(e) => setForm((prev) => ({ ...prev, isActive: e.target.checked }))} />
                Item activo
              </label>
            </div>
          </div>

          <div className="flex items-center justify-between gap-3 pt-2">
            <div>
              {category && !category.isSystem && (
                <button type="button" onClick={() => onDelete(category)} className="inline-flex items-center gap-2 rounded-lg border border-red-200 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-50">
                  <Trash2 className="h-4 w-4" />
                  Eliminar
                </button>
              )}
            </div>
            <div className="flex gap-2">
              <button type="button" onClick={onClose} className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">Cancelar</button>
              <button type="submit" className="rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700">Guardar</button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
};

export default FinancialCategoryModal;
