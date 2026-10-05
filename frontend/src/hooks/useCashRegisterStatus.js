import { useQuery } from '@tanstack/react-query';
import useAuthStore from '../stores/authStore';
import useCompanyFeatures from './useCompanyFeatures';
import { cashRegisterService } from '../services/cashRegisterService';

const useCashRegisterStatus = () => {
  const { isAuthenticated, tenantId, branchId } = useAuthStore();
  const features = useCompanyFeatures();
  // Cash feature availability is not CashOperator permission: waiters can read ONLY status.
  const cashEnabled = features.hasFeature('cash');
  const operationalAccess = features.canAccess('restaurant') || features.canAccess('pos');
  const enabled = isAuthenticated && !!tenantId && !!branchId && cashEnabled && operationalAccess;
  const query = useQuery({
    queryKey: ['cash-register-status', tenantId, branchId],
    queryFn: cashRegisterService.getStatus,
    enabled,
    refetchInterval: 30_000,
  });

  let label = 'CARGANDO';
  let className = 'bg-gray-100 text-gray-600';
  if (!isAuthenticated) {
    label = 'SIN SESIÓN';
  } else if (!tenantId || !branchId) {
    label = 'SIN CONTEXTO DE SUCURSAL';
  } else if (features.isError || (enabled && query.isError)) {
    label = 'NO SE PUDO VERIFICAR';
    className = 'bg-amber-50 text-amber-700';
  } else if (!operationalAccess) {
    label = features.isReady ? 'NO DISPONIBLE PARA TU ROL' : 'CARGANDO';
  } else if (features.isReady && !cashEnabled) {
    label = 'NO HABILITADA';
  } else if (enabled && query.isSuccess) {
    // Fail closed if the endpoint payload is missing, belongs to another branch or is malformed.
    const status = query.data?.data;
    if (status?.branchId !== branchId || !['open', 'closed'].includes(status?.status)) {
      label = 'NO SE PUDO VERIFICAR';
      className = 'bg-amber-50 text-amber-700';
    } else {
      label = status.status === 'open' ? 'ABIERTA' : 'CERRADA';
      className = status.status === 'open' ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-700';
    }
  }

  return { label, className, canRetry: enabled && label === 'NO SE PUDO VERIFICAR', refetch: query.refetch };
};

export default useCashRegisterStatus;
