import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { GitBranch, Pencil, PlusCircle, ToggleLeft, ToggleRight, X } from 'lucide-react';
import platformService from '../../../services/platformService';

const EMPTY_FORM = Object.freeze({
  name: '',
  code: '',
  branchType: 'general',
  email: '',
  phone: '',
  address: '',
  city: '',
  state: '',
  country: 'CO',
  postalCode: '',
  maxTables: '',
  maxCapacity: '',
});

const getErrorMessage = (error) =>
  error?.response?.data?.message
  || error?.response?.data?.error?.message
  || 'No fue posible guardar la sucursal';

const toForm = (branch) => ({
  ...EMPTY_FORM,
  ...branch,
  email: branch.email ?? '',
  phone: branch.phone ?? '',
  state: branch.state ?? '',
  postalCode: branch.postalCode ?? '',
  maxTables: branch.maxTables ?? '',
  maxCapacity: branch.maxCapacity ?? '',
});

const toPayload = (form) => ({
  name: form.name.trim(),
  code: form.code.trim(),
  branchType: form.branchType.trim(),
  email: form.email.trim() || null,
  phone: form.phone.trim() || null,
  address: form.address.trim(),
  city: form.city.trim(),
  state: form.state.trim() || null,
  country: form.country.trim(),
  postalCode: form.postalCode.trim() || null,
  maxTables: form.maxTables === '' ? null : Number(form.maxTables),
  maxCapacity: form.maxCapacity === '' ? null : Number(form.maxCapacity),
});

const BranchForm = ({ branch, companyId, onCancel, onSaved }) => {
  const [form, setForm] = useState(branch ? toForm(branch) : { ...EMPTY_FORM });
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);

  const update = (field, value) => setForm((current) => ({ ...current, [field]: value }));

  const submit = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    try {
      const payload = toPayload(form);
      if (branch) {
        await platformService.updateAdminBranch(companyId, branch.id, {
          ...payload,
          isActive: branch.isActive,
        });
      }
      else await platformService.createAdminBranch(companyId, payload);
      onSaved();
    } catch (requestError) {
      setError(getErrorMessage(requestError));
    } finally {
      setSaving(false);
    }
  };

  return (
    <form onSubmit={submit} className="space-y-3 rounded-lg border border-indigo-100 bg-indigo-50 p-4">
      <h3 className="font-semibold text-gray-900">{branch ? 'Editar sucursal' : 'Nueva sucursal'}</h3>
      {error && <div role="alert" className="rounded-md bg-red-50 p-2 text-sm text-red-700">{error}</div>}
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="text-sm text-gray-700">Nombre
          <input aria-label="Nombre" required maxLength={200} className="input mt-1" value={form.name} onChange={(e) => update('name', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Código
          <input aria-label="Código" required maxLength={20} className="input mt-1 uppercase" value={form.code} onChange={(e) => update('code', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Tipo
          <input aria-label="Tipo" required maxLength={50} className="input mt-1" value={form.branchType} onChange={(e) => update('branchType', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">País
          <input aria-label="País" required minLength={2} maxLength={2} className="input mt-1 uppercase" value={form.country} onChange={(e) => update('country', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Dirección
          <input aria-label="Dirección" required maxLength={500} className="input mt-1" value={form.address} onChange={(e) => update('address', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Ciudad
          <input aria-label="Ciudad" required maxLength={100} className="input mt-1" value={form.city} onChange={(e) => update('city', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Email
          <input aria-label="Email" type="email" maxLength={100} className="input mt-1" value={form.email} onChange={(e) => update('email', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Teléfono
          <input aria-label="Teléfono" maxLength={20} className="input mt-1" value={form.phone} onChange={(e) => update('phone', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Departamento
          <input aria-label="Departamento" maxLength={100} className="input mt-1" value={form.state} onChange={(e) => update('state', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Código postal
          <input aria-label="Código postal" maxLength={10} className="input mt-1" value={form.postalCode} onChange={(e) => update('postalCode', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Máximo de mesas
          <input aria-label="Máximo de mesas" type="number" min="1" className="input mt-1" value={form.maxTables} onChange={(e) => update('maxTables', e.target.value)} />
        </label>
        <label className="text-sm text-gray-700">Capacidad máxima
          <input aria-label="Capacidad máxima" type="number" min="1" className="input mt-1" value={form.maxCapacity} onChange={(e) => update('maxCapacity', e.target.value)} />
        </label>
      </div>
      <div className="flex justify-end gap-2">
        <button type="button" className="btn btn-secondary" onClick={onCancel}>Cancelar</button>
        <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando...' : 'Guardar sucursal'}</button>
      </div>
    </form>
  );
};

const CompanyBranchesPanel = ({ companyId, companyName, onClose }) => {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(null);
  const [creating, setCreating] = useState(false);
  const [actionError, setActionError] = useState('');
  const [updatingId, setUpdatingId] = useState(null);

  const queryKey = ['admin-company-branches', companyId];
  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => platformService.getAdminBranches(companyId),
    enabled: Number.isInteger(companyId) && companyId > 0,
  });
  const branches = data?.data ?? [];

  const refresh = async () => {
    setCreating(false);
    setEditing(null);
    await queryClient.invalidateQueries({ queryKey });
  };

  const toggleStatus = async (branch) => {
    setUpdatingId(branch.id);
    setActionError('');
    try {
      const payload = {
        ...toPayload(toForm(branch)),
        isActive: !branch.isActive,
      };
      await platformService.updateAdminBranch(companyId, branch.id, payload);
      await refresh();
    } catch (error) {
      setActionError(getErrorMessage(error));
    } finally {
      setUpdatingId(null);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" role="dialog" aria-modal="true" aria-label={`Sucursales de ${companyName}`}>
      <div className="max-h-[90vh] w-full max-w-4xl overflow-y-auto rounded-xl bg-white shadow-xl">
        <div className="sticky top-0 z-10 flex items-center justify-between border-b bg-white p-5">
          <div>
            <h2 className="text-lg font-bold text-gray-900">Sucursales de {companyName}</h2>
            <p className="text-sm text-gray-500">Administrá las sedes del comercio seleccionado.</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar sucursales" className="rounded p-2 text-gray-500 hover:bg-gray-100"><X size={18} /></button>
        </div>

        <div className="space-y-4 p-5">
          {actionError && <div role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{actionError}</div>}
          {!creating && !editing && (
            <button type="button" onClick={() => { setEditing(null); setCreating(true); }} className="btn btn-primary flex items-center gap-2">
              <PlusCircle size={16} /> Nueva sucursal
            </button>
          )}
          {creating && <BranchForm key="new" companyId={companyId} onCancel={() => setCreating(false)} onSaved={refresh} />}
          {editing && <BranchForm key={editing.id} companyId={companyId} branch={editing} onCancel={() => setEditing(null)} onSaved={refresh} />}

          {isLoading ? (
            <p className="py-8 text-center text-sm text-gray-500">Cargando sucursales...</p>
          ) : isError ? (
            <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">No fue posible cargar las sucursales.</p>
          ) : branches.length === 0 ? (
            <p className="py-8 text-center text-sm text-gray-500">El comercio no tiene sucursales registradas.</p>
          ) : (
            <div className="divide-y rounded-lg border">
              {branches.map((branch) => (
                <div key={branch.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
                  <div className="flex min-w-0 items-center gap-3">
                    <GitBranch size={18} className="text-indigo-600" />
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="font-medium text-gray-900">{branch.name}</span>
                        {branch.isMain && <span className="rounded bg-indigo-50 px-2 py-0.5 text-xs text-indigo-700">Principal</span>}
                        <span className={`rounded px-2 py-0.5 text-xs ${branch.isActive ? 'bg-green-50 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
                          {branch.isActive ? 'Activa' : 'Inactiva'}
                        </span>
                      </div>
                      <p className="text-xs text-gray-500">{branch.code} · {branch.city} · {branch.address}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <button type="button" onClick={() => { setCreating(false); setEditing(branch); }} className="flex items-center gap-1 text-sm text-indigo-600 hover:text-indigo-800">
                      <Pencil size={14} /> Editar
                    </button>
                    <button
                      type="button"
                      disabled={updatingId === branch.id}
                      onClick={() => toggleStatus(branch)}
                      className="flex items-center gap-1 text-sm text-gray-600 hover:text-indigo-700 disabled:opacity-50"
                    >
                      {branch.isActive ? <ToggleRight size={18} /> : <ToggleLeft size={18} />}
                      {branch.isActive ? 'Desactivar' : 'Activar'}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CompanyBranchesPanel;
