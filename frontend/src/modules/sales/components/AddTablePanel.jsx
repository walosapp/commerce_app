/**
 * Panel lateral para Agregar Mesa
 * Slide-in panel con grid de productos y resumen
 */

import { useEffect, useMemo, useRef, useState, useCallback } from 'react';
import { GripVertical, ShoppingCart, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatCurrency } from '../../../utils/formatCurrency';
import ProductGrid from './ProductGrid';

const DEFAULT_DESKTOP_WIDTH = 560;
const HOVER_EXPANDED_WIDTH = 720;
const MIN_DESKTOP_WIDTH = 480;

const AddTablePanel = ({
  isOpen,
  onClose,
  onCreateTable,
  products = [],
  isLoading = false,
  title = 'Nueva Mesa',
  submitLabel = 'Crear Mesa',
}) => {
  const [selectedItems, setSelectedItems] = useState([]);
  const [saving, setSaving] = useState(false);
  const [desktopWidth, setDesktopWidth] = useState(DEFAULT_DESKTOP_WIDTH);
  const [isHovering, setIsHovering] = useState(false);
  const [isResizing, setIsResizing] = useState(false);
  const [hasManualResize, setHasManualResize] = useState(false);
  const resizeStateRef = useRef({ startX: 0, startWidth: DEFAULT_DESKTOP_WIDTH });

  const handleUpdateItem = useCallback((item) => {
    setSelectedItems((prev) => {
      const existing = prev.findIndex((i) => i.productId === item.productId);
      if (item.quantity <= 0) {
        return prev.filter((i) => i.productId !== item.productId);
      }
      if (existing >= 0) {
        const updated = [...prev];
        updated[existing] = item;
        return updated;
      }
      return [...prev, item];
    });
  }, []);

  const totalItems = selectedItems.reduce((sum, i) => sum + i.quantity, 0);
  const totalPrice = selectedItems.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0);

  const effectiveDesktopWidth = useMemo(() => {
    if (typeof window === 'undefined') return desktopWidth;

    const maxWidth = Math.floor(window.innerWidth * 0.78);
    const autoExpandedWidth = Math.min(HOVER_EXPANDED_WIDTH, maxWidth);

    if (!hasManualResize && isHovering) {
      return Math.max(MIN_DESKTOP_WIDTH, autoExpandedWidth);
    }

    return Math.max(MIN_DESKTOP_WIDTH, Math.min(desktopWidth, maxWidth));
  }, [desktopWidth, hasManualResize, isHovering]);

  useEffect(() => {
    if (!isOpen) {
      setIsHovering(false);
      setIsResizing(false);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!isResizing) return undefined;

    const handlePointerMove = (event) => {
      const viewportWidth = window.innerWidth;
      const maxWidth = Math.floor(viewportWidth * 0.78);
      const delta = resizeStateRef.current.startX - event.clientX;
      const nextWidth = resizeStateRef.current.startWidth + delta;
      setDesktopWidth(Math.max(MIN_DESKTOP_WIDTH, Math.min(nextWidth, maxWidth)));
    };

    const handlePointerUp = () => {
      setIsResizing(false);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);

    return () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };
  }, [isResizing]);

  const handleResizeStart = (event) => {
    if (window.innerWidth < 1024) return;
    resizeStateRef.current = {
      startX: event.clientX,
      startWidth: effectiveDesktopWidth,
    };
    setHasManualResize(true);
    setIsResizing(true);
  };

  const handleCreate = async () => {
    if (selectedItems.length === 0) return;
    setSaving(true);
    try {
      await onCreateTable({
        items: selectedItems.map((i) => ({
          productId: i.productId,
          productName: i.productName,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
        })),
      });
      setSelectedItems([]);
      onClose();
    } catch (err) {
      toast.error(err?.response?.data?.message || 'No fue posible guardar la mesa');
    } finally {
      setSaving(false);
    }
  };

  const handleClose = () => {
    setSelectedItems([]);
    onClose();
  };

  return (
    <>
      {isOpen && <div className="fixed inset-0 z-[60] bg-black/40" onClick={handleClose} />}

      <div
        className={`fixed inset-y-0 right-0 z-[70] w-full bg-white shadow-2xl transition-transform duration-300 ease-in-out lg:w-auto ${isOpen ? 'translate-x-0' : 'translate-x-full'}`}
        style={{ width: typeof window !== 'undefined' && window.innerWidth >= 1024 ? effectiveDesktopWidth : '100%' }}
        onMouseEnter={() => setIsHovering(true)}
        onMouseLeave={() => setIsHovering(false)}
      >
        <div className="flex h-full flex-col overflow-hidden">
          <button
            type="button"
            onPointerDown={handleResizeStart}
            className="absolute left-0 top-0 hidden h-full w-4 -translate-x-1/2 cursor-col-resize items-center justify-center text-gray-300 transition-colors hover:text-primary-500 lg:flex"
            title="Arrastra para expandir el panel"
          >
            <GripVertical className="h-5 w-5" />
          </button>

          <div className="flex items-center justify-between border-b px-5 py-4 flex-shrink-0">
            <div>
              <h2 className="text-lg font-bold text-gray-900">{title}</h2>
              <p className="text-xs text-gray-500">
                Selecciona productos para la mesa
                <span className="hidden lg:inline">. Puedes expandir este panel al pasar el cursor o arrastrando su borde.</span>
              </p>
            </div>
            <button onClick={handleClose} className="rounded-lg p-1.5 hover:bg-gray-100 transition-colors">
              <X className="h-5 w-5" />
            </button>
          </div>

          <div className="flex-1 overflow-hidden px-5 py-4">
            {isLoading ? (
              <div className="flex items-center justify-center h-full">
                <div className="animate-spin h-8 w-8 rounded-full border-4 border-primary-500 border-t-transparent" />
              </div>
            ) : (
              <ProductGrid
                products={products}
                selectedItems={selectedItems}
                onUpdateItem={handleUpdateItem}
              />
            )}
          </div>

          <div className="border-t bg-gray-50 px-5 py-4 flex-shrink-0 space-y-3">
            {selectedItems.length > 0 && (
              <div className="space-y-1 max-h-28 overflow-y-auto scrollbar-subtle">
                {selectedItems.map((item) => (
                  <div key={item.productId} className="flex items-center justify-between text-sm">
                    <span className="text-gray-600 truncate flex-1">
                      {item.productName} x {item.quantity}
                    </span>
                    <span className="font-medium text-gray-900 ml-2">
                      {formatCurrency(item.quantity * item.unitPrice)}
                    </span>
                  </div>
                ))}
              </div>
            )}

            <div className="flex items-center justify-between pt-2 border-t">
              <div>
                <p className="text-xs text-gray-500">{totalItems} productos</p>
                <p className="text-lg font-bold text-gray-900">{formatCurrency(totalPrice)}</p>
              </div>
              <button
                onClick={handleCreate}
                disabled={saving || selectedItems.length === 0}
                className="flex items-center gap-2 rounded-lg bg-primary-600 px-5 py-2.5 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors"
              >
                <ShoppingCart className="h-4 w-4" />
                {saving ? 'Guardando...' : submitLabel}
              </button>
            </div>
          </div>
        </div>
      </div>
    </>
  );
};

export default AddTablePanel;
