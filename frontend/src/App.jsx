/**
 * Componente Principal de la Aplicacion
 * Punto de entrada de React
 */

import React from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'react-hot-toast';
import Layout from './components/layout/Layout';
import InventoryPage from './modules/inventory/InventoryPage';
import SalesPage from './modules/sales/SalesPage';
import AiAssistantPage from './modules/ai-assistant/AiAssistantPage';
import LoginPage from './modules/auth/LoginPage';
import useAuthStore from './stores/authStore';
import { setAuthStateGetter } from './config/api';
import useUiStore from './stores/uiStore';
import DashboardPage from './modules/dashboard/DashboardPage';
import SettingsPage from './modules/settings/SettingsPage';
import CatalogPage from './modules/settings/CatalogPage';
import AlertsPage from './modules/alerts/AlertsPage';
import FinancePage from './modules/finance/FinancePage';
import TenantsPage from './modules/admin/TenantsPage';
import CompaniesPage from './modules/admin/CompaniesPage';
import DeliveryOrdersPage from './modules/delivery/DeliveryOrdersPage';
import SuppliersPage from './modules/suppliers/SuppliersPage';
import UsersPage from './modules/users/UsersPage';
import PosDeliPage from './modules/pos-deli/PosDeliPage';
import ProfilePage from './modules/profile/ProfilePage';
import FeatureRoute from './components/routing/FeatureRoute';
import PostLoginLanding from './components/routing/PostLoginLanding';
import AuthenticatedRoute from './components/routing/AuthenticatedRoute';
import { canAccessPlatform, canManageCatalog, canManageSettings, canManageTenantUsers, isPlatformOnlyUser } from './config/companyFeatures';

setAuthStateGetter(() => useAuthStore.getState());

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      refetchOnMount: true,
      retry: 1,
      staleTime: 0,
    },
  },
});

const ProtectedRoute = ({ children, requiredRole = null, allowedRoles = null, authorize = null }) => {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (isPlatformOnlyUser(user)) {
    return <Navigate to="/admin/tenants" replace />;
  }

  if (requiredRole && user?.role !== requiredRole) {
    return <Navigate to="/" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user?.role)) {
    return <Navigate to="/" replace />;
  }

  if (authorize && !authorize(user)) {
    return <Navigate to="/" replace />;
  }

  return <Layout>{children}</Layout>;
};

const PlatformRoute = ({ children }) => {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (!canAccessPlatform(user)) return <Navigate to="/" replace />;

  return <Layout>{children}</Layout>;
};

const ThemeSync = () => {
  const theme = useUiStore((state) => state.theme);

  React.useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme || 'light');
  }, [theme]);

  return null;
};

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeSync />
      <BrowserRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <Routes>
          <Route path="/" element={<FeatureRoute feature="dashboard"><DashboardPage /></FeatureRoute>} />
          <Route path="/landing" element={<PostLoginLanding />} />
          <Route path="/inventory" element={<FeatureRoute feature="inventory"><InventoryPage /></FeatureRoute>} />
          <Route path="/ai-assistant" element={<FeatureRoute feature="ai"><AiAssistantPage /></FeatureRoute>} />
          <Route path="/sales" element={<FeatureRoute feature="restaurant"><SalesPage initialTab="tables" /></FeatureRoute>} />
          <Route path="/pos-deli" element={<FeatureRoute feature="pos"><PosDeliPage /></FeatureRoute>} />
          <Route path="/cash" element={<FeatureRoute feature="cash"><SalesPage initialTab="cash" /></FeatureRoute>} />
          <Route path="/finance" element={<FeatureRoute feature="finance"><FinancePage /></FeatureRoute>} />
          <Route path="/suppliers" element={<FeatureRoute feature="suppliers"><SuppliersPage initialTab="suppliers" /></FeatureRoute>} />
          <Route path="/purchases" element={<FeatureRoute feature="purchases"><SuppliersPage initialTab="orders" /></FeatureRoute>} />
          <Route path="/users" element={<ProtectedRoute authorize={canManageTenantUsers}><UsersPage /></ProtectedRoute>} />
          <Route path="/profile" element={<AuthenticatedRoute><ProfilePage /></AuthenticatedRoute>} />
          <Route path="/settings" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/branding" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/themes" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/discounts" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/catalog" element={<FeatureRoute feature="inventory" authorize={canManageCatalog}><CatalogPage /></FeatureRoute>} />
          <Route path="/settings/devices" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/plan" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/ai" element={<FeatureRoute feature="ai"><SettingsPage /></FeatureRoute>} />
          <Route path="/settings/payments" element={<ProtectedRoute authorize={canManageSettings}><SettingsPage /></ProtectedRoute>} />
          <Route path="/alerts" element={<FeatureRoute feature="inventory"><AlertsPage /></FeatureRoute>} />
          <Route path="/admin/tenants" element={<PlatformRoute><TenantsPage /></PlatformRoute>} />
          <Route path="/admin/companies" element={<PlatformRoute><CompaniesPage /></PlatformRoute>} />
          <Route path="/delivery" element={<FeatureRoute feature="delivery"><DeliveryOrdersPage /></FeatureRoute>} />
          <Route path="/login" element={<LoginPage />} />
        </Routes>
      </BrowserRouter>

      <Toaster
        position="top-right"
        toastOptions={{
          duration: 3000,
          style: {
            borderRadius: '0.75rem',
            padding: '12px 16px',
          },
          success: {
            iconTheme: {
              primary: '#10b981',
              secondary: '#fff',
            },
          },
          error: {
            iconTheme: {
              primary: '#ef4444',
              secondary: '#fff',
            },
          },
        }}
      />
    </QueryClientProvider>
  );
}

export default App;
