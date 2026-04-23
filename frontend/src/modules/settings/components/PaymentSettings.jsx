/**
 * PaymentSettings
 * Gestión de métodos de pago registrados (TC/PSE via Wompi).
 */

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CreditCard, Trash2, Star, Plus, Building2 } from 'lucide-react';
import toast from 'react-hot-toast';
import platformService from '../../../services/platformService';

const PaymentSettings = () => {
  const queryClient = useQueryClient();
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState({ type: 'card', holderName: '', last4: '', providerToken: '', isDefault: false });

  const { data: res, isLoading } = useQuery({
    queryKey: ['payment-methods'],
    queryFn: platformService.getPaymentMethods,
  });

  const methods = res?.data ?? [];

  const addMutation = useMutation({
    mutationFn: () => platformService.addPaymentMethod(form),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payment-methods'] });
      setShowForm(false);
      setForm({ type: 'card', holderName: '', last4: '', providerToken: '', isDefault: false });
      toast.success('Método de pago agregado');
    },
    onError: () => toast.error('Error al agregar método de pago'),
  });

  const setDefaultMutation = useMutation({
    mutationFn: (id) => platformService.setDefaultPaymentMethod(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payment-methods'] });
      toast.success('Método por defecto actualizado');
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (id) => platformService.deletePaymentMethod(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payment-methods'] });
      toast.success('Método de pago eliminado');
    },
    onError: () => toast.error('Error al eliminar'),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold text-gray-900">Métodos de pago</h2>
        <button
          onClick={() => setShowForm((v) => !v)}
          className="flex items-center gap-2 rounded-lg bg-primary-600 px-3 py-2 text-sm font-medium text-white hover:bg-primary-700 transition-colors"
        >
          <Plus className="h-4 w-4" />
          Agregar
        </button>
      </div>

      {showForm && (
        <div className="rounded-xl border border-gray-200 bg-white p-4 space-y-3">
          <h3 className="text-sm font-medium text-gray-900">Nuevo método de pago</h3>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Tipo</label>
              <select value={form.type} onChange={(e) => setForm({ ...form, type: e.target.value })} className="input">
                <option value="card">Tarjeta de crédito/débito</option>
                <option value="pse">PSE</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Titular</label>
              <input value={form.holderName} onChange={(e) => setForm({ ...form, holderName: e.target.value })} className="input" placeholder="Nombre del titular" />
            </div>
            {form.type === 'card' && (
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Últimos 4 dígitos</label>
                <input value={form.last4} onChange={(e) => setForm({ ...form, last4: e.target.value.slice(0, 4) })} className="input" placeholder="1234" maxLength={4} />
              </div>
            )}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Token Wompi</label>
              <input value={form.providerToken} onChange={(e) => setForm({ ...form, providerToken: e.target.value })} className="input" placeholder="tok_..." />
            </div>
          </div>
          <label className="flex items-center gap-2 text-sm text-gray-700 cursor-pointer">
            <input type="checkbox" checked={form.isDefault} onChange={(e) => setForm({ ...form, isDefault: e.target.checked })} className="rounded" />
            Establecer como método por defecto
          </label>
          <div className="flex gap-2">
            <button onClick={() => addMutation.mutate()} disabled={addMutation.isPending || !form.holderName || !form.providerToken} className="rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors">
              {addMutation.isPending ? 'Guardando...' : 'Guardar'}
            </button>
            <button onClick={() => setShowForm(false)} className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 transition-colors">
              Cancelar
            </button>
          </div>
        </div>
      )}

      {isLoading ? (
        <p className="text-sm text-gray-400">Cargando...</p>
      ) : methods.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-xl border border-dashed border-gray-300 py-12 text-gray-400">
          <CreditCard className="h-10 w-10 mb-2" />
          <p className="text-sm">No tienes métodos de pago registrados</p>
        </div>
      ) : (
        <div className="space-y-3">
          {methods.map((m) => (
            <div key={m.id} className={`flex items-center justify-between rounded-xl border p-4 bg-white ${m.isDefault ? 'border-primary-300 bg-primary-50' : 'border-gray-200'}`}>
              <div className="flex items-center gap-3">
                {m.type === 'card'
                  ? <CreditCard className="h-5 w-5 text-gray-500" />
                  : <Building2 className="h-5 w-5 text-gray-500" />}
                <div>
                  <p className="text-sm font-medium text-gray-900">
                    {m.holderName}
                    {m.last4 && <span className="text-gray-400 ml-1">···· {m.last4}</span>}
                    {m.bankName && <span className="text-gray-500 ml-1">({m.bankName})</span>}
                  </p>
                  <p className="text-xs text-gray-400 capitalize">{m.type === 'card' ? 'Tarjeta' : 'PSE'} · {m.provider}</p>
                </div>
              </div>
              <div className="flex items-center gap-1">
                {m.isDefault ? (
                  <span className="flex items-center gap-1 text-xs text-primary-600 font-medium mr-2">
                    <Star className="h-3 w-3 fill-primary-600" /> Por defecto
                  </span>
                ) : (
                  <button onClick={() => setDefaultMutation.mutate(m.id)} className="rounded p-1.5 text-gray-400 hover:bg-yellow-50 hover:text-yellow-600 transition-colors" title="Establecer como defecto">
                    <Star className="h-4 w-4" />
                  </button>
                )}
                <button onClick={() => deleteMutation.mutate(m.id)} className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-500 transition-colors" title="Eliminar">
                  <Trash2 className="h-4 w-4" />
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default PaymentSettings;
