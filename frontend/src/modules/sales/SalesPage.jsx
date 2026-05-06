/**
 * Pagina de Ventas
 * �Qu� es? Vista principal del modulo de ventas
 * �Para qu�? Gestionar mesas, pedidos y facturacion
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PlusCircle, ShoppingCart, LayoutGrid, CreditCard, TableProperties, TrendingUp, Wallet } from 'lucide-react';
import toast from 'react-hot-toast';
import salesService from '../../services/salesService';
import { cashRegisterService } from '../../services/cashRegisterService';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import AddTablePanel from './components/AddTablePanel';
import TableCard from './components/TableCard';
import InvoicePanel from './components/InvoicePanel';
import CreditsPanel from './components/CreditsPanel';
import SalesSummaryTab from './components/SalesSummaryTab';
import CashRegisterBar from './components/CashRegisterBar';
import OpenCashRegisterModal from './components/OpenCashRegisterModal';
import CloseCashRegisterModal from './components/CloseCashRegisterModal';
import CashMovementModal from './components/CashMovementModal';
import CashRegisterHistory from './components/CashRegisterHistory';

import { formatCurrency } from '../../utils/formatCurrency';

const CashSummaryView = ({ register }) => {
  const elapsed = Math.floor((Date.now() - new Date(register.openedAt).getTime()) / 60000);
  const hours = Math.floor(elapsed / 60);
  const mins = elapsed % 60;
  const timeStr = hours > 0 ? `${hours}h ${mins}m` : `${mins}m`;
  const expectedCash = register.openingAmount + register.totalCashSales + register.cashIn - register.cashOut - register.totalCredits;

  const stats = [
    { label: 'Ventas totales', value: formatCurrency(register.totalSales), color: 'text-gray-900' },
    { label: 'Efectivo', value: formatCurrency(register.totalCashSales), color: 'text-green-600' },
    { label: 'Tarjeta', value: formatCurrency(register.totalCardSales), color: 'text-blue-600' },
    { label: 'Transferencia', value: formatCurrency(register.totalTransferSales), color: 'text-purple-600' },
    { label: 'Entradas manuales', value: `+${formatCurrency(register.cashIn)}`, color: 'text-green-600' },
    { label: 'Salidas manuales', value: `-${formatCurrency(register.cashOut)}`, color: 'text-red-600' },
    { label: 'Descuentos', value: `-${formatCurrency(register.totalDiscounts)}`, color: 'text-orange-600' },
    { label: 'Órdenes', value: register.orderCount, color: 'text-gray-900' },
  ];

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <div className="rounded-xl bg-white border border-gray-200 p-5">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-base font-bold text-gray-900">Turno actual</h3>
            <p className="text-xs text-gray-500">Abierta hace {timeStr} · {register.openedByName || 'Usuario'}</p>
          </div>
          <span className="bg-green-100 text-green-700 text-xs font-medium px-2.5 py-1 rounded-full">Abierta</span>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          {stats.map(({ label, value, color }) => (
            <div key={label} className="rounded-lg bg-gray-50 p-3">
              <p className="text-xs text-gray-500 mb-0.5">{label}</p>
              <p className={`text-sm font-bold ${color}`}>{value}</p>
            </div>
          ))}
        </div>
      </div>
      <div className="rounded-xl bg-white border border-gray-200 p-5">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-xs text-gray-500">Efectivo esperado en caja</p>
            <p className="text-xl font-bold text-gray-900">{formatCurrency(expectedCash)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">Monto apertura</p>
            <p className="text-lg font-semibold text-gray-600">{formatCurrency(register.openingAmount)}</p>
          </div>
        </div>
      </div>
    </div>
  );
};

const SalesPage = () => {
  const { branchId } = useAuthStore();
  const queryClient = useQueryClient();
  const areaRef = useRef(null);
  const quantitySyncTimeoutsRef = useRef(new Map());

  const [showAddPanel, setShowAddPanel] = useState(false);
  const [invoiceTarget, setInvoiceTarget] = useState(null);
  const [addProductsTarget, setAddProductsTarget] = useState(null);
  const [arrangeKey, setArrangeKey] = useState(0);
  const [showCredits, setShowCredits] = useState(false);
  const [activeTab, setActiveTab] = useState('tables');

  // Cash register state
  const [showOpenCash, setShowOpenCash] = useState(false);
  const [showCloseCash, setShowCloseCash] = useState(false);
  const [cashMovementType, setCashMovementType] = useState(null);
  const [showCashHistory, setShowCashHistory] = useState(false);

  // Cash register query
  const { data: cashRegData, isLoading: cashLoading } = useQuery({
    queryKey: ['cash-register-active', branchId],
    queryFn: () => cashRegisterService.getActive(),
    enabled: !!branchId,
    refetchInterval: 60000,
  });
  const activeRegister = cashRegData?.data || null;

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

  const refetchCashRegister = () => {
    queryClient.invalidateQueries({ queryKey: ['cash-register-active'] });
    queryClient.invalidateQueries({ queryKey: ['cash-register-history'] });
  };

  const handleOpenCash = async (data) => {
    await cashRegisterService.open(data);
    toast.success('Caja abierta exitosamente');
    refetchCashRegister();
  };

  const handleCloseCash = async (id, data) => {
    await cashRegisterService.close(id, data);
    toast.success('Caja cerrada exitosamente');
    refetchCashRegister();
  };

  const handleCashMovement = async (id, data) => {
    await cashRegisterService.addMovement(id, data);
    toast.success(data.type === 'in' ? 'Entrada registrada' : 'Salida registrada');
    refetchCashRegister();
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

  const TABS = [
    { k: 'tables',  label: 'Mesas',   icon: TableProperties },
    { k: 'credits', label: 'Créditos', icon: CreditCard },
    { k: 'sales',   label: 'Ventas',   icon: TrendingUp },
    { k: 'cash',    label: 'Caja',     icon: Wallet },
  ];

  return (
    <div className="flex flex-col -m-4 h-[calc(100%+2rem)] overflow-hidden">

      {/* Cash register bar */}
      <CashRegisterBar
        register={activeRegister}
        onOpen={() => setShowOpenCash(true)}
        onClose={() => setShowCloseCash(true)}
        onMovement={(type) => setCashMovementType(type)}
        onHistory={() => setShowCashHistory(true)}
      />

      {/* Top bar */}
      <div className="px-4 md:px-6 py-4 border-b bg-white flex items-center justify-between gap-3 flex-wrap flex-shrink-0">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-primary-100 flex items-center justify-center">
            <ShoppingCart size={20} className="text-primary-600" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Ventas</h1>
            <p className="text-sm text-gray-500">
              {tables.length} mesa{tables.length !== 1 ? 's' : ''} activa{tables.length !== 1 ? 's' : ''}
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {activeTab === 'tables' && tables.length > 0 && (
            <button
              onClick={() => setArrangeKey((k) => k + 1)}
              className="hidden md:flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors"
            >
              <LayoutGrid size={16} /> Ordenar
            </button>
          )}
          {activeTab === 'tables' && (
            <button
              onClick={() => setShowAddPanel(true)}
              className="flex items-center gap-2 bg-primary-600 hover:bg-primary-700 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors"
            >
              <PlusCircle size={16} /> Agregar Mesa
            </button>
          )}
        </div>
      </div>

      {/* Tabs */}
      <div className="flex border-b bg-white px-6 flex-shrink-0">
        {TABS.map(({ k, label, icon: Icon }) => (
          <button
            key={k}
            onClick={() => setActiveTab(k)}
            className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
              activeTab === k
                ? 'border-primary-600 text-primary-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <Icon size={16} /> {label}
            {k === 'tables' && tables.length > 0 && (
              <span className="ml-1 bg-primary-100 text-primary-700 text-xs rounded-full px-1.5">
                {tables.length}
              </span>
            )}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-hidden flex flex-col">

        {/* ── MESAS TAB ── */}
        {activeTab === 'tables' && (
          <div
            ref={areaRef}
            className="flex-1 rounded-none border-0 bg-gray-50/50 relative overflow-y-auto"
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
                  <PlusCircle className="h-4 w-4" /> Agregar Mesa
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
        )}

        {/* ── CRÉDITOS TAB ── */}
        {activeTab === 'credits' && (
          <CreditsPanel inline />
        )}

        {/* ── VENTAS TAB ── */}
        {activeTab === 'sales' && (
          <SalesSummaryTab />
        )}

        {/* ── CAJA TAB ── */}
        {activeTab === 'cash' && (
          <div className="flex-1 overflow-y-auto p-4 md:p-6">
            {!activeRegister ? (
              <div className="flex flex-col items-center justify-center h-60 text-gray-400">
                <Wallet size={48} className="mb-3 opacity-50" />
                <p className="text-lg font-medium">No hay caja abierta</p>
                <p className="text-sm mt-1">Abre una caja para ver el resumen del turno</p>
                <button
                  onClick={() => setShowOpenCash(true)}
                  className="mt-4 rounded-lg bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700 transition-colors"
                >
                  Abrir Caja
                </button>
              </div>
            ) : (
              <CashSummaryView register={activeRegister} />
            )}
          </div>
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

      <OpenCashRegisterModal
        isOpen={showOpenCash}
        onClose={() => setShowOpenCash(false)}
        onConfirm={handleOpenCash}
      />

      <CloseCashRegisterModal
        isOpen={showCloseCash}
        onClose={() => setShowCloseCash(false)}
        onConfirm={handleCloseCash}
        register={activeRegister}
      />

      <CashMovementModal
        isOpen={!!cashMovementType}
        onClose={() => setCashMovementType(null)}
        onConfirm={handleCashMovement}
        registerId={activeRegister?.id}
        type={cashMovementType || 'in'}
      />

      <CashRegisterHistory
        isOpen={showCashHistory}
        onClose={() => setShowCashHistory(false)}
      />
    </div>
  );
};

export default SalesPage;


