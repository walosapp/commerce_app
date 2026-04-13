import { CircleSlash2, Edit3, Repeat, Trash2 } from 'lucide-react';
import { formatCurrency } from '../../../utils/formatCurrency';

const typeStyles = {
  income: 'bg-green-100 text-green-700',
  expense: 'bg-red-100 text-red-700',
};

const statusStyles = {
  pending: 'bg-yellow-100 text-yellow-800',
  posted: 'bg-green-100 text-green-800',
  skipped: 'bg-gray-100 text-gray-600',
};

const isFromTemplate = (entry) => !!entry.financialItemId || entry.isManual === false;

const FinancialEntryTable = ({ entries, isLoading, onEdit, onDelete }) => {
  if (isLoading) {
    return (
      <div className="card flex h-64 items-center justify-center">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary-500 border-t-transparent" />
      </div>
    );
  }

  if (!entries.length) {
    return (
      <div className="card flex h-64 flex-col items-center justify-center text-center">
        <p className="text-lg font-semibold text-gray-900">No hay movimientos cargados</p>
        <p className="mt-2 text-sm text-gray-500">Registra un gasto o ingreso para empezar a medir la operacion real.</p>
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200 text-sm">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Fecha</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Estado</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Tipo</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Categoria</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Descripcion</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Sucursal</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Ocurr.</th>
              <th className="px-4 py-3 text-left font-medium text-gray-500">Naturaleza</th>
              <th className="px-4 py-3 text-right font-medium text-gray-500">Monto</th>
              <th className="px-4 py-3 text-right font-medium text-gray-500">Acciones</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {entries.map((entry) => (
              <tr key={entry.id}>
                <td className="px-4 py-3 text-gray-700">{new Date(entry.entryDate).toLocaleDateString('es-CO')}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ${statusStyles[entry.status] || 'bg-gray-100 text-gray-700'}`}>
                    {entry.status || 'posted'}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-medium ${typeStyles[entry.type] || 'bg-gray-100 text-gray-700'}`}>
                    {entry.type === 'income' ? 'Ingreso' : 'Gasto'}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-700">{entry.categoryName}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-1.5">
                    {isFromTemplate(entry) && <Repeat className="h-3.5 w-3.5 flex-shrink-0 text-primary-500" title="Generado desde item financiero" />}
                    <p className="font-medium text-gray-900">{entry.description}</p>
                  </div>
                  {entry.notes && <p className="mt-1 text-xs text-gray-500">{entry.notes}</p>}
                </td>
                <td className="px-4 py-3 text-gray-700">{entry.branchName || 'General'}</td>
                <td className="px-4 py-3 text-gray-700">{entry.occurrenceInMonth || 1}</td>
                <td className="px-4 py-3 text-gray-700">{entry.nature || '-'}</td>
                <td className="px-4 py-3 text-right font-semibold text-gray-900">{formatCurrency(entry.amount)}</td>
                <td className="px-4 py-3">
                  <div className="flex justify-end gap-2">
                    <button
                      onClick={() => onEdit(entry)}
                      disabled={entry.status === 'skipped'}
                      className="rounded-lg border border-gray-200 p-2 text-gray-600 transition-colors hover:bg-gray-50 disabled:opacity-50"
                    >
                      <Edit3 className="h-4 w-4" />
                    </button>
                    <button
                      onClick={() => onDelete(entry)}
                      disabled={entry.status === 'skipped'}
                      title={isFromTemplate(entry) ? 'Omitir del mes' : 'Eliminar'}
                      className={`rounded-lg border border-gray-200 p-2 transition-colors disabled:opacity-50 ${
                        isFromTemplate(entry)
                          ? 'text-gray-600 hover:bg-gray-50'
                          : 'text-red-600 hover:bg-red-50'
                      }`}
                    >
                      {isFromTemplate(entry) ? <CircleSlash2 className="h-4 w-4" /> : <Trash2 className="h-4 w-4" />}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default FinancialEntryTable;
