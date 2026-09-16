import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import LoginPage from '../../../modules/auth/LoginPage';

const { login, setAuth, navigate } = vi.hoisted(() => ({ login: vi.fn(), setAuth: vi.fn(), navigate: vi.fn() }));

vi.mock('react-router-dom', async (importOriginal) => ({
  ...(await importOriginal()),
  useNavigate: () => navigate,
}));
vi.mock('../../../stores/authStore', () => ({ default: () => ({ setAuth }) }));
vi.mock('../../../services/authService', () => ({ default: { login } }));
vi.mock('react-hot-toast', () => ({ default: { success: vi.fn(), error: vi.fn() } }));

const renderLogin = () => render(<MemoryRouter><LoginPage /></MemoryRouter>);

const submitCredentials = (username = 'dev@walos.app') => {
  fireEvent.change(screen.getByLabelText(/usuario/i), { target: { value: username } });
  fireEvent.change(document.getElementById('password'), { target: { value: 'secreto' } });
  fireEvent.submit(document.querySelector('form'));
};

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setAuth.mockReturnValue(true);
  });

  it('renders login form without development credentials', () => {
    renderLogin();
    expect(screen.getByLabelText(/usuario/i)).toBeInTheDocument();
    expect(document.getElementById('password')).toBeInTheDocument();
    expect(screen.getByText('Walos')).toBeInTheDocument();
    expect(screen.queryByText(/Credenciales de desarrollo/i)).not.toBeInTheDocument();
  });

  it('updates inputs and toggles password visibility', () => {
    renderLogin();
    const username = screen.getByLabelText(/usuario/i);
    const password = document.getElementById('password');
    fireEvent.change(username, { target: { value: 'usuario@example.test' } });
    fireEvent.change(password, { target: { value: 'clave-segura' } });
    expect(username).toHaveValue('usuario@example.test');
    expect(password).toHaveAttribute('type', 'password');
    fireEvent.click(screen.getAllByRole('button').find((button) => button.type === 'button'));
    expect(password).toHaveAttribute('type', 'text');
  });

  it('rejects an empty form before calling the backend', async () => {
    const toast = (await import('react-hot-toast')).default;
    renderLogin();
    fireEvent.submit(document.querySelector('form'));
    expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/^Ingresa usuario y/));
    expect(login).not.toHaveBeenCalled();
  });

  it('consumes trusted dev isPlatformAdmin but enters the operational shell', async () => {
    const user = { id: 1, name: 'Dev', role: 'dev', companyId: 1, branchId: 1, isPlatformAdmin: true };
    login.mockResolvedValue({ success: true, data: { token: 'jwt', user } });
    renderLogin();
    submitCredentials();

    await waitFor(() => expect(setAuth).toHaveBeenCalledWith({ token: 'jwt', user }));
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('uses isPlatformAdmin to keep platform_admin in the platform shell', async () => {
    const user = { id: 2, name: 'Plataforma', role: 'platform_admin', companyId: 1, branchId: 1, isPlatformAdmin: true };
    login.mockResolvedValue({ success: true, data: { token: 'jwt-platform', user } });
    renderLogin();
    submitCredentials('platform@walos.app');

    await waitFor(() => expect(setAuth).toHaveBeenCalledWith({ token: 'jwt-platform', user }));
    expect(navigate).toHaveBeenCalledWith('/admin/tenants');
  });

  it('resolves an operator landing against enabled tenant features after login', async () => {
    const user = { id: 3, name: 'Caja', role: 'cashier', companyId: 1, branchId: 1, isPlatformAdmin: false };
    login.mockResolvedValue({ success: true, data: { token: 'jwt-cashier', user } });
    renderLogin();
    submitCredentials('cashier@walos.app');

    await waitFor(() => expect(setAuth).toHaveBeenCalledWith({ token: 'jwt-cashier', user }));
    expect(navigate).toHaveBeenCalledWith('/landing');
  });

});
