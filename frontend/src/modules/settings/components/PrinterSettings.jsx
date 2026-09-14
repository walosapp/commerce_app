import { useEffect, useState } from 'react';
import { CircleDot, Link, Loader2, Printer, RefreshCw, Save, Unlock } from 'lucide-react';
import toast from 'react-hot-toast';
import useAuthStore from '../../../stores/authStore';
import usePrintAgentStore from '../../../stores/printAgentStore';

const PrinterSettings = () => {
  const companyId = useAuthStore((state) => state.tenantId);
  const branchId = useAuthStore((state) => state.branchId);
  const store = usePrintAgentStore();
  const [pairingCode, setPairingCode] = useState('');

  useEffect(() => {
    store.initializeContext({ companyId, branchId });
    store.checkHealth();
    // Store actions are stable. Context changes must refresh company and branch.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyId, branchId]);

  const isConnected = store.status === 'connected';
  const canUseAgent = isConnected && Boolean(store.token);
  const canRunCommands = canUseAgent && store.configurationSaved && Boolean(store.config.printerName);

  const pair = async () => {
    if (!/^\d{6}$/.test(pairingCode)) {
      toast.error('Ingresa el codigo de 6 digitos mostrado por el agente.');
      return;
    }
    if (!store.config.companyId || !store.config.branchId) {
      toast.error('No se pudo determinar el comercio o la sede de la sesion.');
      return;
    }

    try {
      await store.pair(pairingCode);
      setPairingCode('');
      toast.success('Agente vinculado correctamente.');
      await store.loadPrinters();
    } catch {
      // El store expone el detalle seguro del error.
    }
  };

  const save = async () => {
    if (!store.config.printerName) {
      toast.error('Selecciona una impresora.');
      return;
    }
    try {
      await store.saveConfig();
      toast.success('Configuracion de impresora guardada localmente.');
    } catch {}
  };

  const runCommand = async (command) => {
    try {
      const result = await (command === 'test-print' ? store.testPrint() : store.openDrawer());
      if (result?.status === 'failed' || result?.status === 'uncertain') {
        toast.error('El trabajo no se repetira porque su resultado fisico es incierto. Usa un job nuevo solo tras verificar el equipo.');
      } else {
        toast.success(result?.status === 'replayed' ? 'Trabajo ya procesado; no se repitio.' : 'Comando enviado correctamente.');
      }
    } catch {}
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-gray-200 bg-gray-50 p-4">
        <div className="flex items-center gap-3">
          <span className={`h-3 w-3 rounded-full ${isConnected ? 'bg-emerald-500' : 'bg-gray-400'}`} />
          <div>
            <p className="text-sm font-semibold text-gray-900">
              {store.isChecking ? 'Detectando agente...' : isConnected ? 'Walos Print Agent conectado' : 'Walos Print Agent desconectado'}
            </p>
            <p className="text-xs text-gray-500">
              {store.version ? `Version ${store.version} · 127.0.0.1:17831` : 'Agente local en 127.0.0.1:17831'}
            </p>
          </div>
        </div>
        <button type="button" className="btn-secondary inline-flex items-center gap-2" onClick={store.checkHealth} disabled={store.isChecking}>
          {store.isChecking ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
          Detectar agente
        </button>
      </div>

      {store.error && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          {store.error}
        </div>
      )}

      {isConnected && !store.token && (
        <div className="rounded-2xl border border-sky-200 bg-sky-50 p-4">
          <div className="mb-3 flex items-center gap-2">
            <Link className="h-4 w-4 text-sky-700" />
            <h4 className="text-sm font-semibold text-gray-900">Vincular este navegador</h4>
          </div>
          <p className="mb-3 text-xs text-gray-600">Ingresa el codigo temporal de seis digitos visible en el icono de Walos Print Agent.</p>
          <div className="flex max-w-md gap-2">
            <input
              className="input"
              inputMode="numeric"
              maxLength={6}
              value={pairingCode}
              onChange={(event) => setPairingCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
              placeholder="000000"
              aria-label="Codigo de vinculacion"
            />
            <button type="button" className="btn-primary" onClick={pair} disabled={store.isPairing}>
              {store.isPairing ? 'Vinculando...' : 'Vincular'}
            </button>
          </div>
        </div>
      )}

      <fieldset disabled={!canUseAgent} className="space-y-4 disabled:opacity-60">
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Impresora instalada</label>
            <div className="flex gap-2">
              <select
                className="input"
                aria-label="Impresora instalada"
                value={store.config.printerName}
                onChange={(event) => store.updateConfig({ printerName: event.target.value })}
              >
                <option value="">Seleccionar impresora...</option>
                {store.printers.map((printer) => (
                  <option key={printer.name} value={printer.name}>
                    {printer.name}{printer.isDefault ? ' (predeterminada)' : ''}
                  </option>
                ))}
              </select>
              <button type="button" className="btn-secondary" onClick={store.loadPrinters} disabled={store.isLoadingPrinters} title="Actualizar impresoras">
                {store.isLoadingPrinters ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
              </button>
            </div>
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Identificador del puesto</label>
            <input className="input" value={store.config.workstationId} readOnly />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">Pin del cajon</label>
            <select className="input" value={store.config.drawerPin} onChange={(event) => store.updateConfig({ drawerPin: Number(event.target.value) })}>
              <option value={0}>Pin 2 (m = 0)</option>
              <option value={1}>Pin 5 (m = 1)</option>
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Pulso ON (ms)</label>
              <input type="number" min="10" max="500" className="input" value={store.config.drawerOnTimeMs} onChange={(event) => store.updateConfig({ drawerOnTimeMs: Number(event.target.value) })} />
            </div>
            <div>
              <label className="mb-1 block text-sm font-medium text-gray-700">Pulso OFF (ms)</label>
              <input type="number" min="10" max="510" className="input" value={store.config.drawerOffTimeMs} onChange={(event) => store.updateConfig({ drawerOffTimeMs: Number(event.target.value) })} />
            </div>
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          <button type="button" className="btn-primary inline-flex items-center gap-2" onClick={save} disabled={store.isSaving || !store.config.printerName}>
            <Save className="h-4 w-4" />
            {store.isSaving ? 'Guardando...' : 'Guardar configuracion'}
          </button>
          <button type="button" className="btn-secondary inline-flex items-center gap-2" onClick={() => runCommand('test-print')} disabled={Boolean(store.activeCommand) || !canRunCommands}>
            <Printer className="h-4 w-4" /> Imprimir prueba
          </button>
          <button type="button" className="btn-secondary inline-flex items-center gap-2" onClick={() => runCommand('open-drawer')} disabled={Boolean(store.activeCommand) || !canRunCommands}>
            <Unlock className="h-4 w-4" /> Abrir cajon
          </button>
        </div>
      </fieldset>

      <div className="grid gap-2 text-xs text-gray-500 md:grid-cols-3">
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Comercio: {store.config.companyId || 'sin contexto'}</span>
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Sede: {store.config.branchId || 'sin contexto'}</span>
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Configuracion solo local</span>
      </div>
    </div>
  );
};

export default PrinterSettings;
