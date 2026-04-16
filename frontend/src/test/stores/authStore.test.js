import { describe, it, expect, beforeEach } from 'vitest'
import { createStore } from 'zustand/vanilla'

describe('authStore', () => {
  let store

  beforeEach(() => {
    store = createStore((set, get) => ({
      user: null,
      token: null,
      tenantId: null,
      branchId: null,
      isAuthenticated: false,

      setAuth: (data) => {
        set({
          user: data.user,
          token: data.token,
          tenantId: data.user?.companyId,
          branchId: data.user?.branchId,
          isAuthenticated: true,
        })
      },

      logout: () => {
        set({
          user: null,
          token: null,
          tenantId: null,
          branchId: null,
          isAuthenticated: false,
        })
      },

      hasPermission: (module, action) => {
        const { user } = get()
        if (!user?.permissions) return false
        return (
          user.permissions.all?.[action] === true ||
          user.permissions[module]?.[action] === true
        )
      },
    }))
  })

  it('should initialize with null token and user', () => {
    const state = store.getState()
    expect(state.token).toBeNull()
    expect(state.user).toBeNull()
    expect(state.isAuthenticated).toBe(false)
  })

  it('should set auth data correctly', () => {
    const testUser = { 
      id: 1, 
      name: 'Test User', 
      email: 'test@test.com',
      companyId: 10,
      branchId: 5
    }
    store.getState().setAuth({ token: 'jwt-token-123', user: testUser })

    const state = store.getState()
    expect(state.token).toBe('jwt-token-123')
    expect(state.user).toEqual(testUser)
    expect(state.tenantId).toBe(10)
    expect(state.branchId).toBe(5)
    expect(state.isAuthenticated).toBe(true)
  })

  it('should clear auth on logout', () => {
    store.getState().setAuth({ token: 'token', user: { id: 1, companyId: 1 } })
    store.getState().logout()

    const state = store.getState()
    expect(state.token).toBeNull()
    expect(state.user).toBeNull()
    expect(state.tenantId).toBeNull()
    expect(state.branchId).toBeNull()
    expect(state.isAuthenticated).toBe(false)
  })

  it('should check permissions correctly', () => {
    const testUser = { 
      id: 1, 
      name: 'Admin',
      permissions: {
        all: { read: true, write: true },
        inventory: { read: true, write: false }
      }
    }
    store.getState().setAuth({ token: 'token', user: testUser })

    expect(store.getState().hasPermission('inventory', 'read')).toBe(true)
    // 'all.write' is true, so even though 'inventory.write' is false, the OR returns true
    expect(store.getState().hasPermission('inventory', 'write')).toBe(true)
    expect(store.getState().hasPermission('sales', 'read')).toBe(true)
    // 'all.delete' is undefined, 'inventory.delete' is undefined → false
    expect(store.getState().hasPermission('inventory', 'delete')).toBe(false)
  })

  it('should return false for permissions when not authenticated', () => {
    expect(store.getState().hasPermission('inventory', 'read')).toBe(false)
  })
})
