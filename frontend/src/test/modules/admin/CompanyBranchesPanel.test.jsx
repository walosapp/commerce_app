import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import CompanyBranchesPanel from '../../../modules/admin/components/CompanyBranchesPanel';

const { getAdminBranches, createAdminBranch, updateAdminBranch } = vi.hoisted(() => ({
  getAdminBranches: vi.fn(),
  createAdminBranch: vi.fn(),
  updateAdminBranch: vi.fn(),
}));

vi.mock('../../../services/platformService', () => ({
  default: { getAdminBranches, createAdminBranch, updateAdminBranch },
}));

const branch = {
  id: 9,
  companyId: 25,
  name: 'Principal',
  code: 'MAIN',
  branchType: 'general',
  email: null,
  phone: null,
  address: 'Calle 1',
  city: 'Bogotá',
  state: null,
  country: 'CO',
  postalCode: null,
  maxTables: null,
  maxCapacity: null,
  isMain: true,
  isActive: true,
};

const renderPanel = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <CompanyBranchesPanel companyId={25} companyName="Comercio Demo" onClose={vi.fn()} />
    </QueryClientProvider>,
  );
};

describe('CompanyBranchesPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getAdminBranches.mockResolvedValue({ data: [branch] });
    createAdminBranch.mockResolvedValue({ data: { ...branch, id: 10, name: 'Norte' } });
    updateAdminBranch.mockResolvedValue({ data: branch });
  });

  it('loads and updates branches only inside the selected company', async () => {
    renderPanel();

    expect((await screen.findAllByText('Principal')).length).toBeGreaterThan(0);
    expect(getAdminBranches).toHaveBeenCalledWith(25);

    fireEvent.click(screen.getByRole('button', { name: 'Desactivar' }));
    await waitFor(() => expect(updateAdminBranch).toHaveBeenCalledWith(25, 9, {
      name: 'Principal',
      code: 'MAIN',
      branchType: 'general',
      email: null,
      phone: null,
      address: 'Calle 1',
      city: 'Bogotá',
      state: null,
      country: 'CO',
      postalCode: null,
      maxTables: null,
      maxCapacity: null,
      isActive: false,
    }));
  });

  it('preserves populated branch fields when activating through full replacement', async () => {
    const inactiveBranch = {
      ...branch,
      name: ' Norte ',
      code: ' NOR ',
      branchType: ' retail ',
      email: ' norte@walos.app ',
      phone: ' 3001234567 ',
      address: ' Carrera 2 ',
      city: ' Medellín ',
      state: ' Antioquia ',
      country: ' CO ',
      postalCode: ' 050001 ',
      maxTables: 12,
      maxCapacity: 48,
      isActive: false,
    };
    getAdminBranches.mockResolvedValue({ data: [inactiveBranch] });
    renderPanel();

    await screen.findAllByText('Norte');
    fireEvent.click(screen.getByRole('button', { name: 'Activar' }));

    await waitFor(() => expect(updateAdminBranch).toHaveBeenCalledWith(25, 9, {
      name: 'Norte',
      code: 'NOR',
      branchType: 'retail',
      email: 'norte@walos.app',
      phone: '3001234567',
      address: 'Carrera 2',
      city: 'Medellín',
      state: 'Antioquia',
      country: 'CO',
      postalCode: '050001',
      maxTables: 12,
      maxCapacity: 48,
      isActive: true,
    }));
  });

  it('creates a branch with the typed backend contract', async () => {
    renderPanel();
    await screen.findAllByText('Principal');
    fireEvent.click(screen.getByRole('button', { name: /Nueva sucursal/i }));
    fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: 'Norte' } });
    fireEvent.change(screen.getByLabelText('Código'), { target: { value: 'nor' } });
    fireEvent.change(screen.getByLabelText('Dirección'), { target: { value: 'Carrera 2' } });
    fireEvent.change(screen.getByLabelText('Ciudad'), { target: { value: 'Medellín' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar sucursal' }));

    await waitFor(() => expect(createAdminBranch).toHaveBeenCalledWith(25, expect.objectContaining({
      name: 'Norte', code: 'nor', address: 'Carrera 2', city: 'Medellín', country: 'CO', branchType: 'general',
    })));
  });

  it('edits a branch using the company and branch identifiers', async () => {
    renderPanel();
    await screen.findAllByText('Principal');
    fireEvent.click(screen.getByRole('button', { name: 'Editar' }));
    fireEvent.change(screen.getByLabelText('Nombre'), { target: { value: 'Sede central' } });
    fireEvent.click(screen.getByRole('button', { name: 'Guardar sucursal' }));

    await waitFor(() => expect(updateAdminBranch).toHaveBeenCalledWith(
      25,
      9,
      expect.objectContaining({ name: 'Sede central', isActive: true }),
    ));
  });

  it('displays backend conflicts without inventing a successful state', async () => {
    updateAdminBranch.mockRejectedValue({ response: { status: 409, data: { message: 'La sucursal tiene operaciones o usuarios activos' } } });
    renderPanel();
    await screen.findAllByText('Principal');

    fireEvent.click(screen.getByRole('button', { name: 'Desactivar' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('La sucursal tiene operaciones o usuarios activos');
  });
});
