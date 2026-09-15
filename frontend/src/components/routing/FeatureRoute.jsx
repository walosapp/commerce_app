import { Navigate } from 'react-router-dom';
import Layout from '../layout/Layout';
import useCompanyFeatures from '../../hooks/useCompanyFeatures';
import useAuthStore from '../../stores/authStore';

const STATUS_CONTENT = Object.freeze({
  loading: { title: 'Cargando módulos...', message: null },
  error: {
    title: 'No fue posible verificar los módulos',
    message: 'Intentá nuevamente. El acceso permanece bloqueado hasta validar la configuración del comercio.',
  },
  denied: {
    title: 'Acceso denegado',
    message: 'Tu rol no tiene permiso para utilizar este módulo.',
  },
  disabled: {
    title: 'Módulo no habilitado',
    message: 'Este módulo no está habilitado para el comercio.',
  },
});

const FeatureStatus = ({ status }) => {
  const content = STATUS_CONTENT[status];
  return (
    <div className="flex min-h-64 items-center justify-center">
      <div className="max-w-md rounded-xl border border-gray-200 bg-white p-6 text-center shadow-sm">
        <h1 className="text-lg font-semibold text-gray-900">{content.title}</h1>
        {content.message && <p className="mt-2 text-sm text-gray-500">{content.message}</p>}
      </div>
    </div>
  );
};

const FeatureRoute = ({ authorize = null, children, feature }) => {
  const { isAuthenticated, user } = useAuthStore();
  const { hasFeature, isError, isPlatformOnly, isReady, roleAllows } = useCompanyFeatures();

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (isPlatformOnly) return <Navigate to="/admin/tenants" replace />;
  if (!roleAllows(feature) || (authorize && !authorize(user))) {
    return <Layout><FeatureStatus status="denied" /></Layout>;
  }
  if (feature !== 'dashboard' && !isReady) {
    return <Layout><FeatureStatus status={isError ? 'error' : 'loading'} /></Layout>;
  }
  if (!hasFeature(feature)) return <Layout><FeatureStatus status="disabled" /></Layout>;

  return <Layout>{children}</Layout>;
};

export default FeatureRoute;
