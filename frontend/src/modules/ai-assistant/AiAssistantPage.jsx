/**
 * Página del Asistente de IA
 * ¿Qué es? Vista principal del asistente de IA
 * ¿Para qué? Módulo independiente para interactuar con la IA
 */

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { AlertCircle, Info, X } from 'lucide-react';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import AIChat from './components/AIChat';
import toast from 'react-hot-toast';

const AiAssistantPage = () => {
  const { branchId } = useAuthStore();
  const [showTips, setShowTips] = useState(false);

  const { data: lowStockData } = useQuery({
    queryKey: ['lowStock', branchId],
    queryFn: () => inventoryService.getLowStock(branchId),
    enabled: !!branchId,
  });

  const handleActionConfirmed = () => {
    toast.success('Acción confirmada y aplicada correctamente');
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Asistente de IA</h1>
          <p className="mt-1 text-sm text-gray-500">
            Gestiona tu negocio con asistencia de inteligencia artificial
          </p>
        </div>
        <button
          onClick={() => setShowTips(!showTips)}
          className="flex items-center gap-2 rounded-lg border border-primary-200 bg-primary-50 px-3 py-2 text-sm font-medium text-primary-700 transition-colors hover:bg-primary-100"
          title="Consejos de uso"
        >
          <Info className="h-4 w-4" />
          <span className="hidden sm:inline">Ayuda</span>
        </button>
      </div>

      {/* Tips Panel - collapsible */}
      {showTips && (
        <div className="relative rounded-lg border border-primary-200 bg-primary-50 p-4">
          <button
            onClick={() => setShowTips(false)}
            className="absolute right-3 top-3 rounded p-1 text-primary-400 hover:bg-primary-100 hover:text-primary-600"
          >
            <X className="h-4 w-4" />
          </button>
          <h3 className="mb-3 font-semibold text-primary-900">
            💡 Consejos Rápidos
          </h3>
          <div className="grid gap-2 sm:grid-cols-2">
            <p className="text-sm text-primary-800">
              • Usa el micrófono para registrar pedidos más rápido
            </p>
            <p className="text-sm text-primary-800">
              • Di "Me llegaron X productos a Y pesos" para registrar entradas
            </p>
            <p className="text-sm text-primary-800">
              • Pregunta "¿Cuánto estoy ganando?" para ver márgenes
            </p>
            <p className="text-sm text-primary-800">
              • El asistente te alertará automáticamente sobre stock bajo
            </p>
            <p className="text-sm text-primary-800">
              • Consulta reportes de ventas, proveedores y más
            </p>
          </div>
        </div>
      )}

      {/* Low Stock Alert */}
      {lowStockData && lowStockData.count > 0 && (
        <div className="rounded-lg border-l-4 border-yellow-500 bg-yellow-50 p-4">
          <div className="flex items-start gap-3">
            <AlertCircle className="h-5 w-5 shrink-0 text-yellow-600" />
            <div>
              <h3 className="font-semibold text-yellow-900">
                Productos con Stock Bajo ({lowStockData.count})
              </h3>
              <ul className="mt-2 space-y-1">
                {lowStockData.data.slice(0, 5).map((item) => (
                  <li key={item.id} className="text-sm text-yellow-800">
                    • {item.product_name}: {item.quantity} {item.unit}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Chat - full width */}
      <AIChat onActionConfirmed={handleActionConfirmed} />
    </div>
  );
};

export default AiAssistantPage;
