import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import Layout from '../../components/layout/Layout';
import { canRoleAccessFeature } from '../../config/companyFeatures';

const { featureState, authState, queryConfigs } = vi.hoisted(() => ({
  featureState: {
    canAccess: vi.fn(),
    isPlatformOnly: false,
    isReady: true,
  },
  authState: {
    user: { role: 'manager', isPlatformAdmin: false, email: 'manager@walos.app' },
    logout: vi.fn(),
    branchId: 7,
    tenantId: 25,
  },
  queryConfigs: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn(), refetchQueries: vi.fn() }),
  useQuery: (config) => {
    queryConfigs.push(config);
    if (config.queryKey[0] === 'company-settings') return { data: { data: { name: 'Comercio' } } };
    return { data: { data: [] } };
  },
}));

vi.mock('../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));
vi.mock('../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../stores/uiStore', () => ({
  default: (selector) => selector({
    sidebarCollapsed: false,
    setSidebarCollapsed: vi.fn(),
    companyName: 'Comercio',
    companyLogoUrl: null,
    brandingTenantId: 25,
    setBranding: vi.fn(),
    resetBranding: vi.fn(),
    setTheme: vi.fn(),
  }),
}));
vi.mock('../../utils/pwaBranding', () => ({
  resetTenantPwaBranding: vi.fn(),
  syncTenantPwaBranding: vi.fn(),
}));

describe('Layout company features', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
    featureState.isPlatformOnly = false;
    authState.user = { role: 'manager', isPlatformAdmin: false, email: 'manager@walos.app' };
    featureState.canAccess.mockImplementation((code) => ['dashboard', 'restaurant'].includes(code));
  });

  it('builds navigation from independent restaurant and POS features', () => {
    render(<MemoryRouter><Layout><div>Contenido</div></Layout></MemoryRouter>);

    expect(screen.getByRole('link', { name: /Restaurante/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^POS$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Inventario/i })).not.toBeInTheDocument();
  });

  it('does not enable the global inventory alerts query when inventory is unavailable', () => {
    render(<MemoryRouter><Layout><div>Contenido</div></Layout></MemoryRouter>);

    const alertsQuery = queryConfigs.find((config) => config.queryKey[0] === 'alerts');
    expect(alertsQuery.enabled).toBe(false);
    expect(screen.queryByTitle('Ver alertas')).not.toBeInTheDocument();
  });

  it('shows platform navigation only to a platform-only administrator', () => {
    authState.user = { role: 'platform_admin', isPlatformAdmin: true, email: 'platform@walos.app' };
    featureState.isPlatformOnly = true;
    featureState.canAccess.mockReturnValue(false);

    render(<MemoryRouter initialEntries={['/admin/tenants']}><Layout><div>Comercios</div></Layout></MemoryRouter>);

    expect(screen.getByRole('link', { name: /Comercios/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Dashboard/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Restaurante/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Configuracion/i })).not.toBeInTheDocument();
  });

  it('lets a trusted dev see platform navigation and all operational modules', () => {
    authState.user = { role: 'dev', isPlatformAdmin: true, email: 'dev@walos.app' };
    featureState.isPlatformOnly = false;
    featureState.canAccess.mockReturnValue(true);

    render(<MemoryRouter><Layout><div>Dashboard</div></Layout></MemoryRouter>);

    expect(screen.getByRole('link', { name: /Comercios/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Restaurante/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /POS/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Configuracion/i })).toBeInTheDocument();
  });

  it('does not expose Users or Settings navigation to an operational cashier', () => {
    authState.user = { role: 'cashier', isPlatformAdmin: false, email: 'cashier@walos.app' };
    featureState.canAccess.mockImplementation((code) =>
      canRoleAccessFeature(authState.user, code));

    render(<MemoryRouter><Layout><div>Dashboard</div></Layout></MemoryRouter>);

    expect(screen.queryByRole('link', { name: /Dashboard/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Inventario/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Usuarios/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Configuracion/i })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Restaurante/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /POS/i })).toHaveAttribute('href', '/pos-deli');
    expect(screen.getByRole('link', { name: /Caja/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Delivery/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Compras/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Proveedores/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Finanzas/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Asistente IA/i })).not.toBeInTheDocument();
  });

  it('shows waiter only Restaurante and Delivery tenant modules', () => {
    authState.user = { role: 'waiter', isPlatformAdmin: false, email: 'waiter@walos.app' };
    featureState.canAccess.mockImplementation((code) =>
      canRoleAccessFeature(authState.user, code));

    render(<MemoryRouter><Layout><div>Restaurante</div></Layout></MemoryRouter>);

    expect(screen.getByRole('link', { name: /Restaurante/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Delivery/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Dashboard/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Inventario/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^POS$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Caja/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Usuarios/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Configuracion/i })).not.toBeInTheDocument();
  });

  it('fails closed for a non-canonical legacy role', () => {
    authState.user = { role: 'admin', isPlatformAdmin: false, email: 'legacy-admin@walos.app' };
    featureState.canAccess.mockImplementation((code) =>
      canRoleAccessFeature(authState.user, code));

    render(<MemoryRouter initialEntries={['/pos-deli']}><Layout><div>POS</div></Layout></MemoryRouter>);

    expect(screen.queryByRole('link', { name: /Inventario/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^POS$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Dashboard/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Restaurante/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Caja/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Finanzas/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Usuarios/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Configuracion/i })).not.toBeInTheDocument();
    expect(queryConfigs.find((config) => config.queryKey[0] === 'company-settings').enabled).toBe(true);
  });

  it('loads tenant-safe branding for an authenticated operational cashier', () => {
    authState.user = { role: 'cashier', isPlatformAdmin: false, email: 'cashier@walos.app' };
    featureState.canAccess.mockImplementation((code) => code === 'dashboard');

    render(<MemoryRouter><Layout><div>Dashboard</div></Layout></MemoryRouter>);

    expect(queryConfigs.find((config) => config.queryKey[0] === 'company-settings').enabled).toBe(true);
  });
});
