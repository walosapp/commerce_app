import { useEffect, useState } from 'react';
import { Clock, MapPin, Monitor } from 'lucide-react';
import useAuthStore from '../../../stores/authStore';
import useCashRegisterStatus from '../../../hooks/useCashRegisterStatus';

const PosHeader = ({ children }) => {
  const { user, branchId } = useAuthStore();
  const { label: cashLabel, className: cashClass, canRetry, refetch } = useCashRegisterStatus();
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 30_000);
    return () => window.clearInterval(timer);
  }, []);


  return (
    <header className="grid shrink-0 grid-cols-1 items-center gap-x-5 gap-y-3 rounded-2xl border border-gray-200 bg-white px-5 py-4 sm:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_minmax(240px,1.25fr)_minmax(0,0.75fr)]">
      <div className="order-1 flex min-w-0 items-center gap-3">
        <div className="shrink-0 rounded-xl bg-primary-50 p-3 text-primary-600"><Monitor className="h-6 w-6" /></div>
        <div className="min-w-0">
          <h1 className="text-2xl font-black tracking-tight text-gray-900">POS</h1>
          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-2 text-sm">
            <span className={`inline-flex items-center gap-2 rounded-full px-2.5 py-1 text-xs font-bold ${cashClass}`}>
              <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current" />Caja: {cashLabel}
            </span>
            <span className="break-words text-gray-500">Cajero: <span className="font-semibold text-gray-700">{user?.name || user?.firstName || 'Sin identificar'}</span></span>
            {canRetry && <button type="button" onClick={() => refetch()} className="text-xs font-semibold text-primary-600 hover:underline">Reintentar caja</button>}
          </div>
        </div>
      </div>
      <div className="order-3 min-w-0 sm:col-span-2 xl:order-2 xl:col-span-1">{children}</div>
      <div className="order-2 flex min-w-0 flex-wrap items-center gap-x-4 gap-y-2 text-sm text-gray-500 sm:justify-end xl:order-3">
        <span className="inline-flex min-w-0 items-center gap-2"><MapPin className="h-4 w-4 shrink-0" /><span className="break-words">Sucursal: <strong className="text-gray-900">{user?.branchName || (branchId ? `#${branchId}` : 'Sin asignar')}</strong></span></span>
        <time dateTime={now.toISOString()} className="inline-flex shrink-0 items-center gap-2 font-semibold tabular-nums text-gray-700"><Clock className="h-4 w-4" />{now.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false })}</time>
      </div>
    </header>
  );
};

export default PosHeader;
