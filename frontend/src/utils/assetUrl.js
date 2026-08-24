import { API_BASE_URL, buildApiUrl } from '../config/api';

const MANAGED_IMAGE_KEY = /^companies\/[1-9]\d*\/(?:branding\/[A-Za-z0-9._-]+|products\/[1-9]\d*\/[A-Za-z0-9._-]+)$/;

export const resolveAssetUrl = (reference) => {
  if (!reference) return null;

  const normalizedReference = String(reference).trim();
  if (MANAGED_IMAGE_KEY.test(normalizedReference)) {
    const params = new URLSearchParams({ key: normalizedReference });
    return `${buildApiUrl('media/image')}?${params.toString()}`;
  }

  try {
    const absolute = new URL(normalizedReference);
    return absolute.protocol === 'http:' || absolute.protocol === 'https:'
      ? absolute.toString()
      : null;
  } catch {
    return normalizedReference.startsWith('/uploads/')
      ? `${API_BASE_URL}${normalizedReference}`
      : null;
  }
};
