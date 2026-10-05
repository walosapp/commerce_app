import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import userService from '../../services/userService';

vi.mock('../../config/api', () => ({
  default: { get: vi.fn(), post: vi.fn() },
}));

describe('userService tenant password reset', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.post.mockResolvedValue({ data: { success: true } });
    api.get.mockResolvedValue({ data: { data: [] } });
  });

  it('uses the tenant-scoped users endpoint without accepting a company id from the UI', async () => {
    await userService.resetPassword(17, 'Changed2@');

    expect(api.post).toHaveBeenCalledWith('/users/17/reset-password', {
      newPassword: 'Changed2@',
    });
  });

  it('loads only active branch options from the tenant users API', async () => {
    await userService.getBranches();

    expect(api.get).toHaveBeenCalledWith('/users/branches');
  });
});
