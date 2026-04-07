/**
 * Formulario de Branding
 * ¿Qué es? Sección para editar nombre, contacto y logo del negocio
 * ¿Para qué? Personalizar la identidad visible de la app
 */

import { Camera, ImageIcon, Trash2, Upload } from 'lucide-react';

const BrandingForm = ({
  values,
  onChange,
  logoPreview,
  onLogoSelect,
  onRemoveLogo,
  fileInputRef,
  cameraInputRef,
}) => {
  const handleFileChange = (event) => {
    const file = event.target.files?.[0];
    if (file) {
      onLogoSelect(file);
    }
  };

  const handleDrop = (event) => {
    event.preventDefault();
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      onLogoSelect(file);
    }
  };

  return (
    <section className="card space-y-6">
      <div>
        <h2 className="text-lg font-bold text-gray-900">Branding del negocio</h2>
        <p className="mt-1 text-sm text-gray-500">
          Define cómo se ve tu marca en el header, sidebar y futuras facturas.
        </p>
      </div>

      <div>
        <label className="mb-2 block text-sm font-medium text-gray-700">Logo</label>
        <div
          onDrop={handleDrop}
          onDragOver={(event) => event.preventDefault()}
          className="relative rounded-xl border-2 border-dashed border-gray-300 bg-gray-50 p-4 transition-colors hover:border-primary-400 hover:bg-primary-50/40"
        >
          {logoPreview ? (
            <div className="relative flex items-center gap-4">
              <img
                src={logoPreview}
                alt="Logo del negocio"
                className="h-24 w-24 rounded-xl border border-gray-200 object-cover"
              />
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium text-gray-900">Logo cargado</p>
                <p className="mt-1 text-xs text-gray-500">
                  Puedes reemplazarlo arrastrando otra imagen o usando los botones.
                </p>
                <div className="mt-3 flex flex-wrap gap-2">
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="flex items-center gap-2 rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
                  >
                    <Upload className="h-4 w-4" />
                    Cambiar logo
                  </button>
                  <button
                    type="button"
                    onClick={onRemoveLogo}
                    className="flex items-center gap-2 rounded-lg border border-red-200 px-3 py-2 text-sm font-medium text-red-600 transition-colors hover:bg-red-50"
                  >
                    <Trash2 className="h-4 w-4" />
                    Quitar
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center py-8 text-center">
              <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-white shadow-sm">
                <ImageIcon className="h-7 w-7 text-gray-400" />
              </div>
              <p className="text-sm font-medium text-gray-700">
                Arrastra una imagen o usa una de estas opciones
              </p>
              <p className="mt-1 text-xs text-gray-500">JPG, PNG o WebP. Máximo 2MB.</p>
              <div className="mt-4 flex flex-wrap justify-center gap-2">
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  className="flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
                >
                  <Upload className="h-4 w-4" />
                  Subir archivo
                </button>
                <button
                  type="button"
                  onClick={() => cameraInputRef.current?.click()}
                  className="flex items-center gap-2 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
                >
                  <Camera className="h-4 w-4" />
                  Tomar foto
                </button>
              </div>
            </div>
          )}

          <input
            ref={fileInputRef}
            type="file"
            accept="image/jpeg,image/png,image/webp"
            className="hidden"
            onChange={handleFileChange}
          />
          <input
            ref={cameraInputRef}
            type="file"
            accept="image/*"
            capture="environment"
            className="hidden"
            onChange={handleFileChange}
          />
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <label className="mb-1 block text-sm font-medium text-gray-700">Nombre del negocio</label>
          <input
            type="text"
            value={values.displayName}
            onChange={(event) => onChange('displayName', event.target.value)}
            placeholder="Ej: Walos Bar Central"
            className="input"
          />
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">Email</label>
          <input
            type="email"
            value={values.email}
            onChange={(event) => onChange('email', event.target.value)}
            placeholder="contacto@negocio.com"
            className="input"
          />
        </div>

        <div>
          <label className="mb-1 block text-sm font-medium text-gray-700">Teléfono</label>
          <input
            type="text"
            value={values.phone}
            onChange={(event) => onChange('phone', event.target.value)}
            placeholder="+57 300 123 4567"
            className="input"
          />
        </div>
      </div>
    </section>
  );
};

export default BrandingForm;
