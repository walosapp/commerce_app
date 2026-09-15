import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SuppliersPage from '../../../modules/suppliers/SuppliersPage';

const { featureState, queryConfigs } = vi.hoisted(() => ({
  featureState: { canAccess: vi.fn() },
  queryConfigs: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  useQuery: (config) => {
    queryConfigs.push(config);
    return { data: { data: [] }, isLoading: false, refetch: vi.fn() };
  },
}));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));
vi.mock('../../../stores/authStore', () => ({ default: () => ({ tenantId: 25 }) }));
vi.mock('../../../modules/suppliers/components/SupplierFormModal', () => ({ default: () => null }));
vi.mock('../../../modules/suppliers/components/SupplierDetailPanel', () => ({ default: () => null }));
vi.mock('../../../modules/suppliers/components/PurchaseOrderModal', () => ({ default: () => null }));
vi.mock('../../../modules/suppliers/components/ReceiveOrderModal', () => ({ default: () => null }));
vi.mock('../../../modules/suppliers/components/PurchaseOrderDetailPanel', () => ({ default: () => null }));

describe('SuppliersPage feature isolation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
  });

  it('does not fetch purchases when only suppliers is enabled', () => {
    featureState.canAccess.mockImplementation((code) => code === 'suppliers');

    render(<SuppliersPage initialTab="suppliers" />);

    expect(queryConfigs.find((config) => config.queryKey[0] === 'suppliers')?.enabled).toBe(true);
    expect(queryConfigs.find((config) => config.queryKey[0] === 'purchase-orders')?.enabled).toBe(false);
    expect(screen.getByRole('button', { name: /Proveedores/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Compras/i })).not.toBeInTheDocument();
  });

  it('does not fetch suppliers while browsing purchases only', () => {
    featureState.canAccess.mockImplementation((code) => code === 'purchases');

    render(<SuppliersPage initialTab="orders" />);

    expect(queryConfigs.find((config) => config.queryKey[0] === 'suppliers')?.enabled).toBe(false);
    expect(queryConfigs.find((config) => config.queryKey[0] === 'purchase-orders')?.enabled).toBe(true);
    expect(screen.getByRole('button', { name: /Compras/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Proveedores/i })).not.toBeInTheDocument();
  });

  it('synchronizes the active view when routing between suppliers and purchases', async () => {
    featureState.canAccess.mockImplementation((code) => ['suppliers', 'purchases'].includes(code));
    const view = render(<SuppliersPage initialTab="suppliers" />);
    expect(screen.getByRole('heading', { name: 'Proveedores' })).toBeInTheDocument();

    view.rerender(<SuppliersPage initialTab="orders" />);
    expect(await screen.findByRole('heading', { name: 'Compras' })).toBeInTheDocument();

    view.rerender(<SuppliersPage initialTab="suppliers" />);
    expect(await screen.findByRole('heading', { name: 'Proveedores' })).toBeInTheDocument();
  });
});
