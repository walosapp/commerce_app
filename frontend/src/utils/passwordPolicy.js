export const validatePassword = (password) => {
  if (!password || password.length < 8) return 'La contraseña debe tener al menos 8 caracteres';
  if (/[^\x21-\x7E]/.test(password)) return 'Usá solo caracteres ASCII visibles, sin espacios';
  if (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password) || !/[^A-Za-z0-9]/.test(password)) {
    return 'Incluí mayúscula, minúscula, número y carácter especial';
  }
  return null;
};
