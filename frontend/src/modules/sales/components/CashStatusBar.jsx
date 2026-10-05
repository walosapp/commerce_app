import { Wallet } from 'lucide-react';
import useCashRegisterStatus from '../../../hooks/useCashRegisterStatus';

const CashStatusBar = () => {
  const { label, className, canRetry, refetch } = useCashRegisterStatus();
  return (
    <div className="flex flex-shrink-0 flex-wrap items-center justify-between gap-3 border-b border-gray-200 bg-white px-4 py-3 md:px-6">
      <div className="flex flex-wrap items-center gap-3">
        <span className={`inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-bold ${className}`}><Wallet size={14} />Caja: {label}</span>
        <span className="text-xs text-gray-500">La apertura y el cierre se gestionan desde el módulo Caja.</span>
      </div>
      {canRetry && <button type="button" onClick={() => refetch()} className="text-xs font-semibold text-primary-600 hover:underline">Reintentar caja</button>}
    </div>
  );
};

export default CashStatusBar;
