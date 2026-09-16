import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import salesService from '../../services/salesService';

vi.mock('../../config/api', () => ({
  default: {
    get: vi.fn(),
  },
}));

describe('salesService role-scoped order item routes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.get.mockResolvedValue({ data: { data: [] } });
  });

  it('keeps completed order items on the invoice-operator route', async () => {
    await salesService.getOrderItems(44);

    expect(api.get).toHaveBeenCalledWith('/sales/orders/44/items');
  });

  it('exposes the waiter-safe route only for active restaurant order items', async () => {
    await salesService.getActiveRestaurantOrderItems(44);

    expect(api.get).toHaveBeenCalledWith('/sales/restaurant/orders/44/items');
  });
});
