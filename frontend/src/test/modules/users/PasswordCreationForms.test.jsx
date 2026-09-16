import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import UserFormModal from '../../../modules/users/components/UserFormModal';
import CreateTenantModal from '../../../modules/admin/components/CreateTenantModal';

vi.mock('@tanstack/react-query', () => ({
  useQuery: () => ({ data: { data: [{ id: 3, name: 'Cashier' }] } }),
}));
vi.mock('../../../stores/authStore', () => ({
  default: () => ({
    tenantId: 10,
    user: { id: 7, role: 'manager', isPlatformAdmin: false },
  }),
}));
vi.mock('../../../services/userService', () => ({ default: {} }));
vi.mock('../../../services/adminService', () => ({ default: {} }));

describe('password policy in creation forms', () => {
  beforeEach(() => vi.clearAllMocks());

  it('rejects a weak password in UserFormModal before saving', () => {
    const onSave = vi.fn();
    render(<UserFormModal user={null} onSave={onSave} onClose={vi.fn()} />);

    fireEvent.change(screen.getByPlaceholderText('Carlos'), { target: { value: 'New' } });
    fireEvent.change(screen.getByPlaceholderText(/López/), { target: { value: 'User' } });
    fireEvent.change(screen.getByPlaceholderText('usuario@comercio.com'), { target: { value: 'new@test.local' } });
    fireEvent.change(screen.getByPlaceholderText(/Mínimo 8/), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: 'Crear usuario' }));

    expect(onSave).not.toHaveBeenCalled();
    expect(screen.getByText(/Incluí mayúscula/)).toBeInTheDocument();
  });

  it('rejects a weak tenant administrator password before creating the tenant', () => {
    const onCreated = vi.fn();
    render(<CreateTenantModal isOpen onClose={vi.fn()} onCreated={onCreated} />);

    fireEvent.click(screen.getByRole('button', { name: /Siguiente/ }));
    fireEvent.click(screen.getByRole('button', { name: /Siguiente/ }));
    fireEvent.change(screen.getByPlaceholderText(/Mínimo 8/), { target: { value: 'password123' } });
    fireEvent.click(screen.getByRole('button', { name: /Siguiente/ }));
    fireEvent.click(screen.getByRole('button', { name: /Crear Comercio/ }));

    expect(onCreated).not.toHaveBeenCalled();
    expect(screen.getByText(/Incluí mayúscula/)).toBeInTheDocument();
  });
});
