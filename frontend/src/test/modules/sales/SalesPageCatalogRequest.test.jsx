import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SalesPage from '../../../modules/sales/SalesPage';

const mocks = vi.hoisted(() => ({
  getSaleCatalog: vi.fn(),
  getTables: vi.fn(),
  auth: { isAuthenticated: true, tenantId: 155, branchId: 91, user: { role: 'cashier', isPlatformAdmin: false } },
}));

vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../hooks/useCompanyFeatures', () => ({
  default: () => ({ canAccess: (code) => code === 'restaurant', hasFeature: () => false, isReady: true }),
}));
vi.mock('../../../services/inventoryService', () => ({
  default: { getSaleCatalog: mocks.getSaleCatalog },
}));
vi.mock('../../../services/salesService', () => ({
  default: {
    getTables: mocks.getTables,
    cancelTable: vi.fn(),
    invoiceTable: vi.fn(),
  },
}));
vi.mock('../../../services/cashRegisterService', () => ({
  cashRegisterService: { getStatus: vi.fn() },
}));
vi.mock('../../../services/printService', () => ({ default: {} }));
vi.mock('../../../stores/postSaleHardwareStore', () => ({
  default: (selector) => selector({ enqueuePostSale: vi.fn() }),
}));
vi.mock('../../../stores/printAgentStore', () => ({
  default: (selector) => selector({ printCashClose: vi.fn() }),
}));

vi.mock('../../../modules/sales/components/AddTablePanel', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/TableCard', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/InvoicePanel', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CreditsPanel', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/SalesSummaryTab', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashRegisterBar', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/OpenCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CloseCashRegisterModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashMovementModal', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/CashRegisterHistory', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/OrderHistoryTab', () => ({ default: () => null }));
vi.mock('../../../modules/sales/components/KitchenTicket', () => ({ default: () => null }));

describe('SalesPage catalog request', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.tenantId = 155;
    mocks.auth.branchId = 91;
    mocks.auth.user = { role: 'cashier', isPlatformAdmin: false };
    mocks.getTables.mockResolvedValue({ data: [] });
    mocks.getSaleCatalog.mockResolvedValue({ data: [] });
  });

  it.each(['waiter', 'cashier'])('requests the sale catalog for %s with a valid branch', async (role) => {
    mocks.auth.user = { role, isPlatformAdmin: false };
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <SalesPage />
      </QueryClientProvider>,
    );

    await waitFor(() => expect(mocks.getSaleCatalog).toHaveBeenCalledWith(91));
  });
});
