import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import UsersPage from '../../../modules/users/UsersPage';

const { resetPassword, toast } = vi.hoisted(() => ({
  resetPassword: vi.fn(),
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('@tanstack/react-query', () => ({
  useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  useQuery: ({ queryKey }) => queryKey[0] === 'users'
    ? {
        data: {
          data: [
            { id: 7, firstName: 'Current', lastName: 'Manager', email: 'manager@test.local', roleCode: 'manager', isActive: true },
            { id: 8, firstName: 'Target', lastName: 'Cashier', email: 'cashier@test.local', roleCode: 'cashier', isActive: true },
          ],
        },
        isLoading: false,
        refetch: vi.fn(),
      }
    : { data: { data: [] }, isLoading: false, refetch: vi.fn() },
}));
vi.mock('../../../stores/authStore', () => ({
  default: () => ({
    tenantId: 10,
    user: { id: 7, role: 'manager', email: 'manager@test.local' },
  }),
}));
vi.mock('../../../services/userService', () => ({
  default: {
    getAll: vi.fn(),
    resetPassword,
  },
}));
vi.mock('../../../services/adminService', () => ({ default: { getTenants: vi.fn() } }));
vi.mock('../../../modules/users/components/UserFormModal', () => ({ default: () => null }));
vi.mock('react-hot-toast', () => ({ default: toast }));

describe('UsersPage tenant password reset', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    resetPassword.mockResolvedValue({ success: true });
  });

  it('uses tenant reset for another user and directs self reset to Perfil', async () => {
    render(<UsersPage />);

    expect(screen.getByTitle("Usa 'Cambiar mi contraseña' en tu perfil")).toBeDisabled();
    fireEvent.click(screen.getByTitle('Resetear contraseña'));
    fireEvent.change(screen.getByLabelText('Nueva contraseña'), { target: { value: 'Changed2@' } });
    fireEvent.change(screen.getByLabelText('Confirmar contraseña'), { target: { value: 'Changed2@' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(resetPassword).toHaveBeenCalledWith(8, 'Changed2@'));
    expect(toast.success).toHaveBeenCalledWith('Contraseña actualizada');
  });

  it('catches a reset failure in the modal without closing or rethrowing it', async () => {
    resetPassword.mockRejectedValue({ response: { data: { message: 'Reset rejected' } } });
    render(<UsersPage />);

    fireEvent.click(screen.getByTitle('Resetear contraseña'));
    fireEvent.change(screen.getByLabelText('Nueva contraseña'), { target: { value: 'Changed2@' } });
    fireEvent.change(screen.getByLabelText('Confirmar contraseña'), { target: { value: 'Changed2@' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Reset rejected'));
    expect(screen.getByLabelText('Nueva contraseña')).toBeInTheDocument();
    expect(toast.success).not.toHaveBeenCalled();
  });
});
