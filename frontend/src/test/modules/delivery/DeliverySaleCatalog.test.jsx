import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CreateDeliveryOrderPanel from '../../../modules/delivery/components/CreateDeliveryOrderPanel';

const { getSaleCatalog, queryConfigs } = vi.hoisted(() => ({
  getSaleCatalog: vi.fn(),
  queryConfigs: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (config) => {
    queryConfigs.push(config);
    return {
      data: {
        data: [
          {
            productId: 10,
            productName: 'Producto vendible',
            salePrice: 5000,
            trackStock: false,
            isConfiguredForSale: true,
          },
          {
            productId: 11,
            productName: 'Receta pendiente',
            salePrice: 6000,
            trackStock: false,
            isConfiguredForSale: false,
          },
        ],
      },
      isLoading: false,
    };
  },
}));
vi.mock('../../../stores/authStore', () => ({
  default: () => ({ branchId: 7, tenantId: 25 }),
}));
vi.mock('../../../services/inventoryService', () => ({
  inventoryService: { getSaleCatalog },
}));

describe('delivery sale catalog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
    getSaleCatalog.mockResolvedValue({ data: [] });
  });

  it('uses the sale-safe catalog and excludes products not configured for sale', async () => {
    render(<CreateDeliveryOrderPanel isOpen onClose={vi.fn()} onCreated={vi.fn()} />);

    expect(screen.getByText('Producto vendible')).toBeInTheDocument();
    expect(screen.queryByText('Receta pendiente')).not.toBeInTheDocument();

    const catalogQuery = queryConfigs.find((config) => config.queryKey[0] === 'sale-catalog');
    expect(catalogQuery.enabled).toBe(true);
    await catalogQuery.queryFn();
    expect(getSaleCatalog).toHaveBeenCalledWith(7);
  });
});
