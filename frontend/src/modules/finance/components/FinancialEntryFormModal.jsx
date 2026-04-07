import React from 'react';
import { Receipt } from 'lucide-react';

const typeOptions = [
  { value: 'expense', label: 'Gasto' },
  { value: 'income', label: 'Ingreso' },
];

const natureOptions = ['fixed', 'variable'];
const frequencyOptions = ['one_time', 'daily', 'weekly', 'monthly'];

const FinancialEntryFormModal = ({ isOpen, onClose, onSave, entry, categories, branchId }) => {
  const [form, setForm] = React.useState({
    type: 'expense',
    categoryId: '',
    description: '',
    amount: '',
    entryDate: new Date().toISOString().slice(0, 10),
    nature: 'variable',
    frequency: 'one_time',
    notes: '',
    branchId: branchId || '',
  });

  React.useEffect(() => {
    if (!isOpen) return;
    setForm({
      type: entry?.type || 'expense',
      categoryId: entry?.categoryId || '',
      description: entry?.description || '',
      amount: entry?.amount || '',
      entryDate: entry?.entryDate ? new Date(entry.entryDate).toISOString().slice(0, 10) : new Date().toISOString().slice(0, 10),
      nature: entry?.nature || 'variable',
      frequency: entry?.frequency || 'one_time',
      notes: entry?.notes || '',
      branchId: entry?.branchId || branchId || '',
    });
  }, [branchId, entry, isOpen]);

  if (!isOpen) return null;

  const availableCategories = categories.filter((category) => category.type === form.type);

  const submit = async (e) => {
    e.preventDefault();
    await onSave({
      ...form,
      branchId: form.branchId || null,
      categoryId: Number(form.categoryId),
      amount: Number(form.amount),
      entryDate: form.entryDate,
    });
  };

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-2xl rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <form onSubmit={submit} className="space-y-5 p-6">
          <div className="flex items-center gap-2">
            <Receipt className="h-5 w-5 text-primary-600" />
            <h2 className="text-lg font-bold text-gray-900">{entry ? 'Editar movimiento' : 'Nuevo movimiento'}</h2>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Tipo</label>
              <select className="input" value={form.type} onChange={(e) => setForm((prev) => ({ ...prev, type: e.target.value, categoryId: '' }))}>
                {typeOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Categoria</label>
              <select className="input" value={form.categoryId} onChange={(e) => setForm((prev) => ({ ...prev, categoryId: e.target.value }))} required>
                <option value="">Selecciona una categoria</option>
                {availableCategories.map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="mb-1 block text-sm font-medium text-gray-700">Descripcion</label>
              <input className="input" value={form.description} onChange={(e) => setForm((prev) => ({ ...prev, description: e.target.value }))} required />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Monto</label>
              <input type="number" min="0" step="0.01" className="input" value={form.amount} onChange={(e) => setForm((prev) => ({ ...prev, amount: e.target.value }))} required />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Fecha</label>
              <input type="date" className="input" value={form.entryDate} onChange={(e) => setForm((prev) => ({ ...prev, entryDate: e.target.value }))} required />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Naturaleza</label>
              <select className="input" value={form.nature} onChange={(e) => setForm((prev) => ({ ...prev, nature: e.target.value }))}>
                {natureOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Periodicidad</label>
              <select className="input" value={form.frequency} onChange={(e) => setForm((prev) => ({ ...prev, frequency: e.target.value }))}>
                {frequencyOptions.map((option) => <option key={option} value={option}>{option}</option>)}
              </select>
            </div>
            <div className="md:col-span-2">
              <label className="mb-1 block text-sm font-medium text-gray-700">Notas</label>
              <textarea className="input min-h-[96px]" value={form.notes} onChange={(e) => setForm((prev) => ({ ...prev, notes: e.target.value }))} />
            </div>
          </div>

          <div className="flex justify-end gap-2">
            <button type="button" onClick={onClose} className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">Cancelar</button>
            <button type="submit" className="rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700">Guardar</button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default FinancialEntryFormModal;

