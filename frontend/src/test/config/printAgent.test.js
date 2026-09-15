import { describe, expect, it } from 'vitest';
import { resolvePrintAgentDownloadUrl } from '../../config/printAgent';

describe('printAgent download configuration', () => {
  it('acepta HTTPS para releases', () => {
    expect(resolvePrintAgentDownloadUrl(
      'https://github.com/walosapp/commerce_app/releases/latest/download/Walos-Agent-Setup.exe',
      { isDevelopment: false }
    )).toBe('https://github.com/walosapp/commerce_app/releases/latest/download/Walos-Agent-Setup.exe');
  });

  it('rechaza HTTP remoto incluso en desarrollo', () => {
    expect(resolvePrintAgentDownloadUrl(
      'http://downloads.example.test/Walos-Agent-Setup.exe',
      { isDevelopment: true }
    )).toBeNull();
  });

  it('admite HTTP solamente para loopback en desarrollo', () => {
    expect(resolvePrintAgentDownloadUrl(
      'http://127.0.0.1:8000/Walos-Agent-Setup.exe',
      { isDevelopment: true }
    )).toBe('http://127.0.0.1:8000/Walos-Agent-Setup.exe');

    expect(resolvePrintAgentDownloadUrl(
      'http://127.0.0.1:8000/Walos-Agent-Setup.exe',
      { isDevelopment: false }
    )).toBeNull();
  });

  it('rechaza credenciales embebidas y esquemas ejecutables', () => {
    expect(resolvePrintAgentDownloadUrl(
      'https://user:secret@example.test/Walos-Agent-Setup.exe',
      { isDevelopment: false }
    )).toBeNull();
    expect(resolvePrintAgentDownloadUrl('javascript:alert(1)', { isDevelopment: false })).toBeNull();
  });
});
