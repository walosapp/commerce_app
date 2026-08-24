import { buildApiUrl } from '../config/api';

const DEFAULT_MANIFEST_HREF = '/manifest.webmanifest';
const DEFAULT_APPLE_ICON_HREF = '/icons/icon-192x192.png';

const getManifestLink = () => document.querySelector('link[rel="manifest"]');
const getAppleTouchIconLink = () => document.querySelector('link[rel="apple-touch-icon"]');

const ensureLink = (selector, attributes) => {
  let link = document.querySelector(selector);
  if (!link) {
    link = document.createElement('link');
    Object.entries(attributes).forEach(([key, value]) => link.setAttribute(key, value));
    document.head.appendChild(link);
  }
  return link;
};

const normalizeVersion = (logoUrl) => {
  if (!logoUrl) return 'default';
  const [, version = logoUrl] = logoUrl.split('/uploads/branding/');
  return version;
};

export const buildTenantManifestUrl = ({ tenantId, logoUrl }) => {
  if (!tenantId || !logoUrl) return DEFAULT_MANIFEST_HREF;

  const version = normalizeVersion(logoUrl);
  const params = new URLSearchParams({
    tenantId: String(tenantId),
    v: version,
  });

  return `${buildApiUrl('pwa/manifest.webmanifest')}?${params.toString()}`;
};

export const buildTenantAppleIconUrl = ({ tenantId, logoUrl, size = 180 }) => {
  if (!tenantId || !logoUrl) return DEFAULT_APPLE_ICON_HREF;

  const version = normalizeVersion(logoUrl);
  const params = new URLSearchParams({ v: version });
  return `${buildApiUrl(`pwa/icon/${tenantId}/${size}.png`)}?${params.toString()}`;
};

export const syncTenantPwaBranding = ({ tenantId, logoUrl }) => {
  const manifestLink = ensureLink('link[rel="manifest"]', { rel: 'manifest' });
  const appleTouchIcon = ensureLink('link[rel="apple-touch-icon"]', { rel: 'apple-touch-icon' });

  if (!manifestLink.dataset.defaultHref) {
    manifestLink.dataset.defaultHref = manifestLink.getAttribute('href') || DEFAULT_MANIFEST_HREF;
  }

  if (!appleTouchIcon.dataset.defaultHref) {
    appleTouchIcon.dataset.defaultHref = appleTouchIcon.getAttribute('href') || DEFAULT_APPLE_ICON_HREF;
  }

  manifestLink.setAttribute('href', buildTenantManifestUrl({ tenantId, logoUrl }));
  appleTouchIcon.setAttribute('href', buildTenantAppleIconUrl({ tenantId, logoUrl }));
};

export const resetTenantPwaBranding = () => {
  const manifestLink = getManifestLink();
  const appleTouchIcon = getAppleTouchIconLink();

  if (manifestLink) {
    manifestLink.setAttribute('href', manifestLink.dataset.defaultHref || DEFAULT_MANIFEST_HREF);
  }

  if (appleTouchIcon) {
    appleTouchIcon.setAttribute('href', appleTouchIcon.dataset.defaultHref || DEFAULT_APPLE_ICON_HREF);
  }
};
