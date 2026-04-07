/**
 * Formato de moneda consistente para toda la app
 * Usa separador de miles con punto y decimales con coma (formato COP)
 */
export const formatCurrency = (value, decimals = 0) => {
  if (value == null || isNaN(value)) return '-';
  return `$${Number(value).toLocaleString('es-CO', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })}`;
};
