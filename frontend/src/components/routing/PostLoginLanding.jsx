import { Navigate } from 'react-router-dom';
import useAuthStore from '../../stores/authStore';

const PostLoginLanding = () => {
  const { isAuthenticated, user } = useAuthStore();

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (user?.role === 'platform_admin' && user?.isPlatformAdmin === true) {
    return <Navigate to="/admin/tenants" replace />;
  }
  return <Navigate to="/" replace />;
};

export default PostLoginLanding;
