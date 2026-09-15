import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const publicPath = (...parts) => path.join(process.cwd(), 'public', ...parts);
const expectedSizes = [72, 96, 128, 144, 152, 192, 384, 512];

const readPngSize = (filePath) => {
  const bytes = fs.readFileSync(filePath);
  expect(bytes.subarray(0, 8).toString('hex')).toBe('89504e470d0a1a0a');
  return {
    width: bytes.readUInt32BE(16),
    height: bytes.readUInt32BE(20),
  };
};

describe('static PWA branding', () => {
  it('declares every required Walos icon in the static manifest', () => {
    const manifest = JSON.parse(fs.readFileSync(publicPath('manifest.webmanifest'), 'utf8'));

    expect(manifest.name).toBe('Walos - Gestión Comercial');
    expect(manifest.icons).toHaveLength(expectedSizes.length);
    expect(manifest.icons.map((icon) => icon.src)).toEqual(
      expectedSizes.map((size) => `/icons/icon-${size}x${size}.png`)
    );
    expect(manifest.icons.every((icon) => icon.type === 'image/png')).toBe(true);
  });

  it.each(expectedSizes)('ships a real square %ipx PNG', (size) => {
    const dimensions = readPngSize(publicPath('icons', `icon-${size}x${size}.png`));
    expect(dimensions).toEqual({ width: size, height: size });
  });

  it('uses the Walos PWA assets instead of the Vite placeholder', () => {
    const html = fs.readFileSync(path.join(process.cwd(), 'index.html'), 'utf8');

    expect(html).toContain('href="/manifest.webmanifest"');
    expect(html).toContain('href="/icons/icon-192x192.png"');
    expect(html).not.toContain('/vite.svg');
  });
});
