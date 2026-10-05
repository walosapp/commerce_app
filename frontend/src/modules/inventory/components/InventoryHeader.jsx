import { useEffect, useState } from 'react';
import { Clock, MapPin, Package, Search } from 'lucide-react';
import useAuthStore from '../../../stores/authStore';

const InventoryHeader = ({ search, onSearchChange, actions }) => {
  const { user, branchId } = useAuthStore();
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(new Date()), 30_000);
    return () => window.clearInterval(timer);
  }, []);

  return (
    <header className="inventory-header grid shrink-0 grid-cols-1 items-center gap-x-5 gap-y-3 rounded-2xl border border-gray-200 bg-white px-5 py-4 sm:grid-cols-[minmax(0,1fr)_auto] xl:grid-cols-[minmax(0,1fr)_minmax(240px,1.25fr)_minmax(0,1fr)]">
      <div className="order-1 flex min-w-0 items-center gap-3">
        <div className="shrink-0 rounded-xl bg-primary-50 p-3 text-primary-600"><Package className="h-6 w-6" /></div>
        <div className="min-w-0">
          <h1 className="text-2xl font-black tracking-tight text-gray-900">Inventario</h1>
          <p className="mt-1 break-words text-sm text-gray-500">Operador: <span className="font-semibold text-gray-700">{user?.name || user?.firstName || 'Sin identificar'}</span></p>
        </div>
      </div>
      <div className="relative order-3 min-w-0 sm:col-span-2 xl:order-2 xl:col-span-1">
        <Search aria-hidden="true" className="pointer-events-none absolute left-4 top-1/2 h-5 w-5 -translate-y-1/2 text-gray-500" />
        <input type="search" aria-label="Buscar en inventario" placeholder="Buscar por nombre, SKU o categoría..."
          value={search} onChange={e => onSearchChange(e.target.value)}
          className="w-full rounded-xl border border-gray-200 bg-gray-50 py-3 pl-11 pr-4 text-sm text-gray-900 placeholder:text-gray-400 focus:border-primary-300 focus:bg-white focus:outline-none focus:ring-2 focus:ring-primary-100" />
      </div>
      <div className="order-2 flex min-w-0 flex-col gap-3 sm:items-end xl:order-3">
        <div className="flex min-w-0 flex-wrap items-center gap-x-4 gap-y-2 text-sm text-gray-500 sm:justify-end">
          <span className="inline-flex min-w-0 items-center gap-2"><MapPin className="h-4 w-4 shrink-0" /><span className="break-words">Sucursal: <strong className="text-gray-900">{user?.branchName || (branchId ? `#${branchId}` : 'Sin asignar')}</strong></span></span>
          <time dateTime={now.toISOString()} className="inline-flex shrink-0 items-center gap-2 font-semibold tabular-nums text-gray-700"><Clock className="h-4 w-4" />{now.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit', hour12: false })}</time>
        </div>
        {actions}
      </div>
    </header>
  );
};

export default InventoryHeader;
