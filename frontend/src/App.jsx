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
import AlertsPage from './modules/alerts/AlertsPage';
import FinancePage from './modules/finance/FinancePage';
import TenantsPage from './modules/admin/TenantsPage';
import CompaniesPage from './modules/admin/CompaniesPage';
import DeliveryOrdersPage from './modules/delivery/DeliveryOrdersPage';
import SuppliersPage from './modules/suppliers/SuppliersPage';
import UsersPage from './modules/users/UsersPage';

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

const ProtectedRoute = ({ children, requiredRole = null, allowedRoles = null }) => {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  if (requiredRole && user?.role !== requiredRole) {
    return <Navigate to="/" replace />;
  }

  if (allowedRoles && !allowedRoles.includes(user?.role)) {
    return <Navigate to="/" replace />;
  }

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
          <Route path="/" element={<Navigate to="/sales" replace />} />
          <Route path="/inventory" element={<ProtectedRoute><InventoryPage /></ProtectedRoute>} />
          <Route path="/ai-assistant" element={<ProtectedRoute><AiAssistantPage /></ProtectedRoute>} />
          <Route path="/sales" element={<ProtectedRoute><SalesPage /></ProtectedRoute>} />
          <Route path="/finance" element={<ProtectedRoute><FinancePage /></ProtectedRoute>} />
          <Route path="/suppliers" element={<ProtectedRoute><SuppliersPage /></ProtectedRoute>} />
          <Route path="/users" element={<ProtectedRoute allowedRoles={['dev','admin','manager']}><UsersPage /></ProtectedRoute>} />
          <Route path="/settings" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/branding" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/themes" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/discounts" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/catalog" element={<ProtectedRoute allowedRoles={['dev','admin','manager']}><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/plan" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/ai" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/settings/payments" element={<ProtectedRoute><SettingsPage /></ProtectedRoute>} />
          <Route path="/alerts" element={<ProtectedRoute><AlertsPage /></ProtectedRoute>} />
          <Route path="/admin/tenants" element={<ProtectedRoute requiredRole="dev"><TenantsPage /></ProtectedRoute>} />
          <Route path="/admin/companies" element={<ProtectedRoute requiredRole="dev"><CompaniesPage /></ProtectedRoute>} />
          <Route path="/delivery" element={<ProtectedRoute><DeliveryOrdersPage /></ProtectedRoute>} />
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
