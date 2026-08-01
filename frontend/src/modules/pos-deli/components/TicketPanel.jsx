import { Trash2, Plus, Minus, Banknote } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const TicketPanel = ({ items, total, selectedItemId, onSelectItem, onRemoveItem, onUpdateQuantity, onCheckout }) => {
  return (
    <div className="flex h-full flex-col rounded-2xl border border-gray-200 bg-white shadow-sm">
      <div className="border-b border-gray-200 px-4 py-4">
        <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Ticket activo</p>
        <p className="mt-1 text-xl font-black text-gray-900">{items.length} item{items.length === 1 ? '' : 's'}</p>
      </div>

      <div className="flex-1 space-y-3 overflow-y-auto p-4">
        {items.length === 0 ? (
          <div className="flex h-full items-center justify-center text-center text-sm text-gray-400">
            El ticket está vacío
          </div>
        ) : (
          items.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => onSelectItem(item.id)}
              className={`w-full rounded-xl border p-3 text-left transition-colors ${
                selectedItemId === item.id
                  ? 'border-primary-500 bg-primary-50'
                  : 'border-gray-200 bg-white hover:border-gray-300'
              }`}
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-gray-900">{item.name}</p>
                  <p className="mt-1 text-xs text-gray-500">
                    {item.isWeighed
                      ? `${item.quantity.toFixed(3)} kg × ${formatCurrency(item.unitPrice)}/kg`
                      : `${item.quantity} und × ${formatCurrency(item.unitPrice)}`}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    onRemoveItem(item.id);
                  }}
                  className="rounded-lg p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>

              <div className="mt-3 flex items-center justify-between gap-3">
                {!item.isWeighed ? (
                  <div className="inline-flex items-center rounded-xl border border-gray-200">
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onUpdateQuantity(item.id, item.quantity - 1);
                      }}
                      className="px-3 py-2 text-gray-600 hover:bg-gray-50"
                    >
                      <Minus className="h-4 w-4" />
                    </button>
                    <span className="min-w-[40px] text-center text-sm font-bold text-gray-900">{item.quantity}</span>
                    <button
                      type="button"
                      onClick={(e) => {
                        e.stopPropagation();
                        onUpdateQuantity(item.id, item.quantity + 1);
                      }}
                      className="px-3 py-2 text-gray-600 hover:bg-gray-50"
                    >
                      <Plus className="h-4 w-4" />
                    </button>
                  </div>
                ) : <span />}

                <p className="text-base font-black text-gray-900">{formatCurrency(item.subtotal)}</p>
              </div>
            </button>
          ))
        )}
      </div>

      <div className="border-t border-gray-200 p-4">
        <div className="mb-4 flex items-center justify-between">
          <span className="text-sm font-medium text-gray-500">TOTAL</span>
          <span className="text-2xl font-black text-gray-900">{formatCurrency(total)}</span>
        </div>

        <button
          type="button"
          onClick={onCheckout}
          disabled={items.length === 0}
          className="inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-primary-600 px-4 py-4 text-base font-black text-white transition-colors hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-50"
        >
          <Banknote className="h-5 w-5" />
          COBRAR (F12)
        </button>
      </div>
    </div>
  );
};

export default TicketPanel;
