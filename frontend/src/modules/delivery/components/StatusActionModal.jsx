import { useState } from 'react';
import { X, Loader2, AlertTriangle } from 'lucide-react';

const ACTION_LABELS = {
  reject:  { title: 'Rechazar pedido',   btn: 'Rechazar',  color: 'bg-red-600 hover:bg-red-700' },
  cancel:  { title: 'Cancelar pedido',   btn: 'Cancelar',  color: 'bg-red-600 hover:bg-red-700' },
  return:  { title: 'Devolver pedido',   btn: 'Confirmar', color: 'bg-orange-500 hover:bg-orange-600' },
};

const StatusActionModal = ({ action, onConfirm, onClose }) => {
  const [comment, setComment] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  if (!action) return null;

  const meta = ACTION_LABELS[action] ?? { title: action, btn: 'Confirmar', color: 'bg-indigo-600 hover:bg-indigo-700' };

  const handleSubmit = async () => {
    if (!comment.trim()) { setError('El comentario es obligatorio'); return; }
    setLoading(true);
    setError('');
    try {
      await onConfirm(comment.trim());
      onClose();
    } catch (e) {
      setError(e?.response?.data?.message || 'Error al cambiar el estado');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl shadow-xl w-full max-w-md">
        <div className="flex items-center gap-3 px-6 py-4 border-b">
          <AlertTriangle size={20} className="text-orange-500" />
          <h3 className="text-base font-semibold text-gray-900">{meta.title}</h3>
          <button onClick={onClose} className="ml-auto text-gray-400 hover:text-gray-600"><X size={18} /></button>
        </div>
        <div className="px-6 py-5 space-y-3">
          <label className="block text-sm font-medium text-gray-700">
            Motivo / comentario <span className="text-red-500">*</span>
          </label>
          <textarea
            rows={3}
            value={comment}
            onChange={e => setComment(e.target.value)}
            placeholder="Describe el motivo..."
            className="w-full border border-gray-300 rounded-lg px-3 py-2 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>
        <div className="flex gap-3 px-6 pb-5 justify-end">
          <button onClick={onClose} className="text-sm text-gray-500 hover:text-gray-700 px-4 py-2">
            Cancelar
          </button>
          <button
            onClick={handleSubmit}
            disabled={loading}
            className={`flex items-center gap-2 text-white text-sm font-medium px-5 py-2 rounded-lg transition-colors disabled:opacity-50 ${meta.color}`}
          >
            {loading ? <Loader2 size={15} className="animate-spin" /> : null}
            {meta.btn}
          </button>
        </div>
      </div>
    </div>
  );
};

export default StatusActionModal;
