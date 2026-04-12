import { useEffect, useMemo, useState } from 'react';
import { PlusCircle, Repeat, Trash2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import financeService from '../../../services/financeService';

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

const RecurringTemplatesModal = ({ isOpen, onClose, templates = [], categories = [], branchId, onUpdated }) => {
  const [editing, setEditing] = useState(null);
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState(null);
  const [form, setForm] = useState({
    type: 'expense',
    categoryId: '',
    description: '',
    defaultAmount: '',
    nature: 'fixed',
    frequency: 'monthly',
    dayOfMonth: 1,
    biweeklyDay1: 1,
    biweeklyDay2: 15,
    isActive: true,
  });

  const availableCategories = useMemo(
    () => categories.filter((c) => c.type === form.type),
    [categories, form.type]
  );

  useEffect(() => {
    if (!isOpen) return;
    setEditing(null);
    setForm({
      type: 'expense',
      categoryId: '',
      description: '',
      defaultAmount: '',
      nature: 'fixed',
      frequency: 'monthly',
      dayOfMonth: 1,
      biweeklyDay1: 1,
      biweeklyDay2: 15,
      isActive: true,
    });
  }, [isOpen]);

  const startCreate = () => {
    setEditing(null);
    setForm((prev) => ({
      ...prev,
      type: 'expense',
      categoryId: '',
      description: '',
      defaultAmount: '',
      nature: 'fixed',
      frequency: 'monthly',
      dayOfMonth: 1,
      biweeklyDay1: 1,
      biweeklyDay2: 15,
      isActive: true,
    }));
  };

  const startEdit = (tpl) => {
    setEditing(tpl);
    setForm({
      type: tpl.type || 'expense',
      categoryId: tpl.categoryId || '',
      description: tpl.description || '',
      defaultAmount: tpl.defaultAmount ?? '',
      nature: tpl.nature || 'fixed',
      frequency: tpl.frequency || 'monthly',
      dayOfMonth: tpl.dayOfMonth ?? 1,
      biweeklyDay1: tpl.biweeklyDay1 ?? 1,
      biweeklyDay2: tpl.biweeklyDay2 ?? 15,
      isActive: tpl.isActive !== false,
    });
  };

  const submit = async (e) => {
    e.preventDefault();

    if (!form.categoryId) {
      toast.error('Selecciona una categoria');
      return;
    }

    if (!form.description.trim()) {
      toast.error('La descripcion es obligatoria');
      return;
    }

    const defaultAmount = Number(form.defaultAmount);
    if (!Number.isFinite(defaultAmount) || defaultAmount <= 0) {
      toast.error('Monto invalido');
      return;
    }

    setSaving(true);
    try {
      const payload = {
        branchId: branchId || null,
        categoryId: Number(form.categoryId),
        type: form.type,
        description: form.description,
        defaultAmount,
        dayOfMonth: Number(form.dayOfMonth || 1),
        nature: form.nature,
        frequency: form.frequency,
        biweeklyDay1: form.frequency === 'biweekly' ? Number(form.biweeklyDay1 || 1) : null,
        biweeklyDay2: form.frequency === 'biweekly' ? Number(form.biweeklyDay2 || 15) : null,
        isActive: !!form.isActive,
      };

      if (editing) {
        await financeService.updateTemplate(editing.id, payload);
        toast.success('Plantilla actualizada');
      } else {
        await financeService.createTemplate(payload);
        toast.success('Plantilla creada');
      }

      setEditing(null);
      onUpdated?.();
      startCreate();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible guardar la plantilla');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (tpl) => {
    setDeletingId(tpl.id);
    try {
      await financeService.deleteTemplate(tpl.id);
      toast.success('Plantilla eliminada');
      if (editing?.id === tpl.id) startCreate();
      onUpdated?.();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible eliminar');
    } finally {
      setDeletingId(null);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-5xl rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between gap-4 border-b p-5">
          <div>
            <div className="flex items-center gap-2">
              <Repeat className="h-5 w-5 text-primary-600" />
              <h2 className="text-lg font-bold text-gray-900">Plantillas fijas</h2>
            </div>
            <p className="mt-1 text-sm text-gray-500">Crea plantillas recurrentes para iniciar el mes sin volver a registrar lo mismo.</p>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-gray-500 transition-colors hover:bg-gray-100">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="grid gap-6 p-5 lg:grid-cols-2">
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-sm font-semibold uppercase tracking-wide text-gray-600">Plantillas</h3>
              <button
                onClick={startCreate}
                className="inline-flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                <PlusCircle className="h-4 w-4" />
                Nueva
              </button>
            </div>

            <div className="max-h-[60vh] space-y-2 overflow-y-auto pr-1">
              {templates.length === 0 ? (
                <div className="rounded-xl border border-dashed border-gray-300 p-6 text-sm text-gray-500">
                  Aun no tienes plantillas.
                </div>
              ) : (
                templates.map((tpl) => (
                  <button
                    key={tpl.id}
                    type="button"
                    onClick={() => startEdit(tpl)}
                    className={`w-full rounded-xl border p-4 text-left transition-colors ${
                      editing?.id === tpl.id ? 'border-primary-300 bg-primary-50' : 'border-gray-200 hover:bg-gray-50'
                    }`}
                  >
                    <div className="flex items-center justify-between gap-3">
                      <div className="min-w-0">
                        <p className="truncate text-sm font-semibold text-gray-900">{tpl.description}</p>
                        <p className="mt-1 truncate text-xs text-gray-500">
                          {tpl.categoryName || 'Categoria'} • {tpl.type} • {tpl.nature} • {tpl.frequency}
                        </p>
                      </div>
                      <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${tpl.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                        {tpl.isActive ? 'Activa' : 'Inactiva'}
                      </span>
                    </div>
                  </button>
                ))
              )}
            </div>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white">
            <form onSubmit={submit} className="space-y-4 p-5">
              <div>
                <h3 className="text-sm font-semibold uppercase tracking-wide text-gray-600">{editing ? 'Editar plantilla' : 'Nueva plantilla'}</h3>
              </div>

              <div className="grid gap-4 md:grid-cols-2">
                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Tipo</label>
                  <select
                    className="input"
                    value={form.type}
                    onChange={(e) => setForm((prev) => ({ ...prev, type: e.target.value, categoryId: '' }))}
                  >
                    <option value="expense">Gasto</option>
                    <option value="income">Ingreso</option>
                  </select>
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Categoria</label>
                  <select
                    className="input"
                    value={form.categoryId}
                    onChange={(e) => setForm((prev) => ({ ...prev, categoryId: e.target.value }))}
                    required
                  >
                    <option value="">Selecciona una categoria</option>
                    {availableCategories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="md:col-span-2">
                  <label className="mb-1 block text-sm font-medium text-gray-700">Descripcion</label>
                  <input
                    className="input"
                    value={form.description}
                    onChange={(e) => setForm((prev) => ({ ...prev, description: e.target.value }))}
                    required
                  />
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Monto default</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="input"
                    value={form.defaultAmount}
                    onChange={(e) => setForm((prev) => ({ ...prev, defaultAmount: e.target.value }))}
                    required
                  />
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Naturaleza</label>
                  <select
                    className="input"
                    value={form.nature}
                    onChange={(e) => setForm((prev) => ({ ...prev, nature: e.target.value }))}
                  >
                    {natureOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="mb-1 block text-sm font-medium text-gray-700">Periodicidad</label>
                  <select
                    className="input"
                    value={form.frequency}
                    onChange={(e) => setForm((prev) => ({ ...prev, frequency: e.target.value }))}
                  >
                    {frequencyOptions.map((o) => (
                      <option key={o.value} value={o.value}>
                        {o.label}
                      </option>
                    ))}
                  </select>
                </div>

                {form.frequency === 'monthly' && (
                  <div className="md:col-span-2">
                    <label className="mb-1 block text-sm font-medium text-gray-700">Dia del mes</label>
                    <input
                      type="number"
                      min="1"
                      max="31"
                      className="input"
                      value={form.dayOfMonth}
                      onChange={(e) => setForm((prev) => ({ ...prev, dayOfMonth: e.target.value }))}
                    />
                  </div>
                )}

                {form.frequency === 'biweekly' && (
                  <>
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700">Dia 1</label>
                      <input
                        type="number"
                        min="1"
                        max="31"
                        className="input"
                        value={form.biweeklyDay1}
                        onChange={(e) => setForm((prev) => ({ ...prev, biweeklyDay1: e.target.value }))}
                      />
                    </div>
                    <div>
                      <label className="mb-1 block text-sm font-medium text-gray-700">Dia 2</label>
                      <input
                        type="number"
                        min="1"
                        max="31"
                        className="input"
                        value={form.biweeklyDay2}
                        onChange={(e) => setForm((prev) => ({ ...prev, biweeklyDay2: e.target.value }))}
                      />
                    </div>
                  </>
                )}

                <div className="md:col-span-2">
                  <label className="flex items-center gap-2 text-sm font-medium text-gray-700">
                    <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={(e) => setForm((prev) => ({ ...prev, isActive: e.target.checked }))}
                    />
                    Activa
                  </label>
                </div>
              </div>

              <div className="flex items-center justify-between gap-2 pt-2">
                {editing ? (
                  <button
                    type="button"
                    onClick={() => remove(editing)}
                    disabled={deletingId === editing.id}
                    className="inline-flex items-center gap-2 rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-red-600 hover:bg-red-50 disabled:opacity-50"
                  >
                    <Trash2 className="h-4 w-4" />
                    Eliminar
                  </button>
                ) : (
                  <div />
                )}

                <div className="flex gap-2">
                  <button
                    type="button"
                    onClick={onClose}
                    className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                  >
                    Cerrar
                  </button>
                  <button
                    type="submit"
                    disabled={saving}
                    className="rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
                  >
                    {saving ? 'Guardando...' : 'Guardar'}
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
};

export default RecurringTemplatesModal;
