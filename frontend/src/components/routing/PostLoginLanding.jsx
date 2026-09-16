import { Navigate } from 'react-router-dom';
import useAuthStore from '../../stores/authStore';
import { getDefaultRouteForUser } from '../../config/companyFeatures';
import useCompanyFeatures from '../../hooks/useCompanyFeatures';

const PostLoginLanding = () => {
  const { isAuthenticated, user } = useAuthStore();
  const { canAccess, isError, isReady } = useCompanyFeatures();

  if (!isAuthenticated) return <Navigate to="/login" replace />;

  const immediateRoute = getDefaultRouteForUser(user);
  if (immediateRoute !== '/landing') return <Navigate to={immediateRoute} replace />;

  if (!isReady) {
    return <div>{isError ? 'No fue posible verificar los módulos' : 'Cargando módulos...'}</div>;
  }

  const operatorRoute = getDefaultRouteForUser(user, canAccess);
  if (!operatorRoute) return <div>Tu usuario no tiene módulos operativos habilitados.</div>;

  return <Navigate to={operatorRoute} replace />;
};

export default PostLoginLanding;
