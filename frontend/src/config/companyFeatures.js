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
  cashier: ['dashboard', 'inventory', 'restaurant', 'pos', 'cash', 'delivery'],
  waiter: ['dashboard', 'inventory', 'restaurant', 'delivery'],
});

export const isTrustedDev = (user) =>
  user?.role === 'dev' && user?.isPlatformAdmin === true;

export const isPlatformOnlyUser = (user) =>
  user?.role === 'platform_admin' && user?.isPlatformAdmin === true;

export const canAccessPlatform = (user) =>
  isTrustedDev(user) || isPlatformOnlyUser(user);

export const getDefaultRouteForUser = (user) => {
  if (isPlatformOnlyUser(user)) return '/admin/tenants';
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
  (isTrustedDev(user) || user?.role === 'super_admin');

export const canOperateCash = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager', 'cashier'].includes(user?.role));

export const canManageDelivery = (user) =>
  !isPlatformOnlyUser(user) &&
  (isTrustedDev(user) || ['super_admin', 'manager'].includes(user?.role));
