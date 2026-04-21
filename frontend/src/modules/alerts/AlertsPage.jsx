/**
 * Página de Alertas
 * ¿Qué es? Vista detallada de alertas activas del inventario
 * ¿Para qué? Priorizar acciones operativas desde la campana del header
 */

import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import {
  AlertTriangle,
  Bell,
  ChevronRight,
  Clock3,
  PackagePlus,
  RefreshCcw,
  Siren,
  Truck,
} from 'lucide-react';
import toast from 'react-hot-toast';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import AddStockModal from '../inventory/components/AddStockModal';

const severityMap = {
  critical: {
    label: 'Crítica',
    badge: 'bg-red-100 text-red-700',
    accent: 'bg-red-50 border-red-200',
    icon: AlertTriangle,
  },
  high: {
    label: 'Alta',
    badge: 'bg-orange-100 text-orange-700',
    accent: 'bg-orange-50 border-orange-200',
    icon: Siren,
  },
  medium: {
    label: 'Media',
    badge: 'bg-yellow-100 text-yellow-700',
    accent: 'bg-yellow-50 border-yellow-200',
    icon: Bell,
  },
  low: {
    label: 'Baja',
    badge: 'bg-blue-100 text-blue-700',
    accent: 'bg-blue-50 border-blue-200',
    icon: Bell,
  },
};

const typeMap = {
  out_of_stock: {
    label: 'Sin stock',
    description: 'El producto ya no tiene unidades disponibles.',
  },
  low_stock: {
    label: 'Stock bajo',
    description: 'Conviene reponer pronto para evitar quiebres de inventario.',
  },
};

const formatDate = (value) => {
  if (!value) return 'Sin fecha';

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return 'Sin fecha';

  return new Intl.DateTimeFormat('es-CO', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(parsed);
};

const AlertsPage = () => {
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [addStockTarget, setAddStockTarget] = useState(null);

  const { data: alertsData, isLoading: alertsLoading, refetch } = useQuery({
    queryKey: ['alerts', branchId],
    queryFn: () => inventoryService.getAlerts(branchId),
    enabled: !!branchId,
    refetchInterval: 30000,
  });

  const { data: stockData } = useQuery({
    queryKey: ['stock', branchId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId,
  });

  const alerts = alertsData?.data || [];
  const stockItems = stockData?.data || [];

  const stats = useMemo(() => {
    const critical = alerts.filter((item) => item.severity?.toLowerCase() === 'critical').length;
    const high = alerts.filter((item) => item.severity?.toLowerCase() === 'high').length;
    const out = alerts.filter((item) => item.alertType?.toLowerCase() === 'out_of_stock').length;

    return [
      {
        label: 'Alertas activas',
        value: alerts.length,
        color: 'text-red-600',
        bgColor: 'bg-red-100',
        icon: Bell,
      },
      {
        label: 'Críticas',
        value: critical,
        color: 'text-orange-600',
        bgColor: 'bg-orange-100',
        icon: AlertTriangle,
      },
      {
        label: 'Sin stock',
        value: out,
        color: 'text-yellow-600',
        bgColor: 'bg-yellow-100',
        icon: PackagePlus,
      },
      {
        label: 'Alta severidad',
        value: high,
        color: 'text-blue-600',
        bgColor: 'bg-blue-100',
        icon: Siren,
      },
    ];
  }, [alerts]);

  const getStockItemForAlert = (alert) =>
    stockItems.find((item) => item.productId === alert.productId) || null;

  const handleAddStock = async ({ quantity, unitCost }) => {
    if (!addStockTarget || !branchId) {
      return;
    }

    await inventoryService.addStock({
      branchId,
      productId: addStockTarget.productId,
      quantity,
      unitCost,
      notes: `Reposición rápida desde alertas para ${addStockTarget.productName}`,
    });

    toast.success(`+${quantity} unidades agregadas`);
    queryClient.invalidateQueries({ queryKey: ['stock'] });
    queryClient.invalidateQueries({ queryKey: ['lowStock'] });
    queryClient.invalidateQueries({ queryKey: ['alerts'] });
  };

  return (
    <div className="flex flex-col -m-4 h-[calc(100%+2rem)] overflow-hidden">
      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-red-50 flex items-center justify-center">
            <Bell size={20} className="text-red-500" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Alertas</h1>
            <p className="text-sm text-gray-500">Prioriza quiebres de stock y toma acción rápida</p>
          </div>
        </div>
        <button
          onClick={() => refetch()}
          className="flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
        >
          <RefreshCcw className="h-4 w-4" /> Actualizar
        </button>
      </div>

      <div className="flex-1 overflow-y-auto p-4 md:p-6 flex flex-col gap-6">
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {stats.map((stat) => {
          const Icon = stat.icon;
          return (
            <div key={stat.label} className="card">
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">{stat.label}</p>
                  <p className="mt-2 text-3xl font-bold text-gray-900">{stat.value}</p>
                </div>
                <div className={`rounded-lg p-3 ${stat.bgColor}`}>
                  <Icon className={`h-6 w-6 ${stat.color}`} />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      <div className="flex-1 overflow-y-auto rounded-xl border border-gray-200 bg-white">
        {alertsLoading ? (
          <div className="flex h-full items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-500 border-t-transparent" />
          </div>
        ) : alerts.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center px-6 text-center text-gray-400">
            <Bell className="mb-4 h-16 w-16 opacity-40" />
            <p className="text-lg font-medium text-gray-600">No hay alertas activas</p>
            <p className="mt-1 text-sm">Todo se ve estable por ahora en esta sucursal.</p>
          </div>
        ) : (
          <div className="divide-y divide-gray-100">
            {alerts.map((alert) => {
              const severity = severityMap[alert.severity?.toLowerCase()] || severityMap.medium;
              const type = typeMap[alert.alertType?.toLowerCase()] || {
                label: alert.alertType || 'Alerta',
                description: 'Revisa este evento para definir la acción adecuada.',
              };
              const SeverityIcon = severity.icon;
              const stockItem = getStockItemForAlert(alert);

              return (
                <div key={alert.id} className="p-4 sm:p-5">
                  <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                    <div className="flex min-w-0 gap-4">
                      <div className={`flex h-12 w-12 flex-shrink-0 items-center justify-center rounded-xl border ${severity.accent}`}>
                        <SeverityIcon className="h-5 w-5 text-gray-700" />
                      </div>

                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <h2 className="text-base font-semibold text-gray-900">
                            {alert.productName || 'Producto sin nombre'}
                          </h2>
                          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${severity.badge}`}>
                            {severity.label}
                          </span>
                          <span className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-700">
                            {type.label}
                          </span>
                        </div>

                        <p className="mt-1 text-sm text-gray-500">
                          SKU: {alert.sku || 'Sin SKU'} <span className="mx-2 text-gray-300">•</span>
                          Estado: {alert.status || 'active'}
                        </p>

                        <p className="mt-3 text-sm text-gray-700">
                          {alert.message || type.description}
                        </p>

                        <div className="mt-3 flex flex-wrap items-center gap-3 text-xs text-gray-500">
                          <span className="inline-flex items-center gap-1.5">
                            <Clock3 className="h-3.5 w-3.5" />
                            {formatDate(alert.createdAt)}
                          </span>
                          {stockItem && (
                            <span className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 px-2.5 py-1 font-medium text-gray-700">
                              Stock actual: {stockItem.quantity} {stockItem.unit || ''}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>

                    <div className="flex flex-shrink-0 flex-wrap gap-2 lg:justify-end">
                      <button
                        onClick={() => stockItem && setAddStockTarget(stockItem)}
                        disabled={!stockItem}
                        className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-primary-700 disabled:opacity-50"
                      >
                        <PackagePlus className="h-4 w-4" />
                        Agregar stock
                      </button>
                      <button
                        onClick={() => navigate('/suppliers')}
                        className="flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
                      >
                        <Truck className="h-4 w-4" />
                        Pedir a proveedor
                      </button>
                    </div>
                  </div>

                  <div className="mt-4 rounded-xl bg-gray-50 px-4 py-3 text-sm text-gray-600">
                    <span className="font-medium text-gray-800">Sugerencia:</span>{' '}
                    {alert.alertType?.toLowerCase() === 'out_of_stock'
                      ? 'Repón este producto hoy o valida sustitutos para no frenar ventas.'
                      : 'Programa reposición antes de que la mesa o la operación consuman el stock restante.'}
                    <span className="ml-2 inline-flex items-center gap-1 text-primary-600">
                      Revisar ahora
                      <ChevronRight className="h-3.5 w-3.5" />
                    </span>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      </div>

      <AddStockModal
        isOpen={!!addStockTarget}
        onClose={() => setAddStockTarget(null)}
        onConfirm={handleAddStock}
        product={addStockTarget}
      />
    </div>
  );
};

export default AlertsPage;
