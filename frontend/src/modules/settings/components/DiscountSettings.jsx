/**
 * Formulario de Descuentos
 * ¿Qué es? Seccion de reglas operativas para descuentos manuales
 * ¿Para qué? Definir topes y validaciones que usa la facturacion
 */

const DiscountSettings = ({ values, onChange }) => {
  return (
    <div className="card space-y-5">
      <div>
        <h2 className="text-lg font-bold text-gray-900">Reglas de descuentos</h2>
        <p className="mt-1 text-sm text-gray-500">
          Controla cuánto puede descontar el equipo al momento de facturar.
        </p>
      </div>

      <label className="flex items-start gap-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
        <input
          type="checkbox"
          checked={values.manualDiscountEnabled}
          onChange={(e) => onChange('manualDiscountEnabled', e.target.checked)}
          className="mt-1 h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
        />
        <div>
          <p className="text-sm font-medium text-gray-900">Permitir descuento manual</p>
          <p className="mt-1 text-xs text-gray-500">
            Si se desactiva, la factura solo se podrá cerrar por el total completo.
          </p>
        </div>
      </label>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">Porcentaje máximo permitido</label>
          <input
            type="number"
            min="0"
            max="100"
            step="0.01"
            value={values.maxDiscountPercent}
            onChange={(e) => onChange('maxDiscountPercent', e.target.value)}
            className="input"
            disabled={!values.manualDiscountEnabled}
          />
        </div>
        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">Valor fijo máximo permitido</label>
          <input
            type="number"
            min="0"
            step="1"
            value={values.maxDiscountAmount}
            onChange={(e) => onChange('maxDiscountAmount', e.target.value)}
            className="input"
            disabled={!values.manualDiscountEnabled}
          />
        </div>
      </div>

      <label className="flex items-start gap-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
        <input
          type="checkbox"
          checked={values.discountRequiresOverride}
          onChange={(e) => onChange('discountRequiresOverride', e.target.checked)}
          className="mt-1 h-4 w-4 rounded border-gray-300 text-primary-600 focus:ring-primary-500"
          disabled={!values.manualDiscountEnabled}
        />
        <div>
          <p className="text-sm font-medium text-gray-900">Pedir confirmación adicional</p>
          <p className="mt-1 text-xs text-gray-500">
            Cuando un descuento supere el umbral definido, el cajero tendrá que confirmarlo antes de facturar.
          </p>
        </div>
      </label>

      <div>
        <label className="mb-1 block text-sm font-medium text-gray-700">Umbral para confirmación adicional (%)</label>
        <input
          type="number"
          min="0"
          max="100"
          step="0.01"
          value={values.discountOverrideThresholdPercent}
          onChange={(e) => onChange('discountOverrideThresholdPercent', e.target.value)}
          className="input max-w-xs"
          disabled={!values.manualDiscountEnabled || !values.discountRequiresOverride}
        />
        <p className="mt-2 text-xs text-gray-500">
          Ejemplo: si el máximo es 15% y el umbral es 8%, desde 8% pedirá confirmación adicional; por encima de 15% bloqueará.
        </p>
      </div>
    </div>
  );
};

export default DiscountSettings;
