import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { PlusCircle, Trash2, Loader2, ChefHat, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import recipeService from '../../../services/recipeService';
import { inventoryService } from '../../../services/inventoryService';
import useAuthStore from '../../../stores/authStore';

const RecipeManager = ({ productId, productName }) => {
  const { branchId, tenantId } = useAuthStore();
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [form, setForm] = useState({ ingredientId: '', quantity: '', unitId: '' });
  const [selectedName, setSelectedName] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const [saving, setSaving] = useState(false);

  const { data: recipeData, isLoading: loadingRecipe } = useQuery({
    queryKey: ['recipe', productId],
    queryFn: () => recipeService.getByProduct(productId),
    enabled: !!productId,
  });

  const { data: stockData } = useQuery({
    queryKey: ['stock', branchId, tenantId],
    queryFn: () => inventoryService.getStock(branchId),
    enabled: !!branchId,
  });

  const recipe = recipeData?.data ?? [];
  const existingIds = recipe.map(r => r.ingredientId);

  // Only show supply-type products as ingredients (or all if no supply exists)
  const supplies = (stockData?.data ?? []).filter(p => {
    const isSupply = p.productType === 'supply' || p.productType === 'simple';
    const notAlreadyAdded = !existingIds.includes(p.productId);
    const notSelf = p.productId !== productId;
    const matchesSearch = !search.trim() || p.productName?.toLowerCase().includes(search.toLowerCase());
    return isSupply && notAlreadyAdded && notSelf && matchesSearch;
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['recipe', productId] });

  const selectIngredient = (p) => {
    setForm(f => ({ ...f, ingredientId: p.productId }));
    setSelectedName(p.productName);
    setSearch(p.productName);
    setShowDropdown(false);
  };

  const handleAdd = async () => {
    if (!form.ingredientId) { toast.error('Selecciona un insumo'); return; }
    if (!form.quantity || Number(form.quantity) <= 0) { toast.error('Ingresa una cantidad válida'); return; }
    setSaving(true);
    try {
      await recipeService.upsertIngredient(productId, {
        ingredientId: Number(form.ingredientId),
        quantity: Number(form.quantity),
        unitId: form.unitId ? Number(form.unitId) : undefined,
      });
      toast.success(`${selectedName} agregado a la receta`);
      setForm({ ingredientId: '', quantity: '', unitId: '' });
      setSearch('');
      setSelectedName('');
      invalidate();
    } catch {
      toast.error('Error al agregar ingrediente');
    } finally {
      setSaving(false);
    }
  };

  const handleRemove = async (ingredientId, name) => {
    try {
      await recipeService.removeIngredient(productId, ingredientId);
      toast.success(`${name} eliminado de la receta`);
      invalidate();
    } catch {
      toast.error('Error al eliminar ingrediente');
    }
  };

  return (
    <div className="rounded-xl border border-indigo-200 bg-indigo-50/40 p-4 space-y-4">
      <div className="flex items-center gap-2">
        <ChefHat size={18} className="text-indigo-600" />
        <h3 className="text-sm font-semibold text-indigo-800">Receta / Ingredientes</h3>
        <span className="text-xs text-indigo-500 font-normal">
          — Al vender, se descontarán estos insumos del stock
        </span>
      </div>

      {/* Current ingredients */}
      {loadingRecipe ? (
        <div className="flex items-center gap-2 text-sm text-gray-400">
          <Loader2 size={14} className="animate-spin" /> Cargando receta...
        </div>
      ) : recipe.length === 0 ? (
        <p className="text-xs text-indigo-400 italic">Sin ingredientes. Agrega los insumos que usa este producto.</p>
      ) : (
        <div className="space-y-1.5">
          {recipe.map(r => (
            <div key={r.ingredientId} className="flex items-center justify-between bg-white rounded-lg px-3 py-2 shadow-sm border border-indigo-100">
              <div className="flex items-center gap-2 min-w-0">
                <div className="w-2 h-2 rounded-full bg-indigo-400 shrink-0" />
                <span className="text-sm font-medium text-gray-800 truncate">{r.ingredientName}</span>
                <span className="text-xs text-gray-500 shrink-0">
                  {r.quantity} {r.unitAbbreviation ?? 'und'}
                </span>
              </div>
              <button
                onClick={() => handleRemove(r.ingredientId, r.ingredientName)}
                className="text-red-400 hover:text-red-600 transition-colors shrink-0 ml-2"
              >
                <Trash2 size={14} />
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Add ingredient */}
      <div className="border-t border-indigo-200 pt-3 space-y-2">
        <p className="text-xs font-medium text-indigo-700">Agregar ingrediente:</p>
        <div className="relative">
          <Search size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            placeholder="Buscar insumo..."
            value={search}
            onChange={e => { setSearch(e.target.value); setShowDropdown(true); setForm(f => ({ ...f, ingredientId: '' })); setSelectedName(''); }}
            onFocus={() => setShowDropdown(true)}
            className="w-full pl-7 pr-3 py-1.5 border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
          {showDropdown && search && supplies.length > 0 && (
            <div className="absolute z-10 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg max-h-40 overflow-y-auto">
              {supplies.slice(0, 20).map(p => (
                <button
                  key={p.productId}
                  onMouseDown={() => selectIngredient(p)}
                  className="w-full text-left px-3 py-2 text-sm hover:bg-indigo-50 border-b border-gray-100 last:border-b-0"
                >
                  <span className="font-medium">{p.productName}</span>
                  <span className="ml-2 text-xs text-gray-400">stock: {p.quantity}</span>
                </button>
              ))}
            </div>
          )}
        </div>
        <div className="flex gap-2">
          <input
            type="number"
            placeholder="Cantidad"
            min="0.001"
            step="0.001"
            value={form.quantity}
            onChange={e => setForm(f => ({ ...f, quantity: e.target.value }))}
            className="w-28 border border-gray-300 rounded-lg px-2 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-indigo-400"
          />
          <button
            onClick={handleAdd}
            disabled={saving || !form.ingredientId || !form.quantity}
            className="flex items-center gap-1.5 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white text-xs font-medium px-3 py-1.5 rounded-lg transition-colors"
          >
            {saving ? <Loader2 size={12} className="animate-spin" /> : <PlusCircle size={13} />}
            Agregar
          </button>
        </div>
      </div>
    </div>
  );
};

export default RecipeManager;
