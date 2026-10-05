import { Scale, PlugZap, Plug2, AlertTriangle } from 'lucide-react';

const ScaleIndicator = ({
  weight,
  isStable,
  isConnected,
  error,
  onConnect,
  onDisconnect,
  showActions = true,
  helperText = null,
  unit = 'kg',
  compact = false,
}) => {
  if (compact) {
    return (
      <div aria-label="Estado de la báscula" title={helperText || undefined} className="inline-flex min-w-0 flex-wrap items-center gap-x-2 gap-y-1 text-xs">
        <Scale aria-hidden="true" className="h-3.5 w-3.5 shrink-0 text-gray-400" />
        <span className="text-gray-500">Báscula</span>
        <span className="font-bold tabular-nums text-gray-900">{weight.toFixed(3)} {unit}</span>
        <span className={`inline-flex items-center gap-1.5 ${!isConnected ? 'text-gray-400' : isStable ? 'text-green-600' : 'text-amber-600'}`}>
          <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current" />
          {!isConnected ? 'Desconectada' : isStable ? 'Peso estable' : 'Peso inestable'}
        </span>
        {showActions && (
          <button type="button" onClick={isConnected ? onDisconnect : onConnect} className="font-semibold text-primary-600 hover:underline">
            {isConnected ? 'Desconectar' : 'Conectar báscula'}
          </button>
        )}
        {error && <span role="alert" className="inline-flex min-w-0 items-start gap-1 text-amber-700"><AlertTriangle aria-hidden="true" className="h-3.5 w-3.5 shrink-0" /><span className="break-words">{error}</span></span>}
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className={`rounded-xl p-3 ${isConnected ? 'bg-green-50 text-green-600' : 'bg-gray-100 text-gray-500'}`}>
            <Scale className="h-6 w-6" />
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Bascula</p>
            <p className="text-2xl font-black text-gray-900">{weight.toFixed(3)} {unit}</p>
            <p className={`text-xs font-medium ${isConnected ? (isStable ? 'text-green-600' : 'text-amber-600') : 'text-gray-400'}`}>
              {!isConnected ? 'Desconectada' : isStable ? 'Peso estable' : 'Peso inestable'}
            </p>
          </div>
        </div>

        {showActions && (
          <button
            type="button"
            onClick={isConnected ? onDisconnect : onConnect}
            className={`inline-flex items-center gap-2 rounded-xl px-4 py-2 text-sm font-semibold transition-colors ${
              isConnected
                ? 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                : 'bg-primary-600 text-white hover:bg-primary-700'
            }`}
          >
            {isConnected ? <Plug2 className="h-4 w-4" /> : <PlugZap className="h-4 w-4" />}
            {isConnected ? 'Desconectar' : 'Conectar bascula'}
          </button>
        )}
      </div>

      {helperText && (
        <p className="mt-3 text-xs text-gray-500">{helperText}</p>
      )}

      {error && (
        <div className="mt-3 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-700">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}
    </div>
  );
};

export default ScaleIndicator;
