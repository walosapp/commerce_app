import { formatCurrency } from '../../../utils/formatCurrency';
import { calculateExpectedCash } from '../../../utils/cashRegister';

const CashSummaryView = ({ register }) => {
  const elapsed = Math.floor((Date.now() - new Date(register.openedAt).getTime()) / 60000);
  const hours = Math.floor(elapsed / 60);
  const mins = elapsed % 60;
  const timeStr = hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  const expectedCash = calculateExpectedCash(register);

  const stats = [
    { label: 'Ventas totales', value: formatCurrency(register.totalSales), color: 'text-gray-900' },
    { label: 'Efectivo', value: formatCurrency(register.totalCashSales), color: 'text-green-600' },
    { label: 'Tarjeta', value: formatCurrency(register.totalCardSales), color: 'text-blue-600' },
    { label: 'Transferencia', value: formatCurrency(register.totalTransferSales), color: 'text-purple-600' },
    { label: 'Entradas manuales', value: `+${formatCurrency(register.cashIn)}`, color: 'text-green-600' },
    { label: 'Salidas manuales', value: `-${formatCurrency(register.cashOut)}`, color: 'text-red-600' },
    { label: 'Descuentos', value: `-${formatCurrency(register.totalDiscounts)}`, color: 'text-orange-600' },
    { label: 'Órdenes', value: register.orderCount, color: 'text-gray-900' },
  ];

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <div className="rounded-xl bg-white border border-gray-200 p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-base font-bold text-gray-900">Turno actual</h3>
            <p className="text-xs text-gray-500">Abierta hace {timeStr} · {register.openedByName || 'Usuario'}</p>
          </div>
          <span className="bg-green-100 text-green-700 text-xs font-medium px-2.5 py-1 rounded-full">Abierta</span>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {stats.map(({ label, value, color }) => (
            <div key={label} className="rounded-lg bg-gray-50 p-3">
              <p className="text-xs text-gray-500 mb-0.5">{label}</p>
              <p className={`text-sm font-bold ${color}`}>{value}</p>
            </div>
          ))}
        </div>
      </div>
      <div className="rounded-xl bg-white border border-gray-200 p-5">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs text-gray-500">Efectivo esperado en caja</p>
            <p className="text-xl font-bold text-gray-900">{formatCurrency(expectedCash)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">Monto apertura</p>
            <p className="text-lg font-semibold text-gray-600">{formatCurrency(register.openingAmount)}</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CashSummaryView;
