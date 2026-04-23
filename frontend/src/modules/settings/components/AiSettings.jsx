/**
 * AiSettings
 * Configuración de API key de IA y visualización de consumo de tokens.
 */

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Cpu, Eye, EyeOff, Save, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import platformService from '../../../services/platformService';
import { formatCurrency } from '../../../utils/formatCurrency';

const PROVIDERS = [
  { value: 'openai',    label: 'OpenAI (GPT)' },
  { value: 'gemini',    label: 'Google Gemini' },
  { value: 'anthropic', label: 'Anthropic Claude' },
];

const AiSettings = () => {
  const queryClient = useQueryClient();
  const [showKey, setShowKey] = useState(false);
  const [apiKey, setApiKey] = useState('');
  const [provider, setProvider] = useState('openai');

  const { data: res, isLoading } = useQuery({
    queryKey: ['ai-usage'],
    queryFn: platformService.getAiUsage,
    onSuccess: (data) => {
      if (data?.data?.aiProvider) setProvider(data.data.aiProvider);
    },
  });

  const settings = res?.data;

  const updateMutation = useMutation({
    mutationFn: () => platformService.updateAiKey({ provider, apiKey: apiKey || null, managed: !apiKey }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['ai-usage'] });
      setApiKey('');
      toast.success('Configuración de IA actualizada');
    },
    onError: () => toast.error('Error al actualizar la configuración'),
  });

  if (isLoading) return <div className="text-center py-8 text-gray-400">Cargando...</div>;

  return (
    <div className="space-y-6">
      <div className="rounded-xl border border-gray-200 bg-white p-5">
        <div className="flex items-center gap-3 mb-4">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-purple-100">
            <Cpu className="h-5 w-5 text-purple-600" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-gray-900">API Key de IA</h2>
            <p className="text-xs text-gray-500">
              {settings?.aiKeyManaged
                ? 'Tu plan incluye acceso gestionado a la IA de Walos'
                : 'Usando API key propia'}
            </p>
          </div>
        </div>

        {settings?.aiKeyManaged ? (
          <div className="rounded-lg bg-blue-50 border border-blue-200 p-3 text-sm text-blue-700">
            Tu API key está gestionada por Walos. No necesitas configurar nada.
          </div>
        ) : (
          <div className="space-y-3">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Proveedor</label>
              <select
                value={provider}
                onChange={(e) => setProvider(e.target.value)}
                className="input"
              >
                {PROVIDERS.map((p) => (
                  <option key={p.value} value={p.value}>{p.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">
                API Key {settings?.hasCustomKey && <span className="text-xs text-gray-400">(ya configurada — deja vacío para mantener)</span>}
              </label>
              <div className="relative">
                <input
                  type={showKey ? 'text' : 'password'}
                  value={apiKey}
                  onChange={(e) => setApiKey(e.target.value)}
                  placeholder={settings?.hasCustomKey ? '••••••••••••••••' : 'sk-...'}
                  className="input pr-10"
                />
                <button
                  type="button"
                  onClick={() => setShowKey((v) => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showKey ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>
            <button
              onClick={() => updateMutation.mutate()}
              disabled={updateMutation.isPending}
              className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2 text-sm font-medium text-white hover:bg-primary-700 disabled:opacity-50 transition-colors"
            >
              <Save className="h-4 w-4" />
              {updateMutation.isPending ? 'Guardando...' : 'Guardar configuración'}
            </button>
          </div>
        )}
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5">
        <h2 className="text-base font-semibold text-gray-900 mb-4">Consumo de tokens</h2>
        <div className="grid grid-cols-2 gap-4">
          <div className="rounded-lg bg-gray-50 p-4">
            <p className="text-xs text-gray-500 mb-1">Tokens usados</p>
            <p className="text-2xl font-bold text-gray-900">{(settings?.aiTokensUsed ?? 0).toLocaleString()}</p>
          </div>
          <div className="rounded-lg bg-gray-50 p-4">
            <p className="text-xs text-gray-500 mb-1">Costo estimado</p>
            <p className="text-2xl font-bold text-primary-600">{formatCurrency(settings?.aiEstimatedCost ?? 0)}</p>
          </div>
        </div>
        {settings?.aiTokensResetAt && (
          <p className="text-xs text-gray-400 mt-3 flex items-center gap-1">
            <RefreshCw className="h-3 w-3" />
            Último reset: {new Date(settings.aiTokensResetAt).toLocaleDateString()}
          </p>
        )}
      </div>
    </div>
  );
};

export default AiSettings;
