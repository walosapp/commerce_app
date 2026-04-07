/**
 * Página de Login
 * ¿Qué es? Formulario de inicio de sesión
 * ¿Para qué? Autenticar al usuario contra el backend
 */

import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { LogIn, Loader2, Eye, EyeOff } from 'lucide-react';
import useAuthStore from '../../stores/authStore';
import authService from '../../services/authService';
import toast from 'react-hot-toast';

const LoginPage = () => {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!username.trim() || !password.trim()) {
      toast.error('Ingresa usuario y contraseña');
      return;
    }

    setIsLoading(true);

    try {
      const result = await authService.login(username, password);

      if (result.success) {
        setAuth({
          token: result.data.token,
          user: result.data.user,
        });

        localStorage.setItem('token', result.data.token);
        localStorage.setItem('tenantId', result.data.user.companyId);
        localStorage.setItem('branchId', result.data.user.branchId);

        toast.success(`Bienvenido, ${result.data.user.name}`);
        navigate('/inventory');
      }
    } catch (error) {
      const msg = error.response?.data?.message || 'Error al iniciar sesión';
      toast.error(msg);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex h-full items-center justify-center bg-gradient-to-br from-primary-50 to-gray-100">
      <div className="w-full max-w-md px-4">
        <div className="card">
          {/* Header */}
          <div className="mb-8 text-center">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-primary-100">
              <LogIn className="h-7 w-7 text-primary-600" />
            </div>
            <h1 className="text-2xl font-bold text-gray-900">Walos</h1>
            <p className="mt-1 text-sm text-gray-500">
              Sistema de Gestión Comercial con IA
            </p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-5">
            <div>
              <label htmlFor="username" className="mb-1.5 block text-sm font-medium text-gray-700">
                Usuario
              </label>
              <input
                id="username"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Ingresa tu usuario"
                autoComplete="username"
                autoFocus
                className="input"
              />
            </div>

            <div>
              <label htmlFor="password" className="mb-1.5 block text-sm font-medium text-gray-700">
                Contraseña
              </label>
              <div className="relative">
                <input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Ingresa tu contraseña"
                  autoComplete="current-password"
                  className="input pr-10"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="btn btn-primary w-full py-2.5"
            >
              {isLoading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Iniciando sesión...
                </>
              ) : (
                <>
                  <LogIn className="mr-2 h-4 w-4" />
                  Iniciar Sesión
                </>
              )}
            </button>
          </form>

          {/* Dev hint */}
          <div className="mt-6 rounded-lg bg-gray-50 p-3 text-center text-xs text-gray-400">
            <p className="font-medium text-gray-500">Credenciales de desarrollo</p>
            <p className="mt-1">Usuario: <span className="font-mono text-gray-600">admin@mibar.com</span> | Contraseña: <span className="font-mono text-gray-600">admin123</span></p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default LoginPage;
