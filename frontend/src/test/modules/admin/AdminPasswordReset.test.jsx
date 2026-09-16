import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import AdminUsersPage from '../../../modules/admin/AdminUsersPage';
import EditTenantModal from '../../../modules/admin/components/EditTenantModal';

const { adminGetAll, adminResetPassword, getTenants, resetTenantPassword, toast } = vi.hoisted(() => ({
  adminGetAll: vi.fn(),
  adminResetPassword: vi.fn(),
  getTenants: vi.fn(),
  resetTenantPassword: vi.fn(),
  toast: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../../../services/userService', () => ({
  default: { adminGetAll, adminResetPassword },
}));
vi.mock('../../../services/adminService', () => ({
  default: { getTenants, resetTenantPassword },
}));
vi.mock('react-hot-toast', () => ({ default: toast }));

const renderAdminUsers = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <AdminUsersPage />
    </QueryClientProvider>,
  );
};

describe('admin password reset forms', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getTenants.mockResolvedValue({ data: [] });
    adminGetAll.mockResolvedValue({
      data: [{
        id: 8,
        companyId: 10,
        companyName: 'Tenant',
        firstName: 'Target',
        lastName: 'User',
        email: 'target@test.local',
        roleCode: 'cashier',
        roleName: 'Cashier',
        isActive: true,
      }],
    });
  });

  it('enforces shared complexity and catches reset errors in AdminUsersPage', async () => {
    adminResetPassword.mockRejectedValue({ response: { data: { message: 'Reset rejected' } } });
    renderAdminUsers();
    fireEvent.click(await screen.findByTitle(/Resetear contrase/));
    const input = screen.getByPlaceholderText(/8/);

    fireEvent.change(input, { target: { value: 'short' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(adminResetPassword).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'Changed2@' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Reset rejected'));
    expect(screen.getByPlaceholderText(/8/)).toBeInTheDocument();
  });

  it('enforces shared complexity and catches reset errors in EditTenantModal', async () => {
    resetTenantPassword.mockRejectedValue({ response: { data: { message: 'Tenant reset rejected' } } });
    render(<EditTenantModal
      tenant={{ id: 10, name: 'Tenant', adminEmail: 'admin@test.local' }}
      onClose={vi.fn()}
      onSaved={vi.fn()}
    />);
    const input = screen.getByPlaceholderText(/8/);

    fireEvent.change(input, { target: { value: 'weakpass' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar' }));
    expect(resetTenantPassword).not.toHaveBeenCalled();

    fireEvent.change(input, { target: { value: 'Changed2@' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Tenant reset rejected'));
  });
});
