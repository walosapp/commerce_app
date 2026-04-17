import { useState, useRef } from 'react';
import { X, Download, Upload, FileSpreadsheet, CheckCircle2, AlertCircle, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../../config/api';

const ImportProductsModal = ({ isOpen, onClose, onImported }) => {
  const [file, setFile] = useState(null);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const inputRef = useRef(null);

  if (!isOpen) return null;

  const handleDownloadTemplate = async () => {
    setDownloading(true);
    try {
      const res = await api.get('/inventory/products/template', { responseType: 'blob' });
      const url = URL.createObjectURL(new Blob([res.data]));
      const a = document.createElement('a');
      a.href = url;
      a.download = 'plantilla_productos.xlsx';
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      toast.error('Error descargando plantilla');
    } finally {
      setDownloading(false);
    }
  };

  const handleFileChange = (e) => {
    const f = e.target.files?.[0];
    if (!f) return;
    if (!f.name.endsWith('.xlsx') && !f.name.endsWith('.xls')) {
      toast.error('Solo se aceptan archivos Excel (.xlsx)');
      return;
    }
    setFile(f);
    setResult(null);
  };

  const handleImport = async () => {
    if (!file) { toast.error('Selecciona un archivo primero'); return; }
    setLoading(true);
    setResult(null);
    try {
      const fd = new FormData();
      fd.append('file', file);
      const res = await api.post('/inventory/products/import', fd, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      setResult(res.data);
      if (res.data.data?.created > 0) {
        toast.success(res.data.message);
        onImported?.();
      }
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al importar');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setFile(null);
    setResult(null);
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-lg flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b">
          <div className="flex items-center gap-2">
            <FileSpreadsheet size={18} className="text-green-600" />
            <h2 className="text-base font-semibold text-gray-900">Importar Productos desde Excel</h2>
          </div>
          <button onClick={handleClose} className="p-1 rounded-lg hover:bg-gray-100"><X size={18} /></button>
        </div>

        <div className="p-6 space-y-5">
          {/* Step 1 */}
          <div className="flex items-start gap-3">
            <span className="flex-shrink-0 w-6 h-6 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center">1</span>
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-800 mb-2">Descarga la plantilla con tus categorías y unidades</p>
              <button
                onClick={handleDownloadTemplate}
                disabled={downloading}
                className="flex items-center gap-2 px-4 py-2 text-sm font-medium text-green-700 bg-green-50 hover:bg-green-100 border border-green-200 rounded-lg transition-colors disabled:opacity-50"
              >
                {downloading ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />}
                {downloading ? 'Generando...' : 'Descargar Plantilla'}
              </button>
            </div>
          </div>

          <div className="border-t border-dashed border-gray-200" />

          {/* Step 2 */}
          <div className="flex items-start gap-3">
            <span className="flex-shrink-0 w-6 h-6 rounded-full bg-primary-100 text-primary-700 text-xs font-bold flex items-center justify-center">2</span>
            <div className="flex-1">
              <p className="text-sm font-medium text-gray-800 mb-2">Llena la plantilla y súbela aquí</p>
              <div
                className={`rounded-xl border-2 border-dashed p-6 text-center transition-colors cursor-pointer ${
                  file ? 'border-green-400 bg-green-50' : 'border-gray-300 hover:border-primary-400 hover:bg-primary-50/30'
                }`}
                onClick={() => inputRef.current?.click()}
              >
                <input ref={inputRef} type="file" accept=".xlsx,.xls" className="hidden" onChange={handleFileChange} />
                {file ? (
                  <div className="flex items-center justify-center gap-2 text-green-700">
                    <FileSpreadsheet size={20} />
                    <span className="text-sm font-medium">{file.name}</span>
                  </div>
                ) : (
                  <>
                    <Upload size={24} className="mx-auto mb-2 text-gray-400" />
                    <p className="text-sm text-gray-500">Haz clic o arrastra tu archivo Excel aquí</p>
                    <p className="text-xs text-gray-400 mt-1">.xlsx — máx 5MB</p>
                  </>
                )}
              </div>
            </div>
          </div>

          {/* Result */}
          {result && (
            <div className="rounded-xl border border-gray-200 bg-gray-50 p-4 space-y-3">
              <div className="flex items-center gap-2 text-sm font-semibold text-gray-800">
                <CheckCircle2 size={16} className="text-green-600" />
                {result.data?.created} producto(s) importados
                {result.data?.errors?.length > 0 && (
                  <span className="ml-auto text-amber-600 flex items-center gap-1">
                    <AlertCircle size={14} /> {result.data.errors.length} con errores
                  </span>
                )}
              </div>
              {result.data?.errors?.length > 0 && (
                <div className="space-y-1 max-h-36 overflow-y-auto">
                  {result.data.errors.map((e, i) => (
                    <div key={i} className="text-xs text-red-700 bg-red-50 border border-red-200 rounded-lg px-3 py-1.5">
                      <span className="font-medium">Fila {e.rowNumber} — {e.name || '(sin nombre)'}:</span> {e.error}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-3 px-6 py-4 border-t bg-gray-50 rounded-b-2xl">
          <button onClick={handleClose} className="px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 rounded-lg">
            {result ? 'Cerrar' : 'Cancelar'}
          </button>
          {!result && (
            <button
              onClick={handleImport}
              disabled={!file || loading}
              className="flex items-center gap-2 px-5 py-2 text-sm font-medium text-white bg-primary-600 hover:bg-primary-700 disabled:opacity-50 rounded-lg transition-colors"
            >
              {loading ? <Loader2 size={14} className="animate-spin" /> : <Upload size={14} />}
              {loading ? 'Importando...' : 'Importar'}
            </button>
          )}
        </div>
      </div>
    </div>
  );
};

export default ImportProductsModal;
