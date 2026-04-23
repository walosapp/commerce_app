/**
 * Selector de Temas
 * ¿Qué es? Galería de temas visuales disponibles
 * ¿Para qué? Permitir cambiar rápidamente la identidad cromática de la app
 */

import { Check } from 'lucide-react';

const THEMES = [
  {
    id: 'light',
    name: 'Claro',
    description: 'Limpio y luminoso para operación diaria.',
    preview: ['bg-white', 'bg-blue-100', 'bg-gray-100'],
  },
  {
    id: 'dark',
    name: 'Oscuro',
    description: 'Superficies profundas con alto contraste.',
    preview: ['bg-slate-900', 'bg-slate-700', 'bg-cyan-400'],
  },
  {
    id: 'grayscale',
    name: 'Escala de grises',
    description: 'Monocromático y sobrio.',
    preview: ['bg-zinc-50', 'bg-zinc-400', 'bg-zinc-800'],
  },
  {
    id: 'neon',
    name: 'Neón',
    description: 'Oscuro con acentos intensos y vibrantes.',
    preview: ['bg-slate-950', 'bg-fuchsia-500', 'bg-cyan-400'],
  },
  {
    id: 'pink',
    name: 'Pink',
    description: 'Cálido, expresivo y moderno.',
    preview: ['bg-rose-50', 'bg-rose-300', 'bg-pink-600'],
  },
  {
    id: 'purple',
    name: 'Morado',
    description: 'Profundo y elegante con tono premium.',
    preview: ['bg-violet-50', 'bg-violet-300', 'bg-violet-700'],
  },
  {
    id: 'glass',
    name: 'Glass iOS',
    description: 'Efecto glassmorphism con gradiente purpura.',
    preview: ['bg-purple-400', 'bg-blue-300', 'bg-fuchsia-300'],
    isGlass: true,
  },];

const ThemeSelector = ({ selectedTheme, onSelect }) => {
  return (
    <section className="card space-y-6">
      <div>
        <h2 className="text-lg font-bold text-gray-900">Temas visuales</h2>
        <p className="mt-1 text-sm text-gray-500">
          Cambia la personalidad de toda la interfaz y revisa el resultado al instante.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {THEMES.map((theme) => {
          const isActive = selectedTheme === theme.id;

          return (
            <button
              key={theme.id}
              type="button"
              onClick={() => onSelect(theme.id)}
              className={`relative overflow-hidden rounded-xl border p-4 text-left transition-all duration-200 ${
                isActive
                  ? 'border-primary-500 ring-2 ring-primary-200 shadow-md'
                  : 'border-gray-200 hover:border-gray-300 hover:shadow-sm'
              }`}
            >
              {isActive && (
                <span className="absolute right-3 top-3 z-10 flex h-6 w-6 items-center justify-center rounded-full bg-primary-600 text-white shadow-sm">
                  <Check className="h-3.5 w-3.5" />
                </span>
              )}

              {theme.isGlass ? (
                <div className="mb-4 h-12 rounded-lg overflow-hidden relative" style={{background: 'linear-gradient(135deg,#667eea,#764ba2,#f093fb)'}}>
                  <div className="absolute inset-0 flex gap-1.5 p-1.5">
                    {[0,1,2].map(i => (
                      <div key={i} className="flex-1 rounded-md" style={{background:'rgba(255,255,255,0.45)',backdropFilter:'blur(8px)',border:'1px solid rgba(255,255,255,0.6)'}} />
                    ))}
                  </div>
                </div>
              ) : (
                <div className="mb-4 flex gap-2">
                  {theme.preview.map((colorClass) => (
                    <span
                      key={`${theme.id}-${colorClass}`}
                      className={`h-12 flex-1 rounded-lg border border-black/5 ${colorClass}`}
                    />
                  ))}
                </div>
              )}

              <p className="text-sm font-semibold text-gray-900">{theme.name}</p>
              <p className="mt-1 text-sm text-gray-500">{theme.description}</p>
            </button>
          );
        })}
      </div>
    </section>
  );
};

export default ThemeSelector;


