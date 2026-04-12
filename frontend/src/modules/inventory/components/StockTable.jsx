/**
 * Tabla de Stock
 * ¿Qué es? Componente que muestra el inventario actual en tabla
 * ¿Para qué? Visualizar stock con filtros, ordenamiento y estados
 */

import { useState, useMemo } from 'react';
import { Search, ArrowUpDown, Package, Pencil, Trash2, Plus, ImageIcon } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';

const StockTable = ({ stock = [], isLoading = false, onEdit, onDelete, onAddStock }) => {
  const [search, setSearch] = useState('');
  const [sortField, setSortField] = useState('productName');
  const [sortDirection, setSortDirection] = useState('asc');

  const handleSort = (field) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const filteredAndSorted = useMemo(() => {
    if (!stock) return [];

    let filtered = stock.filter((item) =>
      item.productName?.toLowerCase().includes(search.toLowerCase()) ||
      item.sku?.toLowerCase().includes(search.toLowerCase()) ||
      item.category?.toLowerCase().includes(search.toLowerCase())
    );

    filtered.sort((a, b) => {
      const aVal = a[sortField] ?? '';
      const bVal = b[sortField] ?? '';

      if (typeof aVal === 'number' && typeof bVal === 'number') {
        return sortDirection === 'asc' ? aVal - bVal : bVal - aVal;
      }

      const comparison = String(aVal).localeCompare(String(bVal));
      return sortDirection === 'asc' ? comparison : -comparison;
    });

    return filtered;
  }, [stock, search, sortField, sortDirection]);

  const getStatusBadge = (status) => {
    switch (status) {
      case 'ok':
        return <span className="badge badge-success">OK</span>;
      case 'reorder':
        return <span className="badge badge-info">Reordenar</span>;
      case 'low':
        return <span className="badge badge-warning">Bajo</span>;
      case 'out':
        return <span className="badge badge-danger">Agotado</span>;
      default:
        return <span className="badge badge-info">{status}</span>;
    }
  };

  const getTypeBadge = (type) => {
    const map = {
      simple: { label: 'Simple', cls: 'bg-gray-100 text-gray-600' },
      prepared: { label: 'Preparación', cls: 'bg-blue-100 text-blue-700' },
      combo: { label: 'Combo', cls: 'bg-purple-100 text-purple-700' },
      service: { label: 'Servicio', cls: 'bg-teal-100 text-teal-700' },
    };
    const t = map[type] || map.simple;
    return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${t.cls}`}>{t.label}</span>;
  };

  if (isLoading) {
    return (
      <div className="card flex items-center justify-center py-12">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary-500" />
        <span className="ml-3 text-gray-500">Cargando stock...</span>
      </div>
    );
  }

  return (
    <div className="card p-0 overflow-hidden">
      {/* Search */}
      <div className="border-b border-gray-200 p-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder="Buscar por nombre, SKU o categoría..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="input pl-10"
          />
        </div>
      </div>

      {/* Table */}
      {filteredAndSorted.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-12 text-gray-500">
          <Package className="h-12 w-12 mb-3 text-gray-300" />
          <p className="font-medium">No se encontraron productos</p>
          <p className="text-sm mt-1">
            {search ? 'Intenta con otro término de búsqueda' : 'No hay datos de stock disponibles'}
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-gray-50 text-xs uppercase text-gray-500">
              <tr>
                <th
                  className="cursor-pointer px-4 py-3 hover:bg-gray-100"
                  onClick={() => handleSort('productName')}
                >
                  <div className="flex items-center gap-1">
                    Producto
                    <ArrowUpDown className="h-3 w-3" />
                  </div>
                </th>
                <th className="px-4 py-3">SKU</th>
                <th className="px-4 py-3">Categoría</th>
                <th
                  className="cursor-pointer px-4 py-3 hover:bg-gray-100"
                  onClick={() => handleSort('quantity')}
                >
                  <div className="flex items-center gap-1">
                    Físico
                    <ArrowUpDown className="h-3 w-3" />
                  </div>
                </th>
                <th
                  className="cursor-pointer px-4 py-3 hover:bg-gray-100"
                  onClick={() => handleSort('reservedQuantity')}
                >
                  <div className="flex items-center gap-1">
                    Comprometido
                    <ArrowUpDown className="h-3 w-3" />
                  </div>
                </th>
                <th
                  className="cursor-pointer px-4 py-3 hover:bg-gray-100"
                  onClick={() => handleSort('availableQuantity')}
                >
                  <div className="flex items-center gap-1">
                    Disponible
                    <ArrowUpDown className="h-3 w-3" />
                  </div>
                </th>
                <th className="px-4 py-3">Unidad</th>
                <th className="px-4 py-3">Costo</th>
                <th className="px-4 py-3">Precio Venta</th>
                <th className="px-4 py-3">Estado</th>
                <th className="px-4 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {filteredAndSorted.map((item) => (
                <tr key={item.productId || item.id} className="hover:bg-gray-50">
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-3">
                      {item.imageUrl ? (
                        <img
                          src={`${API_BASE}${item.imageUrl}`}
                          alt={item.productName}
                          className="h-9 w-9 rounded-lg object-cover border border-gray-200 flex-shrink-0"
                        />
                      ) : (
                        <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-gray-100 border border-gray-200 flex-shrink-0">
                          <ImageIcon className="h-4 w-4 text-gray-400" />
                        </div>
                      )}
                      <div className="flex flex-col">
                        <span className="font-medium text-gray-900">{item.productName}</span>
                        <div className="flex items-center gap-1 mt-0.5">
                          {getTypeBadge(item.productType)}
                          {item.isPerishable && (
                            <span className="inline-flex items-center rounded-full bg-orange-100 text-orange-700 px-2 py-0.5 text-[10px] font-medium">Perecedero</span>
                          )}
                        </div>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3 text-gray-500 font-mono text-xs">
                    {item.sku}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {item.category}
                  </td>
                  <td className="px-4 py-3 font-semibold">
                    {item.quantity}
                  </td>
                  <td className="px-4 py-3 font-semibold text-amber-600">
                    {item.reservedQuantity || 0}
                  </td>
                  <td className="px-4 py-3 font-semibold text-gray-900">
                    {item.availableQuantity ?? item.quantity}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {item.unit}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {formatCurrency(item.costPrice)}
                  </td>
                  <td className="px-4 py-3 text-gray-500">
                    {formatCurrency(item.salePrice)}
                  </td>
                  <td className="px-4 py-3">
                    {getStatusBadge(item.stockStatus)}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        onClick={() => onAddStock?.(item)}
                        className="rounded-lg p-1.5 text-green-600 hover:bg-green-50 transition-colors"
                        title="Agregar stock"
                      >
                        <Plus className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => onEdit?.(item)}
                        className="rounded-lg p-1.5 text-blue-600 hover:bg-blue-50 transition-colors"
                        title="Editar producto"
                      >
                        <Pencil className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => onDelete?.(item)}
                        className="rounded-lg p-1.5 text-red-600 hover:bg-red-50 transition-colors"
                        title="Eliminar producto"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Footer */}
      {filteredAndSorted.length > 0 && (
        <div className="border-t border-gray-200 bg-gray-50 px-4 py-3 text-xs text-gray-500">
          Mostrando {filteredAndSorted.length} de {stock?.length || 0} productos
        </div>
      )}
    </div>
  );
};

export default StockTable;
