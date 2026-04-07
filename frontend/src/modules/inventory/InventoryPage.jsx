/**
 * Página de Inventario
 * ¿Qué es? Vista principal del módulo de inventario
 * ¿Para qué? Integrar chat IA, tabla de stock y alertas
 */

import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Package, AlertCircle, TrendingUp, PlusCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import StockTable from './components/StockTable';
import ProductFormModal from './components/ProductFormModal';
import DeleteConfirmModal from './components/DeleteConfirmModal';
import AddStockModal from './components/AddStockModal';

const InventoryPage = () => {
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();

  const [showProductModal, setShowProductModal] = useState(false);
  const [editProduct, setEditProduct] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [addStockTarget, setAddStockTarget] = useState(null);
  const [activeFilter, setActiveFilter] = useState(null); // null | 'all' | 'low' | 'out'

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

  const openEdit = (item) => {
    setEditProduct(item);
    setShowProductModal(true);
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
      ring: 'ring-blue-400',
    },
    {
      key: 'low',
      label: 'Stock Bajo',
      value: lowStockData?.count || 0,
      icon: AlertCircle,
      color: 'text-yellow-600',
      bgColor: 'bg-yellow-100',
      ring: 'ring-yellow-400',
    },
    {
      key: 'out',
      label: 'Sin Stock',
      value: outOfStockCount,
      icon: TrendingUp,
      color: 'text-red-600',
      bgColor: 'bg-red-100',
      ring: 'ring-red-400',
    },
  ];

  const filteredStock = activeFilter
    ? allStock.filter((item) => {
        if (activeFilter === 'all') return true;
        if (activeFilter === 'low') return item.stockStatus === 'low';
        if (activeFilter === 'out') return item.stockStatus === 'out';
        return true;
      })
    : allStock;

  const handleStatClick = (key) => {
    setActiveFilter(activeFilter === key ? null : key);
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Inventario</h1>
          <p className="mt-1 text-sm text-gray-500">
            Gestiona tu inventario con asistencia de IA
          </p>
        </div>
        <button
          onClick={openCreate}
          className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-primary-700 transition-colors shadow-sm"
        >
          <PlusCircle className="h-4 w-4" />
          Agregar Producto
        </button>
      </div>

      {/* Stats */}
      <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {stats.map((stat) => {
          const Icon = stat.icon;
          const isActive = activeFilter === stat.key;
          return (
            <button
              key={stat.key}
              onClick={() => handleStatClick(stat.key)}
              className={`card text-left transition-all duration-200 ${
                isActive ? `ring-2 ${stat.ring} shadow-md` : 'hover:shadow-md'
              }`}
            >
              <div className="flex items-center justify-between">
                <div>
                  <p className="text-sm text-gray-500">{stat.label}</p>
                  <p className="mt-2 text-3xl font-bold">{stat.value}</p>
                  {isActive && (
                    <p className="mt-1 text-xs text-gray-400">Clic para quitar filtro</p>
                  )}
                </div>
                <div className={`rounded-lg p-3 ${stat.bgColor}`}>
                  <Icon className={`h-6 w-6 ${stat.color}`} />
                </div>
              </div>
            </button>
          );
        })}
      </div>

      {/* Stock Table */}
      <StockTable
        stock={filteredStock}
        isLoading={stockLoading}
        onEdit={openEdit}
        onDelete={(item) => setDeleteTarget(item)}
        onAddStock={(item) => setAddStockTarget(item)}
      />

      {/* Modals */}
      <ProductFormModal
        isOpen={showProductModal}
        onClose={() => { setShowProductModal(false); setEditProduct(null); }}
        onSave={editProduct ? handleEditProduct : handleCreateProduct}
        product={editProduct}
      />

      <DeleteConfirmModal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleDeleteProduct}
        productName={deleteTarget?.productName}
      />

      <AddStockModal
        isOpen={!!addStockTarget}
        onClose={() => setAddStockTarget(null)}
        onConfirm={handleAddStock}
        product={addStockTarget}
      />
    </div>
  );
};

export default InventoryPage;
