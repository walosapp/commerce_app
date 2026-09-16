import { useState } from 'react';
import { KeyRound, UserCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import authService from '../../services/authService';
import useAuthStore from '../../stores/authStore';
import { validatePassword } from '../../utils/passwordPolicy';

const ProfilePage = () => {
  const user = useAuthStore((state) => state.user);
  const updateTokens = useAuthStore((state) => state.updateTokens);
  const [form, setForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [saving, setSaving] = useState(false);

  const update = (field) => (event) => setForm((current) => ({
    ...current,
    [field]: event.target.value,
  }));

  const submit = async (event) => {
    event.preventDefault();
    if (!form.currentPassword) {
      toast.error('Ingresá tu contraseña actual');
      return;
    }
    if (form.newPassword !== form.confirmPassword) {
      toast.error('La confirmación de contraseña no coincide');
      return;
    }
    const policyError = validatePassword(form.newPassword);
    if (policyError) {
      toast.error(policyError);
      return;
    }
    if (form.currentPassword === form.newPassword) {
      toast.error('La nueva contraseña debe ser diferente a la actual');
      return;
    }

    setSaving(true);
    try {
      const result = await authService.changePassword(
        form.currentPassword,
        form.newPassword,
        form.confirmPassword,
      );
      if (!updateTokens(result.data)) {
        throw new Error('No fue posible conservar la sesion actual');
      }
      setForm({ currentPassword: '', newPassword: '', confirmPassword: '' });
      toast.success('Contraseña actualizada');
    } catch (error) {
      toast.error(error?.response?.data?.message || 'No fue posible cambiar la contraseña');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <header className="flex items-center gap-3">
        <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-primary-100 text-primary-700">
          <UserCircle className="h-6 w-6" />
        </div>
        <div>
          <h1 className="text-xl font-bold text-gray-900">Perfil</h1>
          <p className="text-sm text-gray-500">{user?.email}</p>
        </div>
      </header>

      <section className="rounded-2xl border border-gray-200 bg-white p-6 shadow-sm">
        <div className="mb-5 flex items-center gap-2">
          <KeyRound className="h-5 w-5 text-primary-600" />
          <h2 className="text-base font-semibold text-gray-900">Cambiar mi contraseña</h2>
        </div>

        <form className="space-y-4" onSubmit={submit}>
          <label className="block text-sm font-medium text-gray-700">
            Contraseña actual
            <input
              type="password"
              autoComplete="current-password"
              value={form.currentPassword}
              onChange={update('currentPassword')}
              className="input mt-1 w-full"
            />
          </label>
          <label className="block text-sm font-medium text-gray-700">
            Nueva contraseña
            <input
              type="password"
              autoComplete="new-password"
              value={form.newPassword}
              onChange={update('newPassword')}
              className="input mt-1 w-full"
            />
          </label>
          <label className="block text-sm font-medium text-gray-700">
            Confirmar nueva contraseña
            <input
              type="password"
              autoComplete="new-password"
              value={form.confirmPassword}
              onChange={update('confirmPassword')}
              className="input mt-1 w-full"
            />
          </label>

          <p className="text-xs text-gray-500">
            Mínimo 8 caracteres, con mayúscula, minúscula, número y carácter especial.
          </p>
          <button type="submit" disabled={saving} className="btn-primary">
            {saving ? 'Actualizando...' : 'Actualizar contraseña'}
          </button>
        </form>
      </section>
    </div>
  );
};

export default ProfilePage;
