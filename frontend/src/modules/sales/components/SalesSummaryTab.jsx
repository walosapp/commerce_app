import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { TrendingUp, Receipt, DollarSign, Tag, CreditCard, ChevronDown, ChevronUp } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';
import useAuthStore from '../../../stores/authStore';
import api from '../../../config/api';

const salesSummaryService = {
  getSummary: (branchId, date) =>
    api.get('/sales/summary', { params: { branchId, date } }).then(r => r.data),
  getCompleted: (branchId, date) =>
    api.get('/sales/orders/completed', { params: { branchId, date } }).then(r => r.data),
};

const StatCard = ({ icon: Icon, label, value, sub, color = 'primary' }) => {
  const colors = {
    primary: 'bg-primary-50 text-primary-600',
    green:   'bg-green-50 text-green-600',
    orange:  'bg-orange-50 text-orange-600',
    red:     'bg-red-50 text-red-600',
  };
  return (
    <div className="bg-white rounded-xl border border-gray-200 p-4 flex items-center gap-4">
      <div className={`w-11 h-11 rounded-xl flex items-center justify-center shrink-0 ${colors[color]}`}>
        <Icon size={22} />
      </div>
      <div className="min-w-0">
        <p className="text-xs text-gray-500 truncate">{label}</p>
        <p className="text-xl font-bold text-gray-900 truncate">{value}</p>
        {sub && <p className="text-xs text-gray-400">{sub}</p>}
      </div>
    </div>
  );
};

const OrderRow = ({ order }) => {
  const [open, setOpen] = useState(false);
  return (
    <div className="border-b border-gray-100 last:border-0">
      <button
        onClick={() => setOpen(v => !v)}
        className="w-full flex items-center gap-3 px-4 py-3 hover:bg-gray-50 transition-colors text-left"
      >
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="text-sm font-semibold text-gray-900">{order.tableName}</span>
            <span className="text-xs text-gray-400">{order.orderNumber}</span>
            {order.hasCredit && (
              <span className="text-xs bg-orange-100 text-orange-700 px-1.5 py-0.5 rounded-full flex items-center gap-1">
                <CreditCard size={10} /> Crédito
              </span>
            )}
            {order.splitReferenceCount > 1 && (
              <span className="text-xs bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded-full">
                ÷{order.splitReferenceCount}
              </span>
            )}
          </div>
          <p className="text-xs text-gray-400 mt-0.5">
            {new Date(order.createdAt).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' })}
          </p>
        </div>
        <div className="text-right shrink-0">
          <p className="text-sm font-bold text-gray-900">{formatCurrency(order.finalTotalPaid)}</p>
          {order.discountAmount > 0 && (
            <p className="text-xs text-red-500">-{formatCurrency(order.discountAmount)}</p>
          )}
        </div>
        {open ? <ChevronUp size={14} className="text-gray-400 shrink-0" /> : <ChevronDown size={14} className="text-gray-400 shrink-0" />}
      </button>
      {open && (
        <div className="px-4 pb-3 bg-gray-50 text-xs text-gray-600 grid grid-cols-2 gap-2">
          <div><span className="text-gray-400">Subtotal:</span> {formatCurrency(order.subtotal)}</div>
          <div><span className="text-gray-400">Descuento:</span> {formatCurrency(order.discountAmount)}</div>
          <div><span className="text-gray-400">Total pagado:</span> {formatCurrency(order.finalTotalPaid)}</div>
          {order.splitReferenceCount > 1 && (
            <div><span className="text-gray-400">Por persona:</span> {formatCurrency(order.finalTotalPaid / order.splitReferenceCount)}</div>
          )}
        </div>
      )}
    </div>
  );
};

const SalesSummaryTab = () => {
  const { branchId } = useAuthStore();
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));

  const { data: summaryData, isLoading: loadingSummary } = useQuery({
    queryKey: ['sales-summary', branchId, date],
    queryFn: () => salesSummaryService.getSummary(branchId, date),
    enabled: !!branchId,
  });

  const { data: ordersData, isLoading: loadingOrders } = useQuery({
    queryKey: ['sales-completed', branchId, date],
    queryFn: () => salesSummaryService.getCompleted(branchId, date),
    enabled: !!branchId,
  });

  const summary = summaryData?.data;
  const orders  = ordersData?.data ?? [];
  const loading = loadingSummary || loadingOrders;

  return (
    <div className="flex-1 overflow-auto p-4 md:p-6 space-y-6">

      {/* Date picker */}
      <div className="flex items-center gap-3">
        <label className="text-sm font-medium text-gray-600">Fecha:</label>
        <input
          type="date"
          value={date}
          onChange={e => setDate(e.target.value)}
          className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
        />
        {loading && <span className="text-xs text-gray-400 animate-pulse">Cargando...</span>}
      </div>

      {/* KPIs */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
          <StatCard icon={DollarSign}  color="green"   label="Ingresos del día"   value={formatCurrency(summary.totalRevenue)} />
          <StatCard icon={Receipt}     color="primary" label="Ventas"             value={summary.totalOrders} sub={`Ticket prom. ${formatCurrency(summary.averageTicket)}`} />
          <StatCard icon={Tag}         color="red"     label="Descuentos"         value={formatCurrency(summary.totalDiscounts)} />
          <StatCard icon={CreditCard}  color="orange"  label="En crédito"         value={formatCurrency(summary.totalCredits)} sub={`${summary.creditOrders} venta${summary.creditOrders !== 1 ? 's' : ''}`} />
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">

        {/* Top productos */}
        {summary?.topProducts?.length > 0 && (
          <div className="bg-white rounded-xl border border-gray-200 p-4">
            <div className="flex items-center gap-2 mb-3">
              <TrendingUp size={16} className="text-primary-600" />
              <h3 className="text-sm font-semibold text-gray-700">Top productos</h3>
            </div>
            <div className="space-y-2">
              {summary.topProducts.map((p, i) => (
                <div key={i} className="flex items-center gap-3">
                  <span className="text-xs font-bold text-gray-400 w-4">{i + 1}</span>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-gray-800 truncate">{p.productName}</p>
                    <div className="mt-1 h-1.5 bg-gray-100 rounded-full overflow-hidden">
                      <div
                        className="h-full bg-primary-500 rounded-full transition-all"
                        style={{ width: `${Math.round((p.totalRevenue / summary.topProducts[0].totalRevenue) * 100)}%` }}
                      />
                    </div>
                  </div>
                  <div className="text-right shrink-0">
                    <p className="text-xs font-semibold text-gray-900">{formatCurrency(p.totalRevenue)}</p>
                    <p className="text-xs text-gray-400">×{p.totalQuantity}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Ventas por hora */}
        {summary?.hourlySales?.length > 0 && (
          <div className="bg-white rounded-xl border border-gray-200 p-4">
            <div className="flex items-center gap-2 mb-3">
              <TrendingUp size={16} className="text-green-600" />
              <h3 className="text-sm font-semibold text-gray-700">Ventas por hora</h3>
            </div>
            <div className="flex items-end gap-1 h-24">
              {(() => {
                const maxRev = Math.max(...summary.hourlySales.map(h => h.revenue), 1);
                return summary.hourlySales.map((h, i) => (
                  <div key={i} className="flex-1 flex flex-col items-center gap-1 group">
                    <div className="relative w-full">
                      <div
                        className="w-full bg-primary-400 rounded-t hover:bg-primary-500 transition-colors cursor-default"
                        style={{ height: `${Math.max(4, Math.round((h.revenue / maxRev) * 80))}px` }}
                        title={`${h.hour}:00 — ${formatCurrency(h.revenue)} (${h.orderCount} venta${h.orderCount !== 1 ? 's' : ''})`}
                      />
                    </div>
                    <span className="text-[9px] text-gray-400">{h.hour}h</span>
                  </div>
                ));
              })()}
            </div>
          </div>
        )}

      </div>

      {/* Listado de ventas */}
      <div className="bg-white rounded-xl border border-gray-200">
        <div className="px-4 py-3 border-b flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-700">Ventas del día</h3>
          <span className="text-xs text-gray-400">{orders.length} registros</span>
        </div>
        {loadingOrders ? (
          <p className="text-center text-sm text-gray-400 py-8">Cargando...</p>
        ) : orders.length === 0 ? (
          <p className="text-center text-sm text-gray-400 py-8">Sin ventas en esta fecha</p>
        ) : (
          orders.map(o => <OrderRow key={o.id} order={o} />)
        )}
      </div>

    </div>
  );
};

export default SalesSummaryTab;
