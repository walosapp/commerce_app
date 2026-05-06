import { useQuery } from '@tanstack/react-query';
import { X, Printer, Loader2 } from 'lucide-react';
import printService from '../../../services/printService';
import { thermalKitchenStyles, openPrintWindow } from './printStyles';

export default function KitchenTicket({ orderId, onClose }) {
  const { data, isLoading, error } = useQuery({
    queryKey: ['kitchen-ticket', orderId],
    queryFn: () => printService.getKitchenTicket(orderId),
    enabled: !!orderId,
  });

  const ticket = data?.data;

  const buildTicketHtml = () => {
    if (!ticket) return '';
    const date = new Date(ticket.createdAt);
    const timeStr = date.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });

    const itemsHtml = ticket.items
      .map(
        (i) =>
          `<div class="kitchen-item">${Number(i.quantity)}x ${i.productName}</div>${i.notes ? `<div class="kitchen-note">→ ${i.notes}</div>` : ''}`
      )
      .join('');

    return `
      <div class="kitchen-header">COMANDA COCINA</div>
      <div class="kitchen-meta">${ticket.tableName} — ${timeStr}</div>
      <div class="kitchen-meta">${ticket.orderNumber}</div>
      <div class="kitchen-divider"></div>
      ${itemsHtml}
      <div class="kitchen-divider"></div>
      <div class="kitchen-meta">Cajero: ${ticket.cashierName}</div>
    `;
  };

  const handlePrint = () => {
    openPrintWindow(buildTicketHtml(), thermalKitchenStyles);
  };

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-sm max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between p-4 border-b">
          <h3 className="text-lg font-semibold text-gray-900">Comanda de Cocina</h3>
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
            <div className="text-red-600 text-center py-8">Error al cargar comanda</div>
          )}

          {ticket && (
            <div className="font-mono text-sm space-y-2 border border-gray-200 rounded-lg p-4 bg-gray-50">
              <p className="text-center font-bold text-lg">COMANDA COCINA</p>
              <div className="text-center text-gray-600">
                {ticket.tableName} — {new Date(ticket.createdAt).toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' })}
              </div>
              <div className="text-center text-gray-500 text-xs">{ticket.orderNumber}</div>
              <div className="border-t-2 border-dashed border-gray-400 my-2" />
              <div className="space-y-2">
                {ticket.items.map((item, i) => (
                  <div key={i}>
                    <p className="font-bold text-base">{Number(item.quantity)}x {item.productName}</p>
                    {item.notes && (
                      <p className="text-gray-500 pl-3 italic text-xs">→ {item.notes}</p>
                    )}
                  </div>
                ))}
              </div>
              <div className="border-t-2 border-dashed border-gray-400 my-2" />
              <p className="text-center text-gray-500 text-xs">Cajero: {ticket.cashierName}</p>
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
            disabled={!ticket}
            className="flex-1 flex items-center justify-center gap-2 px-4 py-2 bg-orange-600 text-white rounded-lg hover:bg-orange-700 disabled:opacity-50 text-sm font-medium"
          >
            <Printer size={16} /> Imprimir Comanda
          </button>
        </div>
      </div>
    </div>
  );
}
