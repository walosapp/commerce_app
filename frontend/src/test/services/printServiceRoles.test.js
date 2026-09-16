import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import printService from '../../services/printService';

vi.mock('../../config/api', () => ({
  default: {
    get: vi.fn(),
  },
}));

describe('printService role-scoped kitchen routes', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.get.mockResolvedValue({ data: { data: {} } });
  });

  it('keeps completed kitchen tickets on the invoice-operator route', async () => {
    await printService.getKitchenTicket(44);
    expect(api.get).toHaveBeenCalledWith('/sales/orders/44/kitchen');
  });

  it('uses the waiter-safe route for active restaurant kitchen tickets', async () => {
    await printService.getActiveRestaurantKitchenTicket(44);
    expect(api.get).toHaveBeenCalledWith('/sales/restaurant/orders/44/kitchen');
  });
});
