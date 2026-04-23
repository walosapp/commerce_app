/**
 * CompanyPlanPanel
 * Panel lateral de detalle de un comercio: servicios, facturas, config IA.
 */

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  X, Package, FileText, Cpu, ToggleLeft, ToggleRight,
  CheckCircle, XCircle, Plus, RefreshCw
} from 'lucide-react';
import toast from 'react-hot-toast';
import platformService from '../../../services/platformService';
import { formatCurrency } from '../../../utils/formatCurrency';

const TABS = ['Servicios', 'Facturas', 'IA'];

const statusBadge = (status) => {
  const map = {
    draft: ['bg-gray-100 text-gray-600', 'Borrador'],
    sent: ['bg-yellow-100 text-yellow-700', 'Enviada'],
    paid: ['bg-green-100 text-green-700', 'Pagada'],
    overdue: ['bg-red-100 text-red-700', 'Vencida'],
    cancelled: ['bg-gray-100 text-gray-400', 'Cancelada'],
  };
  const [cls, label] = map[status] ?? ['bg-gray-100 text-gray-600', status];
  return <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>{label}</span>;
};

const CompanyPlanPanel = ({ companyId, onClose }) => {
  const queryClient = useQueryClient();
  const [tab, setTab] = useState('Servicios');

  const { data: response, isLoading } = useQuery({
    queryKey: ['admin-company-plan', companyId],
    queryFn: () => platformService.getAdminCompanyPlan(companyId),
    enabled: !!companyId,
  });

  const plan = response?.data;

  const toggleServiceMutation = useMutation({
    mutationFn: ({ serviceCode, isActive, currentData }) =>
      platformService.updateService(companyId, serviceCode, { ...currentData, isActive }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-company-plan', companyId] });
      queryClient.invalidateQueries({ queryKey: ['admin-companies'] });
      toast.success('Servicio actualizado');
    },
    onError: () => toast.error('Error al actualizar servicio'),
  });

  const updateInvoiceMutation = useMutation({
    mutationFn: ({ invoiceId, status }) =>
      platformService.updateInvoiceStatus(invoiceId, { status }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-company-plan', companyId] });
      toast.success('Estado de factura actualizado');
    },
    onError: () => toast.error('Error al actualizar factura'),
  });

  return (
    <>
      <div className="fixed inset-0 z-[60] bg-black/40 md:hidden" onClick={onClose} />
      <div className="w-full max-w-md flex flex-col bg-white border-l border-gray-200 flex-shrink-0 overflow-hidden">
        <div className="flex items-center justify-between border-b px-5 py-4 flex-shrink-0">
          <div>
            <h2 className="text-lg font-bold text-gray-900">{plan?.companyName || 'Comercio'}</h2>
            <p className="text-xs text-gray-500 capitalize">Plan: {plan?.subscriptionPlan}</p>
          </div>
          <button onClick={onClose} className="rounded-lg p-1.5 hover:bg-gray-100 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex border-b flex-shrink-0">
          {TABS.map((t) => (
            <button
              key={t}
              onClick={() => setTab(t)}
              className={`flex-1 py-2.5 text-sm font-medium transition-colors ${
                tab === t
                  ? 'border-b-2 border-primary-600 text-primary-600'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              {t}
            </button>
          ))}
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {isLoading ? (
            <div className="flex items-center justify-center h-32 text-gray-400">Cargando...</div>
          ) : (
            <>
              {tab === 'Servicios' && (
                <div className="space-y-3">
                  {(plan?.services ?? []).map((svc) => (
                    <div key={svc.serviceCode} className="flex items-center justify-between rounded-lg border border-gray-200 p-3">
                      <div>
                        <p className="text-sm font-medium text-gray-900">{svc.serviceName}</p>
                        <p className="text-xs text-gray-500">
                          {formatCurrency(svc.effectivePrice)} / {svc.billingFrequency === 'monthly' ? 'mes' : 'año'}
                        </p>
                        {svc.nextBillingDate && (
                          <p className="text-xs text-gray-400">Próx: {svc.nextBillingDate}</p>
                        )}
                      </div>
                      <button
                        onClick={() => toggleServiceMutation.mutate({
                          serviceCode: svc.serviceCode,
                          isActive: !svc.isActive,
                          currentData: svc,
                        })}
                        className="text-gray-400 hover:text-primary-600 transition-colors"
                        title={svc.isActive ? 'Desactivar' : 'Activar'}
                      >
                        {svc.isActive
                          ? <ToggleRight className="h-6 w-6 text-primary-600" />
                          : <ToggleLeft className="h-6 w-6" />}
                      </button>
                    </div>
                  ))}
                  {(plan?.services ?? []).length === 0 && (
                    <p className="text-sm text-gray-400 text-center py-8">Sin servicios asignados</p>
                  )}
                </div>
              )}

              {tab === 'Facturas' && (
                <div className="space-y-3">
                  {(plan?.recentInvoices ?? []).map((inv) => (
                    <div key={inv.id} className="rounded-lg border border-gray-200 p-3">
                      <div className="flex items-center justify-between mb-1">
                        <p className="text-sm font-medium text-gray-900">{inv.invoiceNumber}</p>
                        {statusBadge(inv.status)}
                      </div>
                      <p className="text-xs text-gray-500">{inv.periodStart} → {inv.periodEnd}</p>
                      <p className="text-sm font-bold text-primary-600 mt-1">{formatCurrency(inv.total)}</p>
                      {(inv.status === 'draft' || inv.status === 'sent') && (
                        <div className="flex gap-2 mt-2">
                          {inv.status === 'draft' && (
                            <button
                              onClick={() => updateInvoiceMutation.mutate({ invoiceId: inv.id, status: 'sent' })}
                              className="flex items-center gap-1 rounded-lg bg-primary-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-primary-700 transition-colors"
                            >
                              Enviar
                            </button>
                          )}
                          <button
                            onClick={() => updateInvoiceMutation.mutate({ invoiceId: inv.id, status: 'paid' })}
                            className="flex items-center gap-1 rounded-lg bg-green-600 px-2.5 py-1 text-xs font-medium text-white hover:bg-green-700 transition-colors"
                          >
                            Marcar pagada
                          </button>
                        </div>
                      )}
                    </div>
                  ))}
                  {(plan?.recentInvoices ?? []).length === 0 && (
                    <p className="text-sm text-gray-400 text-center py-8">Sin facturas</p>
                  )}
                </div>
              )}

              {tab === 'IA' && plan?.aiSettings && (
                <div className="space-y-4">
                  <div className="rounded-lg border border-gray-200 p-4">
                    <div className="flex items-center justify-between mb-3">
                      <p className="text-sm font-medium text-gray-900">Modo de API Key</p>
                      {plan.aiSettings.aiKeyManaged
                        ? <span className="inline-flex rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700">Gestionada por Walos</span>
                        : <span className="inline-flex rounded-full bg-purple-100 px-2 py-0.5 text-xs font-medium text-purple-700">Propia del comercio</span>}
                    </div>
                    <p className="text-xs text-gray-500">Proveedor: <span className="font-medium">{plan.aiSettings.aiProvider}</span></p>
                    <p className="text-xs text-gray-500">Key configurada: <span className="font-medium">{plan.aiSettings.hasCustomKey ? 'Sí' : 'No'}</span></p>
                  </div>
                  <div className="rounded-lg border border-gray-200 p-4">
                    <p className="text-sm font-medium text-gray-900 mb-2">Consumo del período</p>
                    <p className="text-2xl font-bold text-primary-600">{plan.aiSettings.aiTokensUsed.toLocaleString()}</p>
                    <p className="text-xs text-gray-500">tokens utilizados</p>
                    <p className="text-sm font-medium text-gray-700 mt-2">{formatCurrency(plan.aiSettings.aiEstimatedCost)} estimado</p>
                    {plan.aiSettings.aiTokensResetAt && (
                      <p className="text-xs text-gray-400 mt-1">Reset: {new Date(plan.aiSettings.aiTokensResetAt).toLocaleDateString()}</p>
                    )}
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </>
  );
};

export default CompanyPlanPanel;
