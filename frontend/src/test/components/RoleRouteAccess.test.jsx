import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import FeatureRoute from '../../components/routing/FeatureRoute';

const { authState } = vi.hoisted(() => ({
  authState: {
    isAuthenticated: true,
    user: { role: 'manager', isPlatformAdmin: false },
  },
}));

vi.mock('../../stores/authStore', () => ({ default: () => authState }));
vi.mock('../../hooks/useCompanyFeatures', async () => {
  const { canRoleAccessFeature, isPlatformOnlyUser } = await vi.importActual('../../config/companyFeatures');
  return {
    default: () => ({
      hasFeature: () => true,
      isError: false,
      isPlatformOnly: isPlatformOnlyUser(authState.user),
      isReady: true,
      roleAllows: (feature) => canRoleAccessFeature(authState.user, feature),
    }),
  };
});
vi.mock('../../components/layout/Layout', () => ({
  default: ({ children }) => <div data-testid="layout">{children}</div>,
}));

const renderDirectUrl = ({ path, feature, label }) => render(
  <MemoryRouter initialEntries={[path]}>
    <Routes>
      <Route
        path={path}
        element={<FeatureRoute feature={feature}><div>{label}</div></FeatureRoute>}
      />
      <Route path="/admin/tenants" element={<div>Platform only</div>} />
    </Routes>
  </MemoryRouter>,
);

describe('direct URL access follows the frozen V1 role matrix', () => {
  beforeEach(() => {
    authState.isAuthenticated = true;
    authState.user = { role: 'manager', isPlatformAdmin: false };
  });

  it.each([
    ['waiter', '/finance', 'finance'],
    ['waiter', '/inventory', 'inventory'],
    ['cashier', '/', 'dashboard'],
    ['cashier', '/inventory', 'inventory'],
  ])('denies %s when opening %s directly', (role, path, feature) => {
    authState.user = { role, isPlatformAdmin: false };

    renderDirectUrl({ path, feature, label: 'Protected module' });

    expect(screen.queryByText('Protected module')).not.toBeInTheDocument();
    expect(screen.getByText('Acceso denegado')).toBeInTheDocument();
  });

  it('redirects a platform administrator away from a tenant POS URL', () => {
    authState.user = { role: 'platform_admin', isPlatformAdmin: true };

    renderDirectUrl({ path: '/pos-deli', feature: 'pos', label: 'Tenant POS' });

    expect(screen.queryByText('Tenant POS')).not.toBeInTheDocument();
    expect(screen.getByText('Platform only')).toBeInTheDocument();
  });

  it.each([
    [{ role: 'waiter', isPlatformAdmin: false }, '/sales', 'restaurant'],
    [{ role: 'cashier', isPlatformAdmin: false }, '/pos-deli', 'pos'],
    [{ role: 'dev', isPlatformAdmin: true }, '/finance', 'finance'],
  ])('allows an authorized role to open %s directly', (user, path, feature) => {
    authState.user = user;

    renderDirectUrl({ path, feature, label: 'Allowed module' });

    expect(screen.getByText('Allowed module')).toBeInTheDocument();
    expect(screen.queryByText('Acceso denegado')).not.toBeInTheDocument();
  });
});
