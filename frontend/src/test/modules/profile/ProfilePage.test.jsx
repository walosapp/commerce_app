import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import ProfilePage from '../../../modules/profile/ProfilePage';

const { changePassword, updateTokens, toast } = vi.hoisted(() => ({
  changePassword: vi.fn(),
  updateTokens: vi.fn(),
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../../services/authService', () => ({
  default: { changePassword },
}));
vi.mock('../../../stores/authStore', () => ({
  default: (selector) => selector({
    user: { email: 'cashier@walos.app', role: 'cashier' },
    updateTokens,
  }),
}));
vi.mock('react-hot-toast', () => ({ default: toast }));

const fill = (current, next, confirmation) => {
  fireEvent.change(screen.getByLabelText('Contraseña actual'), { target: { value: current } });
  fireEvent.change(screen.getByLabelText('Nueva contraseña'), { target: { value: next } });
  fireEvent.change(screen.getByLabelText('Confirmar nueva contraseña'), { target: { value: confirmation } });
};

describe('ProfilePage password change', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    changePassword.mockResolvedValue({
      success: true,
      data: { token: 'new-access', refreshToken: 'new-refresh' },
    });
    updateTokens.mockReturnValue(true);
  });

  it('is available to an operational user and keeps the current UI session after success', async () => {
    render(<ProfilePage />);
    fill('Current1!', 'Changed2@', 'Changed2@');

    fireEvent.click(screen.getByRole('button', { name: 'Actualizar contraseña' }));

    await waitFor(() => expect(changePassword).toHaveBeenCalledWith(
      'Current1!', 'Changed2@', 'Changed2@'));
    expect(updateTokens).toHaveBeenCalledWith({ token: 'new-access', refreshToken: 'new-refresh' });
    expect(toast.success).toHaveBeenCalledWith('Contraseña actualizada');
    expect(screen.getByLabelText('Contraseña actual')).toHaveValue('');
  });

  it('rejects mismatch and weak passwords before calling the API', () => {
    render(<ProfilePage />);
    fill('Current1!', 'weak', 'different');

    fireEvent.click(screen.getByRole('button', { name: 'Actualizar contraseña' }));

    expect(changePassword).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('La confirmación de contraseña no coincide');
  });
});
