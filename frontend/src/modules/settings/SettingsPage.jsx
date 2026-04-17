/**
 * Pagina de Configuracion
 * �Qu� es? Vista principal del modulo de configuracion
 * �Para qu�? Personalizar branding, tema y reglas operativas de la aplicacion
 */

import { useEffect, useMemo, useRef, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Loader2, Save } from 'lucide-react';
import { useLocation, useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import companyService from '../../services/companyService';
import useAuthStore from '../../stores/authStore';
import useUiStore from '../../stores/uiStore';
import BrandingForm from './components/BrandingForm';
import ThemeSelector from './components/ThemeSelector';
import DiscountSettings from './components/DiscountSettings';
import CatalogSettings from './components/CatalogSettings';
import SettingsSectionNav from './components/SettingsSectionNav';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:3000';

const getSectionFromPath = (pathname) => {
  if (pathname.includes('/settings/themes')) return 'themes';
  if (pathname.includes('/settings/discounts')) return 'discounts';
  if (pathname.includes('/settings/catalog')) return 'catalog';
  return 'branding';
};

const SettingsPage = () => {
  const queryClient = useQueryClient();
  const { tenantId } = useAuthStore();
  const fileInputRef = useRef(null);
  const cameraInputRef = useRef(null);
  const { setTheme, setBranding } = useUiStore();
  const location = useLocation();
  const navigate = useNavigate();

  const activeSection = useMemo(() => getSectionFromPath(location.pathname), [location.pathname]);

  const [form, setForm] = useState({
    displayName: '',
    email: '',
    phone: '',
    themePreference: 'light',
  });
  const [operationsForm, setOperationsForm] = useState({
    manualDiscountEnabled: true,
    maxDiscountPercent: '15',
    maxDiscountAmount: '50000',
    discountRequiresOverride: false,
    discountOverrideThresholdPercent: '10',
  });
  const [logoFile, setLogoFile] = useState(null);
  const [logoPreview, setLogoPreview] = useState(null);
  const [saving, setSaving] = useState(false);

  const { data: settingsData, isLoading: settingsLoading } = useQuery({
    queryKey: ['company-settings', tenantId],
    queryFn: () => companyService.getSettings(),
    enabled: !!tenantId,
  });

  const { data: operationsData, isLoading: operationsLoading } = useQuery({
    queryKey: ['company-operations-settings', tenantId],
    queryFn: () => companyService.getOperationsSettings(),
    enabled: !!tenantId,
  });

  useEffect(() => {
    if (location.pathname === '/settings') {
      navigate('/settings/branding', { replace: true });
    } else if (location.pathname === '/settings/catalog') {
      // allow
    }
  }, [location.pathname, navigate]);

  useEffect(() => {
    const settings = settingsData?.data;
    if (!settings) return;

    setForm({
      displayName: settings.displayName || settings.name || '',
      email: settings.email || '',
      phone: settings.phone || '',
      themePreference: settings.themePreference || 'light',
    });
    setLogoPreview(settings.logoUrl ? `${API_BASE}${settings.logoUrl}` : null);
    setTheme(settings.themePreference || 'light');
    setBranding({
      companyName: settings.displayName || settings.name || 'Walos',
      companyLogoUrl: settings.logoUrl || null,
    });
  }, [settingsData, setBranding, setTheme]);

  useEffect(() => {
    const settings = operationsData?.data;
    if (!settings) return;

    setOperationsForm({
      manualDiscountEnabled: !!settings.manualDiscountEnabled,
      maxDiscountPercent: String(settings.maxDiscountPercent ?? 15),
      maxDiscountAmount: String(settings.maxDiscountAmount ?? 50000),
      discountRequiresOverride: !!settings.discountRequiresOverride,
      discountOverrideThresholdPercent: String(settings.discountOverrideThresholdPercent ?? 10),
    });
  }, [operationsData]);

  const handleChange = (field, value) => {
    setForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleOperationsChange = (field, value) => {
    setOperationsForm((prev) => ({ ...prev, [field]: value }));
  };

  const handleThemeSelect = (theme) => {
    setForm((prev) => ({ ...prev, themePreference: theme }));
    setTheme(theme);
  };

  const handleLogoSelect = (file) => {
    if (!file) return;

    if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
      toast.error('Usa una imagen JPG, PNG o WebP');
      return;
    }

    if (file.size > 2 * 1024 * 1024) {
      toast.error('La imagen no puede superar 2MB');
      return;
    }

    setLogoFile(file);
    const reader = new FileReader();
    reader.onloadend = () => setLogoPreview(reader.result);
    reader.readAsDataURL(file);
  };

  const handleRemoveLogo = () => {
    setLogoFile(null);
    setLogoPreview(null);
  };

  const handleSaveBranding = async () => {
    if (!form.displayName.trim()) {
      toast.error('Ingresa el nombre del negocio');
      return;
    }

    await companyService.updateSettings(form);

    if (logoFile) {
      await companyService.uploadLogo(logoFile);
      setLogoFile(null);
    }

    await queryClient.invalidateQueries({ queryKey: ['company-settings', tenantId] });
  };

  const handleSaveOperations = async () => {
    await companyService.updateOperationsSettings({
      manualDiscountEnabled: operationsForm.manualDiscountEnabled,
      maxDiscountPercent: Number(operationsForm.maxDiscountPercent || 0),
      maxDiscountAmount: Number(operationsForm.maxDiscountAmount || 0),
      discountRequiresOverride: operationsForm.discountRequiresOverride,
      discountOverrideThresholdPercent: Number(operationsForm.discountOverrideThresholdPercent || 0),
    });

    await queryClient.invalidateQueries({ queryKey: ['company-operations-settings', tenantId] });
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      if (activeSection === 'catalog') {
        toast.success('Los cambios en catalogo se guardan automaticamente');
        return;
      } else if (activeSection === 'discounts') {
        await handleSaveOperations();
      } else {
        await handleSaveBranding();
      }
      toast.success('Configuracion guardada');
    } catch (error) {
      toast.error(error.response?.data?.message || 'No se pudo guardar la configuracion');
    } finally {
      setSaving(false);
    }
  };

  if (settingsLoading || operationsLoading) {
    return (
      <div className="flex h-[calc(100vh-7rem)] items-center justify-center">
        <div className="card flex items-center gap-3 px-6 py-5">
          <Loader2 className="h-5 w-5 animate-spin text-primary-600" />
          <span className="text-sm font-medium text-gray-700">Cargando configuracion...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-[calc(100vh-7rem)] flex-col overflow-hidden gap-6">
      <div className="flex flex-shrink-0 flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Configuracion</h1>
          <p className="mt-1 text-sm text-gray-500">
            Organiza branding, temas y reglas operativas desde un mismo modulo.
          </p>
        </div>

        <button
          type="button"
          onClick={handleSave}
          disabled={saving}
          className="flex items-center gap-2 rounded-lg bg-primary-600 px-4 py-2.5 text-sm font-medium text-white shadow-sm transition-colors hover:bg-primary-700 disabled:opacity-50"
        >
          {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}
          {saving ? 'Guardando...' : 'Guardar cambios'}
        </button>
      </div>

      <SettingsSectionNav activeSection={activeSection} />

      <div className="flex-1 overflow-y-auto space-y-6 pr-1">
        {activeSection === 'branding' && (
          <BrandingForm
            values={form}
            onChange={handleChange}
            logoPreview={logoPreview}
            onLogoSelect={handleLogoSelect}
            onRemoveLogo={handleRemoveLogo}
            fileInputRef={fileInputRef}
            cameraInputRef={cameraInputRef}
          />
        )}

        {activeSection === 'themes' && (
          <ThemeSelector selectedTheme={form.themePreference} onSelect={handleThemeSelect} />
        )}

        {activeSection === 'catalog' && (
          <CatalogSettings />
        )}

        {activeSection === 'discounts' && (
          <DiscountSettings values={operationsForm} onChange={handleOperationsChange} />
        )}
      </div>
    </div>
  );
};

export default SettingsPage;

