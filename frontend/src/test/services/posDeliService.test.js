import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import posDeliService from '../../services/posDeliService';

vi.mock('../../config/api', () => ({
  default: {
    post: vi.fn(),
  },
}));

describe('posDeliService.createSale', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.post.mockResolvedValue({ data: { data: { saleId: 1 } } });
  });

  it('sends the stable Idempotency-Key header', async () => {
    const payload = { items: [], payments: [] };

    await posDeliService.createSale(payload, 'sale-intent-123');

    expect(api.post).toHaveBeenCalledWith('/pos-deli/sale', payload, {
      headers: { 'Idempotency-Key': 'sale-intent-123' },
    });
  });

  it('remains compatible when no key is supplied', async () => {
    const payload = { items: [], payments: [] };

    await posDeliService.createSale(payload);

    expect(api.post).toHaveBeenCalledWith('/pos-deli/sale', payload, {
      headers: undefined,
    });
  });
});
