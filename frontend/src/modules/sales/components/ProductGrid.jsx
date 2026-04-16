/**
 * Grid de Productos para Ventas
 * Seleccion rapida de productos al crear una mesa
 */

import { useState, useMemo } from 'react';
import { Plus, Minus, Search, ImageIcon } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';
const CARD_WIDTH = 168;

const ProductGrid = ({ products = [], selectedItems, onUpdateItem }) => {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return products;
    const q = search.toLowerCase();
    return products.filter(
      (p) =>
        p.productName?.toLowerCase().includes(q) ||
        p.sku?.toLowerCase().includes(q) ||
        p.category?.toLowerCase().includes(q)
    );
  }, [products, search]);

  const getQuantity = (productId) => {
    const item = selectedItems.find((i) => i.productId === productId);
    return item?.quantity || 0;
  };

  const handleIncrement = (product) => {
    const current = getQuantity(product.productId);
    const available = Number(product.availableQuantity ?? product.quantity ?? 0);
    if (current >= available) return;

    onUpdateItem({
      productId: product.productId,
      productName: product.productName,
      unitPrice: product.salePrice,
      quantity: current + 1,
      imageUrl: product.imageUrl,
    });
  };

  const handleDecrement = (product) => {
    const current = getQuantity(product.productId);
    if (current <= 0) return;

    onUpdateItem({
      productId: product.productId,
      productName: product.productName,
      unitPrice: product.salePrice,
      quantity: current - 1,
      imageUrl: product.imageUrl,
    });
  };

  return (
    <div className="flex h-full flex-col">
      <div className="relative mb-4">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Buscar producto..."
          className="input pl-10"
        />
      </div>

      <div className="flex-1 overflow-y-auto pr-1 scrollbar-subtle">
        <div
          className="grid justify-center gap-3 sm:justify-start"
          style={{ gridTemplateColumns: `repeat(auto-fit, minmax(${CARD_WIDTH}px, ${CARD_WIDTH}px))` }}
        >
          {filtered.map((product) => {
            const qty = getQuantity(product.productId);
            const isSelected = qty > 0;
            const available = Number(product.availableQuantity ?? product.quantity ?? 0);
            const maxed = available <= 0 || qty >= available;

            return (
              <div
                key={product.productId}
                className={`relative w-[168px] overflow-hidden rounded-xl border-2 bg-white transition-all ${
                  isSelected
                    ? 'border-primary-500 shadow-md'
                    : maxed
                      ? 'cursor-not-allowed border-gray-200 opacity-70'
                      : 'cursor-pointer border-gray-200 hover:border-gray-300 hover:shadow-sm'
                }`}
                onClick={() => !maxed && handleIncrement(product)}
              >
                {isSelected && (
                  <div className="absolute right-2 top-2 z-10 flex h-6 w-6 items-center justify-center rounded-full bg-primary-600 text-xs font-bold text-white shadow">
                    {qty}
                  </div>
                )}

                <div className="flex aspect-square items-center justify-center overflow-hidden bg-gray-50">
                  {product.imageUrl ? (
                    <img
                      src={`${API_BASE}${product.imageUrl}`}
                      alt={product.productName}
                      className="h-full w-full object-cover"
                      onError={e => { e.currentTarget.style.display = 'none'; }}
                    />
                  ) : (
                    <ImageIcon className="h-10 w-10 text-gray-300" />
                  )}
                </div>

                <div className="p-2.5">
                  <p className="truncate text-xs font-medium text-gray-900">{product.productName}</p>
                  <p className="mt-0.5 text-sm font-bold text-primary-600">{formatCurrency(product.salePrice)}</p>
                  <p className="mt-1 text-[11px] text-gray-500">
                    Disponible: {available} {product.unit || ''}
                  </p>
                </div>

                {isSelected && (
                  <div className="flex border-t border-gray-100" onClick={(e) => e.stopPropagation()}>
                    <button
                      onClick={() => handleDecrement(product)}
                      className="flex flex-1 items-center justify-center py-1.5 text-red-500 transition-colors hover:bg-red-50"
                    >
                      <Minus className="h-4 w-4" />
                    </button>
                    <div className="flex items-center justify-center px-3 text-sm font-bold text-gray-700">
                      {qty}
                    </div>
                    <button
                      onClick={() => handleIncrement(product)}
                      disabled={qty >= available}
                      className="flex flex-1 items-center justify-center py-1.5 text-green-500 transition-colors hover:bg-green-50 disabled:opacity-40"
                    >
                      <Plus className="h-4 w-4" />
                    </button>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {filtered.length === 0 && (
          <div className="flex flex-col items-center justify-center py-12 text-gray-400">
            <Search className="mb-2 h-8 w-8" />
            <p className="text-sm">No se encontraron productos</p>
          </div>
        )}
      </div>
    </div>
  );
};

export default ProductGrid;
