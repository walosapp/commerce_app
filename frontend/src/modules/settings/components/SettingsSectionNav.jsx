import { Palette, Store, Percent, LayoutList, CreditCard, Cpu, FileText } from 'lucide-react';
import { Link } from 'react-router-dom';
import useAuthStore from '../../../stores/authStore';

const CATALOG_ROLES = ['dev', 'super_admin', 'admin', 'manager'];

export const sections = [
  { key: 'branding',  label: 'Branding',    path: '/settings/branding',  icon: Store },
  { key: 'themes',    label: 'Temas',        path: '/settings/themes',    icon: Palette },
  { key: 'discounts', label: 'Descuentos',   path: '/settings/discounts', icon: Percent },
  { key: 'catalog',   label: 'Catalogo',     path: '/settings/catalog',   icon: LayoutList, roles: CATALOG_ROLES },
  { key: 'plan',      label: 'Mi Plan',      path: '/settings/plan',      icon: FileText },
  { key: 'ai',        label: 'IA',           path: '/settings/ai',        icon: Cpu },
  { key: 'payments',  label: 'Pagos',        path: '/settings/payments',  icon: CreditCard },
];

const SettingsSectionNav = ({ activeSection }) => {
  const { user } = useAuthStore();
  const visibleSections = sections.filter(s => !s.roles || s.roles.includes(user?.role));

  return (
    <div className="flex border-b bg-white px-6 flex-shrink-0">
      {visibleSections.map(({ key, label, icon: Icon }) => (
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
};

export default SettingsSectionNav;
