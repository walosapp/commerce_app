import { useEffect, useMemo, useState } from 'react';
import { CheckSquare, PlayCircle, Square, X } from 'lucide-react';

const MonthItemSelectorModal = ({ isOpen, onClose, onConfirm, items = [], monthLabel, loading = false }) => {
  const [selectedIds, setSelectedIds] = useState([]);

  const sortedItems = useMemo(
    () => [...items].sort((a, b) => (a.description || '').localeCompare(b.description || '')),
    [items]
  );

  useEffect(() => {
    if (!isOpen) return;
    setSelectedIds(sortedItems.filter((item) => item.autoIncludeInMonth !== false).map((item) => item.id));
  }, [isOpen, sortedItems]);

  if (!isOpen) return null;

  const toggle = (id) => {
    setSelectedIds((prev) => (prev.includes(id) ? prev.filter((itemId) => itemId !== id) : [...prev, id]));
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
          {sortedItems.length === 0 ? (
            <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-500">
              Aun no tienes items financieros creados.
            </div>
          ) : (
            sortedItems.map((item) => {
              const selected = selectedIds.includes(item.id);
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => toggle(item.id)}
                  className={`flex w-full items-start justify-between gap-3 rounded-xl border p-4 text-left transition-colors ${
                    selected ? 'border-primary-300 bg-primary-50' : 'border-gray-200 hover:bg-gray-50'
                  }`}
                >
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      {selected ? <CheckSquare className="h-4 w-4 text-primary-600" /> : <Square className="h-4 w-4 text-gray-400" />}
                      <p className="truncate text-sm font-semibold text-gray-900">{item.description}</p>
                    </div>
                    <p className="mt-1 text-xs text-gray-500">
                      {item.categoryName || 'Categoria'} • {item.type} • {item.nature} • {item.frequency}
                    </p>
                  </div>
                  <span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${item.autoIncludeInMonth !== false ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                    {item.autoIncludeInMonth !== false ? 'Auto mensual' : 'Manual'}
                  </span>
                </button>
              );
            })
          )}
        </div>

        <div className="flex items-center justify-end gap-2 border-t p-5">
          <button onClick={onClose} type="button" className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">
            Cancelar
          </button>
          <button
            onClick={() => onConfirm(selectedIds)}
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
