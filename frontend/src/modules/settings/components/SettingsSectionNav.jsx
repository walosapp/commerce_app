import { Palette, Store, Percent, LayoutList } from 'lucide-react';
import { Link } from 'react-router-dom';

export const sections = [
  { key: 'branding',  label: 'Branding',    path: '/settings/branding',  icon: Store },
  { key: 'themes',    label: 'Temas',        path: '/settings/themes',    icon: Palette },
  { key: 'discounts', label: 'Descuentos',   path: '/settings/discounts', icon: Percent },
  { key: 'catalog',   label: 'Catalogo',     path: '/settings/catalog',   icon: LayoutList },
];

const SettingsSectionNav = ({ activeSection }) => (
  <div className="flex border-b bg-white px-6 flex-shrink-0">
    {sections.map(({ key, label, icon: Icon }) => (
      <Link
        key={key}
        to={`/settings/${key === 'branding' ? 'branding' : key}`}
        className={`flex items-center gap-2 px-4 py-3 text-sm font-medium border-b-2 transition-colors ${
          activeSection === key
            ? 'border-primary-600 text-primary-600'
            : 'border-transparent text-gray-500 hover:text-gray-700'
        }`}
      >
        <Icon size={16} /> {label}
      </Link>
    ))}
  </div>
);

export default SettingsSectionNav;
