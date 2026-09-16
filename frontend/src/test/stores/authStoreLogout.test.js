import { beforeEach, describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const { remoteLogout } = vi.hoisted(() => ({ remoteLogout: vi.fn() }));

vi.mock('../../services/authService', () => ({
  authService: { logout: remoteLogout },
}));

describe('authStore forced logout', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.resetModules();
    remoteLogout.mockReset();
  });

  it('clears local credentials even when the invalidated access token makes remote logout fail', async () => {
    remoteLogout.mockRejectedValue(new Error('401'));
    const { default: useAuthStore } = await import('../../stores/authStore');
    useAuthStore.getState().setAuth({
      token: 'invalidated-access',
      refreshToken: 'invalidated-refresh',
      user: {
        id: 7,
        role: 'manager',
        companyId: 10,
        branchId: 2,
        isPlatformAdmin: false,
      },
    });

    await expect(useAuthStore.getState().logout()).resolves.toBeUndefined();

    expect(useAuthStore.getState()).toMatchObject({
      user: null,
      token: null,
      refreshToken: null,
      isAuthenticated: false,
    });
  });

  it('handles API 401 locally without recursively calling the protected logout endpoint', () => {
    const source = readFileSync(join(process.cwd(), 'src/config/api.js'), 'utf8');
    const unauthorizedHandler = source.slice(source.indexOf("status === 401"));

    expect(unauthorizedHandler).toContain('state.clearAuth?.()');
    expect(unauthorizedHandler).not.toContain('state.logout?.()');
  });
});
