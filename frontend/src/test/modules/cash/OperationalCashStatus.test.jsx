import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import RestaurantHeader from '../../../modules/sales/components/RestaurantHeader';
import PosHeader from '../../../modules/pos-deli/components/PosHeader';
import { canRoleAccessFeature } from '../../../config/companyFeatures';

const mocks = vi.hoisted(() => ({ auth: {}, features: {}, getStatus: vi.fn(), getActive: vi.fn() }));
vi.mock('../../../stores/authStore', () => ({ default: () => mocks.auth }));
vi.mock('../../../hooks/useCompanyFeatures', () => ({ default: () => mocks.features }));
vi.mock('../../../services/cashRegisterService', () => ({ cashRegisterService: { getStatus: mocks.getStatus, getActive: mocks.getActive } }));

const mount = (children = <RestaurantHeader />) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(['cash-register-active', 155, 91], { data: { status: 'open', totalSales: 999999, openingAmount: 50000 } });
  const view = render(<QueryClientProvider client={client}>{children}</QueryClientProvider>);
  return { client, ...view };
};

beforeEach(() => {
  vi.clearAllMocks();
  mocks.auth = { isAuthenticated: true, tenantId: 155, branchId: 91, user: { role: 'waiter', name: 'Mesero', isPlatformAdmin: false } };
  mocks.features = {
    isReady: true, isError: false, hasFeature: () => true,
    canAccess: code => canRoleAccessFeature(mocks.auth.user, code),
  };
  mocks.getStatus.mockResolvedValue({ data: { branchId: 91, status: 'open' } });
});

describe('HU2: estado mínimo operativo, sin API financiera', () => {
  it.each(['waiter', 'cashier', 'manager', 'super_admin'])('consulta solo estado para %s', async (role) => {
    mocks.auth.user.role = role;
    const { client } = mount();
    expect(await screen.findByText('Caja: ABIERTA')).toBeInTheDocument();
    expect(mocks.getStatus).toHaveBeenCalledOnce();
    expect(mocks.getActive).not.toHaveBeenCalled();
    expect(client.getQueryData(['cash-register-status', 155, 91])).toEqual({ data: { branchId: 91, status: 'open' } });
    expect(screen.queryByText(/999\.999|50\.000|Ventas totales/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Abrir|Cerrar|Entrada|Salida|Historial/ })).not.toBeInTheDocument();
  });

  it('POS utiliza la misma query de estado, no una caja completa', async () => {
    mocks.auth.user.role = 'cashier';
    mount(<><RestaurantHeader /><PosHeader /></>);
    await waitFor(() => expect(screen.getAllByText('Caja: ABIERTA')).toHaveLength(2));
    expect(mocks.getStatus).toHaveBeenCalledOnce();
    expect(mocks.getActive).not.toHaveBeenCalled();
  });

  it('distingue caja cerrada de respuesta inválida', async () => {
    mocks.getStatus.mockResolvedValue({ data: { branchId: 91, status: 'closed' } });
    mount();
    expect(await screen.findByText('Caja: CERRADA')).toBeInTheDocument();
  });

  it.each([null, { branchId: 92, status: 'open' }, { branchId: 91, status: 'unexpected' }])('no afirma un estado con payload inválido %j', async (data) => {
    mocks.getStatus.mockResolvedValue({ data });
    mount();
    expect(await screen.findByText('Caja: NO SE PUDO VERIFICAR')).toBeInTheDocument();
    expect(screen.queryByText('Caja: CERRADA')).not.toBeInTheDocument();
  });

  it('consulta en curso no se interpreta como caja cerrada', () => {
    mocks.getStatus.mockReturnValue(new Promise(() => {}));
    mount();
    expect(screen.getByText('Caja: CARGANDO')).toBeInTheDocument();
    expect(screen.queryByText('Caja: CERRADA')).not.toBeInTheDocument();
  });

  it('error de red permite retry sin estado financiero cacheado', async () => {
    mocks.getStatus.mockRejectedValueOnce(new Error('Red caída'));
    mount();
    fireEvent.click(await screen.findByRole('button', { name: 'Reintentar caja' }));
    expect(await screen.findByText('Caja: ABIERTA')).toBeInTheDocument();
    expect(mocks.getActive).not.toHaveBeenCalled();
  });

  it.each(['tenantId', 'branchId'])('no consulta sin %s', (field) => {
    mocks.auth[field] = null;
    mount();
    expect(screen.getByText('Caja: SIN CONTEXTO DE SUCURSAL')).toBeInTheDocument();
    expect(mocks.getStatus).not.toHaveBeenCalled();
  });

  it('no consulta feature apagada aunque haya cache de caja activa', () => {
    mocks.features.hasFeature = () => false;
    mount();
    expect(screen.getByText('Caja: NO HABILITADA')).toBeInTheDocument();
    expect(mocks.getStatus).not.toHaveBeenCalled();
  });

  it('error de features no se interpreta como caja cerrada ni como loading eterno', () => {
    mocks.features.isReady = false;
    mocks.features.isError = true;
    mocks.features.canAccess = () => false;
    mocks.features.hasFeature = () => false;
    mount();
    expect(screen.getByText('Caja: NO SE PUDO VERIFICAR')).toBeInTheDocument();
    expect(mocks.getStatus).not.toHaveBeenCalled();
  });

  it.each(['platform_admin', 'unknown'])('no consulta para %s aun con feature activa', (role) => {
    mocks.auth.user.role = role;
    mount();
    expect(mocks.getStatus).not.toHaveBeenCalled();
    expect(screen.queryByText('Caja: ABIERTA')).not.toBeInTheDocument();
  });

  it('no consulta sin autenticación', () => {
    mocks.auth.isAuthenticated = false;
    mount();
    expect(mocks.getStatus).not.toHaveBeenCalled();
    expect(screen.getByText('Caja: SIN SESIÓN')).toBeInTheDocument();
  });

  it('al cambiar sucursal no hereda el estado ni totales de la anterior', async () => {
    const view = mount();
    expect(await screen.findByText('Caja: ABIERTA')).toBeInTheDocument();
    mocks.auth.branchId = 92;
    mocks.getStatus.mockResolvedValue({ data: { branchId: 92, status: 'closed' } });
    view.rerender(<QueryClientProvider client={view.client}><RestaurantHeader /></QueryClientProvider>);
    expect(await screen.findByText('Caja: CERRADA')).toBeInTheDocument();
    expect(view.client.getQueryData(['cash-register-status', 155, 92])).toEqual({ data: { branchId: 92, status: 'closed' } });
  });
});

describe('Encabezado Restaurante con estilo POS sin buscador', () => {
  it.each(['waiter', 'cashier'])('muestra operador, sucursal y hora para %s sin resumen ni buscador', async (role) => {
    mocks.auth.user = { ...mocks.auth.user, role, name: 'Edwin Campo', branchName: 'Principal' };
    const { container } = mount();
    expect(screen.getByRole('heading', { name: 'Restaurante' })).toBeInTheDocument();
    expect(screen.getByText('Edwin Campo')).toBeInTheDocument();
    expect(screen.getByText('Principal')).toBeInTheDocument();
    expect(screen.getByText(/Operador:/)).toBeInTheDocument();
    expect(container.querySelector('time')).toHaveAttribute('datetime');
    expect(container.querySelector('time')).toHaveTextContent(/\d{2}:\d{2}/);
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
    expect(screen.queryByText(/Cajero:|Ventas totales/)).not.toBeInTheDocument();
    expect(await screen.findByText('Caja: ABIERTA')).toBeInTheDocument();
    expect(mocks.getActive).not.toHaveBeenCalled();
  });

  it('conserva el espacio para acciones de mesas sin agregar buscador', () => {
    const onCreate = vi.fn();
    mount(<RestaurantHeader><span>2 mesas activas</span><button onClick={onCreate}>Agregar Mesa</button></RestaurantHeader>);
    expect(screen.getByText('2 mesas activas')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Agregar Mesa' }));
    expect(onCreate).toHaveBeenCalledOnce();
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });

  it('identifica la sucursal por id si la sesión no trae su nombre', () => {
    mount();
    expect(screen.getByText('#91')).toBeInTheDocument();
  });

  it('indica sucursal sin asignar y no consulta estado sin contexto', () => {
    mocks.auth.branchId = null;
    mount();
    expect(screen.getByText('Sin asignar')).toBeInTheDocument();
    expect(screen.getByText('Caja: SIN CONTEXTO DE SUCURSAL')).toBeInTheDocument();
    expect(mocks.getStatus).not.toHaveBeenCalled();
  });
});
