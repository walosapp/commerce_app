import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import TenantCard from '../../../modules/admin/components/TenantCard';

describe('TenantCard modules action', () => {
  it('opens feature management for the selected company', () => {
    const tenant = {
      id: 25,
      name: 'Comercio Demo',
      isActive: true,
      currency: 'COP',
      country: 'CO',
    };
    const onManageFeatures = vi.fn();

    render(
      <TenantCard
        tenant={tenant}
        onToggleStatus={vi.fn()}
        onEdit={vi.fn()}
        onManageFeatures={onManageFeatures}
        onManageBranches={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: /Módulos/i }));

    expect(onManageFeatures).toHaveBeenCalledWith(tenant);
  });

  it('opens branch management for the exact selected company', () => {
    const tenant = { id: 25, name: 'Comercio Demo', isActive: true, currency: 'COP', country: 'CO' };
    const onManageBranches = vi.fn();

    render(<TenantCard tenant={tenant} onToggleStatus={vi.fn()} onEdit={vi.fn()} onManageFeatures={vi.fn()} onManageBranches={onManageBranches} />);
    fireEvent.click(screen.getByRole('button', { name: /Sucursales/i }));

    expect(onManageBranches).toHaveBeenCalledWith(tenant);
  });

  it('disables edit and status changes for the backend-marked System company', () => {
    const tenant = { id: 1, name: 'Walos System', isSystem: true, isActive: true, currency: 'COP', country: 'CO' };
    const onEdit = vi.fn();
    const onToggleStatus = vi.fn();

    render(<TenantCard tenant={tenant} onToggleStatus={onToggleStatus} onEdit={onEdit} onManageFeatures={vi.fn()} onManageBranches={vi.fn()} />);

    expect(screen.getByText('System')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Editar/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Desactivar/i })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /Editar/i }));
    fireEvent.click(screen.getByRole('button', { name: /Desactivar/i }));
    expect(onEdit).not.toHaveBeenCalled();
    expect(onToggleStatus).not.toHaveBeenCalled();
  });
});
