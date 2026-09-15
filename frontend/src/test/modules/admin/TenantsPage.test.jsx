import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import TenantsPage from '../../../modules/admin/TenantsPage';

const { tenants, invalidateQueries } = vi.hoisted(() => ({
  tenants: [{ id: 25, name: 'Comercio Demo', isActive: true, isSystem: false }],
  invalidateQueries: vi.fn(),
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries }),
  useQuery: () => ({ data: { data: tenants }, isLoading: false, refetch: vi.fn() }),
}));
vi.mock('../../../services/adminService', () => ({ default: {} }));
vi.mock('react-hot-toast', () => ({ default: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../../modules/admin/components/TenantCard', () => ({
  default: ({ tenant, onManageBranches }) => <button onClick={() => onManageBranches(tenant)}>Sucursales {tenant.name}</button>,
}));
vi.mock('../../../modules/admin/components/CreateTenantModal', () => ({ default: () => null }));
vi.mock('../../../modules/admin/components/EditTenantModal', () => ({ default: () => null }));
vi.mock('../../../modules/admin/components/CompanyFeaturesPanel', () => ({ default: () => null }));
vi.mock('../../../modules/admin/components/CompanyBranchesPanel', () => ({
  default: ({ companyId, companyName }) => <div>Sucursales panel {companyId} {companyName}</div>,
}));

describe('TenantsPage branch management flow', () => {
  beforeEach(() => vi.clearAllMocks());

  it('opens branches with the selected tenant companyId', () => {
    render(<TenantsPage />);

    fireEvent.click(screen.getByRole('button', { name: 'Sucursales Comercio Demo' }));

    expect(screen.getByText('Sucursales panel 25 Comercio Demo')).toBeInTheDocument();
  });
});
