import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarRange, PlayCircle, Repeat, SlidersHorizontal } from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import financeService from '../../services/financeService';
import FinancialEntryTable from './components/FinancialEntryTable';
import FinancialEntryFormModal from './components/FinancialEntryFormModal';
import FinancialCategoryModal from './components/FinancialCategoryModal';
import MonthInitPanelModal from './components/MonthInitPanelModal';
import MonthItemSelectorModal from './components/MonthItemSelectorModal';

const getMonthValue = (date) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  return `${year}-${month}`;
};

const getMonthRange = (monthValue) => {
  if (!monthValue) {
    return { startDate: undefined, endDate: undefined };
  }

  const [year, month] = monthValue.split('-').map(Number);
  const start = new Date(year, month - 1, 1);
  const end = new Date(year, month, 0);
  return {
    startDate: start.toISOString().slice(0, 10),
    endDate: end.toISOString().slice(0, 10),
  };
};

const getMonthLabel = (monthValue) => {
  if (!monthValue) return 'Periodo abierto';
  const [year, month] = monthValue.split('-').map(Number);
  return new Intl.DateTimeFormat('es-CO', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1));
};

const FinancePage = () => {
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState({
    type: '',
    categoryId: '',
    selectedMonth: getMonthValue(new Date()),
  });
  const [entryModalOpen, setEntryModalOpen] = useState(false);
  const [categoryModalOpen, setCategoryModalOpen] = useState(false);
  const [monthPanelOpen, setMonthPanelOpen] = useState(false);
  const [monthSelectorOpen, setMonthSelectorOpen] = useState(false);
  const [startingMonth, setStartingMonth] = useState(false);
  const [editingEntry, setEditingEntry] = useState(null);
  const [editingCategory, setEditingCategory] = useState(null);

  const monthRange = useMemo(() => getMonthRange(filters.selectedMonth), [filters.selectedMonth]);

  const entryFilters = useMemo(() => ({
    branchId,
    type: filters.type || undefined,
    categoryId: filters.categoryId || undefined,
    startDate: monthRange.startDate,
    endDate: monthRange.endDate,
  }), [branchId, filters.type, filters.categoryId, monthRange.endDate, monthRange.startDate]);

  const { data: entriesData, isLoading: entriesLoading } = useQuery({
    queryKey: ['finance-entries', entryFilters],
    queryFn: () => financeService.getEntries(entryFilters),
    enabled: !!branchId,
  });

  const { data: categoriesData } = useQuery({
    queryKey: ['finance-categories'],
    queryFn: () => financeService.getCategories(),
  });

  const entries = entriesData?.data || [];
  const categories = categoriesData?.data || [];
  const monthLabel = getMonthLabel(filters.selectedMonth);

  const refreshAll = () => {
    queryClient.invalidateQueries({ queryKey: ['finance-entries'] });
    queryClient.invalidateQueries({ queryKey: ['finance-categories'] });
  };

  const handleSaveEntry = async (payload) => {
    if (editingEntry) {
      await financeService.updateEntry(editingEntry.id, payload);
      toast.success('Movimiento actualizado');
    } else {
      await financeService.createEntry(payload);
      toast.success('Movimiento creado');
    }

    setEntryModalOpen(false);
    setEditingEntry(null);
    refreshAll();
  };

  const handleDeleteEntry = async (entry) => {
    await financeService.deleteEntry(entry.id);
    toast.success('Movimiento eliminado');
    refreshAll();
  };

  const handleSaveCategory = async (payload) => {
    if (editingCategory) {
      await financeService.updateCategory(editingCategory.id, payload);
      toast.success('Item financiero actualizado');
    } else {
      await financeService.createCategory(payload);
      toast.success('Item financiero creado');
    }

    setCategoryModalOpen(false);
    setEditingCategory(null);
    refreshAll();
  };

  const handleDeleteCategory = async (category) => {
    await financeService.deleteCategory(category.id);
    toast.success('Item financiero eliminado');
    setCategoryModalOpen(false);
    setEditingCategory(null);
    refreshAll();
  };

  const handleInitMonth = async (selectedCategoryIds = []) => {
    if (!filters.selectedMonth) return;
    setStartingMonth(true);
    try {
      const res = await financeService.initMonth({
        month: filters.selectedMonth,
        branchId,
        selectedCategoryIds,
        useSelectedCategoryIds: true,
      });
      const inserted = res?.data ?? 0;
      toast.success(inserted > 0 ? `Mes iniciado. ${inserted} item(s) generados` : 'Mes ya estaba iniciado');
      refreshAll();
      setMonthSelectorOpen(false);
      setMonthPanelOpen(true);
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible iniciar el mes');
    } finally {
      setStartingMonth(false);
    }
  };

  const applyRelativeMonth = (offset) => {
    const today = new Date();
    const monthDate = new Date(today.getFullYear(), today.getMonth() + offset, 1);
    setFilters((prev) => ({ ...prev, selectedMonth: getMonthValue(monthDate) }));
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Finanzas</h1>
          <p className="mt-1 text-sm text-gray-500">Primero creas tus items financieros. Luego inicias el mes y trabajas solo sobre los items de ese mes.</p>
        </div>
        <div className="flex flex-wrap gap-3">
          <button onClick={() => { setEditingCategory(null); setCategoryModalOpen(true); }} className="inline-flex items-center gap-2 rounded-lg border border-gray-300 px-4 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50">
            <Repeat className="h-4 w-4" />
            Items financieros
          </button>
          <button onClick={() => setMonthSelectorOpen(true)} className="inline-flex items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 px-4 py-2.5 text-sm font-medium text-primary-700 hover:bg-primary-100">
            <PlayCircle className="h-4 w-4" />
            Iniciar mes
          </button>
        </div>
      </div>

      <div className="card space-y-4">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <CalendarRange className="h-4 w-4 text-primary-600" />
              <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-700">Control mensual</h2>
            </div>
            <p className="mt-2 text-lg font-semibold text-gray-900">{monthLabel}</p>
            <p className="mt-1 text-sm text-gray-500">Usa solo estos dos pasos: crear items y luego iniciar el mes con los que quieras cargar.</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button onClick={() => applyRelativeMonth(-1)} className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">Mes anterior</button>
            <button onClick={() => applyRelativeMonth(0)} className="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50">Mes actual</button>
            <input type="month" className="input w-[180px]" value={filters.selectedMonth} onChange={(e) => setFilters((prev) => ({ ...prev, selectedMonth: e.target.value }))} />
          </div>
        </div>
      </div>

      <div className="card space-y-4">
        <div className="flex items-center gap-2">
          <SlidersHorizontal className="h-4 w-4 text-primary-600" />
          <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-700">Filtros de movimientos manuales</h2>
        </div>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          <select className="input" value={filters.type} onChange={(e) => setFilters((prev) => ({ ...prev, type: e.target.value, categoryId: '' }))}>
            <option value="">Todos los tipos</option>
            <option value="expense">Gastos</option>
            <option value="income">Ingresos</option>
          </select>
          <select className="input" value={filters.categoryId} onChange={(e) => setFilters((prev) => ({ ...prev, categoryId: e.target.value }))}>
            <option value="">Todas las categorias</option>
            {categories.filter((category) => !filters.type || category.type === filters.type).map((category) => (
              <option key={category.id} value={category.id}>{category.name}</option>
            ))}
          </select>
          <div className="rounded-lg border border-dashed border-gray-300 px-3 py-2 text-sm text-gray-500">
            Mostrando movimientos de <span className="font-medium text-gray-700">{monthLabel}</span>
          </div>
        </div>
      </div>

      <FinancialEntryTable
        entries={entries}
        isLoading={entriesLoading}
        onEdit={(entry) => { setEditingEntry(entry); setEntryModalOpen(true); }}
        onDelete={handleDeleteEntry}
      />

      <FinancialEntryFormModal
        isOpen={entryModalOpen}
        onClose={() => { setEntryModalOpen(false); setEditingEntry(null); }}
        onSave={handleSaveEntry}
        entry={editingEntry}
        categories={categories}
        branchId={branchId}
      />

      <FinancialCategoryModal
        isOpen={categoryModalOpen}
        onClose={() => { setCategoryModalOpen(false); setEditingCategory(null); }}
        onSave={handleSaveCategory}
        onDelete={handleDeleteCategory}
        category={editingCategory}
      />

      <MonthInitPanelModal
        isOpen={monthPanelOpen}
        onClose={() => setMonthPanelOpen(false)}
        entries={entries}
        categories={categories}
        monthLabel={monthLabel}
        selectedMonth={filters.selectedMonth}
        branchId={branchId}
        onUpdated={refreshAll}
      />

      <MonthItemSelectorModal
        isOpen={monthSelectorOpen}
        onClose={() => setMonthSelectorOpen(false)}
        onConfirm={handleInitMonth}
        items={categories}
        monthLabel={monthLabel}
        loading={startingMonth}
      />

      {!categoryModalOpen && categories.length > 0 && (
        <div className="card">
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h2 className="text-lg font-semibold text-gray-900">Items financieros creados</h2>
              <p className="mt-1 text-sm text-gray-500">Puedes tocar cualquiera para editarlo.</p>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            {categories.map((category) => (
              <button
                key={category.id}
                onClick={() => { setEditingCategory(category); setCategoryModalOpen(true); }}
                className="inline-flex items-center gap-2 rounded-full border border-gray-200 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
              >
                <span className="h-2.5 w-2.5 rounded-full" style={{ backgroundColor: category.colorHex || '#94A3B8' }} />
                {category.name}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default FinancePage;
