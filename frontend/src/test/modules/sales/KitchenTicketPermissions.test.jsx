import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import KitchenTicket from '../../../modules/sales/components/KitchenTicket';

const { getActiveRestaurantKitchenTicket, getKitchenTicket, queryConfigs } = vi.hoisted(() => ({
  getActiveRestaurantKitchenTicket: vi.fn(),
  getKitchenTicket: vi.fn(),
  queryConfigs: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (config) => {
    queryConfigs.push(config);
    return { data: null, isLoading: false, error: null };
  },
}));

vi.mock('../../../services/printService', () => ({
  default: {
    getActiveRestaurantKitchenTicket,
    getKitchenTicket,
  },
}));

describe('KitchenTicket endpoint scope', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    queryConfigs.length = 0;
  });

  it('uses the waiter-safe endpoint for an active restaurant order', async () => {
    render(<KitchenTicket orderId={44} scope="activeRestaurant" onClose={() => {}} />);

    await queryConfigs[0].queryFn();

    expect(queryConfigs[0].queryKey).toEqual(['kitchen-ticket', 'activeRestaurant', 44]);
    expect(getActiveRestaurantKitchenTicket).toHaveBeenCalledWith(44);
    expect(getKitchenTicket).not.toHaveBeenCalled();
  });

  it('keeps completed tickets on the invoice-operator endpoint', async () => {
    render(<KitchenTicket orderId={45} onClose={() => {}} />);

    await queryConfigs[0].queryFn();

    expect(queryConfigs[0].queryKey).toEqual(['kitchen-ticket', 'completed', 45]);
    expect(getKitchenTicket).toHaveBeenCalledWith(45);
    expect(getActiveRestaurantKitchenTicket).not.toHaveBeenCalled();
  });
});
