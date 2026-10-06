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
    featureState.isReady = true;
    authState.user = { role: 'manager', isPlatformAdmin: false, email: 'manager@walos.app' };
    featureState.canAccess.mockImplementation((code) => ['dashboard', 'restaurant'].includes(code));
  });

  it.each([
    { enabled: ['purchases', 'suppliers'], name: 'Compras y proveedores', path: '/purchases' },
    { enabled: ['purchases'], name: 'Compras', path: '/purchases' },
    { enabled: ['suppliers'], name: 'Proveedores', path: '/suppliers' },
  ])('shows one purchasing workspace entry for $enabled without expanding feature access', ({ enabled, name, path }) => {
    featureState.canAccess.mockImplementation(code => enabled.includes(code));
    render(<MemoryRouter><Layout><div>Contenido</div></Layout></MemoryRouter>);
    const links = screen.getAllByRole('link').filter(link => ['/purchases', '/suppliers'].includes(link.getAttribute('href')));
    expect(links).toHaveLength(1);
    expect(screen.getByRole('link', { name })).toHaveAttribute('href', path);
  });

  it.each(['/purchases', '/suppliers'])('highlights the single entry while viewing either workspace route: %s', path => {
    featureState.canAccess.mockImplementation(code => ['purchases', 'suppliers'].includes(code));
    render(<MemoryRouter initialEntries={[path]}><Layout><div>Contenido</div></Layout></MemoryRouter>);
    expect(screen.getByRole('link', { name: 'Compras y proveedores' })).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', { name: /^Proveedores$/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^Compras$/ })).not.toBeInTheDocument();
  });

  it('does not show the combined entry when neither feature is allowed', () => {
    featureState.canAccess.mockReturnValue(false);
    render(<MemoryRouter><Layout><div>Contenido</div></Layout></MemoryRouter>);
    expect(screen.queryByRole('link', { name: /Compras|Proveedores/i })).not.toBeInTheDocument();
  });

  it('does not show the combined entry while feature configuration is unresolved', () => {
    featureState.isReady = false;
    featureState.canAccess.mockReturnValue(true);
    render(<MemoryRouter><Layout><div>Contenido</div></Layout></MemoryRouter>);
    expect(screen.queryByRole('link', { name: /Compras|Proveedores/i })).not.toBeInTheDocument();
  });

  it('bounds inventory to the viewport and delegates scrolling to its table', () => {
    const { container } = render(<MemoryRouter initialEntries={['/inventory']}><Layout><div>Inventario</div></Layout></MemoryRouter>);
    expect(container.firstElementChild).toHaveClass('h-dvh', 'overflow-hidden');
    expect(screen.getByRole('main')).toHaveClass('min-h-0', 'overflow-hidden', 'flex');
    expect(screen.getByRole('main')).not.toHaveClass('overflow-y-auto');
  });

  it.each(['/sales', '/profile', '/pos-deli'])('preserves page scrolling outside inventory: %s', route => {
    const { container } = render(<MemoryRouter initialEntries={[route]}><Layout><div>Contenido</div></Layout></MemoryRouter>);
    expect(container.firstElementChild).toHaveClass('h-full');
    expect(screen.getByRole('main')).toHaveClass('overflow-y-auto');
    expect(screen.getByRole('main')).not.toHaveClass('overflow-hidden');
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
    expect(screen.getByRole('link', { name: /Perfil/i })).toHaveAttribute('href', '/profile');
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
    expect(screen.getByRole('link', { name: /Perfil/i })).toHaveAttribute('href', '/profile');
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
    expect(screen.getByRole('link', { name: /Perfil/i })).toHaveAttribute('href', '/profile');
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
