/**
 * Store de Autenticación (Zustand)
 * ¿Qué es? Estado global para autenticación
 * ¿Para qué? Gestionar usuario, token y permisos
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

const useAuthStore = create(
  persist(
    (set, get) => ({
      user: null,
      token: null,
      tenantId: null,
      branchId: null,
      isAuthenticated: false,

      setAuth: (data) => {
        localStorage.setItem('token', data.token);
        localStorage.setItem('tenantId', data.user.companyId);
        if (data.user.branchId) {
          localStorage.setItem('branchId', data.user.branchId);
        }

        set({
          user: data.user,
          token: data.token,
          tenantId: data.user.companyId,
          branchId: data.user.branchId,
          isAuthenticated: true,
        });
      },

      logout: () => {
        localStorage.removeItem('token');
        localStorage.removeItem('user');
        localStorage.removeItem('tenantId');
        localStorage.removeItem('branchId');

        set({
          user: null,
          token: null,
          tenantId: null,
          branchId: null,
          isAuthenticated: false,
        });
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
      partialize: (state) => ({
        user: state.user,
        token: state.token,
        tenantId: state.tenantId,
        branchId: state.branchId,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
);

export default useAuthStore;
