import { beforeEach, describe, expect, it, vi } from 'vitest';
import api from '../../config/api';
import authService from '../../services/authService';

vi.mock('../../config/api', () => ({
  default: { post: vi.fn() },
}));

describe('authService.changePassword', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    api.post.mockResolvedValue({ data: { success: true } });
  });

  it('sends only the password-change contract to the authenticated endpoint', async () => {
    await authService.changePassword('Current1!', 'Changed2@', 'Changed2@');

    expect(api.post).toHaveBeenCalledWith('/auth/change-password', {
      currentPassword: 'Current1!',
      newPassword: 'Changed2@',
      confirmPassword: 'Changed2@',
    });
  });
});
