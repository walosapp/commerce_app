import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import toast from 'react-hot-toast';
import { LayoutGrid, ChevronRight } from 'lucide-react';
import PosHeader from './components/PosHeader';
import ProductGrid from './components/ProductGrid';
import ProductSearchBar from './components/ProductSearchBar';
import ScaleIndicator from './components/ScaleIndicator';
import TicketPanel from './components/TicketPanel';
import PaymentModal from './components/PaymentModal';
import WeightInputModal from './components/WeightInputModal';
import useScale from './hooks/useScale';
import useBarcodeScanner from './hooks/useBarcodeScanner';
import usePosDeliStore from './stores/posDeliStore';
import usePostSaleHardwareStore from '../../stores/postSaleHardwareStore';
import useAuthStore from '../../stores/authStore';
import posDeliService from '../../services/posDeliService';

const isWeighedProduct = (product) => {
  const unit = `${product.unitAbbreviation || ''}`.toLowerCase();
  return ['kg', 'g', 'gr', 'lb'].includes(unit);
};

const PosDeliPage = () => {
  const { branchId, tenantId } = useAuthStore();
  const canLoadSalesCatalog = !!tenantId && !!branchId;
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState(null);
  const [checkoutOpen, setCheckoutOpen] = useState(false);
  const [weightModalProduct, setWeightModalProduct] = useState(null);
  const [savingSale, setSavingSale] = useState(false);
  const searchInputRef = useRef(null);
  const pendingSaleIntentRef = useRef(null);

  const {
    weight,
    isStable,
    isConnected,
    error,
    config,
  } = useScale();

  const {
    items,
    selectedItemId,
    getTotal,
    addWeighedItem,
    addUnitItem,
    removeItem,
    updateQuantity,
    setSelectedItemId,
    clearTicket,
    getIdempotencyKey,
    renewIdempotencyKey,
  } = usePosDeliStore();
  const enqueuePostSale = usePostSaleHardwareStore((state) => state.enqueuePostSale);

  const total = getTotal();

  const { data: productsData, refetch, isPending, isError } = useQuery({
    queryKey: ['pos-deli-products', tenantId, branchId, ''],
    queryFn: () => posDeliService.getProducts({ search: '' }),
    enabled: canLoadSalesCatalog,
  });

  const { data: favoritesData } = useQuery({
    queryKey: ['pos-deli-favorites', tenantId, branchId],
    queryFn: () => posDeliService.getFavorites(),
    enabled: canLoadSalesCatalog,
  });

  const products = useMemo(() => {
    // El endpoint devuelve el catálogo completo: filtrar localmente conserva
    // las categorías visibles mientras se busca, sin permisos de Inventario.
    const catalog = productsData?.data ?? favoritesData?.data ?? [];
    return catalog.map((product) => ({ ...product, isWeighed: isWeighedProduct(product) }));
  }, [favoritesData?.data, productsData?.data]);

  const categories = useMemo(() => [...new Set(products.map((product) => product.categoryName || 'Sin categoría'))].sort((a, b) => a.localeCompare(b, 'es')), [products]);
  const visibleProducts = useMemo(() => {
    const term = search.trim().toLocaleLowerCase('es');
    return products.filter((product) => (
      (category === null || (product.categoryName || 'Sin categoría') === category)
      && (!term || [product.name, product.sku, product.barcode].some((value) => `${value || ''}`.toLocaleLowerCase('es').includes(term)))
    ));
  }, [products, category, search]);
  const weighedProducts = useMemo(() => visibleProducts.filter((product) => product.isWeighed), [visibleProducts]);
  const unitProducts = useMemo(() => visibleProducts.filter((product) => !product.isWeighed), [visibleProducts]);
  const showScale = isConnected || !!error || products.some((product) => product.isWeighed);

  useEffect(() => {
    setCategory(null);
    setSearch('');
  }, [tenantId, branchId]);

  const handleAddUnit = (product) => {
    if (savingSale) return;
    pendingSaleIntentRef.current = null;
    addUnitItem(product);
    toast.success(`${product.name} agregado`);
  };

  const handleAddWeighed = (product, manualWeight = null) => {
    if (savingSale) return;
    const effectiveWeight = manualWeight ?? weight;

    if (!manualWeight && !isConnected) {
      setWeightModalProduct(product);
      return;
    }

    if (effectiveWeight <= 0) {
      toast.error('Coloca el producto en la bascula antes de agregar');
      return;
    }

    if (!manualWeight && !isStable) {
      toast.error('Espera a que el peso este estable');
      return;
    }

    pendingSaleIntentRef.current = null;
    addWeighedItem(product, effectiveWeight);
    setWeightModalProduct(null);
    toast.success(`${product.name} agregado al ticket`);
  };

  const handleBarcode = async (barcode) => {
    if (savingSale) return;
    try {
      const response = await posDeliService.getProducts({ barcode });
      const product = response?.data?.[0];
      if (!product) {
        toast.error(`Producto no encontrado: ${barcode}`);
        return;
      }

      const normalized = { ...product, isWeighed: isWeighedProduct(product) };
      if (normalized.isWeighed) {
        handleAddWeighed(normalized);
      } else {
        handleAddUnit(normalized);
      }
    } catch (errorScan) {
      toast.error(errorScan?.response?.data?.message || `Producto no encontrado: ${barcode}`);
    }
  };

  useBarcodeScanner({
    onScan: handleBarcode,
  });

  useEffect(() => {
    const onKeyDown = (event) => {
      if (event.key === 'F12') {
        event.preventDefault();
        if (items.length > 0 && !savingSale) setCheckoutOpen(true);
      }

      if (event.key === 'F1') {
        event.preventDefault();
        searchInputRef.current?.focus();
      }

      if (event.key === 'Delete' && selectedItemId && !savingSale) {
        event.preventDefault();
        pendingSaleIntentRef.current = null;
        removeItem(selectedItemId);
      }

      if (event.key === 'Escape') {
        setCheckoutOpen(false);
        setWeightModalProduct(null);
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [items.length, removeItem, savingSale, selectedItemId]);

  const handleRemoveItem = (itemId) => {
    if (savingSale) return;
    pendingSaleIntentRef.current = null;
    removeItem(itemId);
  };

  const handleUpdateQuantity = (itemId, quantity) => {
    if (savingSale) return;
    pendingSaleIntentRef.current = null;
    updateQuantity(itemId, quantity);
  };

  const handleConfirmSale = async ({ method, cashReceived, reference }) => {
    let persistedSale = null;

    try {
      setSavingSale(true);
      const payload = {
        items: items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          isWeighed: item.isWeighed,
        })),
        payments: [
          {
            method,
            amount: total,
            reference: reference || null,
          },
        ],
        cashReceived: method === 'cash' ? cashReceived : null,
      };

      const signature = JSON.stringify(payload);
      if (!pendingSaleIntentRef.current || pendingSaleIntentRef.current.signature !== signature) {
        pendingSaleIntentRef.current = {
          payload,
          signature,
          idempotencyKey: pendingSaleIntentRef.current
            ? renewIdempotencyKey()
            : getIdempotencyKey(),
        };
      }

      const pendingIntent = pendingSaleIntentRef.current;
      const response = await posDeliService.createSale(
        pendingIntent.payload,
        pendingIntent.idempotencyKey
      );
      persistedSale = response.data;
    } catch (saleError) {
      toast.error(saleError?.response?.data?.message || 'No se pudo registrar la venta');
    } finally {
      setSavingSale(false);
    }

    if (!persistedSale) return;

    pendingSaleIntentRef.current = null;
    clearTicket();
    setCheckoutOpen(false);
    toast.success(`Venta ${persistedSale.ticketNumber} registrada`);

    // Desde este punto la venta ya fue confirmada por el backend. Refresco y
    // hardware son efectos postventa independientes: nunca deben volver a
    // ejecutar checkout ni convertir una venta exitosa en un error de venta.
    void refetch().catch(() => undefined);
    void Promise.resolve()
      .then(() => enqueuePostSale({ orderId: persistedSale.saleId }))
      .catch(() => undefined);
  };

  return (
    <div className="flex min-h-0 flex-col gap-4 xl:h-full">
      <PosHeader>
        <ProductSearchBar value={search} onChange={setSearch} inputRef={searchInputRef} />
      </PosHeader>

      <div className="grid min-h-0 flex-1 grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1fr)_320px] xl:grid-cols-[152px_minmax(0,1fr)_320px]">
        <nav aria-label="Categorías de productos" className="min-h-0 rounded-2xl border border-gray-200 bg-white p-3 lg:col-span-2 xl:col-span-1 xl:overflow-y-auto">
          <h2 className="mb-3 flex items-center gap-2 px-2 pt-2 text-xs font-bold uppercase tracking-wide text-gray-500"><LayoutGrid className="h-4 w-4" />Categorías</h2>
          <div className="flex gap-1 overflow-x-auto xl:flex-col xl:overflow-visible">
            {[null, ...categories].map((name) => (
              <button key={name === null ? 'all' : `category:${name}`} type="button" aria-pressed={category === name} onClick={() => setCategory(name)} className={`flex shrink-0 items-center justify-between gap-2 rounded-xl px-3 py-3 text-left text-sm font-semibold transition-colors ${category === name ? 'bg-primary-600 text-white' : 'text-gray-600 hover:bg-gray-50'}`}>
                <span className="break-words">{name ?? 'Todos'}</span>
                {category === name && <ChevronRight aria-hidden="true" className="hidden h-4 w-4 shrink-0 xl:block" />}
              </button>
            ))}
          </div>
        </nav>

        <section aria-label="Catálogo de productos" className="flex min-h-0 min-w-0 flex-col gap-4 rounded-2xl border border-gray-200 bg-gray-50 p-4">
          <div className="shrink-0">
            <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-xs text-gray-400">
              <span>{visibleProducts.length} productos{category ? ` · ${category}` : ''}</span>
              {showScale && <ScaleIndicator
                compact
                weight={weight}
                isStable={isStable}
                isConnected={isConnected}
                error={error}
                unit={config?.weightUnit || 'kg'}
                showActions={false}
                helperText="La conexión de la báscula se administra en Configuración > Dispositivos."
              />}
              <span className="ml-auto">F1 · Buscar</span>
            </div>
          </div>

          <div className="scrollbar-subtle max-h-[50dvh] min-h-0 flex-1 overflow-y-auto lg:max-h-none">
            {!canLoadSalesCatalog ? <p role="alert" className="p-6 text-center text-sm text-amber-700">Necesitás una sucursal asignada para cargar productos.</p>
              : isError ? <div role="alert" className="p-6 text-center text-sm text-gray-600"><p>No se pudieron cargar los productos.</p><button type="button" onClick={() => refetch()} className="mt-3 font-semibold text-primary-600 hover:underline">Reintentar catálogo</button></div>
              : isPending ? <p role="status" className="p-6 text-center text-sm text-gray-500">Cargando productos…</p>
              : <ProductGrid
              weighedProducts={weighedProducts}
              unitProducts={unitProducts}
              onSelectWeighed={(product) => handleAddWeighed(product)}
              onSelectUnit={handleAddUnit}
              disabled={savingSale}
            />}
          </div>
        </section>

        <TicketPanel
          items={items}
          total={total}
          selectedItemId={selectedItemId}
          onSelectItem={setSelectedItemId}
          onRemoveItem={handleRemoveItem}
          onUpdateQuantity={handleUpdateQuantity}
          onCheckout={() => { if (!savingSale) setCheckoutOpen(true); }}
          disabled={savingSale}
        />
      </div>

      <PaymentModal
        isOpen={checkoutOpen}
        total={total}
        onClose={() => setCheckoutOpen(false)}
        onConfirm={handleConfirmSale}
        loading={savingSale}
      />

      <WeightInputModal
        isOpen={!!weightModalProduct}
        product={weightModalProduct}
        onClose={() => setWeightModalProduct(null)}
        onConfirm={(manualWeight) => handleAddWeighed(weightModalProduct, manualWeight)}
      />
    </div>
  );
};

export default PosDeliPage;
