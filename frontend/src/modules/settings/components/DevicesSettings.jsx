import { Activity, Bluetooth, Cable, Cpu, RefreshCw, Scale, Settings2, Usb } from 'lucide-react';
import useDeviceStore from '../../../stores/deviceStore';

const DEVICE_TYPES = [
  { value: 'scale', label: 'Bascula', description: 'Lectura de peso en tiempo real para POS-Deli', icon: Scale },
  { value: 'barcode', label: 'Lector de codigo', description: 'Pendiente de configuracion', icon: Cable },
  { value: 'printer', label: 'Impresora', description: 'Pendiente de configuracion', icon: Bluetooth },
];

const DevicesSettings = () => {
  const selectedDeviceType = useDeviceStore((state) => state.selectedDeviceType);
  const setSelectedDeviceType = useDeviceStore((state) => state.setSelectedDeviceType);
  const scaleConfig = useDeviceStore((state) => state.scaleConfig);
  const updateScaleConfig = useDeviceStore((state) => state.updateScaleConfig);
  const connectScale = useDeviceStore((state) => state.connectScale);
  const disconnectScale = useDeviceStore((state) => state.disconnectScale);
  const clearScaleError = useDeviceStore((state) => state.clearScaleError);
  const scale = useDeviceStore((state) => state.scale);

  const selectedDevice = DEVICE_TYPES.find((item) => item.value === selectedDeviceType) ?? DEVICE_TYPES[0];
  const isScale = selectedDeviceType === 'scale';

  return (
    <div className="space-y-6">
      <section className="card space-y-4">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-sky-100 p-3 text-sky-700">
            <Cpu className="h-5 w-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-gray-900">Dispositivos</h2>
            <p className="text-sm text-gray-500">Administra hardware conectado a la aplicacion</p>
          </div>
        </div>

        <div className="grid gap-3 md:grid-cols-3">
          {DEVICE_TYPES.map(({ value, label, description, icon: Icon }) => (
            <button
              key={value}
              type="button"
              onClick={() => setSelectedDeviceType(value)}
              className={`rounded-2xl border p-4 text-left transition-colors ${
                selectedDeviceType === value
                  ? 'border-primary-600 bg-primary-50'
                  : 'border-gray-200 bg-white hover:border-gray-300'
              }`}
            >
              <div className="mb-3 flex items-center gap-3">
                <div className={`rounded-xl p-2 ${selectedDeviceType === value ? 'bg-primary-100 text-primary-700' : 'bg-gray-100 text-gray-600'}`}>
                  <Icon className="h-4 w-4" />
                </div>
                <span className="text-sm font-semibold text-gray-900">{label}</span>
              </div>
              <p className="text-xs text-gray-500">{description}</p>
            </button>
          ))}
        </div>
      </section>

      <section className="card space-y-4">
        <div className="flex items-center gap-3">
          <div className="rounded-xl bg-emerald-100 p-3 text-emerald-700">
            <Settings2 className="h-5 w-5" />
          </div>
          <div>
            <h3 className="text-base font-semibold text-gray-900">Configuracion de {selectedDevice.label}</h3>
            <p className="text-sm text-gray-500">
              {isScale ? 'Defini el tipo de conexion y proba lectura real desde la bascula.' : 'Este tipo de dispositivo todavia no tiene integracion activa.'}
            </p>
          </div>
        </div>

        {isScale ? (
          <>
            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Nombre del dispositivo</label>
                <input
                  className="input"
                  value={scaleConfig.label}
                  onChange={(event) => updateScaleConfig({ label: event.target.value })}
                  placeholder="Ej: Bascula deli principal"
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Tipo de conexion</label>
                <select
                  className="input"
                  value={scaleConfig.connectionType}
                  onChange={(event) => updateScaleConfig({ connectionType: event.target.value })}
                >
                  <option value="serial">Serial / USB</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Unidad mostrada</label>
                <select
                  className="input"
                  value={scaleConfig.weightUnit}
                  onChange={(event) => updateScaleConfig({ weightUnit: event.target.value })}
                >
                  <option value="kg">kg</option>
                  <option value="g">g</option>
                  <option value="lb">lb</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Baud rate</label>
                <input
                  type="number"
                  className="input"
                  value={scaleConfig.baudRate}
                  onChange={(event) => updateScaleConfig({ baudRate: Number(event.target.value || 9600) })}
                />
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Data bits</label>
                <select
                  className="input"
                  value={scaleConfig.dataBits}
                  onChange={(event) => updateScaleConfig({ dataBits: Number(event.target.value) })}
                >
                  <option value={8}>8</option>
                  <option value={7}>7</option>
                </select>
              </div>
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">Stop bits</label>
                <select
                  className="input"
                  value={scaleConfig.stopBits}
                  onChange={(event) => updateScaleConfig({ stopBits: Number(event.target.value) })}
                >
                  <option value={1}>1</option>
                  <option value={2}>2</option>
                </select>
              </div>
            </div>

            <div className="rounded-2xl border border-gray-200 bg-gray-50 p-4">
              <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h4 className="text-sm font-semibold text-gray-900">Prueba de conexion</h4>
                  <p className="text-xs text-gray-500">Conecta la bascula y valida si realmente esta transmitiendo peso.</p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    onClick={scale.isConnected ? disconnectScale : connectScale}
                    className={`rounded-xl px-4 py-2 text-sm font-semibold transition-colors ${
                      scale.isConnected
                        ? 'bg-gray-200 text-gray-700 hover:bg-gray-300'
                        : 'bg-primary-600 text-white hover:bg-primary-700'
                    }`}
                  >
                    {scale.isConnected ? 'Desconectar' : scale.isConnecting ? 'Conectando...' : 'Conectar bascula'}
                  </button>
                  {scale.error && (
                    <button
                      type="button"
                      onClick={clearScaleError}
                      className="rounded-xl border border-amber-300 bg-white px-3 py-2 text-xs font-semibold text-amber-700 hover:bg-amber-50"
                    >
                      Limpiar error
                    </button>
                  )}
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
                <div className="rounded-xl border border-gray-200 bg-white p-4">
                  <div className="mb-2 flex items-center gap-2 text-gray-500">
                    <Usb className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">Estado</span>
                  </div>
                  <p className={`text-sm font-semibold ${scale.isConnected ? 'text-green-600' : 'text-gray-500'}`}>
                    {scale.isConnected ? 'Conectada' : scale.isConnecting ? 'Conectando...' : 'Desconectada'}
                  </p>
                </div>

                <div className="rounded-xl border border-gray-200 bg-white p-4">
                  <div className="mb-2 flex items-center gap-2 text-gray-500">
                    <Scale className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">Peso</span>
                  </div>
                  <p className="text-2xl font-black text-gray-900">
                    {scale.weight.toFixed(3)} {scaleConfig.weightUnit}
                  </p>
                </div>

                <div className="rounded-xl border border-gray-200 bg-white p-4">
                  <div className="mb-2 flex items-center gap-2 text-gray-500">
                    <Activity className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">Lectura</span>
                  </div>
                  <p className={`text-sm font-semibold ${scale.isStable ? 'text-green-600' : 'text-amber-600'}`}>
                    {scale.isStable ? 'Estable' : 'Sin estabilizar'}
                  </p>
                </div>

                <div className="rounded-xl border border-gray-200 bg-white p-4">
                  <div className="mb-2 flex items-center gap-2 text-gray-500">
                    <RefreshCw className="h-4 w-4" />
                    <span className="text-xs font-semibold uppercase tracking-wide">Ultima lectura</span>
                  </div>
                  <p className="text-sm font-semibold text-gray-900">
                    {scale.lastReadingAt ? new Date(scale.lastReadingAt).toLocaleTimeString() : 'Sin datos'}
                  </p>
                </div>
              </div>

              <div className="mt-4 rounded-xl border border-dashed border-gray-300 bg-white p-4">
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">Trama recibida</p>
                <p className="mt-1 font-mono text-sm text-gray-700">
                  {scale.lastRawLine || 'Todavia no se recibio lectura desde la bascula.'}
                </p>
              </div>

              {scale.error && (
                <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
                  {scale.error}
                </div>
              )}
            </div>
          </>
        ) : (
          <div className="rounded-2xl border border-dashed border-gray-300 bg-gray-50 p-6 text-sm text-gray-500">
            La seleccion del tipo de dispositivo ya queda guardada, pero la integracion activa hoy solo existe para basculas.
          </div>
        )}
      </section>
    </div>
  );
};

export default DevicesSettings;
