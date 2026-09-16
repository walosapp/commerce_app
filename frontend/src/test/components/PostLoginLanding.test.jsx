import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PostLoginLanding from '../../components/routing/PostLoginLanding';

const { authState, featureState } = vi.hoisted(() => ({
  authState: {
    isAuthenticated: true,
    user: { role: 'manager', isPlatformAdmin: false },
  },
  featureState: {
    canAccess: vi.fn(),
    isError: false,
    isReady: true,
  },
}));

vi.mock('../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../hooks/useCompanyFeatures', () => ({ default: () => featureState }));

const renderLanding = () => render(
  <MemoryRouter initialEntries={['/landing']}>
    <Routes>
      <Route path="/landing" element={<PostLoginLanding />} />
      <Route path="/admin/tenants" element={<div>Platform landing</div>} />
      <Route path="/login" element={<div>Login landing</div>} />
      <Route path="/" element={<div>Dashboard landing</div>} />
      <Route path="/sales" element={<div>Restaurant landing</div>} />
      <Route path="/pos-deli" element={<div>POS landing</div>} />
      <Route path="/delivery" element={<div>Delivery landing</div>} />
    </Routes>
  </MemoryRouter>,
);

describe('PostLoginLanding', () => {
  beforeEach(() => {
    authState.isAuthenticated = true;
    authState.user = { role: 'manager', isPlatformAdmin: false };
    featureState.isError = false;
    featureState.isReady = true;
    featureState.canAccess.mockImplementation((feature) => feature === 'restaurant');
  });

  it('routes operational roles to the canonical dashboard', () => {
    renderLanding();
    expect(screen.getByText('Dashboard landing')).toBeInTheDocument();
  });

  it('keeps platform administrators in the platform shell', () => {
    authState.user = { role: 'platform_admin', isPlatformAdmin: true };
    renderLanding();
    expect(screen.getByText('Platform landing')).toBeInTheDocument();
  });

  it.each(['cashier', 'waiter'])('routes %s to Restaurante instead of the forbidden dashboard', (role) => {
    authState.user = { role, isPlatformAdmin: false };
    renderLanding();
    expect(screen.getByText('Restaurant landing')).toBeInTheDocument();
  });

  it('routes a cashier to POS when Restaurante is disabled', () => {
    authState.user = { role: 'cashier', isPlatformAdmin: false };
    featureState.canAccess.mockImplementation((feature) => feature === 'pos');
    renderLanding();
    expect(screen.getByText('POS landing')).toBeInTheDocument();
  });

  it('routes a waiter to Delivery when Restaurante is disabled', () => {
    authState.user = { role: 'waiter', isPlatformAdmin: false };
    featureState.canAccess.mockImplementation((feature) => feature === 'delivery');
    renderLanding();
    expect(screen.getByText('Delivery landing')).toBeInTheDocument();
  });

  it('routes unauthenticated sessions to login', () => {
    authState.isAuthenticated = false;
    renderLanding();
    expect(screen.getByText('Login landing')).toBeInTheDocument();
  });
});
