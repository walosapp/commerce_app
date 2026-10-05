import { useEffect, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Wallet } from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../stores/authStore';
import useCompanyFeatures from '../../hooks/useCompanyFeatures';
import { canOperateCash } from '../../config/companyFeatures';
import { cashRegisterService } from '../../services/cashRegisterService';
import printService from '../../services/printService';
import usePrintAgentStore from '../../stores/printAgentStore';
import CashRegisterBar from '../sales/components/CashRegisterBar';
import CashSummaryView from '../sales/components/CashSummaryView';
import OpenCashRegisterModal from '../sales/components/OpenCashRegisterModal';
import CloseCashRegisterModal from '../sales/components/CloseCashRegisterModal';
import CashMovementModal from '../sales/components/CashMovementModal';
import CashRegisterHistory from '../sales/components/CashRegisterHistory';

// Do not mount financial queries or render cached data outside an authorized context.
const CashPage = () => {
  const { isAuthenticated, tenantId, branchId, user } = useAuthStore();
  const { canAccess } = useCompanyFeatures();

  if (!isAuthenticated || !canOperateCash(user) || !canAccess('cash')) {
    return <div role="alert" className="p-6 text-sm text-gray-600">No tenés acceso a la gestión de caja.</div>;
  }
  if (!tenantId || !branchId) {
    return <div role="alert" className="p-6 text-sm text-amber-700">Necesitás una empresa y una sucursal válida para gestionar la caja. Revisá tu asignación e iniciá sesión nuevamente.</div>;
  }

  return <CashWorkspace key={`${tenantId}:${branchId}:${user?.id}:${user?.role}`} tenantId={tenantId} branchId={branchId} user={user} />;
};

const CashWorkspace = ({ tenantId, branchId, user }) => {
  const queryClient = useQueryClient();
  const printCashClose = usePrintAgentStore(state => state.printCashClose);
  const [showOpen, setShowOpen] = useState(false);
  const [showClose, setShowClose] = useState(false);
  const [movementType, setMovementType] = useState(null);
  const [showHistory, setShowHistory] = useState(false);
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['cash-register-active', tenantId, branchId],
    queryFn: () => cashRegisterService.getActive(),
    refetchInterval: 60000,
  });
  const register = data?.data || null;
  const stateVerified = !isLoading && !isError;

  useEffect(() => {
    if (register) setShowOpen(false);
    setShowClose(false);
    setMovementType(null);
  }, [register?.id]);

  const refreshCash = () => {
    queryClient.invalidateQueries({ queryKey: ['cash-register-active'] });
    queryClient.invalidateQueries({ queryKey: ['cash-register-history'] });
    queryClient.invalidateQueries({ queryKey: ['cash-register-movements'] });
  };

  const runOperation = async (operation, successMessage) => {
    let result;
    try {
      result = await operation();
    } catch (error) {
      toast.error(error?.response?.data?.message || error?.message || 'No fue posible completar la operación de caja');
      throw error;
    }
    toast.success(successMessage);
    refreshCash();
    return result;
  };

  const handleOpen = async (payload) => {
    await runOperation(() => cashRegisterService.open(payload), 'Caja abierta exitosamente');
    setShowOpen(false);
  };
  const handleClose = async (id, payload) => {
    const closed = await runOperation(() => cashRegisterService.close(id, payload), 'Caja cerrada exitosamente');
    setShowClose(false);
    // Printing is a separate outcome: never retry/revert a persisted close on hardware failure.
    if (window.confirm('Caja cerrada. ¿Deseas imprimir el cierre?')) {
      try {
        const report = await printService.getZReport(closed.data.id);
        await printCashClose(report.data);
        toast.success('Cierre enviado a la impresora');
      } catch (error) {
        toast.error(error?.message || 'La caja cerró, pero no fue posible imprimir el cierre');
      }
    }
  };
  const handleMovement = async (id, payload) => {
    await runOperation(() => cashRegisterService.addMovement(id, payload), payload.type === 'in' ? 'Entrada registrada' : 'Salida registrada');
    setMovementType(null);
  };

  return (
    <div className="flex h-full flex-col overflow-hidden bg-gray-50">
      <header className="flex flex-shrink-0 flex-wrap items-center justify-between gap-3 border-b border-gray-200 bg-white px-4 py-4 md:px-6">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary-50 text-primary-600"><Wallet size={22} /></div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Caja</h1>
            <p className="text-xs text-gray-500">Apertura, cierre y movimientos del turno</p>
          </div>
        </div>
        <div className="text-xs text-gray-500">
          <p>Sucursal: <span className="font-semibold text-gray-700">{register?.branchName || user?.branchName || `#${branchId}`}</span></p>
          <p>Operador: <span className="font-semibold text-gray-700">{user?.name || 'Usuario'}</span></p>
        </div>
      </header>

      <CashRegisterBar register={register} isLoading={isLoading} isError={isError} onRetry={refetch}
        onOpen={() => setShowOpen(true)} onClose={() => setShowClose(true)}
        onMovement={setMovementType} onHistory={() => setShowHistory(true)} />

      <main className="scrollbar-subtle flex-1 overflow-y-auto p-4 md:p-6">
        {stateVerified && (register ? <CashSummaryView register={register} /> : (
          <div className="mx-auto max-w-2xl rounded-xl border border-gray-200 bg-white p-8 text-center">
            <Wallet size={36} className="mx-auto mb-3 text-gray-300" />
            <h2 className="text-base font-semibold text-gray-900">Sin turno abierto</h2>
            <p className="mt-2 text-sm text-gray-500">Abrí la caja de esta sucursal para iniciar la operación. Podés consultar los turnos anteriores en Historial.</p>
          </div>
        ))}
      </main>

      <OpenCashRegisterModal isOpen={showOpen && stateVerified && !register} onClose={() => setShowOpen(false)} onConfirm={handleOpen} />
      <CloseCashRegisterModal isOpen={showClose && stateVerified && !!register} register={register} onClose={() => setShowClose(false)} onConfirm={handleClose} />
      <CashMovementModal isOpen={!!movementType && stateVerified && !!register} registerId={register?.id} type={movementType || 'in'} onClose={() => setMovementType(null)} onConfirm={handleMovement} />
      {showHistory && <CashRegisterHistory isOpen onClose={() => setShowHistory(false)} />}
    </div>
  );
};

export default CashPage;
