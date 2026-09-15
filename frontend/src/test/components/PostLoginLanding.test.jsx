import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import PostLoginLanding from '../../components/routing/PostLoginLanding';

const { authState } = vi.hoisted(() => ({
  authState: {
    isAuthenticated: true,
    user: { role: 'manager', isPlatformAdmin: false },
  },
}));

vi.mock('../../stores/authStore', () => ({ default: () => authState }));

const renderLanding = () => render(
  <MemoryRouter initialEntries={['/landing']}>
    <Routes>
      <Route path="/landing" element={<PostLoginLanding />} />
      <Route path="/admin/tenants" element={<div>Platform landing</div>} />
      <Route path="/login" element={<div>Login landing</div>} />
      <Route path="/" element={<div>Dashboard landing</div>} />
    </Routes>
  </MemoryRouter>,
);

describe('PostLoginLanding', () => {
  beforeEach(() => {
    authState.isAuthenticated = true;
    authState.user = { role: 'manager', isPlatformAdmin: false };
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

  it('routes unauthenticated sessions to login', () => {
    authState.isAuthenticated = false;
    renderLanding();
    expect(screen.getByText('Login landing')).toBeInTheDocument();
  });
});
