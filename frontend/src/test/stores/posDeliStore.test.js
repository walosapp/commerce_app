// @vitest-environment jsdom

import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('posDeliStore idempotency lifecycle', () => {
  beforeEach(() => {
    sessionStorage.clear();
    vi.resetModules();
  });

  it('keeps the same key for retries and discards it after a confirmed sale', async () => {
    let sequence = 0;
    const randomUUID = vi.fn(() => `uuid-${++sequence}`);
    vi.stubGlobal('crypto', { randomUUID });
    const { default: usePosDeliStore } = await import('../../modules/pos-deli/stores/posDeliStore');
    const product = { id: 7, name: 'Cafe', salePrice: 10 };

    usePosDeliStore.getState().addUnitItem(product);
    const attemptedKey = usePosDeliStore.getState().getIdempotencyKey();
    expect(usePosDeliStore.getState().getIdempotencyKey()).toBe(attemptedKey);

    usePosDeliStore.getState().clearTicket();
    expect(usePosDeliStore.getState().idempotencyKey).toBeNull();
    expect(usePosDeliStore.getState().getIdempotencyKey()).not.toBe(attemptedKey);
  });

  it('starts a new intent when the cart changes after a failed attempt', async () => {
    let sequence = 0;
    const randomUUID = vi.fn(() => `uuid-${++sequence}`);
    vi.stubGlobal('crypto', { randomUUID });
    const { default: usePosDeliStore } = await import('../../modules/pos-deli/stores/posDeliStore');
    const product = { id: 7, name: 'Cafe', salePrice: 10 };

    usePosDeliStore.getState().addUnitItem(product);
    const attemptedKey = usePosDeliStore.getState().getIdempotencyKey();
    usePosDeliStore.getState().updateQuantity(
      usePosDeliStore.getState().items[0].id,
      2
    );

    expect(usePosDeliStore.getState().getIdempotencyKey()).not.toBe(attemptedKey);
  });
});
