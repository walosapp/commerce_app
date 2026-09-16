import { describe, expect, it } from 'vitest';
import {
  FEATURE_CODES,
  canAccessPlatform,
  canDeleteCatalog,
  canManageCatalog,
  canManageSettings,
  canManageTenantUsers,
  canManageDelivery,
  canCancelSalesTable,
  canInvoiceSales,
  canOperateCash,
  canReviewSales,
  canRoleAccessFeature,
  canWriteInventory,
  getDefaultRouteForUser,
  isPlatformOnlyUser,
  isTrustedDev,
} from '../../config/companyFeatures';

describe('company feature access policy', () => {
  it('keeps dashboard mandatory and defines every V1 feature once', () => {
    expect(FEATURE_CODES).toEqual([
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
  });

  it('recognizes only a platform-capable dev as trusted dev', () => {
    expect(isTrustedDev({ role: 'dev', isPlatformAdmin: true })).toBe(true);
    expect(isTrustedDev({ role: 'dev', isPlatformAdmin: false })).toBe(false);
    expect(isTrustedDev({ role: 'platform_admin', isPlatformAdmin: true })).toBe(false);
    expect(canAccessPlatform({ role: 'platform_admin', isPlatformAdmin: true })).toBe(true);
    expect(canAccessPlatform({ role: 'manager', isPlatformAdmin: true })).toBe(false);
    expect(canRoleAccessFeature({ role: 'dev', isPlatformAdmin: false }, 'inventory')).toBe(false);
  });

  it('keeps platform administrators out of the operational shell', () => {
    const user = { role: 'platform_admin', isPlatformAdmin: true };
    expect(isPlatformOnlyUser(user)).toBe(true);
    expect(canRoleAccessFeature(user, 'dashboard')).toBe(false);
    expect(canRoleAccessFeature(user, 'restaurant')).toBe(false);
  });

  it('keeps restaurant and POS capabilities independent', () => {
    const waiter = { role: 'waiter', isPlatformAdmin: false };
    expect(canRoleAccessFeature(waiter, 'restaurant')).toBe(true);
    expect(canRoleAccessFeature(waiter, 'pos')).toBe(false);
  });

  it.each([
    ['super_admin', ['dashboard', 'inventory', 'restaurant', 'pos', 'cash', 'purchases', 'suppliers', 'delivery', 'finance', 'ai']],
    ['manager', ['dashboard', 'inventory', 'restaurant', 'pos', 'cash', 'purchases', 'suppliers', 'delivery', 'finance', 'ai']],
    ['cashier', ['restaurant', 'pos', 'cash', 'delivery']],
    ['waiter', ['restaurant', 'delivery']],
  ])('matches the frozen V1 module matrix for %s', (role, allowedFeatures) => {
    const user = { role, isPlatformAdmin: false };

    for (const feature of FEATURE_CODES) {
      expect(canRoleAccessFeature(user, feature), `${role} -> ${feature}`)
        .toBe(allowedFeatures.includes(feature));
    }
  });

  it('fails closed for non-canonical roles and matches cash/delivery policies', () => {
    const admin = { role: 'admin', isPlatformAdmin: false };
    const cashier = { role: 'cashier', isPlatformAdmin: false };
    const waiter = { role: 'waiter', isPlatformAdmin: false };
    const manager = { role: 'manager', isPlatformAdmin: false };

    expect(canRoleAccessFeature(admin, 'dashboard')).toBe(false);
    expect(canRoleAccessFeature(admin, 'inventory')).toBe(false);
    expect(canRoleAccessFeature(admin, 'pos')).toBe(false);
    expect(canRoleAccessFeature(admin, 'restaurant')).toBe(false);
    expect(canRoleAccessFeature(admin, 'cash')).toBe(false);
    expect(canRoleAccessFeature(admin, 'purchases')).toBe(false);
    expect(canRoleAccessFeature(admin, 'suppliers')).toBe(false);
    expect(canRoleAccessFeature(admin, 'delivery')).toBe(false);
    expect(canRoleAccessFeature(admin, 'finance')).toBe(false);
    expect(canRoleAccessFeature(admin, 'ai')).toBe(false);
    expect(canManageCatalog(admin)).toBe(false);
    expect(canDeleteCatalog(admin)).toBe(false);
    expect(canWriteInventory(admin)).toBe(false);
    expect(canManageSettings(admin)).toBe(false);
    expect(canManageTenantUsers(admin)).toBe(false);
    expect(canRoleAccessFeature(cashier, 'cash')).toBe(true);
    expect(canRoleAccessFeature(cashier, 'finance')).toBe(false);
    expect(canOperateCash(cashier)).toBe(true);
    expect(canOperateCash(waiter)).toBe(false);
    expect(canInvoiceSales(cashier)).toBe(true);
    expect(canInvoiceSales(waiter)).toBe(false);
    expect(canCancelSalesTable(cashier)).toBe(true);
    expect(canCancelSalesTable(waiter)).toBe(false);
    expect(canReviewSales(cashier)).toBe(true);
    expect(canReviewSales(waiter)).toBe(false);
    expect(canManageDelivery(manager)).toBe(true);
    expect(canManageDelivery(cashier)).toBe(true);
    expect(canManageDelivery(waiter)).toBe(false);
  });

  it('routes each shell identity to an allowed landing page', () => {
    expect(getDefaultRouteForUser({ role: 'platform_admin', isPlatformAdmin: true })).toBe('/admin/tenants');
    expect(getDefaultRouteForUser({ role: 'dev', isPlatformAdmin: true })).toBe('/');
    expect(getDefaultRouteForUser({ role: 'cashier', isPlatformAdmin: false })).toBe('/landing');
    expect(getDefaultRouteForUser({ role: 'waiter', isPlatformAdmin: false })).toBe('/landing');
    expect(getDefaultRouteForUser(
      { role: 'cashier', isPlatformAdmin: false },
      (feature) => feature === 'pos',
    )).toBe('/pos-deli');
    expect(getDefaultRouteForUser(
      { role: 'waiter', isPlatformAdmin: false },
      (feature) => feature === 'delivery',
    )).toBe('/delivery');
  });

  it('matches Settings, Users and InventoryWrite backend policies', () => {
    const dev = { role: 'dev', isPlatformAdmin: true };
    const manager = { role: 'manager', isPlatformAdmin: false };
    const cashier = { role: 'cashier', isPlatformAdmin: false };
    const waiter = { role: 'waiter', isPlatformAdmin: false };

    expect(canManageSettings(dev)).toBe(true);
    expect(canManageTenantUsers(manager)).toBe(true);
    expect(canManageSettings(cashier)).toBe(false);
    expect(canManageTenantUsers(waiter)).toBe(false);
    expect(canWriteInventory(manager)).toBe(true);
    expect(canWriteInventory(cashier)).toBe(false);
    expect(canManageCatalog(manager)).toBe(true);
    expect(canDeleteCatalog(manager)).toBe(true);
  });
});
