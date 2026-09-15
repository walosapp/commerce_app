import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import platformService from '../../services/platformService';

vi.mock('../../config/api', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
}));

describe('platformService company features', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.get.mockResolvedValue({ data: { data: [] } });
    api.put.mockResolvedValue({ data: { success: true } });
    api.post.mockResolvedValue({ data: { success: true } });
  });

  it('scopes every branch operation to the selected company', async () => {
    await platformService.getAdminBranches(25);
    await platformService.createAdminBranch(25, { name: 'Norte' });
    await platformService.updateAdminBranch(25, 9, { isActive: false });

    expect(api.get).toHaveBeenCalledWith('/platform/admin/companies/25/branches');
    expect(api.post).toHaveBeenCalledWith('/platform/admin/companies/25/branches', { name: 'Norte' });
    expect(api.put).toHaveBeenCalledWith('/platform/admin/companies/25/branches/9', { isActive: false });
  });

  it('loads the feature catalog and a tenant feature assignment separately', async () => {
    await platformService.getAdminFeatures();
    await platformService.getAdminCompanyFeatures(25);

    expect(api.get).toHaveBeenNthCalledWith(1, '/platform/admin/features');
    expect(api.get).toHaveBeenNthCalledWith(2, '/platform/admin/companies/25/features');
  });

  it('persists only the requested tenant feature state', async () => {
    await platformService.updateAdminCompanyFeature(25, 'pos', true);

    expect(api.put).toHaveBeenCalledWith(
      '/platform/admin/companies/25/features/pos',
      { isEnabled: true },
    );
  });
});
