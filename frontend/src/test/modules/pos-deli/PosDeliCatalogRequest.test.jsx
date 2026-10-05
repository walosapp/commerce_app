import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PosDeliPage from '../../../modules/pos-deli/PosDeliPage';

const mocks = vi.hoisted(() => ({
  getProducts: vi.fn(),
  getFavorites: vi.fn(),
  auth: { tenantId: 155, branchId: 91, user: { role: 'cashier', isPlatformAdmin: false } },
}));

vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../services/posDeliService', () => ({
  default: {
    getProducts: mocks.getProducts,
    getFavorites: mocks.getFavorites,
    createSale: vi.fn(),
  },
}));
vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale: vi.fn() }),
}));
vi.mock('../../../modules/pos-deli/stores/posDeliStore', () => ({
  default: () => ({
    items: [],
    selectedItemId: null,
    getTotal: () => 0,
    addWeighedItem: vi.fn(),
    addUnitItem: vi.fn(),
    removeItem: vi.fn(),
    updateQuantity: vi.fn(),
    setSelectedItemId: vi.fn(),
    clearTicket: vi.fn(),
    getIdempotencyKey: vi.fn(),
    renewIdempotencyKey: vi.fn(),
  }),
}));
vi.mock('../../../modules/pos-deli/hooks/useScale', () => ({
  default: () => ({ weight: 0, isStable: false, isConnected: false, error: null, config: null }),
}));
vi.mock('../../../modules/pos-deli/hooks/useBarcodeScanner', () => ({ default: vi.fn() }));
vi.mock('../../../modules/pos-deli/components/ProductGrid', () => ({
  default: ({ weighedProducts, unitProducts }) => (
    <div>{[...weighedProducts, ...unitProducts].map(product => <span key={product.id}>{product.name}</span>)}</div>
  ),
}));
vi.mock('../../../modules/pos-deli/components/ProductSearchBar', () => ({ default: () => null }));
vi.mock('../../../modules/pos-deli/components/ScaleIndicator', () => ({ default: () => null }));
vi.mock('../../../modules/pos-deli/components/TicketPanel', () => ({ default: () => null }));
vi.mock('../../../modules/pos-deli/components/PaymentModal', () => ({ default: () => null }));
vi.mock('../../../modules/pos-deli/components/WeightInputModal', () => ({ default: () => null }));

describe('PosDeli catalog request', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getProducts.mockResolvedValue({
      data: [{ id: 501, name: 'Café POS', unitAbbreviation: 'und' }],
    });
    mocks.getFavorites.mockResolvedValue({ data: [] });
  });

  it('loads products for a cashier with valid tenant and branch context', async () => {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <PosDeliPage />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(mocks.getProducts).toHaveBeenCalledWith({ search: '' }));
    expect(await screen.findByText('Café POS')).toBeInTheDocument();
  });
});
