import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FeatureRoute from '../../components/routing/FeatureRoute';

const { featureState, authState } = vi.hoisted(() => ({
  featureState: {
    isReady: true,
    isError: false,
    isPlatformOnly: false,
    hasFeature: vi.fn(),
    roleAllows: vi.fn(),
  },
  authState: { isAuthenticated: true },
}));

vi.mock('../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));
vi.mock('../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../components/layout/Layout', () => ({ default: ({ children }) => <div data-testid="layout">{children}</div> }));

describe('FeatureRoute', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    featureState.isReady = true;
    featureState.isError = false;
    featureState.isPlatformOnly = false;
    authState.isAuthenticated = true;
    featureState.roleAllows.mockReturnValue(true);
    featureState.hasFeature.mockReturnValue(true);
  });

  it('does not mount a disabled company module', () => {
    featureState.hasFeature.mockReturnValue(false);
    const mounted = vi.fn();
    const Page = () => { mounted(); return <div>Módulo real</div>; };

    render(<MemoryRouter><FeatureRoute feature="finance"><Page /></FeatureRoute></MemoryRouter>);

    expect(mounted).not.toHaveBeenCalled();
    expect(screen.getByText('Módulo no habilitado')).toBeInTheDocument();
    expect(screen.queryByText('Acceso denegado')).not.toBeInTheDocument();
  });

  it('does not mount optional modules while feature state is unresolved', () => {
    featureState.isReady = false;

    render(<MemoryRouter><FeatureRoute feature="inventory"><div>Módulo real</div></FeatureRoute></MemoryRouter>);

    expect(screen.queryByText('Módulo real')).not.toBeInTheDocument();
    expect(screen.getByText('Cargando módulos...')).toBeInTheDocument();
  });

  it('mounts an enabled module only after access is resolved', () => {
    render(<MemoryRouter><FeatureRoute feature="restaurant"><div>Mesa operativa</div></FeatureRoute></MemoryRouter>);
    expect(screen.getByText('Mesa operativa')).toBeInTheDocument();
  });

  it('fails closed with an explicit verification error', () => {
    featureState.isReady = false;
    featureState.isError = true;

    render(<MemoryRouter><FeatureRoute feature="ai"><div>IA real</div></FeatureRoute></MemoryRouter>);

    expect(screen.queryByText('IA real')).not.toBeInTheDocument();
    expect(screen.getByText('No fue posible verificar los módulos')).toBeInTheDocument();
  });

  it('keeps dashboard mounted while optional feature state is unresolved', () => {
    featureState.isReady = false;
    render(<MemoryRouter><FeatureRoute feature="dashboard"><div>Resumen principal</div></FeatureRoute></MemoryRouter>);
    expect(screen.getByText('Resumen principal')).toBeInTheDocument();
  });

  it('distinguishes role denial from a disabled company feature', () => {
    featureState.roleAllows.mockReturnValue(false);

    render(<MemoryRouter><FeatureRoute feature="finance"><div>Finanzas</div></FeatureRoute></MemoryRouter>);

    expect(screen.queryByText('Finanzas')).not.toBeInTheDocument();
    expect(screen.getByText('Acceso denegado')).toBeInTheDocument();
    expect(screen.queryByText('Módulo no habilitado')).not.toBeInTheDocument();
  });

  it('renders dashboard permission denial without redirecting legacy admin', () => {
    featureState.roleAllows.mockReturnValue(false);

    render(<MemoryRouter initialEntries={['/']}><FeatureRoute feature="dashboard"><div>Dashboard</div></FeatureRoute></MemoryRouter>);

    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument();
    expect(screen.getByText('Acceso denegado')).toBeInTheDocument();
    expect(screen.getByTestId('layout')).toBeInTheDocument();
  });
});
