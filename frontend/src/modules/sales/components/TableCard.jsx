/**
 * Card de Mesa
 * ¿Qué es? Tarjeta que muestra una mesa activa con sus productos
 * ¿Para qué? Visualizar pedido, total y acciones (facturar/cancelar)
 */

import { useState, useRef, useEffect, useCallback } from 'react';
import { Clock, Receipt, X, GripVertical, Plus, Minus, PlusCircle } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const CARD_W = 326;
const CARD_H = 320;
const GAP = 16;

function calcGridPos(container, idx) {
  const cols = Math.floor((container.width + GAP) / (CARD_W + GAP)) || 1;
  const totalW = cols * CARD_W + (cols - 1) * GAP;
  const offsetX = Math.max(GAP, (container.width - totalW) / 2);
  const col = idx % cols;
  const row = Math.floor(idx / cols);
  return { x: offsetX + col * (CARD_W + GAP), y: GAP + row * (CARD_H + GAP) };
}

const useIsMobile = () => {
  const [mobile, setMobile] = useState(window.innerWidth < 768);
  useEffect(() => {
    const handler = () => setMobile(window.innerWidth < 768);
    window.addEventListener('resize', handler);
    return () => window.removeEventListener('resize', handler);
  }, []);
  return mobile;
};

const TableCard = ({ table, tableIndex = 0, onInvoice, onCancel, onUpdateItemQty, onAddProducts, containerRef, arrangeKey = 0, stockByProduct = {} }) => {
  const isMobile = useIsMobile();
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [dragging, setDragging] = useState(false);
  const [animating, setAnimating] = useState(false);
  const [offset, setOffset] = useState({ x: 0, y: 0 });
  const cardRef = useRef(null);
  const initialized = useRef(false);

  const arrangeToGrid = () => {
    if (!containerRef?.current) return;
    const rect = containerRef.current.getBoundingClientRect();
    setPosition(calcGridPos(rect, tableIndex));
  };

  useEffect(() => {
    if (!initialized.current && containerRef?.current) {
      arrangeToGrid();
      initialized.current = true;
    }
  }, [containerRef, tableIndex]);

  useEffect(() => {
    if (arrangeKey > 0) {
      setAnimating(true);
      arrangeToGrid();
      const t = setTimeout(() => setAnimating(false), 400);
      return () => clearTimeout(t);
    }
  }, [arrangeKey]);

  const handleMouseDown = (e) => {
    if (e.target.closest('button')) return;
    const rect = cardRef.current.getBoundingClientRect();
    setOffset({ x: e.clientX - rect.left, y: e.clientY - rect.top });
    setDragging(true);
  };

  const handleTouchStart = (e) => {
    if (e.target.closest('button')) return;
    const touch = e.touches[0];
    const rect = cardRef.current.getBoundingClientRect();
    setOffset({ x: touch.clientX - rect.left, y: touch.clientY - rect.top });
    setDragging(true);
  };

  useEffect(() => {
    if (!dragging) return;

    const handleMove = (clientX, clientY) => {
      if (!containerRef?.current) return;
      const container = containerRef.current.getBoundingClientRect();
      const newX = clientX - container.left - offset.x;
      const newY = clientY - container.top - offset.y;
      setPosition({
        x: Math.max(0, Math.min(newX, container.width - 320)),
        y: Math.max(0, Math.min(newY, container.height - 100)),
      });
    };

    const onMouseMove = (e) => handleMove(e.clientX, e.clientY);
    const onTouchMove = (e) => {
      e.preventDefault();
      handleMove(e.touches[0].clientX, e.touches[0].clientY);
    };
    const onEnd = () => setDragging(false);

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onEnd);
    window.addEventListener('touchmove', onTouchMove, { passive: false });
    window.addEventListener('touchend', onEnd);

    return () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onEnd);
      window.removeEventListener('touchmove', onTouchMove);
      window.removeEventListener('touchend', onEnd);
    };
  }, [dragging, offset, containerRef]);

  const items = table.items || [];
  const total = table.total || items.reduce((s, i) => s + i.quantity * i.unitPrice, 0);
  const createdAt = new Date(table.createdAt);
  const timeStr = createdAt.toLocaleTimeString('es-CO', { hour: '2-digit', minute: '2-digit' });

  const renderItemRow = (item, idx) => (
    <div key={item.id || idx} className="flex items-center gap-2 py-2 text-sm">
      <div className="flex-1 min-w-0">
        <span className="text-gray-700 truncate block">{item.productName}</span>
        <span className="text-xs text-gray-400">
          {formatCurrency(item.unitPrice)} c/u
        </span>
        {typeof stockByProduct[item.productId]?.availableQuantity !== 'undefined' && (
          <span className="mt-0.5 block text-[11px] text-gray-400">
            Disponible: {stockByProduct[item.productId]?.availableQuantity ?? 0}
          </span>
        )}
      </div>
      <div className="flex items-center gap-1 flex-shrink-0">
        <button
          onClick={() => onUpdateItemQty?.(table, item, item.quantity - 1)}
          className="rounded p-0.5 text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors"
        >
          <Minus className="h-3.5 w-3.5" />
        </button>
        <span className="w-6 text-center text-xs font-bold text-gray-900">{item.quantity}</span>
        <button
          onClick={() => onUpdateItemQty?.(table, item, item.quantity + 1)}
          disabled={(stockByProduct[item.productId]?.availableQuantity ?? 0) <= 0}
          className="rounded p-0.5 text-gray-400 hover:bg-green-50 hover:text-green-500 transition-colors disabled:opacity-40"
        >
          <Plus className="h-3.5 w-3.5" />
        </button>
      </div>
      <span className="font-medium text-gray-900 text-xs whitespace-nowrap w-16 text-right">
        {formatCurrency(item.quantity * item.unitPrice)}
      </span>
    </div>
  );

  const renderFooter = (rounded = '') => (
    <div className={`border-t px-4 py-3 flex items-center justify-between bg-gray-50 flex-shrink-0 ${rounded}`}>
      <div>
        <p className="text-xs text-gray-500">Total</p>
        <p className="text-lg font-bold text-gray-900">{formatCurrency(total)}</p>
      </div>
      <div className="flex gap-1.5">
        <button
          onClick={() => onAddProducts?.(table)}
          className="rounded-lg p-2 text-primary-500 hover:bg-primary-50 transition-colors"
          title="Agregar productos"
        >
          <PlusCircle className="h-4 w-4" />
        </button>
        <button
          onClick={() => onCancel?.(table)}
          className="rounded-lg p-2 text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors"
          title="Cancelar mesa"
        >
          <X className="h-4 w-4" />
        </button>
        <button
          onClick={() => onInvoice?.(table)}
          className="flex items-center gap-1.5 rounded-lg bg-green-600 px-3 py-2 text-xs font-medium text-white hover:bg-green-700 transition-colors"
        >
          <Receipt className="h-3.5 w-3.5" />
          Facturar
        </button>
      </div>
    </div>
  );

  if (isMobile) {
    return (
      <div className="w-full rounded-xl bg-white border border-gray-200 shadow-sm flex flex-col">
        <div className="flex items-center justify-between border-b px-4 py-3 bg-gray-50 rounded-t-xl flex-shrink-0">
          <h3 className="font-bold text-gray-900">Mesa {table.tableNumber}</h3>
          <div className="flex items-center gap-1.5 text-xs text-gray-500">
            <Clock className="h-3 w-3" />
            {timeStr}
          </div>
        </div>
        <div className="px-4 py-1 divide-y divide-gray-100">
          {items.length === 0 ? (
            <p className="text-sm text-gray-400 py-3 text-center">Sin productos</p>
          ) : items.map(renderItemRow)}
        </div>
        {renderFooter('rounded-b-xl')}
      </div>
    );
  }

  return (
    <div
      ref={cardRef}
      style={{ left: position.x, top: position.y }}
      className={`absolute w-[310px] rounded-xl bg-white border border-gray-200 shadow-lg flex flex-col
        ${dragging ? 'shadow-2xl ring-2 ring-primary-300 z-50 cursor-grabbing' : 'z-10 cursor-grab'}
        ${animating ? 'transition-all duration-400 ease-out' : 'transition-shadow duration-200'}`}
      onMouseDown={handleMouseDown}
      onTouchStart={handleTouchStart}
    >
      <div className="flex items-center justify-between border-b px-4 py-3 bg-gray-50 rounded-t-xl flex-shrink-0">
        <div className="flex items-center gap-2">
          <GripVertical className="h-4 w-4 text-gray-400" />
          <h3 className="font-bold text-gray-900">Mesa {table.tableNumber}</h3>
        </div>
        <div className="flex items-center gap-1.5 text-xs text-gray-500">
          <Clock className="h-3 w-3" />
          {timeStr}
        </div>
      </div>
      <div className="scrollbar-subtle flex-1 overflow-y-auto max-h-48 px-4 py-1 divide-y divide-gray-100">
        {items.length === 0 ? (
          <p className="text-sm text-gray-400 py-3 text-center">Sin productos</p>
        ) : items.map(renderItemRow)}
      </div>
      {renderFooter('rounded-b-xl')}
    </div>
  );
};

export default TableCard;
