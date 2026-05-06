import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Search, Download, ChevronLeft, ChevronRight, Filter, X, Printer, RotateCcw } from 'lucide-react';
import salesService from '../../../services/salesService';
import { formatCurrency } from '../../../utils/formatCurrency';
import useAuthStore from '../../../stores/authStore';
import ReceiptPreview from './ReceiptPreview';

const PAYMENT_LABELS = { cash: 'Efectivo', card: 'Tarjeta', transfer: 'Transferencia', nequi: 'Nequi' };
const STATUS_LABELS = { invoiced: 'Facturada', cancelled: 'Cancelada' };
const REFUND_LABELS = { partial_refund: 'Parcial', full_refund: 'Anulada' };

const OrderHistoryTab = () => {
  const { branchId } = useAuthStore();
  const [filters, setFilters] = useState({
    dateFrom: '',
    dateTo: '',
    status: '',
    refundStatus: '',
    paymentMethod: '',
    search: '',
  });
  const [page, setPage] = useState(1);
  const [showFilters, setShowFilters] = useState(false);
  const [receiptOrderId, setReceiptOrderId] = useState(null);
  const limit = 20;

  const queryParams = {
    branchId,
    page,
    limit,
    ...(filters.dateFrom && { dateFrom: filters.dateFrom }),
    ...(filters.dateTo && { dateTo: filters.dateTo }),
    ...(filters.status && { status: filters.status }),
    ...(filters.refundStatus && { refundStatus: filters.refundStatus }),
    ...(filters.paymentMethod && { paymentMethod: filters.paymentMethod }),
    ...(filters.search && { search: filters.search }),
  };

  const { data, isLoading } = useQuery({
    queryKey: ['order-history', queryParams],
    queryFn: () => salesService.searchOrders(queryParams),
    enabled: !!branchId,
    keepPreviousData: true,
  });

  const orders = data?.data ?? [];
  const totalCount = data?.count ?? 0;
  const totalPages = Math.ceil(totalCount / limit);

  const handleExport = async () => {
    try {
      const blob = await salesService.exportOrders(queryParams);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `ventas-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      // silent
    }
  };

  const resetFilters = () => {
    setFilters({ dateFrom: '', dateTo: '', status: '', refundStatus: '', paymentMethod: '', search: '' });
    setPage(1);
  };

  const hasActiveFilters = Object.values(filters).some(v => v !== '');

  return (
    <div className="flex-1 overflow-auto p-4 md:p-6 space-y-4">
      {/* Search + Actions */}
      <div className="flex items-center gap-3 flex-wrap">
        <div className="relative flex-1 min-w-[200px] max-w-md">
          <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder="Buscar por # orden o mesa..."
            value={filters.search}
            onChange={(e) => { setFilters(f => ({ ...f, search: e.target.value })); setPage(1); }}
            className="w-full pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>
        <button
          onClick={() => setShowFilters(v => !v)}
          className={`flex items-center gap-1.5 px-3 py-2 border rounded-lg text-sm font-medium transition-colors ${hasActiveFilters ? 'border-primary-500 text-primary-600 bg-primary-50' : 'border-gray-300 text-gray-600 hover:bg-gray-50'}`}
        >
          <Filter size={14} /> Filtros {hasActiveFilters && <span className="w-2 h-2 bg-primary-500 rounded-full" />}
        </button>
        <button
          onClick={handleExport}
          disabled={isLoading || totalCount === 0}
          className="flex items-center gap-1.5 px-3 py-2 border border-gray-300 rounded-lg text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-50"
        >
          <Download size={14} /> Exportar CSV
        </button>
      </div>

      {/* Filters panel */}
      {showFilters && (
        <div className="bg-white border border-gray-200 rounded-xl p-4 grid grid-cols-2 md:grid-cols-4 gap-3">
          <div>
            <label className="text-xs font-medium text-gray-500 mb-1 block">Desde</label>
            <input
              type="date"
              value={filters.dateFrom}
              onChange={(e) => { setFilters(f => ({ ...f, dateFrom: e.target.value })); setPage(1); }}
              className="w-full border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-500 mb-1 block">Hasta</label>
            <input
              type="date"
              value={filters.dateTo}
              onChange={(e) => { setFilters(f => ({ ...f, dateTo: e.target.value })); setPage(1); }}
              className="w-full border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
          </div>
          <div>
            <label className="text-xs font-medium text-gray-500 mb-1 block">Estado</label>
            <select
              value={filters.status}
              onChange={(e) => { setFilters(f => ({ ...f, status: e.target.value })); setPage(1); }}
              className="w-full border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">Todos</option>
              <option value="invoiced">Facturada</option>
              <option value="cancelled">Cancelada</option>
            </select>
          </div>
          <div>
            <label className="text-xs font-medium text-gray-500 mb-1 block">Método de Pago</label>
            <select
              value={filters.paymentMethod}
              onChange={(e) => { setFilters(f => ({ ...f, paymentMethod: e.target.value })); setPage(1); }}
              className="w-full border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">Todos</option>
              <option value="cash">Efectivo</option>
              <option value="card">Tarjeta</option>
              <option value="transfer">Transferencia</option>
              <option value="nequi">Nequi</option>
            </select>
          </div>
          <div>
            <label className="text-xs font-medium text-gray-500 mb-1 block">Devolución</label>
            <select
              value={filters.refundStatus}
              onChange={(e) => { setFilters(f => ({ ...f, refundStatus: e.target.value })); setPage(1); }}
              className="w-full border border-gray-300 rounded-lg px-2.5 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              <option value="">Todos</option>
              <option value="partial_refund">Parcial</option>
              <option value="full_refund">Anulada</option>
            </select>
          </div>
          {hasActiveFilters && (
            <div className="flex items-end">
              <button
                onClick={resetFilters}
                className="flex items-center gap-1 text-xs text-red-600 hover:text-red-700 font-medium"
              >
                <X size={12} /> Limpiar filtros
              </button>
            </div>
          )}
        </div>
      )}

      {/* Results Table */}
      <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
        <div className="px-4 py-3 border-b flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-700">Historial de ventas</h3>
          <span className="text-xs text-gray-400">{totalCount} registros</span>
        </div>

        {isLoading ? (
          <p className="text-center text-sm text-gray-400 py-8 animate-pulse">Cargando...</p>
        ) : orders.length === 0 ? (
          <p className="text-center text-sm text-gray-400 py-8">Sin resultados</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 border-b">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-gray-500">Orden</th>
                  <th className="text-left px-4 py-2 font-medium text-gray-500">Mesa</th>
                  <th className="text-left px-4 py-2 font-medium text-gray-500">Fecha</th>
                  <th className="text-right px-4 py-2 font-medium text-gray-500">Total</th>
                  <th className="text-center px-4 py-2 font-medium text-gray-500">Pago</th>
                  <th className="text-center px-4 py-2 font-medium text-gray-500">Estado</th>
                  <th className="text-center px-4 py-2 font-medium text-gray-500">Acciones</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {orders.map((order) => {
                  const isRefunded = order.refundStatus === 'full_refund';
                  return (
                    <tr key={order.id} className={`hover:bg-gray-50 ${isRefunded ? 'opacity-60' : ''}`}>
                      <td className="px-4 py-2.5">
                        <span className={`font-medium ${isRefunded ? 'line-through text-gray-400' : 'text-gray-900'}`}>
                          {order.orderNumber}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-gray-600">{order.tableName}</td>
                      <td className="px-4 py-2.5 text-gray-500 text-xs">
                        {new Date(order.createdAt).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' })}
                      </td>
                      <td className={`px-4 py-2.5 text-right font-medium ${isRefunded ? 'line-through text-gray-400' : 'text-gray-900'}`}>
                        {formatCurrency(order.finalTotalPaid)}
                      </td>
                      <td className="px-4 py-2.5 text-center">
                        <span className="text-xs text-gray-500">
                          {PAYMENT_LABELS[order.paymentMethod] || order.paymentMethod || '-'}
                        </span>
                      </td>
                      <td className="px-4 py-2.5 text-center">
                        <div className="flex items-center justify-center gap-1 flex-wrap">
                          <span className={`text-xs px-1.5 py-0.5 rounded-full ${order.status === 'invoiced' ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-600'}`}>
                            {STATUS_LABELS[order.status] || order.status}
                          </span>
                          {order.refundStatus && (
                            <span className={`text-xs px-1.5 py-0.5 rounded-full ${order.refundStatus === 'full_refund' ? 'bg-red-100 text-red-700' : 'bg-orange-100 text-orange-700'}`}>
                              {REFUND_LABELS[order.refundStatus]}
                            </span>
                          )}
                          {order.hasCredit && (
                            <span className="text-xs px-1.5 py-0.5 rounded-full bg-orange-100 text-orange-700">Crédito</span>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-2.5 text-center">
                        <button
                          onClick={() => setReceiptOrderId(order.id)}
                          className="p-1 hover:bg-gray-100 rounded text-primary-600"
                          title="Imprimir recibo"
                        >
                          <Printer size={14} />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t">
            <span className="text-xs text-gray-500">
              Página {page} de {totalPages}
            </span>
            <div className="flex items-center gap-1">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="p-1.5 rounded border border-gray-300 hover:bg-gray-50 disabled:opacity-40"
              >
                <ChevronLeft size={14} />
              </button>
              <button
                onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="p-1.5 rounded border border-gray-300 hover:bg-gray-50 disabled:opacity-40"
              >
                <ChevronRight size={14} />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Receipt Preview Modal */}
      {receiptOrderId && (
        <ReceiptPreview orderId={receiptOrderId} onClose={() => setReceiptOrderId(null)} />
      )}
    </div>
  );
};

export default OrderHistoryTab;
