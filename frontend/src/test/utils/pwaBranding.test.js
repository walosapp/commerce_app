import { afterEach, describe, expect, it } from 'vitest';
import { buildApiUrl } from '../../config/api';
import {
  buildTenantAppleIconUrl,
  buildTenantManifestUrl,
  resetTenantPwaBranding,
  syncTenantPwaBranding,
} from '../../utils/pwaBranding';

describe('tenant PWA branding URLs', () => {
  afterEach(() => {
    document.head
      .querySelectorAll('link[rel="manifest"], link[rel="apple-touch-icon"]')
      .forEach((link) => link.remove());
  });

  it('builds the manifest URL against the configured backend API', () => {
    const url = buildTenantManifestUrl({
      tenantId: 16,
      logoUrl: '/uploads/branding/company_16_1786214735.jpeg',
    });

    expect(url).toBe(
      `${buildApiUrl('pwa/manifest.webmanifest')}?tenantId=16&v=company_16_1786214735.jpeg`,
    );
  });

  it('builds the tenant icon URL against the configured backend API', () => {
    const url = buildTenantAppleIconUrl({
      tenantId: 16,
      logoUrl: '/uploads/branding/company_16_1786214735.jpeg',
      size: 144,
    });

    expect(url).toBe(
      `${buildApiUrl('pwa/icon/16/144.png')}?v=company_16_1786214735.jpeg`,
    );
  });

  it('keeps the default static assets when tenant branding is incomplete', () => {
    expect(buildTenantManifestUrl({ tenantId: 16, logoUrl: null })).toBe(
      '/manifest.webmanifest',
    );
    expect(buildTenantAppleIconUrl({ tenantId: null, logoUrl: '/logo.png' })).toBe(
      '/icons/icon-192x192.png',
    );
  });

  it('syncs tenant links and restores their original values', () => {
    document.head.innerHTML = `
      <link rel="manifest" href="/manifest.webmanifest">
      <link rel="apple-touch-icon" href="/icons/icon-192x192.png">
    `;

    syncTenantPwaBranding({
      tenantId: 16,
      logoUrl: '/uploads/branding/company_16_1786214735.jpeg',
    });

    expect(document.querySelector('link[rel="manifest"]')?.href).toBe(
      buildTenantManifestUrl({
        tenantId: 16,
        logoUrl: '/uploads/branding/company_16_1786214735.jpeg',
      }),
    );
    expect(document.querySelector('link[rel="apple-touch-icon"]')?.href).toBe(
      buildTenantAppleIconUrl({
        tenantId: 16,
        logoUrl: '/uploads/branding/company_16_1786214735.jpeg',
      }),
    );

    resetTenantPwaBranding();

    expect(document.querySelector('link[rel="manifest"]')?.getAttribute('href')).toBe(
      '/manifest.webmanifest',
    );
    expect(document.querySelector('link[rel="apple-touch-icon"]')?.getAttribute('href')).toBe(
      '/icons/icon-192x192.png',
    );
  });
});
