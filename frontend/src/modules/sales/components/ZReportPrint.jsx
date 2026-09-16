import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { X, Printer, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import printService from '../../../services/printService';
import usePrintAgentStore from '../../../stores/printAgentStore';

const fmt = (v) => {
  if (v == null || isNaN(v)) return '-';
  return `$${Number(v).toLocaleString('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 0 })}`;
};

const PAYMENT_LABELS = {
  cash: 'Efectivo',
  card: 'Tarjeta',
  transfer: 'Transferencia',
  nequi: 'Nequi',
};

export default function ZReportPrint({ registerId, onClose }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['z-report', registerId],
    queryFn: () => printService.getZReport(registerId),
    enabled: !!registerId,
  });

  const report = data?.data;
  const printCashClose = usePrintAgentStore((state) => state.printCashClose);
  const [printing, setPrinting] = useState(false);

  const handlePrint = async () => {
    if (!report || printing) return;
    setPrinting(true);
    try {
      const result = await printCashClose(report);
      toast.success(result?.status === 'replayed' ? 'El cierre ya había sido enviado' : 'Cierre enviado a la impresora');
    } catch (printError) {
      toast.error(printError?.message || 'No fue posible imprimir el cierre');
    } finally {
      setPrinting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-md max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-lg font-semibold text-gray-900">Cierre de caja</h3>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded-lg">
            <X size={20} />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-4">
          {isLoading && (
            <div className="flex items-center justify-center h-40">
              <Loader2 className="animate-spin text-primary-600" size={32} />
            </div>
          )}

          {error && (
            <div className="text-red-600 text-center py-8">Error al cargar reporte</div>
          )}

          {report && (
            <div className="font-mono text-xs space-y-1 border border-gray-200 rounded-lg p-3 bg-gray-50">
              <p className="text-center font-bold text-base">CIERRE DE CAJA</p>
              <p className="text-center text-gray-500">{report.companyName}</p>
              <p className="text-center text-gray-500">{report.branchName}</p>
              <div className="border-t border-dashed border-gray-300 my-1" />

              <p className="font-bold text-xs mt-2">Datos de Caja</p>
              <div className="flex justify-between"><span>Apertura</span><span>{report.openedAt ? new Date(report.openedAt).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' }) : '-'}</span></div>
              <div className="flex justify-between"><span>Cierre</span><span>{report.closedAt ? new Date(report.closedAt).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' }) : '-'}</span></div>
              <div className="flex justify-between"><span>Abierta por</span><span>{report.openedByName}</span></div>
              {report.closedByName && <div className="flex justify-between"><span>Cerrada por</span><span>{report.closedByName}</span></div>}
              <div className="border-t border-dashed border-gray-300 my-1" />

              <p className="font-bold text-xs mt-2">Ventas</p>
              <div className="flex justify-between font-bold"><span>Total Ventas</span><span>{fmt(report.totalSales)}</span></div>
              <div className="flex justify-between"><span>Órdenes</span><span>{report.orderCount}</span></div>
              <div className="flex justify-between"><span>Efectivo</span><span>{fmt(report.totalCashSales)}</span></div>
              <div className="flex justify-between"><span>Tarjeta</span><span>{fmt(report.totalCardSales)}</span></div>
              <div className="flex justify-between"><span>Transferencias</span><span>{fmt(report.totalTransferSales)}</span></div>
              <div className="flex justify-between"><span>Otros</span><span>{fmt(report.totalOtherSales)}</span></div>
              <div className="border-t border-dashed border-gray-300 my-1" />

              <p className="font-bold text-xs mt-2">Otros</p>
              <div className="flex justify-between"><span>Descuentos</span><span>{fmt(report.totalDiscounts)}</span></div>
              <div className="flex justify-between"><span>Créditos</span><span>{fmt(report.totalCredits)}</span></div>
              <div className="flex justify-between"><span>Propinas</span><span>{fmt(report.totalTips)}</span></div>
              <div className="flex justify-between"><span>Devoluciones</span><span>{fmt(report.refundTotal)}</span></div>
              <div className="flex justify-between"><span>Entradas</span><span>{fmt(report.cashIn)}</span></div>
              <div className="flex justify-between"><span>Salidas</span><span>{fmt(report.manualCashOut)}</span></div>
              <div className="border-t border-dashed border-gray-300 my-1" />

              <p className="font-bold text-xs mt-2">Arqueo</p>
              <div className="flex justify-between"><span>Base apertura</span><span>{fmt(report.openingAmount)}</span></div>
              <div className="flex justify-between"><span>Cierre declarado</span><span>{fmt(report.closingAmount)}</span></div>
              <div className="flex justify-between"><span>Esperado</span><span>{fmt(report.expectedCash)}</span></div>
              <div className={`flex justify-between font-bold ${(report.difference ?? 0) < 0 ? 'text-red-600' : (report.difference ?? 0) > 0 ? 'text-green-600' : ''}`}>
                <span>Diferencia</span><span>{fmt(report.difference)}</span>
              </div>
              {report.notes && <p className="pt-2 text-gray-600">Observaciones: {report.notes}</p>}

              {report.paymentBreakdown?.length > 0 && (
                <>
                  <div className="border-t border-dashed border-gray-300 my-1" />
                  <p className="font-bold text-xs mt-2">Desglose por Método</p>
                  {report.paymentBreakdown.map((p, i) => (
                    <div key={i} className="flex justify-between">
                      <span>{PAYMENT_LABELS[p.method] || p.method} ({p.transactionCount})</span>
                      <span>{fmt(p.totalAmount)}</span>
                    </div>
                  ))}
                </>
              )}

              {report.movements?.length > 0 && (
                <>
                  <div className="border-t border-dashed border-gray-300 my-1" />
                  <p className="font-bold text-xs mt-2">Movimientos Manuales</p>
                  {report.movements.map((m, i) => (
                    <div key={i} className="text-[10px]">
                      {m.type === 'in' ? '↑' : '↓'} {fmt(m.amount)} — {m.reason}
                    </div>
                  ))}
                </>
              )}
            </div>
          )}
        </div>

        <div className="p-4 border-t flex gap-2">
          <button
            onClick={onClose}
            className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 text-sm font-medium"
          >
            Cerrar
          </button>
          <button
            onClick={handlePrint}
            disabled={!report || printing}
            className="flex-1 flex items-center justify-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 text-sm font-medium"
          >
            {printing ? <Loader2 size={16} className="animate-spin" /> : <Printer size={16} />}
            {printing ? 'Imprimiendo...' : 'Imprimir cierre'}
          </button>
        </div>
      </div>
    </div>
  );
}
