import { Scale, PlugZap, Plug2, AlertTriangle } from 'lucide-react';

const ScaleIndicator = ({ weight, isStable, isConnected, error, onConnect, onDisconnect }) => {
  return (
    <div className="rounded-2xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className={`rounded-xl p-3 ${isConnected ? 'bg-green-50 text-green-600' : 'bg-gray-100 text-gray-500'}`}>
            <Scale className="h-6 w-6" />
          </div>

          <div>
            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Báscula</p>
            <p className="text-2xl font-black text-gray-900">{weight.toFixed(3)} kg</p>
            <p className={`text-xs font-medium ${isConnected ? (isStable ? 'text-green-600' : 'text-amber-600') : 'text-gray-400'}`}>
              {!isConnected ? 'Desconectada' : isStable ? 'Peso estable' : 'Peso inestable'}
            </p>
          </div>
        </div>

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
          {isConnected ? 'Desconectar' : 'Conectar báscula'}
        </button>
      </div>

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
