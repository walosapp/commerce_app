import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { CalendarRange, Landmark } from 'lucide-react';
import useAuthStore from '../../stores/authStore';
import financeService from '../../services/financeService';
import FinancialSummaryCards from '../finance/components/FinancialSummaryCards';

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

const DashboardPage = () => {
  const { branchId } = useAuthStore();
  const [selectedMonth, setSelectedMonth] = useState(getMonthValue(new Date()));
  const monthRange = useMemo(() => getMonthRange(selectedMonth), [selectedMonth]);

  const { data: summaryData, isLoading } = useQuery({
    queryKey: ['finance-summary', branchId, monthRange.startDate, monthRange.endDate],
    queryFn: () => financeService.getSummary({ branchId, startDate: monthRange.startDate, endDate: monthRange.endDate }),
    enabled: !!branchId,
  });

  const summary = summaryData?.data || {};
  const monthLabel = getMonthLabel(selectedMonth);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Dashboard</h1>
          <p className="mt-1 text-sm text-gray-500">Resumen general del negocio.</p>
        </div>
      </div>

      <div className="card space-y-4">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="flex items-center gap-2">
              <Landmark className="h-4 w-4 text-primary-600" />
              <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-700">Finanzas</h2>
            </div>
            <p className="mt-2 text-lg font-semibold text-gray-900">{monthLabel}</p>
            <p className="mt-1 text-sm text-gray-500">Ventas facturadas + ingresos manuales - gastos operativos.</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <CalendarRange className="h-4 w-4 text-gray-400" />
            <input
              type="month"
              className="input w-[180px]"
              value={selectedMonth}
              onChange={(e) => setSelectedMonth(e.target.value)}
            />
          </div>
        </div>

        {isLoading ? (
          <div className="py-10 text-center text-sm text-gray-500">Cargando resumen...</div>
        ) : (
          <FinancialSummaryCards summary={summary} />
        )}
      </div>
    </div>
  );
};

export default DashboardPage;
