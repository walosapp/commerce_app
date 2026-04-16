/**
 * Modal de Producto (Crear / Editar)
 * ¿Qué es? Formulario reutilizable para crear o editar productos
 * ¿Para qué? CRUD de productos con cálculo margen ↔ precio venta
 */

import { useState, useEffect, useRef } from 'react';
import { X, Calculator, Camera, Upload, ImageIcon, Trash2 } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import inventoryService from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';

const ProductFormModal = ({ isOpen, onClose, onSave, product = null }) => {
  const isEdit = !!product;
  const { tenantId } = useAuthStore();

  const PRODUCT_TYPES = [
    { value: 'simple', label: 'Simple (insumo/botella)', trackStock: true },
    { value: 'prepared', label: 'Preparación (plato/bebida)', trackStock: false },
    { value: 'combo', label: 'Combo', trackStock: false },
    { value: 'service', label: 'Servicio', trackStock: false },
  ];

  const [form, setForm] = useState({
    name: '',
    sku: '',
    barcode: '',
    description: '',
    categoryId: '',
    unitId: '',
    costPrice: '',
    marginPercentage: '',
    salePrice: '',
    minStock: '0',
    maxStock: '0',
    reorderPoint: '0',
    isPerishable: false,
    shelfLifeDays: '',
    productType: 'simple',
    trackStock: true,
    isForSale: true,
  });

  const [saving, setSaving] = useState(false);
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const fileInputRef = useRef(null);
  const cameraInputRef = useRef(null);

  const { data: categoriesData } = useQuery({
    queryKey: ['categories', tenantId],
    queryFn: () => inventoryService.getCategories(),
    enabled: isOpen && !!tenantId,
  });

  const { data: unitsData } = useQuery({
    queryKey: ['units', tenantId],
    queryFn: () => inventoryService.getUnits(),
    enabled: isOpen && !!tenantId,
  });

  const categories = categoriesData?.data || [];
  const units = unitsData?.data || [];

  useEffect(() => {
    if (product) {
      setForm({
        name: product.name || product.productName || '',
        sku: product.sku || '',
        barcode: product.barcode || '',
        description: product.description || '',
        categoryId: product.categoryId || '',
        unitId: product.unitId || '',
        costPrice: product.costPrice ?? '',
        marginPercentage: product.marginPercentage ?? '',
        salePrice: product.salePrice ?? '',
        minStock: product.minStock ?? '0',
        maxStock: product.maxStock ?? '0',
        reorderPoint: product.reorderPoint ?? '0',
        isPerishable: product.isPerishable || false,
        shelfLifeDays: product.shelfLifeDays ?? '',
        productType: product.productType || 'simple',
        trackStock: product.trackStock ?? true,
        isForSale: product.isForSale ?? true,
      });
      setImagePreview(product.imageUrl ? `${API_BASE}${product.imageUrl}` : null);
    } else {
      setForm({
        name: '',
        sku: '',
        barcode: '',
        description: '',
        categoryId: '',
        unitId: '',
        costPrice: '',
        marginPercentage: '',
        salePrice: '',
        minStock: '0',
        maxStock: '0',
        reorderPoint: '0',
        isPerishable: false,
        shelfLifeDays: '',
        productType: 'simple',
        trackStock: true,
        isForSale: true,
      });
      setImagePreview(null);
    }
    setImageFile(null);
  }, [product, isOpen]);

  const handleImageSelect = (file) => {
    if (!file) return;
    if (file.size > 2 * 1024 * 1024) {
      alert('La imagen no puede superar 2MB');
      return;
    }
    setImageFile(file);
    const reader = new FileReader();
    reader.onloadend = () => setImagePreview(reader.result);
    reader.readAsDataURL(file);
  };

  const handleDrop = (e) => {
    e.preventDefault();
    const file = e.dataTransfer?.files?.[0];
    if (file && file.type.startsWith('image/')) handleImageSelect(file);
  };

  const removeImage = () => {
    setImageFile(null);
    setImagePreview(null);
  };

  const handleChange = (field, value) => {
    const updated = { ...form, [field]: value };

    if (field === 'costPrice' || field === 'marginPercentage') {
      const cost = parseFloat(field === 'costPrice' ? value : updated.costPrice);
      const margin = parseFloat(field === 'marginPercentage' ? value : updated.marginPercentage);
      if (!isNaN(cost) && !isNaN(margin) && cost > 0) {
        updated.salePrice = (cost * (1 + margin / 100)).toFixed(2);
      }
    }

    if (field === 'salePrice') {
      const cost = parseFloat(updated.costPrice);
      const sale = parseFloat(value);
      if (!isNaN(cost) && !isNaN(sale) && cost > 0) {
        updated.marginPercentage = (((sale - cost) / cost) * 100).toFixed(2);
      }
    }

    setForm(updated);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const payload = {
        name: form.name,
        sku: form.sku,
        barcode: form.barcode || null,
        description: form.description || null,
        categoryId: Number(form.categoryId),
        unitId: Number(form.unitId),
        costPrice: Number(form.costPrice),
        salePrice: Number(form.salePrice),
        marginPercentage: form.marginPercentage ? Number(form.marginPercentage) : null,
        minStock: Number(form.minStock),
        maxStock: Number(form.maxStock),
        reorderPoint: form.trackStock ? Number(form.reorderPoint) : 0,
        isPerishable: form.isPerishable,
        shelfLifeDays: form.isPerishable && form.shelfLifeDays ? Number(form.shelfLifeDays) : null,
        productType: form.productType,
        trackStock: form.trackStock,
        isForSale: form.isForSale,
      };
      await onSave(payload, imageFile);
      onClose();
    } catch (err) {
      console.error('Error guardando producto:', err);
    } finally {
      setSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50" onClick={onClose}>
      <div
        className="relative w-full max-w-2xl max-h-[90vh] overflow-y-auto rounded-xl bg-white shadow-2xl m-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="sticky top-0 z-10 flex items-center justify-between border-b bg-white px-6 py-4 rounded-t-xl">
          <h2 className="text-lg font-bold text-gray-900">
            {isEdit ? 'Editar Producto' : 'Nuevo Producto'}
          </h2>
          <button onClick={onClose} className="rounded-lg p-1.5 hover:bg-gray-100 transition-colors">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-6 space-y-5">
          {/* Imagen del producto */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">Imagen del producto</label>
            <div
              className="relative rounded-lg border-2 border-dashed border-gray-300 bg-gray-50 hover:border-primary-400 hover:bg-primary-50/30 transition-colors"
              onDrop={handleDrop}
              onDragOver={(e) => e.preventDefault()}
            >
              {imagePreview ? (
                <div className="relative flex items-center justify-center p-4">
                  <img
                    src={imagePreview}
                    alt="Preview"
                    className="max-h-48 rounded-lg object-contain"
                  />
                  <button
                    type="button"
                    onClick={removeImage}
                    className="absolute top-2 right-2 rounded-full bg-red-100 p-1.5 text-red-600 hover:bg-red-200 transition-colors"
                    title="Eliminar imagen"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <div className="flex flex-col items-center justify-center py-8 px-4">
                  <ImageIcon className="h-10 w-10 text-gray-400 mb-3" />
                  <p className="text-sm text-gray-500 mb-3">Arrastra una imagen o usa los botones</p>
                  <div className="flex gap-2">
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="flex items-center gap-1.5 rounded-lg border border-gray-300 bg-white px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors"
                    >
                      <Upload className="h-3.5 w-3.5" />
                      Subir archivo
                    </button>
                    <button
                      type="button"
                      onClick={() => cameraInputRef.current?.click()}
                      className="flex items-center gap-1.5 rounded-lg border border-gray-300 bg-white px-3 py-2 text-xs font-medium text-gray-700 hover:bg-gray-50 transition-colors"
                    >
                      <Camera className="h-3.5 w-3.5" />
                      Tomar foto
                    </button>
                  </div>
                </div>
              )}
              <input
                ref={fileInputRef}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                className="hidden"
                onChange={(e) => handleImageSelect(e.target.files?.[0])}
              />
              <input
                ref={cameraInputRef}
                type="file"
                accept="image/*"
                capture="environment"
                className="hidden"
                onChange={(e) => handleImageSelect(e.target.files?.[0])}
              />
            </div>
            <p className="text-xs text-gray-400 mt-1">JPG, PNG o WebP. Máximo 2MB.</p>
          </div>

          {/* Nombre y SKU */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Nombre *</label>
              <input
                type="text"
                required
                value={form.name}
                onChange={(e) => handleChange('name', e.target.value)}
                className="input"
                placeholder="Ej: Coca-Cola 350ml"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">SKU *</label>
              <input
                type="text"
                required
                value={form.sku}
                onChange={(e) => handleChange('sku', e.target.value)}
                className="input"
                placeholder="Ej: COC-350"
              />
            </div>
          </div>

          {/* Categoría y Unidad */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Categoría *</label>
              <select
                required
                value={form.categoryId}
                onChange={(e) => handleChange('categoryId', e.target.value)}
                className="input"
              >
                <option value="">Seleccionar...</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Unidad *</label>
              <select
                required
                value={form.unitId}
                onChange={(e) => handleChange('unitId', e.target.value)}
                className="input"
              >
                <option value="">Seleccionar...</option>
                {units.map((u) => (
                  <option key={u.id} value={u.id}>{u.name} ({u.abbreviation})</option>
                ))}
              </select>
            </div>
          </div>

          {/* Precios */}
          <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-4">
            <div className="flex items-center gap-2 text-sm font-medium text-gray-700">
              <Calculator className="h-4 w-4" />
              Precios y Margen
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div>
                <label className="block text-sm text-gray-600 mb-1">Costo *</label>
                <input
                  type="number"
                  required
                  min="0"
                  step="0.01"
                  value={form.costPrice}
                  onChange={(e) => handleChange('costPrice', e.target.value)}
                  className="input"
                  placeholder="0.00"
                />
              </div>
              <div>
                <label className="block text-sm text-gray-600 mb-1">Margen %</label>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.marginPercentage}
                  onChange={(e) => handleChange('marginPercentage', e.target.value)}
                  className="input"
                  placeholder="30"
                />
              </div>
              <div>
                <label className="block text-sm text-gray-600 mb-1">Precio Venta *</label>
                <input
                  type="number"
                  required
                  min="0"
                  step="0.01"
                  value={form.salePrice}
                  onChange={(e) => handleChange('salePrice', e.target.value)}
                  className="input"
                  placeholder="0.00"
                />
              </div>
            </div>
            <p className="text-xs text-gray-500">
              Al cambiar el margen se recalcula el precio de venta y viceversa.
            </p>
          </div>

          {/* Tipo de Producto */}
          <div className="rounded-lg border border-gray-200 bg-gray-50 p-4 space-y-4">
            <label className="block text-sm font-medium text-gray-700">Tipo de producto</label>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2">
              {PRODUCT_TYPES.map((pt) => (
                <button
                  key={pt.value}
                  type="button"
                  onClick={() => {
                    setForm(prev => ({ ...prev, productType: pt.value, trackStock: pt.trackStock }));
                  }}
                  className={`rounded-lg border px-3 py-2 text-xs font-medium transition-colors ${
                    form.productType === pt.value
                      ? 'border-primary-500 bg-primary-50 text-primary-700'
                      : 'border-gray-300 bg-white text-gray-600 hover:bg-gray-50'
                  }`}
                >
                  {pt.label}
                </button>
              ))}
            </div>

            {!form.trackStock && (
              <p className="text-xs text-amber-600 bg-amber-50 rounded-md px-3 py-2">
                Este producto no requiere control de stock. Siempre estará disponible para venta mientras esté activo.
              </p>
            )}

            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.trackStock}
                onChange={(e) => handleChange('trackStock', e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">Controlar stock</span>
            </label>

            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.isForSale}
                onChange={(e) => handleChange('isForSale', e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">Disponible para venta</span>
            </label>
          </div>

          {/* Stock (solo si trackStock) */}
          {form.trackStock && (
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Stock Mínimo</label>
                <input
                  type="number"
                  min="0"
                  value={form.minStock}
                  onChange={(e) => handleChange('minStock', e.target.value)}
                  className="input"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Stock Máximo</label>
                <input
                  type="number"
                  min="0"
                  value={form.maxStock}
                  onChange={(e) => handleChange('maxStock', e.target.value)}
                  className="input"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Punto Reorden
                  <span className="ml-1 text-xs text-gray-400 font-normal" title="Cuando el stock baje a este nivel, se generará una alerta">
                    (alerta de reabastecimiento)
                  </span>
                </label>
                <input
                  type="number"
                  min="0"
                  value={form.reorderPoint}
                  onChange={(e) => handleChange('reorderPoint', e.target.value)}
                  className="input"
                />
              </div>
            </div>
          )}

          {/* Barcode y Descripción */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Código de Barras</label>
            <input
              type="text"
              value={form.barcode}
              onChange={(e) => handleChange('barcode', e.target.value)}
              className="input"
              placeholder="Opcional"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Descripción</label>
            <textarea
              value={form.description}
              onChange={(e) => handleChange('description', e.target.value)}
              className="input min-h-[80px] resize-y"
              placeholder="Descripción del producto (opcional)"
            />
          </div>

          {/* Perishable */}
          <div className="space-y-2">
            <label className="flex items-center gap-2 cursor-pointer">
              <input
                type="checkbox"
                checked={form.isPerishable}
                onChange={(e) => handleChange('isPerishable', e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
              />
              <span className="text-sm text-gray-700">Producto perecedero</span>
            </label>
            {form.isPerishable && (
              <div className="ml-6">
                <label className="block text-sm text-gray-600 mb-1">Días de vida útil</label>
                <input
                  type="number"
                  min="1"
                  value={form.shelfLifeDays}
                  onChange={(e) => handleChange('shelfLifeDays', e.target.value)}
                  className="input w-32"
                  placeholder="Ej: 7"
                />
              </div>
            )}
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-4 border-t">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving}
              className="rounded-lg bg-primary-600 px-6 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors"
            >
              {saving ? 'Guardando...' : isEdit ? 'Actualizar' : 'Crear Producto'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ProductFormModal;
