import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Puzzle, X } from 'lucide-react';
import toast from 'react-hot-toast';
import platformService from '../../../services/platformService';

const CompanyFeaturesPanel = ({ companyId, companyName, onClose }) => {
  const queryClient = useQueryClient();
  const queryKey = ['admin-company-features', companyId];

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => platformService.getAdminCompanyFeatures(companyId),
    enabled: Boolean(companyId),
  });

  const mutation = useMutation({
    mutationFn: ({ featureCode, isEnabled }) => (
      platformService.updateAdminCompanyFeature(companyId, featureCode, isEnabled)
    ),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey });
      queryClient.invalidateQueries({ queryKey: ['company-features', companyId] });
      toast.success('Módulo actualizado');
    },
    onError: () => toast.error('No se pudo actualizar el módulo'),
  });

  const features = [...(data?.data ?? [])].sort(
    (left, right) => (left.displayOrder ?? 0) - (right.displayOrder ?? 0),
  );

  return (
    <div className="fixed inset-0 z-50 bg-black/40 flex justify-end" role="dialog" aria-modal="true">
      <section className="w-full max-w-lg h-full bg-white shadow-xl flex flex-col">
        <header className="p-5 border-b flex items-start justify-between gap-3">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-10 h-10 rounded-lg bg-indigo-100 flex items-center justify-center shrink-0">
              <Puzzle size={20} className="text-indigo-600" />
            </div>
            <div className="min-w-0">
              <h2 className="font-semibold text-gray-900 truncate">Módulos de {companyName}</h2>
              <p className="text-sm text-gray-500">Habilitá solo las funciones contratadas.</p>
            </div>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar" className="p-2 text-gray-500 hover:bg-gray-100 rounded-lg">
            <X size={18} />
          </button>
        </header>

        <div className="p-5 overflow-y-auto flex-1">
          {isLoading && (
            <div className="py-12 flex justify-center text-gray-400">
              <Loader2 className="animate-spin" aria-label="Cargando módulos" />
            </div>
          )}

          {isError && <p className="text-sm text-red-600">No se pudieron cargar los módulos.</p>}

          {!isLoading && !isError && (
            <div className="divide-y rounded-xl border">
              {features.map(feature => {
                const isMandatory = feature.isMandatory || feature.code === 'dashboard';
                const isPending = mutation.isPending && mutation.variables?.featureCode === feature.code;

                return (
                  <label key={feature.code} className="flex items-center justify-between gap-4 p-4">
                    <span className="min-w-0">
                      <span className="block text-sm font-medium text-gray-900">{feature.name}</span>
                      {feature.description && (
                        <span className="block text-xs text-gray-500 mt-0.5">{feature.description}</span>
                      )}
                      {isMandatory && <span className="block text-xs text-indigo-600 mt-1">Obligatorio</span>}
                    </span>
                    <input
                      type="checkbox"
                      aria-label={feature.name}
                      checked={isMandatory || feature.isEnabled}
                      disabled={isMandatory || isPending}
                      onChange={event => mutation.mutate({
                        featureCode: feature.code,
                        isEnabled: event.target.checked,
                      })}
                      className="h-5 w-5 accent-indigo-600 shrink-0"
                    />
                  </label>
                );
              })}
            </div>
          )}
        </div>
      </section>
    </div>
  );
};

export default CompanyFeaturesPanel;
