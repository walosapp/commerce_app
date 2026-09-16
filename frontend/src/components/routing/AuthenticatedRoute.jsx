import { Navigate } from 'react-router-dom';
import Layout from '../layout/Layout';
import useAuthStore from '../../stores/authStore';

const AuthenticatedRoute = ({ children }) => {
  const { isAuthenticated } = useAuthStore();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  return <Layout>{children}</Layout>;
};

export default AuthenticatedRoute;
