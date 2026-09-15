import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CompanyFeaturesPanel from '../../../modules/admin/components/CompanyFeaturesPanel';

const { getAdminCompanyFeatures, updateAdminCompanyFeature } = vi.hoisted(() => ({
  getAdminCompanyFeatures: vi.fn(),
  updateAdminCompanyFeature: vi.fn(),
}));

vi.mock('../../../services/platformService', () => ({
  default: { getAdminCompanyFeatures, updateAdminCompanyFeature },
}));

const renderPanel = () => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <CompanyFeaturesPanel companyId={25} companyName="Comercio Demo" onClose={vi.fn()} />
    </QueryClientProvider>,
  );
};

describe('CompanyFeaturesPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAdminCompanyFeatures.mockResolvedValue({
      data: [
        { code: 'dashboard', name: 'Dashboard', isEnabled: true, isMandatory: true, displayOrder: 1 },
        { code: 'restaurant', name: 'Restaurante', isEnabled: true, isMandatory: false, displayOrder: 3 },
        { code: 'pos', name: 'POS', isEnabled: false, isMandatory: false, displayOrder: 4 },
      ],
    });
    updateAdminCompanyFeature.mockResolvedValue({ data: { code: 'pos', isEnabled: true } });
  });

  it('shows all company features and prevents disabling dashboard', async () => {
    renderPanel();

    expect(await screen.findByText('Módulos de Comercio Demo')).toBeInTheDocument();
    expect(await screen.findByRole('checkbox', { name: 'Dashboard' })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: 'POS' })).not.toBeChecked();
  });

  it('persists an optional feature toggle through the platform endpoint', async () => {
    renderPanel();

    fireEvent.click(await screen.findByRole('checkbox', { name: 'POS' }));

    await waitFor(() => expect(updateAdminCompanyFeature).toHaveBeenCalledWith(25, 'pos', true));
  });
});
