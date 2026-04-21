/**
 * Pagina de Ventas
 * �Qu� es? Vista principal del modulo de ventas
 * �Para qu�? Gestionar mesas, pedidos y facturacion
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PlusCircle, ShoppingCart, LayoutGrid } from 'lucide-react';
import toast from 'react-hot-toast';
import salesService from '../../services/salesService';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import AddTablePanel from './components/AddTablePanel';
import TableCard from './components/TableCard';
import InvoicePanel from './components/InvoicePanel';

const SalesPage = () => {
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();
  const areaRef = useRef(null);
  const quantitySyncTimeoutsRef = useRef(new Map());

  const [showAddPanel, setShowAddPanel] = useState(false);
  const [invoiceTarget, setInvoiceTarget] = useState(null);
  const [addProductsTarget, setAddProductsTarget] = useState(null);
  const [arrangeKey, setArrangeKey] = useState(0);

  const { data: tablesData, isLoading: tablesLoading } = useQuery({
    queryKey: ['sales-tables', branchId],
    queryFn: () => salesService.getTables(branchId),
    enabled: !!branchId,
    refetchInterval: 30000,
  });

  const { data: stockData, isLoading: stockLoading } = useQuery({
    queryKey: ['stock', branchId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId,
  });

  const tables = tablesData?.data || [];
  const stockItems = stockData?.data || [];
  const products = stockItems.filter(
    (p) =>
      p.productType !== 'supply' &&
      (!p.trackStock || Number(p.availableQuantity ?? p.quantity ?? 0) > 0)
  );
  const stockByProduct = useMemo(
    () =>
      stockItems.reduce((acc, item) => {
        acc[item.productId] = item;
        return acc;
      }, {}),
    [stockItems]
  );

  const desktopAreaHeight = useMemo(() => {
    if (tables.length === 0) return 'auto';
    const cardW = 326, cardH = 320, gap = 16;
    const containerW = areaRef.current?.clientWidth || 1000;
    const cols = Math.floor((containerW + gap) / (cardW + gap)) || 1;
    const rows = Math.ceil(tables.length / cols);
    return `${rows * (cardH + gap) + gap}px`;
  }, [tables.length, arrangeKey]);

  const refetchTables = () => {
    queryClient.invalidateQueries({ queryKey: ['sales-tables'] });
    queryClient.invalidateQueries({ queryKey: ['stock'] });
    queryClient.invalidateQueries({ queryKey: ['lowStock'] });
    queryClient.invalidateQueries({ queryKey: ['alerts'] });
  };

  const updateTableItemQuantityInCache = (tableId, itemId, nextQuantity) => {
    queryClient.setQueryData(['sales-tables', branchId], (current) => {
      if (!current?.data) return current;

      const nextTables = current.data.map((table) => {
        if (table.id !== tableId) return table;

        const nextItems = (table.items || [])
          .map((existingItem) =>
            existingItem.id === itemId
              ? {
                  ...existingItem,
                  quantity: nextQuantity,
                }
              : existingItem
          )
          .filter((existingItem) => existingItem.quantity > 0);

        const nextTotal = nextItems.reduce(
          (sum, existingItem) => sum + existingItem.quantity * existingItem.unitPrice,
          0
        );

        return {
          ...table,
          items: nextItems,
          total: nextTotal,
        };
      });

      return {
        ...current,
        data: nextTables,
        count: nextTables.length,
      };
    });
  };

  const scheduleQuantitySync = (table, item, nextQuantity) => {
    const syncKey = `${table.id}:${item.id}`;
    const currentTimeout = quantitySyncTimeoutsRef.current.get(syncKey);

    if (currentTimeout) clearTimeout(currentTimeout);

    const timeoutId = window.setTimeout(async () => {
      try {
        await salesService.updateItemQuantity(item.id, Math.max(0, nextQuantity), item.orderId);
        refetchTables();
      } catch (err) {
        toast.error(err?.response?.data?.message || 'Error actualizando cantidad');
        refetchTables();
      } finally {
        quantitySyncTimeoutsRef.current.delete(syncKey);
      }
    }, 220);

    quantitySyncTimeoutsRef.current.set(syncKey, timeoutId);
  };

  useEffect(() => {
    return () => {
      quantitySyncTimeoutsRef.current.forEach((timeoutId) => {
        clearTimeout(timeoutId);
      });
      quantitySyncTimeoutsRef.current.clear();
    };
  }, []);

  const handleCreateTable = async (data) => {
    await salesService.createTable(data);
    toast.success('Mesa creada');
    refetchTables();
  };

  const handleInvoice = async (tableId, payload) => {
    await salesService.invoiceTable(tableId, payload);
    toast.success('Mesa facturada exitosamente');
    refetchTables();
  };

  const handleCancel = async (table) => {
    if (!window.confirm(`Cancelar Mesa ${table.tableNumber}?`)) return;
    await salesService.cancelTable(table.id);
    toast.success('Mesa cancelada');
    refetchTables();
  };

  const handleUpdateItemQty = (table, item, newQty) => {
    const nextQuantity = Math.max(0, newQty);
    const delta = nextQuantity - item.quantity;
    const available = Number(stockByProduct[item.productId]?.availableQuantity ?? 0);

    if (delta > 0 && available < delta) {
      toast.error(`No hay stock disponible suficiente para ${item.productName}`);
      return;
    }

    updateTableItemQuantityInCache(table.id, item.id, nextQuantity);
    scheduleQuantitySync(table, item, nextQuantity);
  };

  const handleAddProducts = (table) => {
    setAddProductsTarget(table);
  };

  const handleAddItemsToTable = async (data) => {
    if (!addProductsTarget) return;
    await salesService.addItemsToTable(addProductsTarget.id, data.items);
    toast.success(`Productos agregados a Mesa ${addProductsTarget.tableNumber}`);
    setAddProductsTarget(null);
    refetchTables();
  };

  const handleRenameTable = async (table, name) => {
    try {
      await salesService.renameTable(table.id, name);
      refetchTables();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No se pudo renombrar la mesa');
    }
  };

  return (
    <div className="flex flex-col h-[calc(100vh-7rem)] overflow-hidden">
      <div className="mb-4 flex flex-shrink-0 items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Ventas</h1>
          <p className="mt-1 text-sm text-gray-500">
            {tables.length} mesa{tables.length !== 1 ? 's' : ''} activa{tables.length !== 1 ? 's' : ''}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {tables.length > 0 && (
            <button
              onClick={() => setArrangeKey((k) => k + 1)}
              className="hidden md:flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2.5 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
              title="Ordenar mesas"
            >
              <LayoutGrid className="h-4 w-4" />
              Ordenar
            </button>
          )}
          <button
            onClick={() => setShowAddPanel(true)}
            className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white hover:bg-primary-700 transition-colors shadow-sm"
          >
            <PlusCircle className="h-4 w-4" />
            Agregar Mesa
          </button>
        </div>
      </div>

      <div
        ref={areaRef}
        className="flex-1 rounded-xl border-2 border-dashed border-gray-200 bg-gray-50/50 relative overflow-y-auto"
      >
        {tablesLoading ? (
          <div className="flex h-full items-center justify-center">
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-500 border-t-transparent" />
          </div>
        ) : tables.length === 0 ? (
          <div className="flex h-full flex-col items-center justify-center text-gray-400">
            <ShoppingCart className="mb-4 h-16 w-16 opacity-50" />
            <p className="text-lg font-medium">No hay mesas activas</p>
            <p className="mt-1 text-sm">Crea una mesa para comenzar a vender</p>
            <button
              onClick={() => setShowAddPanel(true)}
              className="mt-4 flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 transition-colors"
            >
              <PlusCircle className="h-4 w-4" />
              Agregar Mesa
            </button>
          </div>
        ) : (
          <>
            <div className="grid grid-cols-1 gap-4 p-4 sm:grid-cols-2 md:hidden">
              {tables.map((table, idx) => (
                <TableCard
                  key={table.id}
                  table={table}
                  tableIndex={idx}
                  containerRef={areaRef}
                  arrangeKey={arrangeKey}
                  onInvoice={(t) => setInvoiceTarget(t)}
                  onCancel={handleCancel}
                  onUpdateItemQty={handleUpdateItemQty}
                  onAddProducts={handleAddProducts}
                  onRename={handleRenameTable}
                  stockByProduct={stockByProduct}
                />
              ))}
            </div>
            <div className="hidden md:block" style={{ minHeight: desktopAreaHeight }}>
              {tables.map((table, idx) => (
                <TableCard
                  key={table.id}
                  table={table}
                  tableIndex={idx}
                  containerRef={areaRef}
                  arrangeKey={arrangeKey}
                  onInvoice={(t) => setInvoiceTarget(t)}
                  onCancel={handleCancel}
                  onUpdateItemQty={handleUpdateItemQty}
                  onAddProducts={handleAddProducts}
                  onRename={handleRenameTable}
                  stockByProduct={stockByProduct}
                />
              ))}
            </div>
          </>
        )}
      </div>

      <AddTablePanel
        isOpen={showAddPanel}
        onClose={() => setShowAddPanel(false)}
        onCreateTable={handleCreateTable}
        products={products}
        isLoading={stockLoading}
      />

      <AddTablePanel
        isOpen={!!addProductsTarget}
        onClose={() => setAddProductsTarget(null)}
        onCreateTable={handleAddItemsToTable}
        products={products}
        isLoading={stockLoading}
        title={`Agregar a Mesa ${addProductsTarget?.tableNumber || ''}`}
        submitLabel="Agregar Productos"
      />

      <InvoicePanel
        isOpen={!!invoiceTarget}
        onClose={() => setInvoiceTarget(null)}
        onConfirm={handleInvoice}
        table={invoiceTarget}
      />
    </div>
  );
};

export default SalesPage;


