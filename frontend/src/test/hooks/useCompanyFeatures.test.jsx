import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import useCompanyFeatures from '../../hooks/useCompanyFeatures';
import useAuthStore from '../../stores/authStore';

const { getMine } = vi.hoisted(() => ({ getMine: vi.fn() }));

vi.mock('../../services/featureService', () => ({
  default: { getMine },
}));

const Probe = () => {
  const features = useCompanyFeatures();
  return (
    <div>
      <span data-testid="ready">{String(features.isReady)}</span>
      <span data-testid="dashboard">{String(features.canAccess('dashboard'))}</span>
      <span data-testid="inventory">{String(features.canAccess('inventory'))}</span>
      <span data-testid="restaurant">{String(features.canAccess('restaurant'))}</span>
      <span data-testid="pos">{String(features.canAccess('pos'))}</span>
      <span data-testid="role-finance">{String(features.roleAllows('finance'))}</span>
    </div>
  );
};

const renderProbe = (client) => render(
  <QueryClientProvider client={client}>
    <Probe />
  </QueryClientProvider>,
);

const setUser = (overrides = {}) => {
  const user = {
    id: 1,
    role: 'manager',
    companyId: 10,
    branchId: 5,
    isPlatformAdmin: false,
    ...overrides,
  };
  useAuthStore.setState({
    user,
    tenantId: user.companyId,
    branchId: user.branchId,
    isAuthenticated: true,
  });
};

describe('useCompanyFeatures', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    useAuthStore.setState({ user: null, tenantId: null, branchId: null, isAuthenticated: false });
  });

  it('keeps optional features fail-closed but dashboard available while loading', () => {
    setUser();
    getMine.mockReturnValue(new Promise(() => {}));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    renderProbe(client);

    expect(screen.getByTestId('dashboard')).toHaveTextContent('true');
    expect(screen.getByTestId('inventory')).toHaveTextContent('false');
    expect(screen.getByTestId('ready')).toHaveTextContent('false');
  });

  it('isolates the feature cache by tenant and keeps restaurant independent from POS', async () => {
    setUser({ companyId: 10 });
    getMine
      .mockResolvedValueOnce({ data: [
        { code: 'restaurant', isEnabled: true },
        { code: 'pos', isEnabled: false },
      ] })
      .mockResolvedValueOnce({ data: [
        { code: 'restaurant', isEnabled: false },
        { code: 'pos', isEnabled: true },
      ] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const view = renderProbe(client);

    await waitFor(() => expect(screen.getByTestId('restaurant')).toHaveTextContent('true'));
    expect(screen.getByTestId('pos')).toHaveTextContent('false');

    act(() => setUser({ companyId: 20 }));
    view.rerender(
      <QueryClientProvider client={client}>
        <Probe />
      </QueryClientProvider>,
    );

    expect(screen.getByTestId('restaurant')).toHaveTextContent('false');
    await waitFor(() => expect(screen.getByTestId('pos')).toHaveTextContent('true'));
    expect(getMine).toHaveBeenCalledTimes(2);
    expect(client.getQueryData(['company-features', 10])).toEqual(expect.objectContaining({ data: expect.any(Array) }));
    expect(client.getQueryData(['company-features', 20])).toEqual(expect.objectContaining({ data: expect.any(Array) }));
  });

  it('lets only a trusted dev bypass tenant features without querying the API', () => {
    setUser({ role: 'dev', isPlatformAdmin: true });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    renderProbe(client);

    expect(screen.getByTestId('ready')).toHaveTextContent('true');
    expect(screen.getByTestId('inventory')).toHaveTextContent('true');
    expect(screen.getByTestId('pos')).toHaveTextContent('true');
    expect(getMine).not.toHaveBeenCalled();
  });

  it('gives a platform-only admin no operational features', () => {
    setUser({ role: 'platform_admin', isPlatformAdmin: true });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    renderProbe(client);

    expect(screen.getByTestId('dashboard')).toHaveTextContent('false');
    expect(screen.getByTestId('inventory')).toHaveTextContent('false');
    expect(getMine).not.toHaveBeenCalled();
  });

  it('reports role capability separately from tenant entitlement', async () => {
    setUser({ role: 'cashier' });
    getMine.mockResolvedValue({ data: [{ code: 'finance', isEnabled: true }] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    renderProbe(client);

    await waitFor(() => expect(getMine).toHaveBeenCalled());
    expect(screen.getByTestId('role-finance')).toHaveTextContent('false');
  });
});
