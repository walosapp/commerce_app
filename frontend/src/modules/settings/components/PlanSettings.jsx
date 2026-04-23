/**
 * PlanSettings
 * Vista de solo lectura del plan y facturas del comercio.
 */

import { useQuery } from '@tanstack/react-query';
import { FileText, CheckCircle, Clock, XCircle, Download } from 'lucide-react';
import platformService from '../../../services/platformService';
import { formatCurrency } from '../../../utils/formatCurrency';

const statusMeta = {
  draft:     { label: 'Borrador',  cls: 'bg-gray-100 text-gray-600' },
  sent:      { label: 'Enviada',   cls: 'bg-yellow-100 text-yellow-700' },
  paid:      { label: 'Pagada',    cls: 'bg-green-100 text-green-700' },
  overdue:   { label: 'Vencida',   cls: 'bg-red-100 text-red-700' },
  cancelled: { label: 'Cancelada', cls: 'bg-gray-100 text-gray-400' },
};

const PlanSettings = () => {
  const { data: planRes, isLoading: planLoading } = useQuery({
    queryKey: ['my-plan'],
    queryFn: platformService.getMyPlan,
  });

  const { data: invoicesRes, isLoading: invoicesLoading } = useQuery({
    queryKey: ['my-invoices'],
    queryFn: platformService.getMyInvoices,
  });

  const plan = planRes?.data;
  const invoices = invoicesRes?.data ?? [];

  if (planLoading) return <div className="text-center py-8 text-gray-400">Cargando plan...</div>;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 mb-3">Servicios contratados</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {(plan?.services ?? []).map((svc) => (
            <div key={svc.serviceCode} className="flex items-center gap-3 rounded-xl border border-gray-200 bg-white p-4">
              <CheckCircle className="h-5 w-5 text-green-500 flex-shrink-0" />
              <div>
                <p className="text-sm font-medium text-gray-900">{svc.serviceName}</p>
                <p className="text-xs text-gray-500">
                  {svc.effectivePrice === 0 ? 'Incluido' : `${formatCurrency(svc.effectivePrice)} / ${svc.billingFrequency === 'monthly' ? 'mes' : 'año'}`}
                </p>
                {svc.nextBillingDate && (
                  <p className="text-xs text-gray-400 mt-0.5 flex items-center gap-1">
                    <Clock className="h-3 w-3" /> Próx: {svc.nextBillingDate}
                  </p>
                )}
              </div>
            </div>
          ))}
          {(plan?.services ?? []).length === 0 && (
            <p className="text-sm text-gray-400 col-span-2">Sin servicios asignados</p>
          )}
        </div>
      </div>

      <div>
        <h2 className="text-base font-semibold text-gray-900 mb-3">Historial de facturas</h2>
        {invoicesLoading ? (
          <p className="text-sm text-gray-400">Cargando...</p>
        ) : invoices.length === 0 ? (
          <p className="text-sm text-gray-400">Sin facturas</p>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
            <table className="min-w-full divide-y divide-gray-200">
              <thead>
                <tr className="bg-gray-50">
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Factura</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Período</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Total</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Estado</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {invoices.map((inv) => {
                  const meta = statusMeta[inv.status] ?? statusMeta.draft;
                  return (
                    <tr key={inv.id} className="hover:bg-gray-50 transition-colors">
                      <td className="px-4 py-3 text-sm font-medium text-gray-900">{inv.invoiceNumber}</td>
                      <td className="px-4 py-3 text-sm text-gray-500">{inv.periodStart} → {inv.periodEnd}</td>
                      <td className="px-4 py-3 text-sm font-bold text-primary-600">{formatCurrency(inv.total)}</td>
                      <td className="px-4 py-3">
                        <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${meta.cls}`}>{meta.label}</span>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
};

export default PlanSettings;
