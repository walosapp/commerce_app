import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import toast from 'react-hot-toast';
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
import posDeliService from '../../services/posDeliService';

const isWeighedProduct = (product) => {
  const unit = `${product.unitAbbreviation || ''}`.toLowerCase();
  return ['kg', 'g', 'gr', 'lb'].includes(unit);
};

const PosDeliPage = () => {
  const [search, setSearch] = useState('');
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

  const { data: productsData, refetch } = useQuery({
    queryKey: ['pos-deli-products', search],
    queryFn: () => posDeliService.getProducts({ search }),
  });

  const { data: favoritesData } = useQuery({
    queryKey: ['pos-deli-favorites'],
    queryFn: () => posDeliService.getFavorites(),
  });

  const products = useMemo(() => {
    const live = productsData?.data ?? [];
    if (live.length > 0) return live.map((product) => ({ ...product, isWeighed: isWeighedProduct(product) }));
    return (favoritesData?.data ?? []).map((product) => ({ ...product, isWeighed: isWeighedProduct(product) }));
  }, [favoritesData?.data, productsData?.data]);

  const weighedProducts = useMemo(() => products.filter((product) => product.isWeighed), [products]);
  const unitProducts = useMemo(() => products.filter((product) => !product.isWeighed), [products]);

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
        if (items.length > 0) setCheckoutOpen(true);
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
    <div className="flex h-full flex-col gap-4 p-4 md:p-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-black text-gray-900">POS</h1>
          <p className="text-sm text-gray-500">Venta rapida para mostrador, barcode y productos por peso</p>
        </div>
      </div>

      <div className="grid flex-1 grid-cols-1 gap-4 xl:grid-cols-[1.4fr_0.8fr]">
        <div className="space-y-4">
          <ProductSearchBar value={search} onChange={setSearch} inputRef={searchInputRef} />
          <ScaleIndicator
            weight={weight}
            isStable={isStable}
            isConnected={isConnected}
            error={error}
            unit={config?.weightUnit || 'kg'}
            showActions={false}
            helperText="La conexion de la bascula ahora se administra en Configuracion > Dispositivos."
          />

          <div className="rounded-2xl border border-gray-200 bg-gray-50 p-4">
            <ProductGrid
              weighedProducts={weighedProducts}
              unitProducts={unitProducts}
              onSelectWeighed={(product) => handleAddWeighed(product)}
              onSelectUnit={handleAddUnit}
            />
          </div>
        </div>

        <TicketPanel
          items={items}
          total={total}
          selectedItemId={selectedItemId}
          onSelectItem={setSelectedItemId}
          onRemoveItem={handleRemoveItem}
          onUpdateQuantity={handleUpdateQuantity}
          onCheckout={() => setCheckoutOpen(true)}
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
