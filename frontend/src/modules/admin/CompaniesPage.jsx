/**
 * CompaniesPage
 * Panel superadmin para gestión de comercios, suscripciones y facturación B2B.
 */

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Building2, ChevronRight, CheckCircle, XCircle, Clock, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import platformService from '../../services/platformService';
import CompanyPlanPanel from './components/CompanyPlanPanel';

const statusBadge = (status) => {
  if (status === 'paid') return <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-700">Pagada</span>;
  if (status === 'overdue') return <span className="inline-flex items-center rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-700">Vencida</span>;
  if (status === 'sent') return <span className="inline-flex items-center rounded-full bg-yellow-100 px-2 py-0.5 text-xs font-medium text-yellow-700">Pendiente</span>;
  return null;
};

const CompaniesPage = () => {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [selectedCompanyId, setSelectedCompanyId] = useState(null);

  const { data: response, isLoading } = useQuery({
    queryKey: ['admin-companies'],
    queryFn: platformService.getAdminCompanies,
  });

  const companies = response?.data ?? [];
  const filtered = companies.filter((c) =>
    c.name.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <div className="flex flex-col h-[calc(100vh-7rem)] overflow-hidden">
      <div className="flex items-center justify-between mb-4 flex-shrink-0">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Gestión de Comercios</h1>
          <p className="mt-1 text-sm text-gray-500">Suscripciones, facturación y configuración de IA</p>
        </div>
      </div>

      <div className="relative mb-4 flex-shrink-0">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-gray-400" />
        <input
          className="input pl-10"
          placeholder="Buscar comercio..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </div>

      <div className="flex flex-1 gap-4 overflow-hidden">
        <div className="flex-1 overflow-y-auto">
          {isLoading ? (
            <div className="flex items-center justify-center h-40 text-gray-400">Cargando...</div>
          ) : filtered.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-40 text-gray-400">
              <Building2 className="h-10 w-10 mb-2" />
              <p>No hay comercios</p>
            </div>
          ) : (
            <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
              <table className="min-w-full divide-y divide-gray-200">
                <thead>
                  <tr className="bg-gray-50">
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Comercio</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Plan</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Servicios</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Próx. factura</th>
                    <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500">Estado</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {filtered.map((company) => (
                    <tr
                      key={company.id}
                      className={`hover:bg-gray-50 transition-colors cursor-pointer ${selectedCompanyId === company.id ? 'bg-primary-50' : ''}`}
                      onClick={() => setSelectedCompanyId(company.id === selectedCompanyId ? null : company.id)}
                    >
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary-100">
                            <Building2 className="h-4 w-4 text-primary-600" />
                          </div>
                          <div>
                            <p className="text-sm font-medium text-gray-900">{company.name}</p>
                            <p className="text-xs text-gray-400">{company.isActive ? 'Activo' : 'Inactivo'}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-4 py-3">
                        <span className="inline-flex items-center rounded-full bg-blue-100 px-2 py-0.5 text-xs font-medium text-blue-700 capitalize">
                          {company.subscriptionPlan}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-700">
                        {company.activeServices} activo{company.activeServices !== 1 ? 's' : ''}
                      </td>
                      <td className="px-4 py-3 text-sm text-gray-500">
                        {company.nextBillingDate ?? '—'}
                      </td>
                      <td className="px-4 py-3">
                        {statusBadge(company.pendingInvoiceStatus)}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <ChevronRight className={`h-4 w-4 text-gray-400 transition-transform ${selectedCompanyId === company.id ? 'rotate-90' : ''}`} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {selectedCompanyId && (
          <CompanyPlanPanel
            companyId={selectedCompanyId}
            onClose={() => setSelectedCompanyId(null)}
          />
        )}
      </div>
    </div>
  );
};

export default CompaniesPage;
