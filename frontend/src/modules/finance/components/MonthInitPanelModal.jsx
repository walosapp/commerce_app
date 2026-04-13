import { useMemo, useState } from 'react';
import { CheckCircle2, CircleSlash2, Edit3, PlusCircle, RotateCcw, X } from 'lucide-react';
import toast from 'react-hot-toast';
import financeService from '../../../services/financeService';
import { formatCurrency } from '../../../utils/formatCurrency';

const statusLabels = { pending: 'Pendientes', posted: 'Registrados', skipped: 'Omitidos' };
const statusOrder = ['pending', 'posted', 'skipped'];

const getMonthEntryDate = (selectedMonth) => {
  if (!selectedMonth) return new Date().toISOString();
  const [year, month] = selectedMonth.split('-').map(Number);
  return new Date(year, month - 1, 1, 12, 0, 0).toISOString();
};

const MonthInitPanelModal = ({ isOpen, onClose, entries = [], categories = [], monthLabel, selectedMonth, branchId, onUpdated }) => {
  const grouped = useMemo(() => {
    const map = { pending: [], posted: [], skipped: [] };
    entries.forEach((e) => {
      if (map[e.status]) map[e.status].push(e);
      else map.posted.push(e);
    });
    return map;
  }, [entries]);

  const [savingIds, setSavingIds] = useState([]);
  const [draftAmounts, setDraftAmounts] = useState({});
  const [confirmedAmounts, setConfirmedAmounts] = useState({});
  const [editingPostedId, setEditingPostedId] = useState(null);

  const [newItem, setNewItem] = useState({
    type: 'expense',
    categoryId: '',
    description: '',
    amount: '',
  });

  const setSaving = (id, value) => {
    setSavingIds((prev) => (value ? [...prev, id] : prev.filter((x) => x !== id)));
  };

  const isSaving = (id) => savingIds.includes(id);

  const canEditAmount = (entry) => (entry.nature || 'fixed') === 'fixed';

  const getAmount = (entry) => {
    const v = draftAmounts[entry.id];
    if (v === undefined || v === null || v === '') return entry.amount;
    return Number(v);
  };

  const updateEntry = async (entry, patch) => {
    setSaving(entry.id, true);
    try {
      await financeService.updateEntry(entry.id, {
        branchId: entry.branchId || null,
        categoryId: entry.categoryId,
        type: entry.type,
        description: entry.description,
        amount: patch.amount ?? getAmount(entry),
        entryDate: new Date(entry.entryDate).toISOString(),
        nature: entry.nature,
        frequency: entry.frequency,
        notes: entry.notes,
        status: patch.status ?? entry.status,
        occurrenceInMonth: entry.occurrenceInMonth,
        isManual: entry.isManual,
      });
      if (patch.status === 'posted' || patch.amount !== undefined) {
        setEditingPostedId(null);
      }
      onUpdated?.();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible actualizar el item');
    } finally {
      setSaving(entry.id, false);
    }
  };

  if (!isOpen) return null;

  const requireConfirm = (entry) => canEditAmount(entry);

  const isConfirmed = (entry) => {
    if (!requireConfirm(entry)) return true;
    return confirmedAmounts[entry.id] === true;
  };

  const createUniqueItem = async () => {
    const categoryId = Number(newItem.categoryId);
    const amount = Number(newItem.amount);

    if (!newItem.description.trim()) {
      toast.error('La descripcion es obligatoria');
      return;
    }
    if (!Number.isFinite(categoryId) || categoryId <= 0) {
      toast.error('Selecciona una categoria');
      return;
    }
    if (!Number.isFinite(amount) || amount <= 0) {
      toast.error('Monto invalido');
      return;
    }

    try {
      await financeService.createEntry({
        branchId: branchId ?? entries[0]?.branchId ?? null,
        categoryId,
        type: newItem.type,
        description: newItem.description,
        amount,
        entryDate: getMonthEntryDate(selectedMonth),
        nature: 'unique',
        frequency: 'unique',
        notes: null,
        status: 'pending',
        occurrenceInMonth: 1,
        isManual: true,
      });
      toast.success('Item agregado');
      setNewItem({ type: 'expense', categoryId: '', description: '', amount: '' });
      onUpdated?.();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible agregar el item');
    }
  };

  const renderPendingCard = (entry) => (
    <div key={entry.id} className="rounded-xl border border-yellow-200 bg-yellow-50/50 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-gray-900">{entry.description}</p>
          <p className="mt-1 text-xs text-gray-500">
            {entry.categoryName} • Ocurr: {entry.occurrenceInMonth} • {new Date(entry.entryDate).toLocaleDateString('es-CO')}
          </p>
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2">
          <div className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2">
            {canEditAmount(entry) ? (
              <input
                type="number"
                min="0"
                step="0.01"
                className="w-32 bg-transparent text-right text-sm font-semibold text-gray-900 outline-none"
                value={draftAmounts[entry.id] ?? entry.amount}
                onChange={(e) => {
                  setDraftAmounts((prev) => ({ ...prev, [entry.id]: e.target.value }));
                  setConfirmedAmounts((prev) => ({ ...prev, [entry.id]: false }));
                }}
                disabled={isSaving(entry.id)}
              />
            ) : (
              <span className="text-sm font-semibold text-gray-900">{formatCurrency(entry.amount)}</span>
            )}
          </div>

          {requireConfirm(entry) && (
            <button
              onClick={() => {
                const v = getAmount(entry);
                if (!Number.isFinite(v) || v <= 0) {
                  toast.error('Monto invalido');
                  return;
                }
                setConfirmedAmounts((prev) => ({ ...prev, [entry.id]: true }));
              }}
              disabled={isSaving(entry.id)}
              className={`rounded-lg border px-3 py-2 text-sm font-medium transition-colors disabled:opacity-50 ${
                isConfirmed(entry)
                  ? 'border-green-200 bg-green-50 text-green-700'
                  : 'border-gray-300 text-gray-700 hover:bg-gray-50'
              }`}
            >
              {isConfirmed(entry) ? 'Confirmado' : 'Confirmar monto'}
            </button>
          )}

          <button
            onClick={() => {
              if (!isConfirmed(entry)) {
                toast.error('Confirma el monto antes de registrar');
                return;
              }
              updateEntry(entry, { status: 'posted', amount: canEditAmount(entry) ? getAmount(entry) : entry.amount });
            }}
            disabled={isSaving(entry.id) || !isConfirmed(entry)}
            className="inline-flex items-center gap-2 rounded-lg bg-primary-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700 disabled:opacity-50"
          >
            <CheckCircle2 className="h-4 w-4" />
            Registrar
          </button>

          <button
            onClick={() => updateEntry(entry, { status: 'skipped' })}
            disabled={isSaving(entry.id)}
            className="inline-flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
          >
            <CircleSlash2 className="h-4 w-4" />
            Omitir
          </button>
        </div>
      </div>
    </div>
  );

  const renderPostedCard = (entry) => {
    const isEditing = editingPostedId === entry.id;

    return (
      <div key={entry.id} className="rounded-xl border border-green-200 bg-green-50/50 p-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-gray-900">{entry.description}</p>
            <p className="mt-1 text-xs text-gray-500">
              {entry.categoryName} • Ocurr: {entry.occurrenceInMonth} • {new Date(entry.entryDate).toLocaleDateString('es-CO')}
            </p>
          </div>

          <div className="flex flex-wrap items-center justify-end gap-2">
            {isEditing && canEditAmount(entry) ? (
              <>
                <div className="flex items-center gap-2 rounded-lg border border-gray-200 bg-white px-3 py-2">
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="w-32 bg-transparent text-right text-sm font-semibold text-gray-900 outline-none"
                    value={draftAmounts[entry.id] ?? entry.amount}
                    onChange={(e) => setDraftAmounts((prev) => ({ ...prev, [entry.id]: e.target.value }))}
                    disabled={isSaving(entry.id)}
                  />
                </div>
                <button
                  onClick={() => {
                    const v = getAmount(entry);
                    if (!Number.isFinite(v) || v <= 0) {
                      toast.error('Monto invalido');
                      return;
                    }
                    updateEntry(entry, { amount: v });
                  }}
                  disabled={isSaving(entry.id)}
                  className="inline-flex items-center gap-2 rounded-lg bg-primary-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700 disabled:opacity-50"
                >
                  <CheckCircle2 className="h-4 w-4" />
                  Guardar
                </button>
                <button
                  onClick={() => { setEditingPostedId(null); setDraftAmounts((prev) => { const n = { ...prev }; delete n[entry.id]; return n; }); }}
                  className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  Cancelar
                </button>
              </>
            ) : (
              <>
                <span className="text-sm font-semibold text-gray-900">{formatCurrency(entry.amount)}</span>
                {canEditAmount(entry) && (
                  <button
                    onClick={() => setEditingPostedId(entry.id)}
                    className="inline-flex items-center gap-1 rounded-lg border border-gray-200 px-2.5 py-1.5 text-xs font-medium text-gray-600 transition-colors hover:bg-gray-50"
                  >
                    <Edit3 className="h-3.5 w-3.5" />
                    Editar
                  </button>
                )}
              </>
            )}
          </div>
        </div>
      </div>
    );
  };

  const renderSkippedCard = (entry) => (
    <div key={entry.id} className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-3 sm:flex-row sm:items-center sm:justify-between">
      <div className="min-w-0">
        <p className="truncate text-sm font-medium text-gray-800">{entry.description}</p>
        <p className="mt-1 text-xs text-gray-500">
          {entry.categoryName} • Ocurr: {entry.occurrenceInMonth}
        </p>
      </div>

      <button
        onClick={() => updateEntry(entry, { status: 'pending' })}
        disabled={isSaving(entry.id)}
        className="inline-flex items-center justify-center gap-2 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 disabled:opacity-50"
      >
        <RotateCcw className="h-4 w-4" />
        Reactivar
      </button>
    </div>
  );

  const renderers = { pending: renderPendingCard, posted: renderPostedCard, skipped: renderSkippedCard };

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-3xl rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between gap-4 border-b p-5">
          <div>
            <h2 className="text-lg font-bold text-gray-900">Control del mes</h2>
            <p className="mt-1 text-sm text-gray-500">
              Revisa todos los items de <span className="font-medium text-gray-700">{monthLabel}</span>. Puedes editar montos de items <span className="font-medium">fixed</span>, registrar pendientes u omitir.
            </p>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-gray-500 transition-colors hover:bg-gray-100">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="max-h-[65vh] overflow-y-auto p-5">
          <div className="mb-6 rounded-xl border border-gray-200 bg-gray-50 p-4">
            <div className="mb-3 flex items-center justify-between">
              <p className="text-sm font-semibold text-gray-800">Agregar item unico del mes</p>
            </div>

            <div className="grid gap-3 md:grid-cols-4">
              <select
                className="input"
                value={newItem.type}
                onChange={(e) => setNewItem((prev) => ({ ...prev, type: e.target.value, categoryId: '' }))}
              >
                <option value="expense">Gasto</option>
                <option value="income">Ingreso</option>
              </select>

              <input
                className="input md:col-span-2"
                placeholder="Descripcion"
                value={newItem.description}
                onChange={(e) => setNewItem((prev) => ({ ...prev, description: e.target.value }))}
              />

              <input
                type="number"
                min="0"
                step="0.01"
                className="input"
                placeholder="Monto"
                value={newItem.amount}
                onChange={(e) => setNewItem((prev) => ({ ...prev, amount: e.target.value }))}
              />

              <select
                className="input md:col-span-3"
                value={newItem.categoryId}
                onChange={(e) => setNewItem((prev) => ({ ...prev, categoryId: e.target.value }))}
              >
                <option value="">Categoria</option>
                {categories
                  .filter((c) => c.type === newItem.type)
                  .map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
              </select>

              <button
                onClick={createUniqueItem}
                className="inline-flex items-center justify-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-700"
              >
                <PlusCircle className="h-4 w-4" />
                Agregar
              </button>
            </div>
          </div>

          {entries.length === 0 ? (
            <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
              No hay items para este mes. Crea items financieros y luego inicia el mes para cargarlos rapidamente.
            </div>
          ) : (
            <div className="space-y-6">
              {statusOrder.map((status) => {
                const items = grouped[status];
                if (!items || items.length === 0) return null;
                const render = renderers[status];

                return (
                  <div key={status}>
                    <h3 className="mb-2 text-sm font-semibold uppercase tracking-wide text-gray-600">
                      {statusLabels[status]} ({items.length})
                    </h3>
                    <div className="space-y-2">
                      {items.map((entry) => render(entry))}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-2 border-t p-5">
          <button onClick={onClose} className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
};

export default MonthInitPanelModal;
