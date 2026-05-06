/**
 * Panel Historial de Cajas
 * ¿Qué es? Panel lateral con historial de turnos de caja
 * ¿Para qué? Consultar turnos anteriores, resúmenes y movimientos
 */

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, ChevronDown, ChevronUp, Clock, User, ArrowDownCircle, ArrowUpCircle, Printer } from 'lucide-react';
import { cashRegisterService } from '../../../services/cashRegisterService';
import { formatCurrency } from '../../../utils/formatCurrency';
import ZReportPrint from './ZReportPrint';

const CashRegisterHistory = ({ isOpen, onClose }) => {
  const [expandedId, setExpandedId] = useState(null);
  const [zReportId, setZReportId] = useState(null);
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const { data: historyData, isLoading } = useQuery({
    queryKey: ['cash-register-history', dateFrom, dateTo],
    queryFn: () => cashRegisterService.getHistory({ dateFrom: dateFrom || undefined, dateTo: dateTo || undefined }),
    enabled: isOpen,
  });

  const { data: movementsData } = useQuery({
    queryKey: ['cash-register-movements', expandedId],
    queryFn: () => cashRegisterService.getMovements(expandedId),
    enabled: !!expandedId,
  });

  const registers = historyData?.data || [];
  const movements = movementsData?.data || [];

  const formatDate = (dateStr) => {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    return d.toLocaleDateString('es-CO', { day: '2-digit', month: 'short', year: 'numeric' });
  };

  const formatTime = (dateStr) => {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    return d.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });
  };

  if (!isOpen) return null;

  return (
    <>
    {zReportId && <ZReportPrint registerId={zReportId} onClose={() => setZReportId(null)} />}
    <div className="fixed inset-0 z-[70] flex justify-end bg-black/50" onClick={onClose}>
      <div
        className="w-full max-w-lg bg-white h-full shadow-2xl flex flex-col"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b flex-shrink-0">
          <div>
            <h3 className="text-lg font-bold text-gray-900">Historial de Cajas</h3>
            <p className="text-sm text-gray-500">{historyData?.count ?? 0} turno{(historyData?.count ?? 0) !== 1 ? 's' : ''}</p>
          </div>
          <button
            onClick={onClose}
            className="rounded-lg p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-600 transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        {/* Filtros */}
        <div className="px-6 py-3 border-b flex-shrink-0 flex items-center gap-3">
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
          />
          <span className="text-gray-400 text-xs">a</span>
          <input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
            className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
          />
        </div>

        {/* List */}
        <div className="flex-1 overflow-y-auto">
          {isLoading ? (
            <div className="flex h-40 items-center justify-center">
              <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-500 border-t-transparent" />
            </div>
          ) : registers.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-40 text-gray-400">
              <Clock size={32} className="mb-2 opacity-50" />
              <p className="text-sm">Sin turnos registrados</p>
            </div>
          ) : (
            <div className="divide-y">
              {registers.map((reg) => {
                const isExpanded = expandedId === reg.id;
                const diff = reg.closingAmount != null ? reg.closingAmount - (reg.expectedCash ?? 0) : null;
                return (
                  <div key={reg.id}>
                    <button
                      onClick={() => setExpandedId(isExpanded ? null : reg.id)}
                      className="w-full px-6 py-4 flex items-center justify-between text-left hover:bg-gray-50 transition-colors"
                    >
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2">
                          <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
                            reg.status === 'open'
                              ? 'bg-green-100 text-green-700'
                              : 'bg-gray-100 text-gray-600'
                          }`}>
                            {reg.status === 'open' ? 'Abierta' : 'Cerrada'}
                          </span>
                          <span className="text-xs text-gray-400">
                            {formatDate(reg.openedAt)} {formatTime(reg.openedAt)}
                          </span>
                        </div>
                        <div className="flex items-center gap-4 mt-1 text-sm">
                          <span className="flex items-center gap-1 text-gray-500">
                            <User size={12} /> {reg.openedByName || 'Usuario'}
                          </span>
                          <span className="font-medium text-gray-800">
                            Ventas: {formatCurrency(reg.totalSales)}
                          </span>
                          {diff != null && Math.abs(diff) > 1 && (
                            <span className={`text-xs font-medium ${diff > 0 ? 'text-green-600' : 'text-red-600'}`}>
                              {diff > 0 ? '+' : ''}{formatCurrency(diff)}
                            </span>
                          )}
                        </div>
                      </div>
                      {isExpanded ? <ChevronUp size={16} className="text-gray-400" /> : <ChevronDown size={16} className="text-gray-400" />}
                    </button>

                    {isExpanded && (
                      <div className="px-6 pb-4 space-y-3">
                        {/* Detail grid */}
                        <div className="grid grid-cols-2 gap-2 text-xs rounded-lg bg-gray-50 p-3">
                          <div><span className="text-gray-400">Apertura:</span> <span className="font-medium">{formatCurrency(reg.openingAmount)}</span></div>
                          <div><span className="text-gray-400">Cierre:</span> <span className="font-medium">{reg.closingAmount != null ? formatCurrency(reg.closingAmount) : '-'}</span></div>
                          <div><span className="text-gray-400">Efectivo:</span> <span className="font-medium">{formatCurrency(reg.totalCashSales)}</span></div>
                          <div><span className="text-gray-400">Tarjeta:</span> <span className="font-medium">{formatCurrency(reg.totalCardSales)}</span></div>
                          <div><span className="text-gray-400">Transferencia:</span> <span className="font-medium">{formatCurrency(reg.totalTransferSales)}</span></div>
                          <div><span className="text-gray-400">Órdenes:</span> <span className="font-medium">{reg.orderCount}</span></div>
                          <div><span className="text-gray-400">Entradas:</span> <span className="font-medium text-green-600">+{formatCurrency(reg.cashIn)}</span></div>
                          <div><span className="text-gray-400">Salidas:</span> <span className="font-medium text-red-600">-{formatCurrency(reg.cashOut)}</span></div>
                          {reg.totalDiscounts > 0 && (
                            <div><span className="text-gray-400">Descuentos:</span> <span className="font-medium text-orange-600">-{formatCurrency(reg.totalDiscounts)}</span></div>
                          )}
                          {reg.totalTips > 0 && (
                            <div><span className="text-gray-400">Propinas:</span> <span className="font-medium">{formatCurrency(reg.totalTips)}</span></div>
                          )}
                        </div>
                        {reg.notes && (
                          <p className="text-xs text-gray-500 italic">{reg.notes}</p>
                        )}

                        <button
                          onClick={() => setZReportId(reg.id)}
                          className="flex items-center gap-1.5 text-xs font-medium text-primary-600 hover:text-primary-700 transition-colors"
                        >
                          <Printer size={12} /> Imprimir Reporte Z
                        </button>

                        {/* Movements */}
                        {movements.length > 0 && (
                          <div>
                            <p className="text-xs font-medium text-gray-500 mb-1">Movimientos</p>
                            <div className="space-y-1">
                              {movements.map((mov) => (
                                <div key={mov.id} className="flex items-center gap-2 text-xs py-1 px-2 rounded bg-white border border-gray-100">
                                  {mov.type === 'in'
                                    ? <ArrowDownCircle size={12} className="text-green-500 flex-shrink-0" />
                                    : <ArrowUpCircle size={12} className="text-red-500 flex-shrink-0" />
                                  }
                                  <span className="flex-1 truncate text-gray-600">{mov.reason}</span>
                                  <span className={`font-medium ${mov.type === 'in' ? 'text-green-600' : 'text-red-600'}`}>
                                    {mov.type === 'in' ? '+' : '-'}{formatCurrency(mov.amount)}
                                  </span>
                                  <span className="text-gray-400">{formatTime(mov.createdAt)}</span>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </div>
  </>
  );
};

export default CashRegisterHistory;
