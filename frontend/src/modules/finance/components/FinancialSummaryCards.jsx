import { ArrowDownCircle, ArrowUpCircle, Landmark, Receipt, Wallet } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const cards = [
  { key: 'sales', label: 'Ventas facturadas', icon: Receipt, iconWrap: 'bg-blue-100 text-blue-600', valueKey: 'systemSalesTotal' },
  { key: 'income', label: 'Ingresos manuales', icon: ArrowUpCircle, iconWrap: 'bg-green-100 text-green-600', valueKey: 'totalIncome' },
  { key: 'expense', label: 'Gastos operativos', icon: ArrowDownCircle, iconWrap: 'bg-red-100 text-red-600', valueKey: 'totalExpense' },
  { key: 'balance', label: 'Resultado del periodo', icon: Wallet, iconWrap: 'bg-primary-100 text-primary-700', valueKey: 'netBalance' },
  {
    key: 'top',
    label: 'Categoria mas movida',
    icon: Landmark,
    iconWrap: 'bg-purple-100 text-purple-600',
    customRender: (summary) => (
      <div>
        <p className="mt-2 text-lg font-bold text-gray-900">{summary?.topCategoryName || 'Sin datos'}</p>
        <p className="mt-1 text-sm text-gray-500">{formatCurrency(summary?.topCategoryAmount || 0)}</p>
      </div>
    ),
  },
];

const FinancialSummaryCards = ({ summary }) => (
  <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
    {cards.map((card) => {
      const Icon = card.icon;
      return (
        <div key={card.key} className="card">
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-sm text-gray-500">{card.label}</p>
              {card.customRender ? card.customRender(summary) : (
                <p className="mt-2 text-3xl font-bold text-gray-900">{formatCurrency(summary?.[card.valueKey] || 0)}</p>
              )}
            </div>
            <div className={`rounded-xl p-3 ${card.iconWrap}`}>
              <Icon className="h-6 w-6" />
            </div>
          </div>
        </div>
      );
    })}
  </div>
);

export default FinancialSummaryCards;
