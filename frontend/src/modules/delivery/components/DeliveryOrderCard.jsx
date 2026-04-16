import { Clock, Phone, MapPin, CheckCircle, ChevronRight, XCircle } from 'lucide-react';

const SOURCE_BADGES = {
  manual:     { label: 'Manual',     bg: 'bg-gray-100 text-gray-600' },
  whatsapp:   { label: 'WhatsApp',   bg: 'bg-green-100 text-green-700' },
  rappi:      { label: 'Rappi',      bg: 'bg-orange-100 text-orange-700' },
  didi_food:  { label: 'Didi',       bg: 'bg-orange-100 text-orange-600' },
  uber_eats:  { label: 'Uber Eats',  bg: 'bg-black/10 text-gray-800' },
  web:        { label: 'Web',        bg: 'bg-blue-100 text-blue-700' },
};

const NEXT_ACTION = {
  new:                { label: 'Aceptar',   action: 'accept',   color: 'text-indigo-600 hover:text-indigo-800' },
  accepted:           { label: 'Preparar',  action: 'prepare',  color: 'text-blue-600 hover:text-blue-800' },
  preparing:          { label: 'Listo',     action: 'ready',    color: 'text-yellow-600 hover:text-yellow-800' },
  ready_for_dispatch: { label: 'Despachar', action: 'dispatch', color: 'text-purple-600 hover:text-purple-800' },
  out_for_delivery:   { label: 'Entregado', action: 'deliver',  color: 'text-green-600 hover:text-green-800' },
};

const elapsedBadge = (createdAt) => {
  const mins = Math.floor((Date.now() - new Date(createdAt)) / 60000);
  if (mins < 30) return <span className="text-xs text-gray-400">{mins}m</span>;
  return <span className="text-xs font-semibold text-red-500 bg-red-50 px-1.5 py-0.5 rounded-full">{mins}m ⚠</span>;
};

const DeliveryOrderCard = ({ order, onClick, onAction }) => {
  const src = SOURCE_BADGES[order.source] ?? SOURCE_BADGES.manual;
  const next = NEXT_ACTION[order.status];

  return (
    <div
      onClick={() => onClick(order)}
      className="bg-white rounded-xl border border-gray-200 shadow-sm p-3.5 cursor-pointer hover:shadow-md hover:border-indigo-200 transition-all space-y-2"
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-bold text-gray-700">#{order.orderNumber}</span>
        <div className="flex items-center gap-1.5">
          <span className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${src.bg}`}>{src.label}</span>
          {elapsedBadge(order.createdAt)}
        </div>
      </div>

      {order.customerName && (
        <p className="text-sm font-medium text-gray-900 truncate">{order.customerName}</p>
      )}

      <div className="space-y-0.5 text-xs text-gray-500">
        {order.customerPhone && (
          <div className="flex items-center gap-1.5">
            <Phone size={11} className="shrink-0" />
            <span className="truncate">{order.customerPhone}</span>
          </div>
        )}
        {order.customerAddress && (
          <div className="flex items-center gap-1.5">
            <MapPin size={11} className="shrink-0" />
            <span className="truncate">{order.customerAddress}</span>
          </div>
        )}
      </div>

      <div className="flex items-center justify-between pt-1 border-t border-gray-100">
        <span className="text-sm font-bold text-gray-800">
          ${Number(order.total).toLocaleString('es-CO')}
        </span>
        <div className="flex items-center gap-2" onClick={e => e.stopPropagation()}>
          {next && (
            <button
              onClick={() => onAction(order, next.action)}
              className={`flex items-center gap-1 text-xs font-medium transition-colors ${next.color}`}
            >
              <CheckCircle size={13} /> {next.label}
            </button>
          )}
          {['new', 'accepted', 'preparing'].includes(order.status) && (
            <button
              onClick={() => onAction(order, 'reject')}
              className="text-xs text-red-400 hover:text-red-600 transition-colors"
              title="Rechazar"
            >
              <XCircle size={13} />
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default DeliveryOrderCard;
