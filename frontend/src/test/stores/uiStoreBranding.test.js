import { beforeEach, describe, expect, it } from 'vitest';
import useUiStore from '../../stores/uiStore';

describe('uiStore tenant branding isolation', () => {
  beforeEach(() => {
    useUiStore.setState({ theme: 'light', sidebarCollapsed: false });
    useUiStore.getState().resetBranding();
  });

  it('associates branding with the tenant and clears it explicitly', () => {
    useUiStore.getState().setBranding({
      tenantId: 25,
      companyName: 'Comercio 25',
      companyLogoUrl: 'companies/25/branding/logo.png',
    });
    useUiStore.getState().setTheme('dark');

    expect(useUiStore.getState()).toMatchObject({
      brandingTenantId: 25,
      companyName: 'Comercio 25',
    });

    useUiStore.getState().resetBranding();
    expect(useUiStore.getState()).toMatchObject({
      brandingTenantId: null,
      companyName: 'Walos',
      companyLogoUrl: null,
      theme: 'light',
    });
  });

  it('does not persist tenant branding across sessions', () => {
    useUiStore.getState().setBranding({
      tenantId: 25,
      companyName: 'Comercio 25',
      companyLogoUrl: 'companies/25/branding/logo.png',
    });

    const persisted = useUiStore.persist.getOptions().partialize(useUiStore.getState());
    expect(persisted).toEqual({ sidebarCollapsed: false });
  });
});
