/**
 * Store de UI
 * ¿Qué es? Estado global para preferencias visuales y branding
 * ¿Para qué? Persistir tema, sidebar y datos visuales de la empresa
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

const useUiStore = create(
  persist(
    (set) => ({
      theme: 'light',
      sidebarCollapsed: false,
      companyName: 'Walos',
      companyLogoUrl: null,
      brandingTenantId: null,

      setTheme: (theme) => set({ theme }),
      setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
      setBranding: ({ companyName, companyLogoUrl, tenantId }) =>
        set((state) => ({
          companyName: companyName === undefined ? state.companyName : companyName,
          companyLogoUrl: companyLogoUrl === undefined ? state.companyLogoUrl : companyLogoUrl,
          brandingTenantId: tenantId === undefined ? state.brandingTenantId : tenantId,
        })),
      resetBranding: () => set({ theme: 'light', companyName: 'Walos', companyLogoUrl: null, brandingTenantId: null }),
    }),
    {
      name: 'ui-storage',
      version: 2,
      migrate: (persistedState) => ({
        sidebarCollapsed: persistedState?.sidebarCollapsed === true,
      }),
      partialize: (state) => ({
        sidebarCollapsed: state.sidebarCollapsed,
      }),
    }
  )
);

export default useUiStore;
