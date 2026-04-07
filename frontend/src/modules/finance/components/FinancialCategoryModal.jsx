import React from 'react';
import { Tag, Trash2 } from 'lucide-react';

const typeOptions = [
  { value: 'expense', label: 'Gasto' },
  { value: 'income', label: 'Ingreso' },
];

const FinancialCategoryModal = ({ isOpen, onClose, onSave, onDelete, category }) => {
  const [form, setForm] = React.useState({ name: '', type: 'expense', colorHex: '#0EA5E9' });

  React.useEffect(() => {
    if (!isOpen) return;
    setForm({
      name: category?.name || '',
      type: category?.type || 'expense',
      colorHex: category?.colorHex || '#0EA5E9',
    });
  }, [category, isOpen]);

  if (!isOpen) return null;

  const submit = async (e) => {
    e.preventDefault();
    await onSave(form);
  };

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-md rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <form onSubmit={submit} className="space-y-4 p-6">
          <div className="flex items-center gap-2">
            <Tag className="h-5 w-5 text-primary-600" />
            <h2 className="text-lg font-bold text-gray-900">{category ? 'Editar categoria' : 'Nueva categoria'}</h2>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Nombre</label>
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

