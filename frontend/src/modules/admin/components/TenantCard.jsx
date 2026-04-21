import { Building2, Users, GitBranch, Calendar, ToggleLeft, ToggleRight, Pencil } from 'lucide-react';

const TenantCard = ({ tenant, onToggleStatus, onEdit }) => {
  const createdAt = tenant.createdAt
    ? new Date(tenant.createdAt).toLocaleDateString('es-CO')
    : '—';

  return (
    <div className={`bg-white rounded-xl border shadow-sm p-5 flex flex-col gap-3 transition-opacity ${!tenant.isActive ? 'opacity-60' : ''}`}>
      <div className="flex items-start justify-between gap-2">
        <div className="flex items-center gap-3 min-w-0">
          <div className="w-10 h-10 rounded-lg bg-indigo-100 flex items-center justify-center shrink-0">
            <Building2 size={20} className="text-indigo-600" />
          </div>
          <div className="min-w-0">
            <p className="font-semibold text-gray-900 truncate">{tenant.name}</p>
            {tenant.legalName && (
              <p className="text-xs text-gray-500 truncate">{tenant.legalName}</p>
            )}
          </div>
        </div>
        <span className={`shrink-0 text-xs font-medium px-2 py-1 rounded-full ${
          tenant.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-600'
        }`}>
          {tenant.isActive ? 'Activo' : 'Inactivo'}
        </span>
      </div>

      <div className="grid grid-cols-2 gap-2 text-sm text-gray-600">
        {tenant.taxId && (
          <div className="col-span-2 text-xs text-gray-400">NIT: {tenant.taxId}</div>
        )}
        <div className="flex items-center gap-1.5">
          <GitBranch size={14} className="text-gray-400" />
          <span>{tenant.branchCount ?? 0} sucursal{tenant.branchCount !== 1 ? 'es' : ''}</span>
        </div>
        <div className="flex items-center gap-1.5">
          <Users size={14} className="text-gray-400" />
          <span>{tenant.userCount ?? 0} usuario{tenant.userCount !== 1 ? 's' : ''}</span>
        </div>
        <div className="flex items-center gap-1.5 col-span-2">
          <Calendar size={14} className="text-gray-400" />
          <span className="text-xs">{createdAt}</span>
        </div>
      </div>

      <div className="pt-1 border-t flex items-center justify-between gap-2">
        <span className="text-xs text-gray-400">{tenant.currency} · {tenant.country}</span>
        <div className="flex items-center gap-3">
          <button
            onClick={() => onEdit(tenant)}
            title="Editar comercio"
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-indigo-600 transition-colors"
          >
            <Pencil size={14} /> Editar
          </button>
          <button
            onClick={() => onToggleStatus(tenant)}
            title={tenant.isActive ? 'Desactivar comercio' : 'Activar comercio'}
            className="flex items-center gap-1.5 text-xs text-gray-500 hover:text-indigo-600 transition-colors"
          >
            {tenant.isActive
              ? <><ToggleRight size={18} className="text-green-500" /> Desactivar</>
              : <><ToggleLeft size={18} className="text-gray-400" /> Activar</>
            }
          </button>
        </div>
      </div>
    </div>
  );
};

export default TenantCard;
