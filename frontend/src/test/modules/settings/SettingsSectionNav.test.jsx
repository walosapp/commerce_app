import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import SettingsSectionNav from '../../../modules/settings/components/SettingsSectionNav';

const { featureState, authState } = vi.hoisted(() => ({
  featureState: { canAccess: vi.fn() },
  authState: { user: { role: 'manager', isPlatformAdmin: false } },
}));

vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));
vi.mock('../../../stores/authStore', () => ({ default: () => authState }));

describe('SettingsSectionNav features', () => {
  beforeEach(() => vi.clearAllMocks());

  it('does not expose AI settings when AI is disabled', () => {
    featureState.canAccess.mockReturnValue(false);
    render(<MemoryRouter><SettingsSectionNav activeSection="branding" /></MemoryRouter>);
    expect(screen.queryByRole('link', { name: /^IA$/i })).not.toBeInTheDocument();
  });

  it('exposes AI settings when role and company feature allow it', () => {
    featureState.canAccess.mockImplementation((code) => code === 'ai');
    render(<MemoryRouter><SettingsSectionNav activeSection="ai" /></MemoryRouter>);
    expect(screen.getByRole('link', { name: /IA/i })).toBeInTheDocument();
  });
});
