import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CashPage from '../../../modules/cash/CashPage';

const mocks = vi.hoisted(() => ({
  auth: {},
  features: { canAccess: vi.fn() },
  getActive: vi.fn(), open: vi.fn(), close: vi.fn(), addMovement: vi.fn(),
  getHistory: vi.fn(), getMovements: vi.fn(), getZReport: vi.fn(), printCashClose: vi.fn(),
  success: vi.fn(), error: vi.fn(),
}));

vi.mock('../../../stores/authStore', () => ({
  default: (selector) => selector ? selector(mocks.auth) : mocks.auth,
}));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => mocks.features }));
vi.mock('../../../services/cashRegisterService', () => ({ cashRegisterService: mocks }));
vi.mock('../../../services/printService', () => ({ default: { getZReport: mocks.getZReport } }));
vi.mock('../../../stores/printAgentStore', () => ({
  default: (selector) => selector({ printCashClose: mocks.printCashClose }),
}));
vi.mock('react-hot-toast', () => ({ default: { success: mocks.success, error: mocks.error } }));
vi.mock('../../../modules/sales/components/ZReportPrint', () => ({ default: () => null }));

const register = {
  id: 81, openedBy: 99, openedByName: 'Otro cajero', branchName: 'Principal',
  openedAt: new Date().toISOString(), openingAmount: 10000, totalSales: 25000,
  totalCashSales: 15000, totalCardSales: 10000, totalTransferSales: 0,
  cashIn: 1000, cashOut: 2000, totalDiscounts: 0, orderCount: 2,
};

function mount({ cached = false } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  if (cached) client.setQueryData(['cash-register-active', 3, 7], { data: register });
  const view = render(<QueryClientProvider client={client}><CashPage /></QueryClientProvider>);
  return { ...view, client };
}

beforeEach(() => {
  vi.clearAllMocks();
  Object.assign(mocks.auth, {
    isAuthenticated: true, tenantId: 3, branchId: 7,
    user: { id: 5, name: 'Cajero actual', branchName: 'Principal', role: 'cashier', isPlatformAdmin: false },
  });
  mocks.features.canAccess.mockReturnValue(true);
  mocks.getActive.mockResolvedValue({ data: null });
  mocks.open.mockResolvedValue({ data: register });
  mocks.close.mockResolvedValue({ data: { ...register, status: 'closed' } });
  mocks.addMovement.mockResolvedValue({ data: {} });
  mocks.getHistory.mockResolvedValue({ data: [], count: 0 });
  mocks.getMovements.mockResolvedValue({ data: [] });
  vi.spyOn(window, 'confirm').mockReturnValue(false);
});

describe('HU1: pantalla propia de Caja', () => {
  it.each(['cashier', 'manager', 'super_admin'])('permite %s sin montar queries ni pestañas de Restaurante', async (role) => {
    mocks.auth.user.role = role;
    const { client } = mount();
    expect(await screen.findByRole('button', { name: 'Abrir Caja' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Caja' })).toBeInTheDocument();
    expect(client.getQueryCache().getAll().map(q => q.queryKey)).toEqual([['cash-register-active', 3, 7]]);
    expect(screen.queryByText('Mesas')).not.toBeInTheDocument();
    expect(screen.queryByText('Créditos')).not.toBeInTheDocument();
  });

  it.each(['waiter', 'platform_admin', 'unknown'])('no consulta ni muestra caja al rol %s aunque haya cache', (role) => {
    mocks.auth.user.role = role;
    mocks.auth.user.isPlatformAdmin = role === 'platform_admin';
    mount({ cached: true });
    expect(screen.getByRole('alert')).toHaveTextContent('No tenés acceso');
    expect(mocks.getActive).not.toHaveBeenCalled();
    expect(screen.queryByText('Ventas totales')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
  });

  it.each(['tenantId', 'branchId'])('requiere %s y no ofrece apertura sin contexto', (field) => {
    mocks.auth[field] = null;
    mount();
    expect(screen.getByRole('alert')).toHaveTextContent('sucursal válida');
    expect(mocks.getActive).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
  });

  it('no consulta caja con feature deshabilitada ni sesión anónima', () => {
    mocks.features.canAccess.mockReturnValue(false);
    const view = mount();
    expect(mocks.getActive).not.toHaveBeenCalled();
    mocks.features.canAccess.mockReturnValue(true);
    mocks.auth.isAuthenticated = false;
    view.rerender(<QueryClientProvider client={view.client}><CashPage /></QueryClientProvider>);
    expect(mocks.getActive).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
  });

  it('bloquea apertura mientras consulta y permite retry después de error', async () => {
    let reject;
    mocks.getActive.mockReturnValueOnce(new Promise((_, rej) => { reject = rej; }));
    mount();
    expect(screen.getByText('Consultando estado de caja...')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
    reject(new Error('Red no disponible'));
    fireEvent.click(await screen.findByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByRole('button', { name: 'Abrir Caja' })).toBeInTheDocument();
    expect(mocks.getActive).toHaveBeenCalledTimes(2);
  });

  it('abre caja y actualiza la query compartida con Restaurante/POS', async () => {
    const { client } = mount();
    const invalidate = vi.spyOn(client, 'invalidateQueries');
    fireEvent.click(await screen.findByRole('button', { name: 'Abrir Caja' }));
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '10000' } });
    mocks.getActive.mockResolvedValue({ data: register });
    fireEvent.click(screen.getAllByRole('button', { name: 'Abrir Caja' }).at(-1));
    expect(await screen.findByText('Turno actual')).toBeInTheDocument();
    expect(mocks.open).toHaveBeenCalledWith({ openingAmount: 10000, notes: null });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cash-register-active'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cash-register-status'] });
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['cash-register-history'] });
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
  });

  it('muestra caja de otro operador y conserva el cálculo de efectivo esperado', async () => {
    mocks.getActive.mockResolvedValue({ data: register });
    mount();
    expect(await screen.findByText('Turno actual')).toBeInTheDocument();
    expect(screen.getByText(/Otro cajero/)).toBeInTheDocument();
    const summary = screen.getByText('Efectivo esperado en caja').parentElement;
    expect(within(summary).getByText(/24\.000/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir Caja' })).not.toBeInTheDocument();
  });

  it.each([['Entrada', 'in'], ['Salida', 'out']])('registra %s en la caja activa', async (button, type) => {
    mocks.getActive.mockResolvedValue({ data: register });
    mount();
    fireEvent.click(await screen.findByRole('button', { name: button }));
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '1000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Otro...' }));
    fireEvent.change(screen.getByPlaceholderText('Describe el motivo'), { target: { value: 'Ajuste de turno' } });
    expect(screen.getByRole('spinbutton').checkValidity()).toBe(true);
    fireEvent.click(screen.getByRole('button', { name: `Registrar ${button}` }));
    await waitFor(() => expect(mocks.addMovement).toHaveBeenCalledWith(81, {
      type, amount: 1000, reason: 'Ajuste de turno', notes: null,
    }));
    await waitFor(() => expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument());
  });

  it('abre historial de turnos sin consultas de Restaurante', async () => {
    const { client } = mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Historial' }));
    expect(await screen.findByText('Sin turnos registrados')).toBeInTheDocument();
    expect(mocks.getHistory).toHaveBeenCalledWith({ dateFrom: undefined, dateTo: undefined });
    expect(client.getQueryCache().getAll().every(q => q.queryKey[0].startsWith('cash-register-'))).toBe(true);
    expect(client.getQueryCache().find({ queryKey: ['cash-register-history', 3, 7, '', ''], exact: true })).toBeDefined();
  });

  it('oculta datos y desmonta historial al cambiar a mesero con cache financiero', async () => {
    mocks.getActive.mockResolvedValue({ data: register });
    const view = mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Historial' }));
    expect(await screen.findByText('Sin turnos registrados')).toBeInTheDocument();
    mocks.auth.user = { ...mocks.auth.user, role: 'waiter' };
    view.rerender(<QueryClientProvider client={view.client}><CashPage /></QueryClientProvider>);
    expect(screen.getByRole('alert')).toHaveTextContent('No tenés acceso');
    expect(screen.queryByText('Turno actual')).not.toBeInTheDocument();
    expect(screen.queryByText('Historial de Cajas')).not.toBeInTheDocument();
    expect(view.client.getQueryData(['cash-register-active', 3, 7])).toEqual({ data: register });
  });

  it('resetea modales y no hereda resumen al cambiar de sucursal', async () => {
    mocks.getActive.mockResolvedValue({ data: register });
    const view = mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Cerrar Caja' }));
    expect(screen.getByRole('spinbutton')).toBeInTheDocument();
    mocks.auth.branchId = 8;
    mocks.getActive.mockResolvedValue({ data: null });
    view.rerender(<QueryClientProvider client={view.client}><CashPage /></QueryClientProvider>);
    expect(await screen.findByRole('button', { name: 'Abrir Caja' })).toBeInTheDocument();
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
    expect(screen.queryByText('Turno actual')).not.toBeInTheDocument();
    expect(view.client.getQueryCache().find({ queryKey: ['cash-register-active', 3, 8], exact: true })).toBeDefined();
  });

  it('cierra una sola vez; fallo de impresión no revierte el cierre', async () => {
    mocks.getActive.mockResolvedValue({ data: register });
    window.confirm.mockReturnValue(true);
    mocks.getZReport.mockResolvedValue({ data: { id: 81 } });
    mocks.printCashClose.mockRejectedValue(new Error('Impresora desconectada'));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Cerrar Caja' }));
    fireEvent.change(screen.getByRole('spinbutton'), { target: { value: '24000' } });
    mocks.getActive.mockResolvedValue({ data: null });
    fireEvent.click(screen.getAllByRole('button', { name: 'Cerrar Caja' }).at(-1));
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('Impresora desconectada'));
    expect(mocks.close).toHaveBeenCalledTimes(1);
    expect(mocks.close).toHaveBeenCalledWith(81, { closingAmount: 24000, notes: null });
    expect(mocks.getZReport).toHaveBeenCalledWith(81);
    expect(mocks.printCashClose).toHaveBeenCalledWith({ id: 81 });
    expect(mocks.success).toHaveBeenCalledWith('Caja cerrada exitosamente');
    expect(screen.queryByRole('spinbutton')).not.toBeInTheDocument();
  });

  it('conserva modal y muestra error si falla apertura', async () => {
    mocks.open.mockRejectedValue(new Error('No fue posible abrir'));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Abrir Caja' }));
    fireEvent.click(screen.getAllByRole('button', { name: 'Abrir Caja' }).at(-1));
    await waitFor(() => expect(mocks.error).toHaveBeenCalledWith('No fue posible abrir'));
    expect(screen.getByRole('spinbutton')).toBeInTheDocument();
  });
});
