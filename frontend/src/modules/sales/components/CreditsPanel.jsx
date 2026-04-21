import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CreditCard, X, ChevronDown, ChevronUp, DollarSign } from 'lucide-react';
import toast from 'react-hot-toast';
import { formatCurrency } from '../../../utils/formatCurrency';
import api from '../../../config/api';

const creditService = {
  getCredits: (params) => api.get('/sales/credits', { params }).then(r => r.data),
  addPayment:  (id, body) => api.post(`/sales/credits/${id}/pay`, body).then(r => r.data),
  cancel:      (id)       => api.delete(`/sales/credits/${id}`).then(r => r.data),
};

const STATUS_LABEL = { pending: 'Pendiente', partial: 'Parcial', paid: 'Pagado', cancelled: 'Cancelado' };
const STATUS_CLS   = {
  pending:   'bg-orange-100 text-orange-700',
  partial:   'bg-blue-100 text-blue-700',
  paid:      'bg-green-100 text-green-700',
  cancelled: 'bg-gray-100 text-gray-500',
};

const CreditRow = ({ credit, onPayment, onCancel }) => {
  const [expanded, setExpanded] = useState(false);
  const [amount, setAmount]     = useState('');
  const [notes, setNotes]       = useState('');
  const [paying, setPaying]     = useState(false);

  const handlePay = async () => {
    const val = Number(amount);
    if (!val || val <= 0) { toast.error('Ingresa un monto válido'); return; }
    setPaying(true);
    try {
      await onPayment(credit.id, { amount: val, notes });
      setAmount(''); setNotes('');
    } finally { setPaying(false); }
  };

  const isPending = credit.status !== 'paid' && credit.status !== 'cancelled';

  return (
    <div className="border-b border-gray-100 last:border-0">
      <button
        onClick={() => setExpanded(v => !v)}
        className="w-full flex items-center gap-3 px-4 py-3 hover:bg-gray-50 transition-colors text-left"
      >
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <span className="font-semibold text-gray-900 truncate">{credit.customerName}</span>
            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_CLS[credit.status]}`}>
              {STATUS_LABEL[credit.status]}
            </span>
          </div>
          <div className="flex items-center gap-3 mt-0.5 text-xs text-gray-500 flex-wrap">
            <span>{credit.orderNumber}</span>
            <span>{new Date(credit.createdAt).toLocaleDateString('es-CO')}</span>
          </div>
        </div>
        <div className="text-right shrink-0">
          <p className="text-xs text-gray-500">Saldo</p>
          <p className="font-bold text-orange-600">{formatCurrency(credit.creditAmount)}</p>
        </div>
        {expanded ? <ChevronUp size={16} className="text-gray-400 shrink-0" /> : <ChevronDown size={16} className="text-gray-400 shrink-0" />}
      </button>

      {expanded && (
        <div className="border-t bg-gray-50 px-4 py-3 space-y-3">
          <div className="grid grid-cols-3 gap-2 text-sm">
            <div>
              <p className="text-xs text-gray-500">Total original</p>
              <p className="font-medium">{formatCurrency(credit.originalTotal)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Pagado</p>
              <p className="font-medium text-green-700">{formatCurrency(credit.amountPaid)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500">Pendiente</p>
              <p className="font-bold text-orange-600">{formatCurrency(credit.creditAmount)}</p>
            </div>
          </div>

          {credit.payments?.length > 0 && (
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase mb-1">Abonos</p>
              {credit.payments.map(p => (
                <div key={p.id} className="flex justify-between text-xs text-gray-600 py-1 border-b border-gray-100 last:border-0">
                  <span>{new Date(p.createdAt).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'short' })}</span>
                  {p.notes && <span className="text-gray-400 truncate max-w-[120px]">{p.notes}</span>}
                  <span className="font-semibold text-green-700">+{formatCurrency(p.amount)}</span>
                </div>
              ))}
            </div>
          )}

          {isPending && (
            <div className="space-y-2">
              <p className="text-xs font-semibold text-gray-600 uppercase">Registrar abono</p>
              <div className="flex gap-2">
                <div className="relative flex-1">
                  <DollarSign size={13} className="absolute left-2.5 top-1/2 -translate-y-1/2 text-gray-400" />
                  <input type="number" min="0" step="100" value={amount}
                    onChange={e => setAmount(e.target.value)}
                    placeholder="Monto"
                    className="input pl-7 text-sm w-full" />
                </div>
                <input type="text" value={notes} onChange={e => setNotes(e.target.value)}
                  placeholder="Nota (opcional)" className="input text-sm flex-1" />
                <button onClick={handlePay} disabled={paying}
                  className="px-3 py-1.5 bg-green-600 text-white text-sm rounded-lg hover:bg-green-700 disabled:opacity-50 transition-colors whitespace-nowrap">
                  {paying ? '...' : 'Abonar'}
                </button>
              </div>
              <button onClick={() => { if (window.confirm('Cancelar este crédito?')) onCancel(credit.id); }}
                className="text-xs text-red-500 hover:text-red-700 transition-colors">
                Cancelar crédito
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
};

const CreditsPanelContent = ({ statusFilter, setStatusFilter, search, setSearch, credits, isLoading, payMutation, cancelMutation, totalPending, onClose, inline }) => (
  <>
    <div className="flex items-center justify-between px-5 py-3 border-b flex-shrink-0">
      <div className="flex items-center gap-2">
        <CreditCard className="h-5 w-5 text-orange-600" />
        <div>
          <h2 className="font-bold text-gray-900">Créditos</h2>
          {totalPending > 0 && (
            <p className="text-xs text-orange-600">Total pendiente: {formatCurrency(totalPending)}</p>
          )}
        </div>
      </div>
      {!inline && onClose && (
        <button onClick={onClose} className="p-1.5 rounded-lg hover:bg-gray-100 transition-colors">
          <X size={18} />
        </button>
      )}
    </div>

    <div className="px-4 py-2 border-b flex gap-2 flex-shrink-0 flex-wrap">
      {['pending', 'partial', 'paid', 'all'].map(s => (
        <button key={s} onClick={() => setStatusFilter(s)}
          className={`text-xs px-3 py-1.5 rounded-full border font-medium transition-colors ${
            statusFilter === s ? 'bg-orange-100 text-orange-700 border-orange-200' : 'bg-white text-gray-600 border-gray-200 hover:bg-gray-50'
          }`}>
          {s === 'all' ? 'Todos' : STATUS_LABEL[s]}
        </button>
      ))}
      <input value={search} onChange={e => setSearch(e.target.value)}
        placeholder="Buscar cliente..." className="input text-sm flex-1 min-w-[120px]" />
    </div>

    <div className="flex-1 overflow-y-auto">
      {isLoading ? (
        <p className="text-center text-sm text-gray-400 py-8">Cargando...</p>
      ) : credits.length === 0 ? (
        <p className="text-center text-sm text-gray-400 py-8">Sin créditos</p>
      ) : credits.map(c => (
        <CreditRow key={c.id} credit={c}
          onPayment={(id, body) => payMutation.mutate({ id, body })}
          onCancel={(id) => cancelMutation.mutate(id)} />
      ))}
    </div>
  </>
);

const CreditsPanel = ({ isOpen, onClose, inline = false }) => {
  const queryClient  = useQueryClient();
  const [statusFilter, setStatusFilter] = useState('pending');
  const [search, setSearch] = useState('');

  const { data, isLoading } = useQuery({
    queryKey: ['credits', statusFilter, search],
    queryFn:  () => creditService.getCredits({ status: statusFilter, search: search || undefined }),
    enabled:  isOpen || inline,
  });

  const payMutation = useMutation({
    mutationFn: ({ id, body }) => creditService.addPayment(id, body),
    onSuccess: () => {
      toast.success('Abono registrado');
      queryClient.invalidateQueries({ queryKey: ['credits'] });
    },
    onError: (err) => toast.error(err?.response?.data?.message || 'Error registrando abono'),
  });

  const cancelMutation = useMutation({
    mutationFn: (id) => creditService.cancel(id),
    onSuccess: () => {
      toast.success('Crédito cancelado');
      queryClient.invalidateQueries({ queryKey: ['credits'] });
    },
    onError: (err) => toast.error(err?.response?.data?.message || 'Error cancelando crédito'),
  });

  const credits = data?.data ?? [];
  const totalPending = credits
    .filter(c => c.status !== 'paid' && c.status !== 'cancelled')
    .reduce((s, c) => s + c.creditAmount, 0);

  const sharedProps = { statusFilter, setStatusFilter, search, setSearch, credits, isLoading, payMutation, cancelMutation, totalPending, onClose, inline };

  if (!isOpen && !inline) return null;

  if (inline) {
    return (
      <div className="flex flex-col flex-1 overflow-hidden bg-white">
        <CreditsPanelContent {...sharedProps} />
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg flex flex-col max-h-[88vh]">
        <CreditsPanelContent {...sharedProps} />
      </div>
    </div>
  );
};

export default CreditsPanel;
