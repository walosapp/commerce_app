import { useEffect, useState } from 'react';
import { CircleDot, Download, Link, Loader2, Printer, RefreshCw, Save, Unlock } from 'lucide-react';
import toast from 'react-hot-toast';
import { resolvePrintAgentDownloadUrl } from '../../../config/printAgent';
import useAuthStore from '../../../stores/authStore';
import usePrintAgentStore from '../../../stores/printAgentStore';
import usePostSaleHardwareStore, { isUnresolvedPostSaleDrawer } from '../../../stores/postSaleHardwareStore';

const DRAWER_STATUS_LABELS = Object.freeze({
  failed: 'Fallida',
  uncertain: 'Resultado incierto',
  stale: 'Requiere revision',
});

const PrinterSettings = () => {
  const companyId = useAuthStore((state) => state.tenantId);
  const branchId = useAuthStore((state) => state.branchId);
  const store = usePrintAgentStore();
  const postSalePolicy = usePostSaleHardwareStore((state) => state.getPolicy(companyId, branchId));
  const updatePostSalePolicy = usePostSaleHardwareStore((state) => state.updatePolicy);
  const postSaleStorageWarning = usePostSaleHardwareStore((state) => state.storageWarning);
  const postSaleIntents = usePostSaleHardwareStore((state) => state.intents);
  const retryPostSaleDrawer = usePostSaleHardwareStore((state) => state.retryPostSaleDrawer);
  const acknowledgePostSaleDrawer = usePostSaleHardwareStore((state) => state.acknowledgePostSaleDrawer);
  const [pairingCode, setPairingCode] = useState('');
  const [drawerActionKey, setDrawerActionKey] = useState(null);
  const downloadUrl = resolvePrintAgentDownloadUrl();

  useEffect(() => {
    store.initializeContext({ companyId, branchId });
    store.checkHealth();
    // Store actions are stable. Context changes must refresh company and branch.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [companyId, branchId]);

  const isConnected = store.status === 'connected';
  const canUseAgent = isConnected && Boolean(store.token);
  const canRunCommands = canUseAgent && store.configurationSaved && Boolean(store.config.printerName);
  const unresolvedDrawerIntents = Object.values(postSaleIntents || {})
    .filter((intent) => (
      Number(intent.companyId) === Number(companyId) &&
      Number(intent.branchId) === Number(branchId) &&
      isUnresolvedPostSaleDrawer(intent)
    ))
    .sort((left, right) => Number(right.orderId) - Number(left.orderId));

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

  const retryDrawer = async (intent) => {
    const actionKey = `${intent.companyId}:${intent.branchId}:${intent.orderId}`;
    setDrawerActionKey(actionKey);
    try {
      const result = await retryPostSaleDrawer({
        companyId: intent.companyId,
        branchId: intent.branchId,
        orderId: intent.orderId,
      });
      toast.success(result?.status === 'replayed'
        ? 'La apertura ya habia sido procesada; no se repitio.'
        : 'Cajon abierto.');
    } catch {
      toast.error('No fue posible reconciliar la apertura. Verifica el cajon antes de volver a intentar.');
    } finally {
      setDrawerActionKey(null);
    }
  };

  const acknowledgeDrawer = (intent) => {
    acknowledgePostSaleDrawer({
      companyId: intent.companyId,
      branchId: intent.branchId,
      orderId: intent.orderId,
    });
    toast.success('Apertura marcada como resuelta.');
  };

  return (
    <div className="space-y-5">
      <section aria-labelledby="walos-agent-title" className="rounded-2xl border border-gray-200 bg-gray-50 p-4">
        <h3 id="walos-agent-title" className="mb-3 text-base font-semibold text-gray-900">Walos Agent</h3>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className={`h-3 w-3 rounded-full ${isConnected ? 'bg-emerald-500' : 'bg-gray-400'}`} />
            <div>
              <p className="text-sm font-semibold text-gray-900">
                {store.isChecking ? 'Detectando agente...' : isConnected ? 'Agente conectado' : 'Agente no detectado'}
              </p>
              <p className="text-xs text-gray-500">
                Versión instalada: {store.version || 'No disponible'}
              </p>
            </div>
          </div>
          <button type="button" className="btn-secondary inline-flex items-center gap-2" onClick={store.checkHealth} disabled={store.isChecking}>
            {store.isChecking ? <Loader2 className="h-4 w-4 animate-spin" /> : <RefreshCw className="h-4 w-4" />}
            Detectar agente
          </button>
        </div>

        {!store.isChecking && !isConnected && (
          <div className="mt-4 border-t border-gray-200 pt-4">
            {downloadUrl ? (
              <a
                href={downloadUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="btn-primary inline-flex items-center gap-2"
              >
                <Download className="h-4 w-4" />
                Descargar Walos Agent
              </a>
            ) : (
              <div>
                <button type="button" className="btn-secondary inline-flex items-center gap-2" disabled>
                  <Download className="h-4 w-4" />
                  Descargar Walos Agent
                </button>
                <p className="mt-2 text-xs text-amber-700">
                  La descarga no está configurada para este entorno. Contactá al administrador.
                </p>
              </div>
            )}
          </div>
        )}
      </section>

      {store.error && (
        <div role="alert" className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          {store.error}
        </div>
      )}

      {postSaleStorageWarning && (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800">
          {postSaleStorageWarning}
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

      <div className="rounded-2xl border border-gray-200 bg-gray-50 p-4">
        <h4 className="text-sm font-semibold text-gray-900">Acciones automaticas despues de la venta</h4>
        <p className="mt-1 text-xs text-gray-500">
          Son preferencias locales de este navegador. La venta se guarda antes de ejecutar cualquier accion fisica.
        </p>
        <div className="mt-3 space-y-3">
          <label className="flex items-start gap-3 text-sm text-gray-700">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4"
              checked={postSalePolicy.autoPrintReceipt}
              disabled={!postSalePolicy.autoPrintReceipt && !canRunCommands}
              onChange={(event) => updatePostSalePolicy(
                { companyId, branchId },
                { autoPrintReceipt: event.target.checked }
              )}
            />
            <span>
              <strong className="block font-medium text-gray-900">Imprimir recibo automaticamente</strong>
              Usa exclusivamente el comprobante persistido del backend.
            </span>
          </label>
          <label className="flex items-start gap-3 text-sm text-gray-700">
            <input
              type="checkbox"
              className="mt-0.5 h-4 w-4"
              checked={postSalePolicy.autoOpenCashDrawer}
              disabled={!postSalePolicy.autoOpenCashDrawer && !canRunCommands}
              onChange={(event) => updatePostSalePolicy(
                { companyId, branchId },
                { autoOpenCashDrawer: event.target.checked }
              )}
            />
            <span>
              <strong className="block font-medium text-gray-900">Abrir cajon con pagos en efectivo</strong>
              Solo se activa si el recibo persistido contiene un pago con metodo exacto cash y monto positivo.
            </span>
          </label>
        </div>
      </div>

      {unresolvedDrawerIntents.length > 0 && (
        <section aria-labelledby="drawer-reconciliation-title" className="rounded-2xl border border-amber-200 bg-amber-50 p-4">
          <h4 id="drawer-reconciliation-title" className="text-sm font-semibold text-gray-900">
            Aperturas de cajon por reconciliar
          </h4>
          <p className="mt-1 text-xs text-gray-600">
            Verifica fisicamente el cajon. Reintentar conserva el mismo trabajo idempotente y nunca imprime el recibo.
          </p>
          <div className="mt-3 space-y-3">
            {unresolvedDrawerIntents.map((intent) => {
              const actionKey = `${intent.companyId}:${intent.branchId}:${intent.orderId}`;
              const isRetrying = drawerActionKey === actionKey;
              return (
                <article key={actionKey} className="rounded-xl border border-amber-200 bg-white p-3">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <p className="text-sm font-medium text-gray-900">
                        Comercio {intent.companyId} · Sede {intent.branchId} · Orden {intent.orderId}
                      </p>
                      <p className="text-xs text-amber-800">
                        Estado: {DRAWER_STATUS_LABELS[intent.drawerStatus] || intent.drawerStatus}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        className="btn-secondary"
                        disabled={!canRunCommands || isRetrying}
                        onClick={() => retryDrawer(intent)}
                      >
                        {isRetrying ? 'Reintentando...' : 'Reintentar apertura'}
                      </button>
                      <button
                        type="button"
                        className="btn-secondary"
                        disabled={isRetrying}
                        onClick={() => acknowledgeDrawer(intent)}
                      >
                        Marcar como resuelto
                      </button>
                    </div>
                  </div>
                </article>
              );
            })}
          </div>
        </section>
      )}

      <div className="grid gap-2 text-xs text-gray-500 md:grid-cols-3">
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Comercio: {store.config.companyId || 'sin contexto'}</span>
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Sede: {store.config.branchId || 'sin contexto'}</span>
        <span className="inline-flex items-center gap-1"><CircleDot className="h-3 w-3" /> Configuracion solo local</span>
      </div>
    </div>
  );
};

export default PrinterSettings;
