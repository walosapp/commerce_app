import { useEffect, useMemo, useState } from 'react';
import { CheckSquare, PlayCircle, PlusCircle, Square, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatCurrency } from '../../../utils/formatCurrency';

const getFrequencyLabel = (item) => {
  if (item.frequency === 'monthly') return `Mensual • dia ${item.dayOfMonth || 1}`;
  if (item.frequency === 'biweekly') return `Quincenal • dias ${item.biweeklyDay1 || 1} y ${item.biweeklyDay2 || 15}`;
  if (item.frequency === 'weekly') return 'Semanal';
  if (item.frequency === 'unique') return 'Unico';
  return item.frequency || 'Sin periodicidad';
};

const MonthItemSelectorModal = ({
  isOpen,
  onClose,
  onConfirm,
  items = [],
  monthLabel,
  loading = false,
  onCreateItem,
}) => {
  const [selectedIds, setSelectedIds] = useState([]);
  const [amounts, setAmounts] = useState({});

  const sortedItems = useMemo(
    () => [...items].sort((a, b) => (a.name || a.description || '').localeCompare(b.name || b.description || '')),
    [items]
  );

  useEffect(() => {
    if (!isOpen) return;
    setSelectedIds(sortedItems.filter((item) => item.autoIncludeInMonth !== false).map((item) => item.id));
    setAmounts(
      sortedItems.reduce((acc, item) => {
        acc[item.id] = item.defaultAmount ?? 0;
        return acc;
      }, {})
    );
  }, [isOpen, sortedItems]);

  if (!isOpen) return null;

  const toggle = (id) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((itemId) => itemId !== id) : [...prev, id]));
  };

  const getAmountValue = (item) => amounts[item.id] ?? item.defaultAmount ?? 0;

  const handleConfirm = () => {
    const selectedItems = sortedItems
      .filter((item) => selectedIds.includes(item.id))
      .map((item) => ({
        categoryId: item.id,
        amount: Number(getAmountValue(item)),
      }));

    const invalidItem = selectedItems.find((item) => !Number.isFinite(item.amount) || item.amount <= 0);
    if (invalidItem) {
      toast.error('Cada item seleccionado debe tener un valor mayor a cero');
      return;
    }

    onConfirm(selectedItems);
  };

  return (
    <div className="fixed inset-0 z-[80] flex items-center justify-center bg-black/50 p-4" onClick={onClose}>
      <div className="w-full max-w-3xl rounded-xl bg-white shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between gap-4 border-b p-5">
          <div>
            <h2 className="text-lg font-bold text-gray-900">Iniciar {monthLabel}</h2>
            <p className="mt-1 text-sm text-gray-500">
              Selecciona los items financieros que quieres incluir en este mes. Los configurados como automaticos ya vienen marcados.
            </p>
          </div>
          <button onClick={onClose} className="rounded-lg p-2 text-gray-500 transition-colors hover:bg-gray-100">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="max-h-[60vh] space-y-3 overflow-y-auto p-5">
          <div className="flex justify-end">
            <button
              type="button"
              onClick={onCreateItem}
              className="inline-flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
            >
              <PlusCircle className="h-4 w-4" />
              Nuevo item financiero
            </button>
          </div>

          {sortedItems.length === 0 ? (
            <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
              Aun no tienes items financieros creados.
            </div>
          ) : (
            sortedItems.map((item) => {
              const selected = selectedIds.includes(item.id);
              return (
                <div
                  key={item.id}
                  className={`flex w-full items-start justify-between gap-3 rounded-xl border p-4 text-left transition-colors ${
                    selected ? 'border-primary-300 bg-primary-50' : 'border-gray-200 hover:bg-gray-50'
                  }`}
                >
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <button type="button" onClick={() => toggle(item.id)} className="shrink-0">
                        {selected ? <CheckSquare className="h-4 w-4 text-primary-600" /> : <Square className="h-4 w-4 text-gray-400" />}
                      </button>
                      <p className="truncate text-sm font-semibold text-gray-900">{item.name || item.description}</p>
                    </div>
                    <div className="mt-2 grid gap-1 text-xs text-gray-500 sm:grid-cols-2">
                      <p>Tipo: <span className="font-medium text-gray-700">{item.type === 'income' ? 'Ingreso' : 'Gasto'}</span></p>
                      <p>Naturaleza: <span className="font-medium text-gray-700">{item.nature}</span></p>
                      <p>Periodicidad: <span className="font-medium text-gray-700">{getFrequencyLabel(item)}</span></p>
                      <p>Monto base: <span className="font-medium text-gray-700">{formatCurrency(item.defaultAmount || 0)}</span></p>
                    </div>
                  </div>
                  <div className="flex flex-col items-end gap-2">
                    <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${item.autoIncludeInMonth !== false ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                      {item.autoIncludeInMonth !== false ? 'Auto mensual' : 'Manual'}
                    </span>
                    <div className="w-32 rounded-lg border border-gray-200 bg-white px-3 py-2">
                      <label className="mb-1 block text-[11px] font-medium uppercase tracking-wide text-gray-500">Valor del mes</label>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        value={getAmountValue(item)}
                        onChange={(event) => setAmounts((prev) => ({ ...prev, [item.id]: event.target.value }))}
                        className="w-full bg-transparent text-right text-sm font-semibold text-gray-900 outline-none"
                      />
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>

        <div className="flex items-center justify-end gap-2 border-t p-5">
          <button onClick={onClose} type="button" className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
            Cancelar
          </button>
          <button
            onClick={handleConfirm}
            type="button"
            disabled={loading}
            className="inline-flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50"
          >
            <PlayCircle className="h-4 w-4" />
            {loading ? 'Iniciando...' : 'Confirmar e iniciar'}
          </button>
        </div>
      </div>
    </div>
  );
};

export default MonthItemSelectorModal;
