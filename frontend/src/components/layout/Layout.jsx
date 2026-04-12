/**
 * Layout Principal
 * �Qu� es? Estructura base de la aplicacion
 * �Para qu�? Header, sidebar y contenido principal
 */

import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Bell,
  Bot,
  ChevronsLeft,
  ChevronsRight,
  LayoutDashboard,
  Landmark,
  LogOut,
  Menu,
  Package,
  Palette,
  Settings,
  ShoppingCart,
  Store,
  Percent,
  Truck,
  Users,
  X,
} from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import companyService from '../../services/companyService';
import inventoryService from '../../services/inventoryService';
import useAuthStore from '../../stores/authStore';
import useUiStore from '../../stores/uiStore';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';

const Layout = ({ children }) => {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { user, logout, branchId } = useAuthStore();
  const collapsed = useUiStore((state) => state.sidebarCollapsed);
  const setSidebarCollapsed = useUiStore((state) => state.setSidebarCollapsed);
  const companyName = useUiStore((state) => state.companyName);
  const companyLogoUrl = useUiStore((state) => state.companyLogoUrl);
  const setBranding = useUiStore((state) => state.setBranding);
  const setTheme = useUiStore((state) => state.setTheme);
  const navigate = useNavigate();
  const location = useLocation();

  const { data: settingsData } = useQuery({
    queryKey: ['company-settings'],
    queryFn: () => companyService.getSettings(),
    enabled: !!user,
    staleTime: 5 * 60 * 1000,
  });

  const { data: alertsData } = useQuery({
    queryKey: ['alerts', branchId],
    queryFn: () => inventoryService.getAlerts(branchId),
    enabled: !!branchId && !!user,
    refetchInterval: 30000,
  });

  useEffect(() => {
    const settings = settingsData?.data;
    if (!settings) return;

    setBranding({
      companyName: settings.displayName || settings.name || 'Walos',
      companyLogoUrl: settings.logoUrl || null,
    });

    if (settings.themePreference) {
      setTheme(settings.themePreference);
    }
  }, [setBranding, setTheme, settingsData]);

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  const menuItems = [
    { name: 'Dashboard', path: '/', icon: LayoutDashboard },
    { name: 'Asistente IA', path: '/ai-assistant', icon: Bot },
    { name: 'Inventario', path: '/inventory', icon: Package },
    { name: 'Ventas', path: '/sales', icon: ShoppingCart },
    { name: 'Finanzas', path: '/finance', icon: Landmark },
    { name: 'Proveedores', path: '/suppliers', icon: Truck },
    { name: 'Usuarios', path: '/users', icon: Users },
    {
      name: 'Configuracion',
      path: '/settings',
      icon: Settings,
      children: [
        { name: 'Branding', path: '/settings/branding', icon: Store },
        { name: 'Temas', path: '/settings/themes', icon: Palette },
        { name: 'Descuentos', path: '/settings/discounts', icon: Percent },
      ],
    },
  ];

  const displayName = companyName || 'Walos';
  const logoSrc = companyLogoUrl ? `${API_BASE}${companyLogoUrl}` : null;
  const walosLogoSrc = '/walos-logo.png';
  const alertsCount = alertsData?.count || alertsData?.data?.length || 0;
  const userDisplayName = user?.first_name
    ? `${user.first_name} ${user?.last_name || ''}`.trim()
    : user?.name || 'Usuario';
  const userInitial = userDisplayName.charAt(0).toUpperCase();

  const renderMenuLink = (item, child = false) => {
    const Icon = item.icon;
    const isRoot = item.path === '/';
    const isActive = isRoot ? location.pathname === '/' : location.pathname.startsWith(item.path);

    return (
      <Link
        key={item.path}
        to={item.path}
        title={collapsed ? item.name : undefined}
        className={`group relative flex items-center gap-3 rounded-lg px-3 transition-colors ${
          child ? 'ml-3 border-l border-gray-200 py-2.5 pl-4 text-sm' : 'py-3'
        } ${
          isActive
            ? 'bg-primary-50 font-semibold text-primary-600'
            : 'text-gray-700 hover:bg-primary-50 hover:text-primary-600'
        }`}
        onClick={() => setSidebarOpen(false)}
      >
        <Icon className={`${child ? 'h-4 w-4' : 'h-5 w-5'} flex-shrink-0`} />
        <span
          className={`whitespace-nowrap font-medium transition-all duration-300 ${
            collapsed ? 'lg:w-0 lg:overflow-hidden lg:opacity-0' : 'opacity-100'
          }`}
        >
          {item.name}
        </span>

        <span
          className={`
            pointer-events-none absolute left-full z-[60] ml-2 hidden whitespace-nowrap rounded-md bg-gray-900 px-2.5 py-1.5
            text-xs font-medium text-white shadow-lg transition-opacity duration-200
            ${collapsed ? 'lg:block opacity-0 group-hover:opacity-100' : ''}
          `}
        >
          {item.name}
        </span>
      </Link>
    );
  };

  return (
    <div className="flex h-full bg-gray-50">
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 transition-opacity duration-300 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      <aside
        style={{ width: sidebarOpen ? 256 : (collapsed ? 68 : 256) }}
        className={`
          fixed inset-y-0 left-0 z-50 flex flex-col overflow-hidden bg-white shadow-lg
          transition-[width] duration-300 ease-in-out
          lg:relative lg:translate-x-0
          ${sidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
        `}
      >
        <div className="flex h-16 items-center gap-3 border-b px-3">
          <div
            className={`flex min-w-0 items-center gap-3 overflow-hidden transition-all duration-300 ${
              collapsed ? 'w-0 opacity-0' : 'flex-1 opacity-100'
            }`}
          >
            <img
              src={walosLogoSrc}
              alt="Walos"
              className="h-10 w-10 flex-shrink-0 rounded-lg border border-gray-200 bg-white object-contain p-1"
            />
          </div>

          {collapsed ? (
            <button
              onClick={() => setSidebarCollapsed(false)}
              className="mx-auto hidden flex-shrink-0 items-center justify-center lg:flex"
              title="Abrir barra lateral"
            >
              <img
                src={walosLogoSrc}
                alt="Walos"
                className="h-10 w-10 rounded-lg object-contain"
              />
            </button>
          ) : (
            <button
              onClick={() => setSidebarCollapsed(true)}
              className="ml-auto hidden flex-shrink-0 items-center justify-center rounded-lg border border-gray-200 p-1.5 text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700 lg:flex"
              title="Cerrar barra lateral"
            >
              <ChevronsLeft className="h-4 w-4" />
            </button>
          )}

          <button onClick={() => setSidebarOpen(false)} className="ml-auto lg:hidden">
            <X className="h-6 w-6" />
          </button>
        </div>

        <nav className="flex-1 space-y-1 overflow-x-hidden overflow-y-auto p-2">
          {menuItems.map((item) => {
            const active = item.path === '/' ? location.pathname === '/' : location.pathname.startsWith(item.path);
            return (
              <div key={item.path} className="space-y-1">
                {renderMenuLink(item)}
                {!collapsed && active && item.children?.map((child) => renderMenuLink(child, true))}
              </div>
            );
          })}
        </nav>

        <div className="border-t p-2">
          <div className="flex items-center gap-3 rounded-lg bg-gray-50 p-2">
            {logoSrc ? (
              <img
                src={logoSrc}
                alt={displayName}
                className="h-10 w-10 flex-shrink-0 rounded-full border border-gray-200 object-cover"
                title={collapsed ? displayName : undefined}
              />
            ) : (
              <div
                className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-primary-500 font-semibold text-white"
                title={collapsed ? userDisplayName : undefined}
              >
                {userInitial}
              </div>
            )}
            <div
              className={`overflow-hidden transition-all duration-300 ${
                collapsed ? 'lg:w-0 lg:opacity-0' : 'flex-1 opacity-100'
              }`}
            >
              <p className="truncate text-sm font-medium">{userDisplayName}</p>
              <p className="truncate text-xs text-gray-500">{user?.email}</p>
            </div>
          </div>
        </div>

        <div className="border-t px-4 py-3">
          <div className="flex justify-center">
            <img
              src={walosLogoSrc}
              alt="Walos"
              className={`object-contain transition-all duration-300 ${
                collapsed ? 'h-6 w-6' : 'h-8 w-auto'
              }`}
            />
          </div>
        </div>
      </aside>

      <div className="flex flex-1 flex-col overflow-hidden">
        <header className="flex h-16 items-center justify-between border-b bg-white px-6 shadow-sm">
          <div className="flex min-w-0 items-center gap-3">
            <button onClick={() => setSidebarOpen(true)} className="lg:hidden">
              <Menu className="h-6 w-6" />
            </button>

            <div className="flex min-w-0 items-center gap-3">
              {logoSrc ? (
                <img
                  src={logoSrc}
                  alt={displayName}
                  className="h-9 w-9 rounded-xl border border-gray-200 bg-white object-contain p-1"
                />
              ) : (
                <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary-100 text-sm font-bold text-primary-700">
                  {displayName.charAt(0).toUpperCase()}
                </div>
              )}

              <div className="min-w-0">
                <p className="truncate text-sm font-semibold text-gray-900">{displayName}</p>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <button
              onClick={() => navigate('/alerts')}
              className="relative rounded-lg p-2 transition-colors hover:bg-gray-100"
              title="Ver alertas"
            >
              <Bell className="h-5 w-5" />
              {alertsCount > 0 && (
                <span className="absolute -right-1 -top-1 inline-flex min-h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1.5 text-[10px] font-bold text-white">
                  {alertsCount > 99 ? '99+' : alertsCount}
                </span>
              )}
            </button>

            <button
              onClick={handleLogout}
              className="flex items-center gap-2 rounded-lg px-3 py-2 transition-colors hover:bg-gray-100"
            >
              <LogOut className="h-5 w-5" />
              <span className="hidden sm:inline">Salir</span>
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
    </div>
  );
};

export default Layout;







