export const FEATURE_CODES = Object.freeze([
  'dashboard',
  'inventory',
  'restaurant',
  'pos',
  'cash',
  'purchases',
  'suppliers',
  'delivery',
  'finance',
  'ai',
]);

export const FEATURE_LABELS = Object.freeze({
  dashboard: 'Dashboard',
  inventory: 'Inventario',
  restaurant: 'Restaurante',
  pos: 'POS',
  cash: 'Caja',
  purchases: 'Compras',
  suppliers: 'Proveedores',
  delivery: 'Delivery',
  finance: 'Finanzas',
  ai: 'IA',
});

const ROLE_FEATURES = Object.freeze({
  super_admin: FEATURE_CODES,
  manager: FEATURE_CODES,
  cashier: ['restaurant', 'pos', 'cash', 'delivery'],
  waiter: ['restaurant', 'delivery'],
});

export const isTrustedDev = (user) =>
  user?.role === 'dev' && user?.isPlatformAdmin === true;

export const isPlatformOnlyUser = (user) =>
  user?.role === 'platform_admin' && user?.isPlatformAdmin === true;

export const canAccessPlatform = (user) =>
  isTrustedDev(user) || isPlatformOnlyUser(user);

const OPERATOR_LANDING_ROUTES = Object.freeze({
  cashier: [
    ['restaurant', '/sales'],
    ['pos', '/pos-deli'],
    ['cash', '/cash'],
    ['delivery', '/delivery'],
  ],
  waiter: [
    ['restaurant', '/sales'],
    ['delivery', '/delivery'],
  ],
});

export const getDefaultRouteForUser = (user, canAccess = null) => {
  if (isPlatformOnlyUser(user)) return '/admin/tenants';
  const operatorRoutes = OPERATOR_LANDING_ROUTES[user?.role];
  if (operatorRoutes) {
    if (typeof canAccess !== 'function') return '/landing';
    return operatorRoutes.find(([feature]) => canAccess(feature))?.[1] ?? null;
  }
  return '/';
};

export const canRoleAccessFeature = (user, featureCode) => {
  if (!FEATURE_CODES.includes(featureCode) || isPlatformOnlyUser(user)) return false;
  if (isTrustedDev(user)) return true;
  return ROLE_FEATURES[user?.role]?.includes(featureCode) === true;
};

export const canManageTenantUsers = (user) =>
  !isPlatformOnlyUser(user) && (isTrustedDev(user) || ['super_admin', 'manager'].includes(user?.role));

export const canManageSettings = canManageTenantUsers;

export const canWriteInventory = (user) =>
  !isPlatformOnlyUser(user) && (isTrustedDev(user) || ['super_admin', 'manager'].includes(user?.role));

export const canManageCatalog = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager'].includes(user?.role));

export const canDeleteCatalog = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager'].includes(user?.role));

export const canOperateCash = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager', 'cashier'].includes(user?.role));

export const canInvoiceSales = canOperateCash;

export const canCancelSalesTable = canOperateCash;

export const canReviewSales = canOperateCash;

export const canManageDelivery = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager', 'cashier'].includes(user?.role));
