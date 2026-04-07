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

      setTheme: (theme) => set({ theme }),
      setSidebarCollapsed: (sidebarCollapsed) => set({ sidebarCollapsed }),
      setBranding: ({ companyName, companyLogoUrl }) =>
        set((state) => ({
          companyName: companyName === undefined ? state.companyName : companyName,
          companyLogoUrl: companyLogoUrl === undefined ? state.companyLogoUrl : companyLogoUrl,
        })),
    }),
    {
      name: 'ui-storage',
      partialize: (state) => ({
        theme: state.theme,
        sidebarCollapsed: state.sidebarCollapsed,
        companyName: state.companyName,
        companyLogoUrl: state.companyLogoUrl,
      }),
    }
  )
);

export default useUiStore;
