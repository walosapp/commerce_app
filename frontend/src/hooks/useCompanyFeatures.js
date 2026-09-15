import { useCallback, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { canRoleAccessFeature, FEATURE_CODES, isPlatformOnlyUser, isTrustedDev } from '../config/companyFeatures';
import featureService from '../services/featureService';
import useAuthStore from '../stores/authStore';

const useCompanyFeatures = () => {
  const user = useAuthStore((state) => state.user);
  const tenantId = useAuthStore((state) => state.tenantId);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const trustedDev = isTrustedDev(user);
  const platformOnly = isPlatformOnlyUser(user);

  const query = useQuery({
    queryKey: ['company-features', tenantId],
    queryFn: featureService.getMine,
    enabled: isAuthenticated && !!tenantId && !trustedDev && !platformOnly,
    staleTime: 5 * 60 * 1000,
  });

  const featureMap = useMemo(() => {
    const map = new Map();
    for (const feature of query.data?.data ?? []) {
      map.set(feature.code, feature);
    }
    return map;
  }, [query.data]);

  const hasFeature = useCallback((featureCode) => {
    if (featureCode === 'dashboard') return !platformOnly;
    if (trustedDev) return FEATURE_CODES.includes(featureCode);
    if (platformOnly || !query.isSuccess) return false;
    return featureMap.get(featureCode)?.isEnabled === true;
  }, [featureMap, platformOnly, query.isSuccess, trustedDev]);

  const canAccess = useCallback((featureCode) =>
    canRoleAccessFeature(user, featureCode) && hasFeature(featureCode),
  [hasFeature, user]);

  const roleAllows = useCallback((featureCode) =>
    canRoleAccessFeature(user, featureCode),
  [user]);

  return {
    features: query.data?.data ?? [],
    hasFeature,
    roleAllows,
    canAccess,
    isReady: trustedDev || platformOnly || query.isSuccess,
    isLoading: !trustedDev && !platformOnly && query.isLoading,
    isError: !trustedDev && !platformOnly && query.isError,
    isPlatformOnly: platformOnly,
    isTrustedDev: trustedDev,
    refetch: query.refetch,
  };
};

export default useCompanyFeatures;
