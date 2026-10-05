import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import UserFormModal from '../../../modules/users/components/UserFormModal';

const mocks = vi.hoisted(() => ({
  queryConfigs: [],
}));

vi.mock('@tanstack/react-query', () => ({
  useQuery: (config) => {
    mocks.queryConfigs.push(config);
    if (config.queryKey[0] === 'user-roles') {
      return {
        data: {
          data: [
            { id: 2, code: 'manager', name: 'Manager' },
            { id: 3, code: 'cashier', name: 'Cashier' },
            { id: 4, code: 'waiter', name: 'Waiter' },
          ],
        },
      };
    }
    if (config.queryKey[0] === 'user-branches') {
      return { data: { data: [{ id: 20, name: 'Centro' }] }, isLoading: false };
    }
    return { data: { data: [] }, isLoading: false };
  },
}));

vi.mock('../../../stores/authStore', () => ({
  default: () => ({
    tenantId: 10,
    user: { id: 7, role: 'manager', isPlatformAdmin: false },
  }),
}));
vi.mock('../../../services/userService', () => ({
  default: { getRoles: vi.fn(), getBranches: vi.fn() },
}));
vi.mock('../../../services/platformService', () => ({
  default: { getAdminBranches: vi.fn() },
}));

const fillRequiredFields = (roleName) => {
  fireEvent.change(screen.getByPlaceholderText('Carlos'), { target: { value: 'New' } });
  fireEvent.change(screen.getByPlaceholderText(/López/), { target: { value: 'User' } });
  fireEvent.change(screen.getByPlaceholderText('usuario@comercio.com'), { target: { value: 'new@test.local' } });
  fireEvent.change(screen.getByPlaceholderText(/Mínimo 8/), { target: { value: 'Changed2@' } });
  fireEvent.change(screen.getAllByRole('combobox')[0], {
    target: { value: roleName === 'Waiter' ? '4' : roleName === 'Cashier' ? '3' : '2' },
  });
};

describe('UserFormModal branch assignment', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.queryConfigs.length = 0;
  });

  it.each(['Waiter', 'Cashier'])('rejects creating %s without a branch', async (roleName) => {
    const onSave = vi.fn();
    render(<UserFormModal user={null} onSave={onSave} onClose={vi.fn()} />);
    fillRequiredFields(roleName);

    fireEvent.click(screen.getByRole('button', { name: 'Crear usuario' }));

    expect(await screen.findByText('Selecciona una sucursal para el rol operativo')).toBeInTheDocument();
    expect(onSave).not.toHaveBeenCalled();
  });

  it.each(['Manager', 'Cashier', 'Waiter'])('submits %s with an explicit active branch', async (roleName) => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(<UserFormModal user={null} onSave={onSave} onClose={vi.fn()} />);
    fillRequiredFields(roleName);
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: '20' } });

    fireEvent.click(screen.getByRole('button', { name: 'Crear usuario' }));

    await waitFor(() => expect(onSave).toHaveBeenCalledWith(expect.objectContaining({ branchId: 20 })));
  });
});
