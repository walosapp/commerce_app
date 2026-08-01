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

  const {
    weight,
    isStable,
    isConnected,
    error,
    connect,
    disconnect,
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
  } = usePosDeliStore();

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
    addUnitItem(product);
    toast.success(`${product.name} agregado`);
  };

  const handleAddWeighed = (product, manualWeight = null) => {
    const effectiveWeight = manualWeight ?? weight;

    if (!manualWeight && !isConnected) {
      setWeightModalProduct(product);
      return;
    }

    if (effectiveWeight <= 0) {
      toast.error('Colocá el producto en la báscula antes de agregar');
      return;
    }

    if (!manualWeight && !isStable) {
      toast.error('Esperá a que el peso esté estable');
      return;
    }

    addWeighedItem(product, effectiveWeight);
    setWeightModalProduct(null);
    toast.success(`${product.name} agregado al ticket`);
  };

  const handleBarcode = async (barcode) => {
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

      if (event.key === 'Delete' && selectedItemId) {
        event.preventDefault();
        removeItem(selectedItemId);
      }

      if (event.key === 'Escape') {
        setCheckoutOpen(false);
        setWeightModalProduct(null);
      }
    };

    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [items.length, removeItem, selectedItemId]);

  const handleConfirmSale = async ({ method, cashReceived, reference }) => {
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

      const response = await posDeliService.createSale(payload);
      const sale = response.data;

      clearTicket();
      setCheckoutOpen(false);
      await refetch();

      toast.success(`Venta ${sale.ticketNumber} registrada`);

      if (typeof window !== 'undefined') {
        const receipt = window.open('', '_blank', 'width=420,height=640');
        if (receipt) {
          receipt.document.write(`
            <html><head><title>Ticket ${sale.ticketNumber}</title></head>
            <body style="font-family: sans-serif; padding: 16px;">
              <h2>POS-Deli</h2>
              <p>Ticket: ${sale.ticketNumber}</p>
              <p>Total: ${sale.total}</p>
              <p>Cambio: ${sale.change}</p>
            </body></html>
          `);
          receipt.document.close();
          receipt.print();
        }
      }
    } catch (saleError) {
      toast.error(saleError?.response?.data?.message || 'No se pudo registrar la venta');
    } finally {
      setSavingSale(false);
    }
  };

  return (
    <div className="flex h-full flex-col gap-4 p-4 md:p-6">
      <div className="flex items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-black text-gray-900">POS-Deli</h1>
          <p className="text-sm text-gray-500">Venta rápida para mostrador, barcode y productos por peso</p>
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
            onConnect={connect}
            onDisconnect={disconnect}
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
          onRemoveItem={removeItem}
          onUpdateQuantity={updateQuantity}
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
