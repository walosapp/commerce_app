/**
 * Grid de Productos para Ventas
 * Seleccion rapida de productos al crear una mesa
 */

import { useState, useMemo } from 'react';
import { Plus, Minus, Search, ImageIcon } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';
import { resolveAssetUrl } from '../../../utils/assetUrl';
const CARD_WIDTH = 168;

const ProductGrid = ({ products = [], selectedItems, onUpdateItem }) => {
  const [search, setSearch] = useState('');
  const [activeCategory, setActiveCategory] = useState('all');

  const categories = useMemo(() => {
    const names = [...new Set(
      products
        .map((product) => product.category?.trim())
        .filter(Boolean)
    )].sort((a, b) => a.localeCompare(b));

    return [
      { key: 'all', label: 'Todo', count: products.length },
      ...names.map((name) => ({
        key: name,
        label: name,
        count: products.filter((product) => (product.category || '').trim() === name).length,
      })),
    ];
  }, [products]);

  const filtered = useMemo(() => {
    return products.filter((p) => {
      const matchesCategory = activeCategory === 'all' || (p.category || '').trim() === activeCategory;

      if (!matchesCategory) return false;
      if (!search.trim()) return true;

      const q = search.toLowerCase();
      return (
        p.productName?.toLowerCase().includes(q) ||
        p.sku?.toLowerCase().includes(q) ||
        p.category?.toLowerCase().includes(q)
      );
    });
  }, [products, search, activeCategory]);

  const getQuantity = (productId) => {
    const item = selectedItems.find((i) => i.productId === productId);
    return item?.quantity || 0;
  };

  const handleIncrement = (product) => {
    const current = getQuantity(product.productId);
    const available = Number(product.availableQuantity ?? product.quantity ?? 0);
    if (!product.trackStock && current >= 0) {}
    else if (current >= available) return;

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

      <div className="flex min-h-0 flex-1 gap-4">
        <aside className="hidden w-36 flex-shrink-0 rounded-xl border border-gray-200 bg-gray-50 p-2 lg:block">
          <div className="space-y-1">
            {categories.map((category) => {
              const isActive = activeCategory === category.key;
              return (
                <button
                  key={category.key}
                  type="button"
                  onClick={() => setActiveCategory(category.key)}
                  className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-xs font-medium transition-colors ${
                    isActive
                      ? 'bg-primary-600 text-white shadow-sm'
                      : 'text-gray-600 hover:bg-white hover:text-gray-900'
                  }`}
                >
                  <span className="truncate pr-2">{category.label}</span>
                  <span className={`rounded-full px-1.5 py-0.5 text-[10px] ${
                    isActive ? 'bg-white/20 text-white' : 'bg-white text-gray-500'
                  }`}>
                    {category.count}
                  </span>
                </button>
              );
            })}
          </div>
        </aside>

        <div className="flex min-h-0 min-w-0 flex-1 flex-col">
          <div className="mb-3 flex gap-2 overflow-x-auto pb-1 lg:hidden scrollbar-subtle">
            {categories.map((category) => {
              const isActive = activeCategory === category.key;
              return (
                <button
                  key={category.key}
                  type="button"
                  onClick={() => setActiveCategory(category.key)}
                  className={`whitespace-nowrap rounded-full border px-3 py-1.5 text-xs font-medium transition-colors ${
                    isActive
                      ? 'border-primary-600 bg-primary-600 text-white'
                      : 'border-gray-200 bg-white text-gray-600'
                  }`}
                >
                  {category.label} <span className="ml-1 opacity-80">({category.count})</span>
                </button>
              );
            })}
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
                const noStockControl = !product.trackStock;
                const maxed = !noStockControl && (available <= 0 || qty >= available);

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
                      {resolveAssetUrl(product.imageUrl) ? (
                        <img
                          src={resolveAssetUrl(product.imageUrl)}
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
                      <p className="mt-1 truncate text-[11px] text-gray-500">{product.category || 'Sin categoría'}</p>
                      <p className="mt-1 text-[11px] text-gray-500">
                        {product.trackStock ? ('Disponible: ' + available + ' ' + (product.unit || '')) : 'Sin limite'}
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
      </div>
    </div>
  );
};

export default ProductGrid;


