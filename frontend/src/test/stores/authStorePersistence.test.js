import { beforeEach, describe, expect, it, vi } from 'vitest';

const legacySession = {
  user: { id: 1, role: 'admin', companyId: 25, branchId: 7 },
  token: 'legacy-token',
  tenantId: 25,
  branchId: 7,
  isAuthenticated: true,
};

describe('authStore persisted contract', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.resetModules();
  });

  it('clears a legacy authenticated session without isPlatformAdmin', async () => {
    localStorage.setItem('auth-storage', JSON.stringify({ state: legacySession, version: 0 }));

    const { default: useAuthStore } = await import('../../stores/authStore');
    await useAuthStore.persist.rehydrate();

    expect(useAuthStore.getState()).toMatchObject({
      user: null,
      token: null,
      tenantId: null,
      branchId: null,
      isAuthenticated: false,
    });
    const persisted = JSON.parse(localStorage.getItem('auth-storage'));
    expect(persisted.state.isAuthenticated).toBe(false);
    expect(persisted.state.user).toBeNull();
  });

  it('rejects a non-canonical role even with an explicit backend platform flag', async () => {
    const current = {
      ...legacySession,
      user: { ...legacySession.user, isPlatformAdmin: false },
    };
    localStorage.setItem('auth-storage', JSON.stringify({ state: current, version: 1 }));

    const { default: useAuthStore } = await import('../../stores/authStore');
    await useAuthStore.persist.rehydrate();

    expect(useAuthStore.getState()).toMatchObject({
      user: null,
      token: null,
      tenantId: null,
      branchId: null,
      isAuthenticated: false,
    });
  });

  it('retains a canonical operational session with an explicit false platform flag', async () => {
    const current = {
      ...legacySession,
      user: { ...legacySession.user, role: 'manager', isPlatformAdmin: false },
    };
    localStorage.setItem('auth-storage', JSON.stringify({ state: current, version: 1 }));

    const { default: useAuthStore } = await import('../../stores/authStore');
    await useAuthStore.persist.rehydrate();

    expect(useAuthStore.getState()).toMatchObject(current);
  });

  it('rejects a non-canonical role immediately on login', async () => {
    const { default: useAuthStore } = await import('../../stores/authStore');

    const accepted = useAuthStore.getState().setAuth({
      token: 'untrusted-token',
      user: { id: 1, role: 'admin', companyId: 25, branchId: 7, isPlatformAdmin: false },
    });

    expect(accepted).toBe(false);
    expect(useAuthStore.getState().isAuthenticated).toBe(false);
  });
});
