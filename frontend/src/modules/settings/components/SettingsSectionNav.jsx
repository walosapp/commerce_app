/**
 * Navegacion de Configuracion
 * ¿Qué es? Navegacion interna del modulo de configuracion
 * ¿Para qué? Moverse entre branding, temas y descuentos con consistencia visual
 */

import { Palette, Store, Percent } from 'lucide-react';
import { Link } from 'react-router-dom';

const sections = [
  {
    key: 'branding',
    label: 'Branding',
    description: 'Logo, nombre y datos visibles del negocio',
    path: '/settings/branding',
    icon: Store,
  },
  {
    key: 'themes',
    label: 'Temas',
    description: 'Look general y apariencia del sistema',
    path: '/settings/themes',
    icon: Palette,
  },
  {
    key: 'discounts',
    label: 'Descuentos',
    description: 'Reglas operativas para facturacion',
    path: '/settings/discounts',
    icon: Percent,
  },
];

const SettingsSectionNav = ({ activeSection }) => {
  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {sections.map((section) => {
        const Icon = section.icon;
        const active = activeSection === section.key;

        return (
          <Link
            key={section.key}
            to={section.path}
            className={`rounded-xl border p-4 transition-all ${
              active
                ? 'border-primary-300 bg-primary-50 shadow-sm'
                : 'border-gray-200 bg-white hover:border-gray-300 hover:shadow-sm'
            }`}
          >
            <div className="flex items-start gap-3">
              <div className={`rounded-lg p-2 ${active ? 'bg-primary-100 text-primary-700' : 'bg-gray-100 text-gray-600'}`}>
                <Icon className="h-4 w-4" />
              </div>
              <div>
                <p className="text-sm font-semibold text-gray-900">{section.label}</p>
                <p className="mt-1 text-xs text-gray-500">{section.description}</p>
              </div>
            </div>
          </Link>
        );
      })}
    </div>
  );
};

export default SettingsSectionNav;

