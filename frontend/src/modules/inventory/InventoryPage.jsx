/**
 * Página de Inventario
 * ¿Qué es? Vista principal del módulo de inventario
 * ¿Para qué? Integrar chat IA, tabla de stock y alertas
 */

import { useEffect, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Package, AlertCircle, TrendingUp, PlusCircle, FileSpreadsheet, Filter, ChevronDown, X } from 'lucide-react';
import toast from 'react-hot-toast';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import { canWriteInventory } from '../../config/companyFeatures';
import StockTable from './components/StockTable';
import InventoryHeader from './components/InventoryHeader';
import ProductFormModal from './components/ProductFormModal';
import DeleteConfirmModal from './components/DeleteConfirmModal';
import AddStockModal from './components/AddStockModal';
import ImportProductsModal from './components/ImportProductsModal';

const InventoryPage = () => {
  const { branchId, user } = useAuthStore();
  const inventoryWriteAllowed = canWriteInventory(user);
  const queryClient = useQueryClient();

  const [showProductModal, setShowProductModal] = useState(false);
  const [showImportModal, setShowImportModal] = useState(false);
  const [editProduct, setEditProduct] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [addStockTarget, setAddStockTarget] = useState(null);
  const [activeFilter, setActiveFilter] = useState(null); // null | 'low' | 'out'
  const [typeFilter, setTypeFilter] = useState(null); // null | 'simple' | 'supply' | 'prepared' | 'service'
  const [search, setSearch] = useState('');
  const [showFilters, setShowFilters] = useState(false);
  const filtersRef = useRef(null);
  const filtersButtonRef = useRef(null);

  useEffect(() => {
    if (!showFilters) return;
    const closeOutside = event => {
      if (!filtersRef.current?.contains(event.target)) setShowFilters(false);
    };
    const closeOnEscape = event => {
      if (event.key === 'Escape') {
        setShowFilters(false);
        filtersButtonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', closeOutside);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOutside);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [showFilters]);

  const { data: stockData, isLoading: stockLoading } = useQuery({
    queryKey: ['stock', branchId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId,
  });

  const { data: lowStockData } = useQuery({
    queryKey: ['lowStock', branchId],
    queryFn: () => inventoryService.getLowStock(branchId),
    enabled: !!branchId,
  });

  const { data: alertsData } = useQuery({
    queryKey: ['alerts', branchId],
    queryFn: () => inventoryService.getAlerts(branchId),
    enabled: !!branchId,
  });

  const refetchAll = () => {
    queryClient.invalidateQueries({ queryKey: ['stock'] });
    queryClient.invalidateQueries({ queryKey: ['lowStock'] });
    queryClient.invalidateQueries({ queryKey: ['alerts'] });
  };

  const handleCreateProduct = async (data, imageFile) => {
    const result = await inventoryService.createProduct(data);
    const newProductId = result?.data?.id;
    if (imageFile && newProductId) {
      await inventoryService.uploadProductImage(newProductId, imageFile);
    }
    toast.success('Producto creado exitosamente');
    refetchAll();
  };

  const handleEditProduct = async (data, imageFile) => {
    await inventoryService.updateProduct(editProduct.productId, data);
    if (imageFile) {
      await inventoryService.uploadProductImage(editProduct.productId, imageFile);
    }
    toast.success('Producto actualizado');
    refetchAll();
  };

  const handleDeleteProduct = async () => {
    await inventoryService.deleteProduct(deleteTarget.productId);
    toast.success('Producto eliminado');
    refetchAll();
  };

  const handleAddStock = async ({ quantity, unitCost }) => {
    const item = addStockTarget;
    await inventoryService.addStock({
      branchId,
      productId: item.productId,
      quantity,
      unitCost,
      notes: `Ingreso manual desde inventario para ${item.productName}`,
    });
    toast.success(`+${quantity} unidades agregadas`);
    refetchAll();
  };

  const openEdit = async (item) => {
    try {
      const res = await inventoryService.getProductById(item.productId);
      setEditProduct({ ...res.data, productId: item.productId });
      setShowProductModal(true);
    } catch (err) {
      console.error('Error cargando producto:', err);
      toast.error('Error cargando datos del producto');
    }
  };

  const openCreate = () => {
    setEditProduct(null);
    setShowProductModal(true);
  };

  const allStock = stockData?.data || [];
  const outOfStockCount = allStock.filter((s) => s.stockStatus === 'out').length;

  const stats = [
    {
      key: 'all',
      label: 'Total Productos',
      value: stockData?.count || 0,
      icon: Package,
      color: 'text-blue-600',
      bgColor: 'bg-blue-100',
    },
    {
      key: 'low',
      label: 'Stock Bajo',
      value: lowStockData?.count || 0,
      icon: AlertCircle,
      color: 'text-yellow-600',
      bgColor: 'bg-yellow-100',
    },
    {
      key: 'out',
      label: 'Sin Stock',
      value: outOfStockCount,
      icon: TrendingUp,
      color: 'text-red-600',
      bgColor: 'bg-red-100',
    },
  ];

  const filteredStock = allStock
    .filter((item) => {
      if (!activeFilter) return true;
      if (activeFilter === 'low') return item.stockStatus === 'low';
      if (activeFilter === 'out') return item.stockStatus === 'out';
      return true;
    })
    .filter((item) => !typeFilter || item.productType === typeFilter);

  const TYPE_CHIPS = [
    { value: 'simple',   label: 'Simples',    color: 'bg-gray-100 text-gray-700 ring-gray-300' },
    { value: 'supply',   label: 'Insumos',    color: 'bg-blue-100 text-blue-700 ring-blue-300' },
    { value: 'prepared', label: 'Preparados', color: 'bg-indigo-100 text-indigo-700 ring-indigo-300' },
    { value: 'service',  label: 'Servicios',  color: 'bg-purple-100 text-purple-700 ring-purple-300' },
  ];

  const filterCount = Number(!!activeFilter) + Number(!!typeFilter);

  return (
    <div className="inventory-workspace flex min-h-0 min-w-0 flex-1 flex-col gap-4">
      <InventoryHeader search={search} onSearchChange={setSearch} actions={inventoryWriteAllowed && (
        <div className="flex flex-wrap items-center gap-2">
          <button
            title="Importar Excel"
            onClick={() => setShowImportModal(true)}
            className="flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors shadow-sm md:px-4 md:py-2.5 md:text-sm"
          >
            <FileSpreadsheet className="h-4 w-4 text-green-600" />
            <span>Importar Excel</span>
          </button>
          <button
            title="Agregar Producto"
            onClick={openCreate}
            className="flex items-center gap-2 rounded-lg bg-primary-600 px-3 py-2 text-xs font-medium text-white hover:bg-primary-700 transition-colors shadow-sm md:px-4 md:py-2.5 md:text-sm"
          >
            <PlusCircle className="h-4 w-4" />
            <span>Agregar Producto</span>
          </button>
        </div>
      )} />

      <div ref={filtersRef} className="relative shrink-0">
      <section aria-label="Resumen de inventario" className="inventory-summary flex flex-wrap items-center gap-3">
        <dl className="grid min-w-0 flex-1 basis-full grid-cols-3 gap-2 sm:basis-0 sm:gap-3">
          {stats.map((stat) => {
            const Icon = stat.icon;
            return (
              <div key={stat.key} className="flex min-w-0 items-center justify-between gap-2 rounded-xl border border-gray-200 bg-white p-3">
                <div className="min-w-0">
                  <dt title={stat.label} className="truncate text-[10px] text-gray-500 sm:text-xs">{stat.label}</dt>
                  <dd className="mt-0.5 text-xl font-bold leading-6 text-gray-900">{stat.value}</dd>
                </div>
                <div className={`hidden shrink-0 rounded-lg p-2 sm:block ${stat.bgColor}`}><Icon className={`h-4 w-4 ${stat.color}`} /></div>
              </div>
            );
          })}
        </dl>
        <div className="flex flex-wrap items-center gap-2">
          <button ref={filtersButtonRef} type="button" aria-expanded={showFilters} aria-controls="inventory-filters" onClick={() => setShowFilters(v => !v)}
            className={`flex items-center gap-2 rounded-lg border px-3 py-2 text-xs font-medium transition-colors ${filterCount ? 'border-primary-300 bg-primary-50 text-primary-700' : 'border-gray-200 bg-white text-gray-600 hover:bg-gray-50'}`}>
            <Filter size={14} />Filtros {filterCount > 0 && <span className="rounded-full bg-primary-100 px-1.5 font-bold">{filterCount}</span>}
            <ChevronDown size={14} className={`transition-transform ${showFilters ? 'rotate-180' : ''}`} />
          </button>
          {filterCount > 0 && <button type="button" onClick={() => { setActiveFilter(null); setTypeFilter(null); }}
            className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700"><X size={13} />Limpiar filtros</button>}
        </div>
      </section>

      {showFilters && <div id="inventory-filters" role="region" aria-label="Filtros de inventario" className="inventory-table-scroll absolute left-0 right-0 top-full z-20 mt-2 max-h-[50dvh] space-y-3 overflow-y-auto rounded-xl border border-gray-200 bg-white p-3 shadow-lg">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs font-medium text-gray-500">Estado:</span>
          {[{ value: null, label: 'Todos' }, { value: 'low', label: 'Stock Bajo' }, { value: 'out', label: 'Sin Stock' }].map(option => (
            <button key={option.label} type="button" aria-pressed={activeFilter === option.value}
              onClick={() => setActiveFilter(activeFilter === option.value ? null : option.value)}
              className={`rounded-full border px-3 py-1 text-xs font-medium transition-colors ${activeFilter === option.value ? 'border-primary-300 bg-primary-50 text-primary-700' : 'border-gray-200 bg-white text-gray-500 hover:bg-gray-50'}`}>
              {option.label}
            </button>
          ))}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-xs text-gray-500 font-medium">Tipo:</span>
          {TYPE_CHIPS.map(chip => (
            <button
              key={chip.value}
              type="button"
              aria-pressed={typeFilter === chip.value}
              onClick={() => setTypeFilter(typeFilter === chip.value ? null : chip.value)}
              className={`text-xs font-medium px-3 py-1 rounded-full border transition-all ${
                typeFilter === chip.value
                  ? `${chip.color} ring-2 shadow-sm`
                  : 'bg-white text-gray-500 border-gray-200 hover:bg-gray-50'
              }`}
            >
              {chip.label}
              <span className="ml-1.5 opacity-60">
                ({allStock.filter(s => s.productType === chip.value).length})
              </span>
            </button>
          ))}
          {typeFilter && (
            <button onClick={() => setTypeFilter(null)} className="text-xs text-gray-400 hover:text-gray-600 underline">
              Quitar filtro
            </button>
          )}
        </div>
      </div>}
      </div>

      {/* Stock Table */}
      <StockTable
        stock={filteredStock}
        search={search}
        isLoading={stockLoading}
        onEdit={inventoryWriteAllowed ? openEdit : undefined}
        onDelete={inventoryWriteAllowed ? (item) => setDeleteTarget(item) : undefined}
        onAddStock={inventoryWriteAllowed ? (item) => setAddStockTarget(item) : undefined}
      />

      {/* Modals */}
      {inventoryWriteAllowed && <ImportProductsModal
        isOpen={showImportModal}
        onClose={() => setShowImportModal(false)}
        onImported={refetchAll}
      />}

      {inventoryWriteAllowed && <ProductFormModal
        isOpen={showProductModal}
        onClose={() => { setShowProductModal(false); setEditProduct(null); }}
        onSave={editProduct ? handleEditProduct : handleCreateProduct}
        product={editProduct}
      />}

      {inventoryWriteAllowed && <DeleteConfirmModal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeleteProduct}
        productName={deleteTarget?.productName}
      />}

      {inventoryWriteAllowed && <AddStockModal
        isOpen={!!addStockTarget}
        onClose={() => setAddStockTarget(null)}
        onConfirm={handleAddStock}
        product={addStockTarget}
      />}
    </div>
  );
};

export default InventoryPage;
