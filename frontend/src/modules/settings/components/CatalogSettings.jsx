import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Tag, Ruler, Plus, Pencil, Trash2, ToggleLeft, ToggleRight, Loader2, X, Check
} from 'lucide-react';
import toast from 'react-hot-toast';
import catalogService from '../../../services/catalogService';
import useAuthStore from '../../../stores/authStore';

const UNIT_TYPES = [
  { value: 'quantity', label: 'Cantidad' },
  { value: 'weight',   label: 'Peso' },
  { value: 'volume',   label: 'Volumen' },
  { value: 'length',   label: 'Longitud' },
];

const ICON_OPTIONS = [
  { value: 'wine',      label: 'Copa' },
  { value: 'coffee',    label: 'Cafe' },
  { value: 'utensils',  label: 'Cubiertos' },
  { value: 'package',   label: 'Paquete' },
  { value: 'trash-2',   label: 'Desechables' },
  { value: 'beef',      label: 'Carne' },
  { value: 'pizza',     label: 'Pizza' },
  { value: 'fish',      label: 'Pescado' },
  { value: 'leaf',      label: 'Vegetal' },
  { value: 'droplets',  label: 'Liquidos' },
];

const COLOR_OPTIONS = [
  '#8B4513', '#4A90E2', '#E74C3C', '#27AE60', '#F39C12',
  '#9B59B6', '#1ABC9C', '#E67E22', '#95A5A6', '#2C3E50',
];

// ─── Inline form row ────────────────────────────────────────────────────────────
const CategoryForm = ({ initial, onSave, onCancel }) => {
  const [form, setForm] = useState({
    name: initial?.name ?? '',
    description: initial?.description ?? '',
    icon: initial?.icon ?? '',
    color: initial?.color ?? '#4A90E2',
    displayOrder: initial?.displayOrder ?? 0,
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.name.trim()) { toast.error('Nombre requerido'); return; }
    setSaving(true);
    try { await onSave(form); }
    finally { setSaving(false); }
  };

  return (
    <form onSubmit={handleSubmit} className="bg-indigo-50 border border-indigo-200 rounded-xl p-4 space-y-3">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Nombre *</label>
          <input
            className="input text-sm"
            value={form.name}
            onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
            placeholder="Ej: Bebidas Alcoholicas"
            autoFocus
          />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Descripcion</label>
          <input
            className="input text-sm"
            value={form.description}
            onChange={e => setForm(f => ({ ...f, description: e.target.value }))}
            placeholder="Opcional"
          />
        </div>
      </div>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Icono</label>
          <select className="input text-sm" value={form.icon} onChange={e => setForm(f => ({ ...f, icon: e.target.value }))}>
            <option value="">Sin icono</option>
            {ICON_OPTIONS.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Orden</label>
          <input
            type="number" min="0" className="input text-sm"
            value={form.displayOrder}
            onChange={e => setForm(f => ({ ...f, displayOrder: Number(e.target.value) }))}
          />
        </div>
        <div className="sm:col-span-2">
          <label className="block text-xs font-medium text-gray-600 mb-1">Color</label>
          <div className="flex flex-wrap gap-1.5">
            {COLOR_OPTIONS.map(c => (
              <button
                key={c} type="button"
                onClick={() => setForm(f => ({ ...f, color: c }))}
                className={`w-6 h-6 rounded-full border-2 transition-transform ${form.color === c ? 'border-gray-800 scale-110' : 'border-transparent'}`}
                style={{ backgroundColor: c }}
              />
            ))}
          </div>
        </div>
      </div>
      <div className="flex justify-end gap-2 pt-1">
        <button type="button" onClick={onCancel} className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 px-3 py-1.5 rounded-lg border border-gray-200 bg-white">
          <X size={12} /> Cancelar
        </button>
        <button type="submit" disabled={saving} className="flex items-center gap-1 text-xs text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 px-3 py-1.5 rounded-lg">
          {saving ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
          {initial ? 'Actualizar' : 'Crear'}
        </button>
      </div>
    </form>
  );
};

const UnitForm = ({ initial, onSave, onCancel }) => {
  const [form, setForm] = useState({
    name: initial?.name ?? '',
    abbreviation: initial?.abbreviation ?? '',
    unitType: initial?.unitType ?? 'quantity',
  });
  const [saving, setSaving] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!form.name.trim() || !form.abbreviation.trim()) { toast.error('Nombre y abreviatura requeridos'); return; }
    setSaving(true);
    try { await onSave(form); }
    finally { setSaving(false); }
  };

  return (
    <form onSubmit={handleSubmit} className="bg-blue-50 border border-blue-200 rounded-xl p-4 space-y-3">
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Nombre *</label>
          <input className="input text-sm" value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Ej: Kilogramo" autoFocus />
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Abreviatura *</label>
          <input className="input text-sm" value={form.abbreviation} onChange={e => setForm(f => ({ ...f, abbreviation: e.target.value }))} placeholder="Ej: kg" disabled={!!initial} />
          {initial && <p className="text-xs text-gray-400 mt-0.5">La abreviatura no se puede cambiar</p>}
        </div>
        <div>
          <label className="block text-xs font-medium text-gray-600 mb-1">Tipo</label>
          <select className="input text-sm" value={form.unitType} onChange={e => setForm(f => ({ ...f, unitType: e.target.value }))}>
            {UNIT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
        </div>
      </div>
      <div className="flex justify-end gap-2 pt-1">
        <button type="button" onClick={onCancel} className="flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 px-3 py-1.5 rounded-lg border border-gray-200 bg-white">
          <X size={12} /> Cancelar
        </button>
        <button type="submit" disabled={saving} className="flex items-center gap-1 text-xs text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 px-3 py-1.5 rounded-lg">
          {saving ? <Loader2 size={12} className="animate-spin" /> : <Check size={12} />}
          {initial ? 'Actualizar' : 'Crear'}
        </button>
      </div>
    </form>
  );
};

// ─── Main Component ─────────────────────────────────────────────────────────────
const CatalogSettings = () => {
  const { tenantId } = useAuthStore();
  const queryClient = useQueryClient();

  const [catForm, setCatForm] = useState(null);   // null | 'new' | {category obj}
  const [unitForm, setUnitForm] = useState(null);  // null | 'new' | {unit obj}

  const { data: catData, isLoading: catLoading } = useQuery({
    queryKey: ['catalog-categories', tenantId],
    queryFn: catalogService.getCategories,
    enabled: !!tenantId,
  });

  const { data: unitData, isLoading: unitLoading } = useQuery({
    queryKey: ['catalog-units', tenantId],
    queryFn: catalogService.getUnits,
    enabled: !!tenantId,
  });

  const categories = catData?.data ?? [];
  const units = unitData?.data ?? [];

  const invalidateCat  = () => queryClient.invalidateQueries({ queryKey: ['catalog-categories', tenantId] });
  const invalidateUnit = () => queryClient.invalidateQueries({ queryKey: ['catalog-units', tenantId] });

  // Categories handlers
  const handleSaveCat = async (form) => {
    if (catForm === 'new') {
      await catalogService.createCategory(form);
      toast.success('Categoria creada');
    } else {
      await catalogService.updateCategory(catForm.id, form);
      toast.success('Categoria actualizada');
    }
    setCatForm(null);
    invalidateCat();
  };

  const handleToggleCat = async (cat) => {
    await catalogService.setCategoryStatus(cat.id, !cat.isActive);
    toast.success(cat.isActive ? 'Categoria desactivada' : 'Categoria activada');
    invalidateCat();
  };

  const handleDeleteCat = async (cat) => {
    if (!window.confirm(`Eliminar categoria "${cat.name}"?`)) return;
    try {
      await catalogService.deleteCategory(cat.id);
      toast.success('Categoria eliminada');
      invalidateCat();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo eliminar');
    }
  };

  // Units handlers
  const handleSaveUnit = async (form) => {
    if (unitForm === 'new') {
      await catalogService.createUnit(form);
      toast.success('Unidad creada');
    } else {
      await catalogService.updateUnit(unitForm.id, form);
      toast.success('Unidad actualizada');
    }
    setUnitForm(null);
    invalidateUnit();
  };

  const handleToggleUnit = async (unit) => {
    await catalogService.setUnitStatus(unit.id, !unit.isActive);
    toast.success(unit.isActive ? 'Unidad desactivada' : 'Unidad activada');
    invalidateUnit();
  };

  const handleDeleteUnit = async (unit) => {
    if (!window.confirm(`Eliminar unidad "${unit.name}"?`)) return;
    try {
      await catalogService.deleteUnit(unit.id);
      toast.success('Unidad eliminada');
      invalidateUnit();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo eliminar');
    }
  };

  return (
    <div className="space-y-8">

      {/* ── CATEGORIES ── */}
      <section className="card space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="rounded-lg bg-indigo-100 p-2">
              <Tag className="h-4 w-4 text-indigo-600" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-gray-900">Categorias de inventario</h2>
              <p className="text-xs text-gray-500">Organiza tus productos por tipo</p>
            </div>
          </div>
          <button
            onClick={() => { setCatForm('new'); }}
            className="flex items-center gap-1.5 text-xs font-medium bg-indigo-600 hover:bg-indigo-700 text-white px-3 py-1.5 rounded-lg transition-colors"
          >
            <Plus size={13} /> Nueva categoria
          </button>
        </div>

        {catForm === 'new' && (
          <CategoryForm onSave={handleSaveCat} onCancel={() => setCatForm(null)} />
        )}

        {catLoading ? (
          <div className="flex items-center gap-2 text-sm text-gray-400 py-4">
            <Loader2 size={16} className="animate-spin" /> Cargando...
          </div>
        ) : categories.length === 0 ? (
          <p className="text-sm text-gray-400 italic py-2">Sin categorias. Crea la primera.</p>
        ) : (
          <div className="space-y-2">
            {categories.map(cat => (
              <div key={cat.id}>
                {catForm?.id === cat.id ? (
                  <CategoryForm initial={cat} onSave={handleSaveCat} onCancel={() => setCatForm(null)} />
                ) : (
                  <div className={`flex items-center justify-between rounded-lg border px-4 py-3 transition-colors ${cat.isActive ? 'bg-white border-gray-200' : 'bg-gray-50 border-gray-200 opacity-60'}`}>
                    <div className="flex items-center gap-3 min-w-0">
                      {cat.color && (
                        <div className="w-3 h-3 rounded-full shrink-0" style={{ backgroundColor: cat.color }} />
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-gray-900 truncate">{cat.name}</p>
                        {cat.description && <p className="text-xs text-gray-400 truncate">{cat.description}</p>}
                      </div>
                      <span className="shrink-0 text-xs text-gray-400 bg-gray-100 rounded-full px-2 py-0.5">
                        {cat.productCount} producto{cat.productCount !== 1 ? 's' : ''}
                      </span>
                    </div>
                    <div className="flex items-center gap-1 shrink-0 ml-3">
                      <button onClick={() => handleToggleCat(cat)} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors" title={cat.isActive ? 'Desactivar' : 'Activar'}>
                        {cat.isActive ? <ToggleRight size={16} className="text-green-500" /> : <ToggleLeft size={16} />}
                      </button>
                      <button onClick={() => setCatForm(cat)} className="p-1.5 rounded-lg text-gray-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors" title="Editar">
                        <Pencil size={14} />
                      </button>
                      <button onClick={() => handleDeleteCat(cat)} disabled={cat.productCount > 0} className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors disabled:opacity-30 disabled:cursor-not-allowed" title={cat.productCount > 0 ? 'Tiene productos asociados' : 'Eliminar'}>
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ))}
          </div>
        )}
      </section>

      {/* ── UNITS ── */}
      <section className="card space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="rounded-lg bg-blue-100 p-2">
              <Ruler className="h-4 w-4 text-blue-600" />
            </div>
            <div>
              <h2 className="text-base font-semibold text-gray-900">Unidades de medida</h2>
              <p className="text-xs text-gray-500">Unidades usadas en productos e insumos</p>
            </div>
          </div>
          <button
            onClick={() => setUnitForm('new')}
            className="flex items-center gap-1.5 text-xs font-medium bg-blue-600 hover:bg-blue-700 text-white px-3 py-1.5 rounded-lg transition-colors"
          >
            <Plus size={13} /> Nueva unidad
          </button>
        </div>

        {unitForm === 'new' && (
          <UnitForm onSave={handleSaveUnit} onCancel={() => setUnitForm(null)} />
        )}

        {unitLoading ? (
          <div className="flex items-center gap-2 text-sm text-gray-400 py-4">
            <Loader2 size={16} className="animate-spin" /> Cargando...
          </div>
        ) : units.length === 0 ? (
          <p className="text-sm text-gray-400 italic py-2">Sin unidades. Crea la primera.</p>
        ) : (
          <div className="space-y-2">
            {/* Group by unit_type */}
            {['quantity', 'weight', 'volume', 'length'].map(type => {
              const group = units.filter(u => u.unitType === type);
              if (!group.length) return null;
              const typeLabel = UNIT_TYPES.find(t => t.value === type)?.label ?? type;
              return (
                <div key={type}>
                  <p className="text-xs font-semibold text-gray-400 uppercase tracking-wide mb-1.5">{typeLabel}</p>
                  <div className="space-y-1.5">
                    {group.map(unit => (
                      <div key={unit.id}>
                        {unitForm?.id === unit.id ? (
                          <UnitForm initial={unit} onSave={handleSaveUnit} onCancel={() => setUnitForm(null)} />
                        ) : (
                          <div className={`flex items-center justify-between rounded-lg border px-4 py-2.5 transition-colors ${unit.isActive ? 'bg-white border-gray-200' : 'bg-gray-50 border-gray-200 opacity-60'}`}>
                            <div className="flex items-center gap-3 min-w-0">
                              <span className="text-xs font-mono font-bold text-blue-700 bg-blue-50 rounded px-2 py-0.5 shrink-0">{unit.abbreviation}</span>
                              <span className="text-sm font-medium text-gray-900 truncate">{unit.name}</span>
                              <span className="text-xs text-gray-400 shrink-0">
                                {unit.productCount} producto{unit.productCount !== 1 ? 's' : ''}
                              </span>
                            </div>
                            <div className="flex items-center gap-1 shrink-0 ml-3">
                              <button onClick={() => handleToggleUnit(unit)} className="p-1.5 rounded-lg text-gray-400 hover:text-gray-700 hover:bg-gray-100 transition-colors" title={unit.isActive ? 'Desactivar' : 'Activar'}>
                                {unit.isActive ? <ToggleRight size={16} className="text-green-500" /> : <ToggleLeft size={16} />}
                              </button>
                              <button onClick={() => setUnitForm(unit)} className="p-1.5 rounded-lg text-gray-400 hover:text-blue-600 hover:bg-blue-50 transition-colors" title="Editar">
                                <Pencil size={14} />
                              </button>
                              <button onClick={() => handleDeleteUnit(unit)} disabled={unit.productCount > 0} className="p-1.5 rounded-lg text-gray-400 hover:text-red-600 hover:bg-red-50 transition-colors disabled:opacity-30 disabled:cursor-not-allowed" title={unit.productCount > 0 ? 'Tiene productos asociados' : 'Eliminar'}>
                                <Trash2 size={14} />
                              </button>
                            </div>
                          </div>
                        )}
                      </div>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
};

export default CatalogSettings;
