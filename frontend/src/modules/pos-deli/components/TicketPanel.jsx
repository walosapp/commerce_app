import { Trash2, Plus, Minus, Banknote, ShoppingBag } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const TicketPanel = ({ items, total, selectedItemId, onSelectItem, onRemoveItem, onUpdateQuantity, onCheckout, disabled = false }) => (
  <section aria-label="Venta actual" className="flex min-h-[420px] min-w-0 flex-col overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm xl:min-h-0">
    <div className="flex shrink-0 items-center justify-between border-b border-gray-100 px-5 py-5">
      <h2 className="text-lg font-black text-gray-900">Venta actual</h2>
      <span className="rounded-full bg-gray-100 px-2.5 py-1 text-xs font-semibold text-gray-500">{items.length} ítem{items.length === 1 ? '' : 's'}</span>
    </div>
    <div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">
      {items.length === 0 ? (
        <div className="flex h-full min-h-[220px] flex-col items-center justify-center gap-3 text-center text-gray-400">
          <ShoppingBag className="h-10 w-10" />
          <p className="font-semibold text-gray-600">Tu venta está vacía</p>
          <p className="max-w-[220px] text-sm">Seleccioná productos para comenzar.</p>
        </div>
      ) : items.map((item) => (
        <article key={item.id} className={`rounded-xl border p-3 transition-colors ${selectedItemId === item.id ? 'border-primary-400 bg-primary-50' : 'border-gray-100 bg-white'}`}>
          <div className="flex items-start gap-2">
            <button type="button" disabled={disabled} onClick={() => onSelectItem(item.id)} aria-pressed={selectedItemId === item.id} className="min-w-0 flex-1 text-left focus-visible:outline-primary-500">
              <span className="block text-sm font-bold text-gray-900">{item.name}</span>
              <span className="mt-1 block text-xs text-gray-500">{item.isWeighed ? `${item.quantity.toFixed(3)} ${item.unit || 'kg'}` : `${item.quantity} und`} × {formatCurrency(item.unitPrice)}</span>
            </button>
            <button type="button" disabled={disabled} aria-label={`Quitar ${item.name}`} onClick={() => onRemoveItem(item.id)} className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-600 disabled:opacity-50"><Trash2 className="h-4 w-4" /></button>
          </div>
          <div className="mt-3 flex items-center justify-between gap-2">
            {!item.isWeighed ? (
              <div className="inline-flex items-center overflow-hidden rounded-lg border border-gray-200 bg-white">
                <button type="button" disabled={disabled} aria-label={`Restar ${item.name}`} onClick={() => onUpdateQuantity(item.id, item.quantity - 1)} className="p-2 text-gray-600 hover:bg-gray-50 disabled:opacity-50"><Minus className="h-4 w-4" /></button>
                <span className="min-w-[28px] text-center text-sm font-bold text-gray-900">{item.quantity}</span>
                <button type="button" disabled={disabled} aria-label={`Sumar ${item.name}`} onClick={() => onUpdateQuantity(item.id, item.quantity + 1)} className="p-2 text-gray-600 hover:bg-gray-50 disabled:opacity-50"><Plus className="h-4 w-4" /></button>
              </div>
            ) : <span className="text-xs text-gray-400">Por peso</span>}
            <p className="text-sm font-black tabular-nums text-gray-900">{formatCurrency(item.subtotal)}</p>
          </div>
        </article>
      ))}
    </div>
    <div className="shrink-0 border-t border-gray-200 bg-gray-50/50 p-5">
      <div className="mb-3 flex items-center justify-between text-sm text-gray-500"><span>Subtotal</span><span className="font-semibold tabular-nums">{formatCurrency(total)}</span></div>
      <div className="mb-5 flex items-center justify-between"><span className="font-bold text-gray-900">Total</span><span className="text-2xl font-black tabular-nums text-gray-900">{formatCurrency(total)}</span></div>
      <button type="button" onClick={onCheckout} disabled={items.length === 0 || disabled} className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-primary-600 px-3 py-4 text-base font-black text-white transition-colors hover:bg-primary-700 disabled:cursor-not-allowed disabled:opacity-50">
        <Banknote className="h-5 w-5 shrink-0" />{disabled ? 'PROCESANDO…' : `COBRAR ${formatCurrency(total)}`}
      </button>
      <p className="mt-2 text-center text-[11px] text-gray-400">F12 para cobrar</p>
    </div>
  </section>
);

export default TicketPanel;
