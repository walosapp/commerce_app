/**
 * Store de Autenticación (Zustand)
 * ¿Qué es? Estado global para autenticación
 * ¿Para qué? Gestionar usuario, token y permisos
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { authService } from '../services/authService';

const EMPTY_AUTH_STATE = Object.freeze({
  user: null,
  token: null,
  refreshToken: null,
  tenantId: null,
  branchId: null,
  isAuthenticated: false,
});

const OPERATIONAL_ROLES = new Set(['super_admin', 'manager', 'cashier', 'waiter']);

export const hasCurrentUserContract = (user) => {
  if (!user || typeof user.isPlatformAdmin !== 'boolean') return false;
  if (user.role === 'dev' || user.role === 'platform_admin') return user.isPlatformAdmin === true;
  return OPERATIONAL_ROLES.has(user.role) && user.isPlatformAdmin === false;
};

export const sanitizePersistedAuthState = (state) => {
  if (
    state?.isAuthenticated !== true
    || !state.token
    || !state.refreshToken
    || !hasCurrentUserContract(state.user)
  ) {
    return { ...EMPTY_AUTH_STATE };
  }

  return state;
};

const useAuthStore = create(
  persist(
    (set, get) => ({
      ...EMPTY_AUTH_STATE,

      setAuth: (data) => {
        if (!data?.token || !data?.refreshToken || !hasCurrentUserContract(data.user)) {
          set({ ...EMPTY_AUTH_STATE });
          return false;
        }
        set({
          user: data.user,
          token: data.token,
          refreshToken: data.refreshToken,
          tenantId: data.user.companyId,
          branchId: data.user.branchId,
          isAuthenticated: true,
        });
        return true;
      },

      updateTokens: (data) => {
        if (!data?.token || !data?.refreshToken || !get().isAuthenticated) {
          return false;
        }
        set({ token: data.token, refreshToken: data.refreshToken });
        return true;
      },

      clearAuth: () => set({ ...EMPTY_AUTH_STATE }),

      logout: async () => {
        try {
          await authService.logout();
        } catch {
          // Local credential removal must not depend on an already-invalid access token.
        } finally {
          get().clearAuth();
        }
      },

      hasPermission: (module, action) => {
        const { user } = get();
        if (!user?.permissions) return false;

        return (
          user.permissions.all?.[action] === true ||
          user.permissions[module]?.[action] === true
        );
      },
    }),
    {
      name: 'auth-storage',
      version: 2,
      migrate: (persistedState) => sanitizePersistedAuthState(persistedState),
      merge: (persistedState, currentState) => ({
        ...currentState,
        ...sanitizePersistedAuthState(persistedState),
      }),
      partialize: (state) => ({
        user: state.user,
        token: state.token,
        refreshToken: state.refreshToken,
        tenantId: state.tenantId,
        branchId: state.branchId,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);

export default useAuthStore;
