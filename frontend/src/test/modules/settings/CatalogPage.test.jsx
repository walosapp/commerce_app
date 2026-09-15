import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CatalogPage from '../../../modules/settings/CatalogPage';

const { authState, catalog, companySettings } = vi.hoisted(() => ({
  authState: {
    user: { role: 'super_admin', isPlatformAdmin: false },
    tenantId: 25,
  },
  catalog: {
    getCategories: vi.fn(),
    getUnits: vi.fn(),
  },
  companySettings: {
    getSettings: vi.fn(),
    getOperationsSettings: vi.fn(),
  },
}));

vi.mock('../../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../../services/catalogService', () => ({
  default: {
    ...catalog,
    createCategory: vi.fn(),
    updateCategory: vi.fn(),
    setCategoryStatus: vi.fn(),
    deleteCategory: vi.fn(),
    createUnit: vi.fn(),
    updateUnit: vi.fn(),
    setUnitStatus: vi.fn(),
    deleteUnit: vi.fn(),
  },
}));
vi.mock('../../../services/companyService', () => ({ default: companySettings }));

const category = { id: 1, name: 'Bebidas', isActive: true, productCount: 0 };
const unit = { id: 2, name: 'Unidad', abbreviation: 'und', unitType: 'quantity', isActive: true, productCount: 0 };

const renderPage = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><CatalogPage /></QueryClientProvider>);
};

describe('CatalogPage permissions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    authState.user = { role: 'super_admin', isPlatformAdmin: false };
    catalog.getCategories.mockResolvedValue({ data: [category] });
    catalog.getUnits.mockResolvedValue({ data: [unit] });
  });

  it('renders the dedicated catalog without settings or operations requests', async () => {
    renderPage();
    expect(await screen.findByText('Bebidas')).toBeInTheDocument();
    expect(screen.getByText('Unidad')).toBeInTheDocument();
    expect(companySettings.getSettings).not.toHaveBeenCalled();
    expect(companySettings.getOperationsSettings).not.toHaveBeenCalled();
  });

  it('allows catalog delete for super admin', async () => {
    renderPage();
    await screen.findByText('Bebidas');
    expect(screen.getAllByTitle('Eliminar')).toHaveLength(2);
  });

  it('keeps manager catalog writes but hides delete actions', async () => {
    authState.user = { role: 'manager', isPlatformAdmin: false };
    renderPage();
    await screen.findByText('Bebidas');
    expect(screen.getByText('Nueva categoria')).toBeInTheDocument();
    expect(screen.queryByTitle('Eliminar')).not.toBeInTheDocument();
  });
});
