import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import CashPage from '../../../modules/cash/CashPage';

const mocks = vi.hoisted(() => ({
  auth: {}, canAccess: vi.fn(), getActive: vi.fn(),
  getCredits: vi.fn(), addPayment: vi.fn(), cancel: vi.fn(),
  getSummary: vi.fn(), getCompleted: vi.fn(), searchOrders: vi.fn(), exportOrders: vi.fn(),
  refund: vi.fn(), success: vi.fn(), error: vi.fn(),
}));
vi.mock('../../../stores/authStore', () => ({ default: (selector) => selector ? selector(mocks.auth) : mocks.auth }));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => ({ canAccess: mocks.canAccess }) }));
vi.mock('../../../services/cashRegisterService', () => ({ cashRegisterService: { getActive: mocks.getActive } }));
vi.mock('../../../services/creditService', () => ({ default: mocks }));
vi.mock('../../../services/salesService', () => ({ default: mocks }));
vi.mock('../../../services/refundService', () => ({ default: { create: mocks.refund } }));
vi.mock('../../../stores/printAgentStore', () => ({ default: (selector) => selector({ printCashClose: vi.fn() }) }));
vi.mock('react-hot-toast', () => ({ default: { success: mocks.success, error: mocks.error } }));
// Exercise the real report components; existing receipt/refund tests cover their modals.
vi.mock('../../../modules/sales/components/OrderItemsList', () => ({ default: ({ orderId }) => <div>Detalle {orderId}</div> }));
vi.mock('../../../modules/sales/components/ReceiptPreview', () => ({
  default: ({ orderId, onClose }) => <div role="dialog">Recibo {orderId}<button onClick={onClose}>Cerrar recibo</button></div>,
}));
vi.mock('../../../modules/sales/components/RefundModal', () => ({
  default: ({ isOpen, order, onConfirm }) => isOpen && <button onClick={() => onConfirm({ orderId: order.id, reason: 'UAT' })}>Confirmar devolución</button>,
}));

const credit = { id: 21, orderId: 41, customerName: 'Cliente crédito', orderNumber: 'ORD-41', status: 'pending',
  createdAt: '2026-10-05T12:00:00Z', creditAmount: 5000, originalTotal: 10000, amountPaid: 5000 };
const order = { id: 41, orderNumber: 'ORD-41', tableName: 'Mesa 1', createdAt: '2026-10-05T12:00:00Z',
  finalTotalPaid: 10000, subtotal: 10000, discountAmount: 0, status: 'completed', paymentMethod: 'cash' };
const summary = { totalRevenue: 10000, totalOrders: 1, averageTicket: 10000, totalDiscounts: 0, totalCredits: 0, creditOrders: 0 };
const clients = [];
function mount() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  clients.push(client);
  const view = render(<QueryClientProvider client={client}><CashPage /></QueryClientProvider>);
  return { ...view, client, refresh: () => view.rerender(<QueryClientProvider client={client}><CashPage /></QueryClientProvider>) };
}
const selectTab = (name) => fireEvent.click(screen.getByRole('tab', { name }));
const noReports = () => {
  for (const fn of [mocks.getCredits, mocks.getSummary, mocks.getCompleted, mocks.searchOrders]) expect(fn).not.toHaveBeenCalled();
};
beforeEach(() => {
  vi.clearAllMocks();
  Object.assign(mocks.auth, { isAuthenticated: true, tenantId: 3, branchId: 7,
    user: { id: 5, name: 'Cajero', role: 'cashier', branchName: 'Principal', isPlatformAdmin: false } });
  mocks.canAccess.mockReturnValue(true);
  mocks.getActive.mockResolvedValue({ data: null });
  mocks.getCredits.mockResolvedValue({ data: [credit] });
  mocks.addPayment.mockResolvedValue({ data: {} });
  mocks.cancel.mockResolvedValue({ data: {} });
  mocks.getSummary.mockResolvedValue({ data: summary });
  mocks.getCompleted.mockResolvedValue({ data: [order] });
  mocks.searchOrders.mockResolvedValue({ data: [order], count: 1 });
  mocks.exportOrders.mockResolvedValue(new Blob(['order,total\n41,10000']));
  mocks.refund.mockResolvedValue({ data: {} });
});
afterEach(() => {
  clients.splice(0).forEach(client => client.clear());
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

describe('HU3: reportes separados en Caja', () => {
  it.each(['cashier', 'manager', 'super_admin'])('ofrece las tres vistas independientes a %s sin consultarlas al entrar', async (role) => {
    mocks.auth.user.role = role;
    const { client } = mount();
    expect(await screen.findByText('Sin turno abierto')).toBeInTheDocument();
    expect(screen.getAllByRole('tab').map(tab => tab.textContent)).toEqual(['Turno', 'Créditos', 'Ventas', 'Historial']);
    expect(screen.getByRole('tab', { name: 'Turno' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('button', { name: 'Turnos de caja' })).toBeInTheDocument();
    noReports();
    expect(client.getQueryCache().getAll().map(q => q.queryKey)).toEqual([['cash-register-active', 3, 7]]);
  });

  it('consulta solamente la vista seleccionada y no unifica Ventas con Historial', async () => {
    const { client } = mount();
    selectTab('Créditos');
    expect(await screen.findByText('Cliente crédito')).toBeInTheDocument();
    expect(mocks.getCredits).toHaveBeenCalledWith({ status: 'pending', search: undefined });
    expect(mocks.getSummary).not.toHaveBeenCalled();
    expect(mocks.searchOrders).not.toHaveBeenCalled();
    selectTab('Ventas');
    expect(await screen.findByText('Ingresos del día')).toBeInTheDocument();
    expect(screen.queryByPlaceholderText('Buscar cliente...')).not.toBeInTheDocument();
    expect(mocks.getSummary).toHaveBeenCalledWith(7, expect.any(String));
    expect(mocks.getCompleted).toHaveBeenCalledWith(7, expect.any(String));
    expect(mocks.searchOrders).not.toHaveBeenCalled();
    selectTab('Historial');
    expect(await screen.findByText('ORD-41')).toBeInTheDocument();
    expect(screen.queryByText('Ingresos del día')).not.toBeInTheDocument();
    expect(mocks.searchOrders).toHaveBeenCalledWith({ branchId: 7, page: 1, limit: 20 });
    const keys = client.getQueryCache().getAll().map(q => q.queryKey);
    for (const prefix of ['credits', 'sales-summary', 'sales-completed', 'order-history']) {
      expect(keys.find(key => key[0] === prefix).slice(0, 3)).toEqual([prefix, 3, 7]);
    }
  });

  it('conserva filtros, búsqueda, abono y condonación de créditos', async () => {
    mount();
    selectTab('Créditos');
    fireEvent.click(await screen.findByRole('button', { name: /Cliente crédito/ }));
    fireEvent.change(screen.getByPlaceholderText('Monto'), { target: { value: '1000' } });
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'card' } });
    fireEvent.change(screen.getByPlaceholderText('Nota (opcional)'), { target: { value: 'Abono' } });
    fireEvent.click(screen.getByRole('button', { name: 'Abonar' }));
    await waitFor(() => expect(mocks.addPayment).toHaveBeenCalledWith(21, { amount: 1000, paymentMethod: 'card', notes: 'Abono' }));
    await waitFor(() => expect(mocks.success).toHaveBeenCalledWith('Abono registrado'));
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    fireEvent.click(screen.getByRole('button', { name: 'Condonar deuda' }));
    await waitFor(() => expect(mocks.cancel).toHaveBeenCalledWith(21));
    fireEvent.click(screen.getByRole('button', { name: 'Parcial' }));
    fireEvent.change(screen.getByPlaceholderText('Buscar cliente...'), { target: { value: 'Cliente' } });
    await waitFor(() => expect(mocks.getCredits).toHaveBeenCalledWith({ status: 'partial', search: 'Cliente' }));
    expect(mocks.searchOrders).not.toHaveBeenCalled();
  });

  it('conserva resumen diario, fecha, recibo y devolución en Ventas', async () => {
    const view = mount();
    selectTab('Ventas');
    fireEvent.click(await screen.findByRole('button', { name: /Mesa 1 ORD-41/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Imprimir recibo' }));
    expect(screen.getByRole('dialog')).toHaveTextContent('Recibo 41');
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar recibo' }));
    fireEvent.click(screen.getByRole('button', { name: 'Anular / Devolver' }));
    fireEvent.click(screen.getByRole('button', { name: 'Confirmar devolución' }));
    await waitFor(() => expect(mocks.refund).toHaveBeenCalledWith({ orderId: 41, reason: 'UAT' }));
    fireEvent.change(view.container.querySelector('input[type="date"]'), { target: { value: '2026-10-04' } });
    await waitFor(() => expect(mocks.getSummary).toHaveBeenCalledWith(7, '2026-10-04'));
    expect(mocks.searchOrders).not.toHaveBeenCalled();
  });

  it('conserva búsqueda, filtros, recibo y exportación CSV en Historial', async () => {
    const view = mount();
    selectTab('Historial');
    fireEvent.click(await screen.findByTitle('Imprimir recibo'));
    expect(screen.getByRole('dialog')).toHaveTextContent('Recibo 41');
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar recibo' }));
    fireEvent.change(screen.getByPlaceholderText('Buscar por # orden o mesa...'), { target: { value: 'ORD-41' } });
    fireEvent.click(screen.getByRole('button', { name: 'Filtros' }));
    fireEvent.change(view.container.querySelector('input[type="date"]'), { target: { value: '2026-10-01' } });
    fireEvent.change(screen.getAllByRole('combobox')[1], { target: { value: 'card' } });
    const params = { branchId: 7, page: 1, limit: 20, search: 'ORD-41', dateFrom: '2026-10-01', paymentMethod: 'card' };
    await waitFor(() => expect(mocks.searchOrders).toHaveBeenCalledWith(params));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeEnabled());
    const create = vi.fn(() => 'blob:test');
    const revoke = vi.fn();
    vi.stubGlobal('URL', { createObjectURL: create, revokeObjectURL: revoke });
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
    fireEvent.click(screen.getByRole('button', { name: 'Exportar CSV' }));
    await waitFor(() => expect(click).toHaveBeenCalledOnce());
    expect(mocks.exportOrders).toHaveBeenCalledWith(params);
    expect(revoke).toHaveBeenCalledWith('blob:test');
    expect(mocks.getSummary).not.toHaveBeenCalled();
  });

  it.each(['waiter', 'platform_admin'])('no monta reportes para %s aunque el cache tenga datos', (role) => {
    mocks.auth.user.role = role;
    mocks.auth.user.isPlatformAdmin = role === 'platform_admin';
    const client = new QueryClient();
    clients.push(client);
    client.setQueryData(['credits', 3, 7, 'pending', ''], { data: [credit] });
    render(<QueryClientProvider client={client}><CashPage /></QueryClientProvider>);
    expect(screen.getByRole('alert')).toHaveTextContent('No tenés acceso');
    expect(screen.queryByText('Cliente crédito')).not.toBeInTheDocument();
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    noReports();
  });

  it('habilita reportes en comercio POS sin Restaurante', async () => {
    mocks.canAccess.mockImplementation(code => ['cash', 'pos'].includes(code));
    mount();
    selectTab('Historial');
    expect(await screen.findByText('Historial de ventas')).toBeInTheDocument();
    await waitFor(() => expect(mocks.searchOrders).toHaveBeenCalled());
  });

  it('no ofrece reportes cuando solamente Caja está habilitada', async () => {
    mocks.canAccess.mockImplementation(code => code === 'cash');
    mount();
    expect(await screen.findByText('Sin turno abierto')).toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(1);
    noReports();
  });

  it('desmonta el reporte al revocar Restaurante/POS', async () => {
    const view = mount();
    selectTab('Créditos');
    expect(await screen.findByText('Cliente crédito')).toBeInTheDocument();
    mocks.canAccess.mockImplementation(code => code === 'cash');
    view.refresh();
    expect(screen.queryByText('Cliente crédito')).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Turno' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getAllByRole('tab')).toHaveLength(1);
  });

  it.each(['tenantId', 'branchId'])('resetea vista y no hereda créditos al cambiar %s', async (field) => {
    const view = mount();
    selectTab('Créditos');
    expect(await screen.findByText('Cliente crédito')).toBeInTheDocument();
    mocks.auth[field] += 1;
    mocks.getCredits.mockResolvedValue({ data: [] });
    view.refresh();
    expect(screen.queryByText('Cliente crédito')).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Turno' })).toHaveAttribute('aria-selected', 'true');
    selectTab('Créditos');
    expect(await screen.findByText('Sin créditos')).toBeInTheDocument();
    expect(view.client.getQueryData(['credits', 3, 7, 'pending', ''])).toEqual({ data: [credit] });
    expect(view.client.getQueryData(['credits', mocks.auth.tenantId, mocks.auth.branchId, 'pending', ''])).toEqual({ data: [] });
  });
});
