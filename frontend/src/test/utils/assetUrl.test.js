import { describe, expect, it } from 'vitest';
import { resolveAssetUrl } from '../../utils/assetUrl';

describe('resolveAssetUrl', () => {
  it('keeps managed public URLs usable without prefixing the API origin', () => {
    expect(resolveAssetUrl('https://storage.example/companies/1/branding/logo.png'))
      .toBe('https://storage.example/companies/1/branding/logo.png');
  });

  it('resolves legacy uploads against the backend origin', () => {
    expect(resolveAssetUrl('/uploads/branding/company_1.png'))
      .toBe('http://localhost:3000/uploads/branding/company_1.png');
  });

  it('resolves canonical product keys through the safe backend media endpoint', () => {
    expect(resolveAssetUrl('companies/16/products/25/image-123.webp'))
      .toBe(
        'http://localhost:3000/api/v1/media/image?key=companies%2F16%2Fproducts%2F25%2Fimage-123.webp',
      );
  });

  it('resolves canonical branding keys through the safe backend media endpoint', () => {
    expect(resolveAssetUrl('companies/16/branding/logo-123.png'))
      .toBe(
        'http://localhost:3000/api/v1/media/image?key=companies%2F16%2Fbranding%2Flogo-123.png',
      );
  });

  it('rejects non-http absolute schemes', () => {
    expect(resolveAssetUrl('javascript:alert(1)')).toBeNull();
  });

  it('rejects unknown relative paths instead of treating them as backend assets', () => {
    expect(resolveAssetUrl('companies/16/avatars/user.png')).toBeNull();
    expect(resolveAssetUrl('/private/file.png')).toBeNull();
  });
});
