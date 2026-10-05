# PLAN DE CONTINUACIÓN — FASE 2: PASSWORD LIFECYCLE

> **Fecha**: 2026-09-16  
> **Estado actual**: ~70% completado  
> **Backup branch**: `backup-fase-2-20260916-recovery`

---

## 📊 ESTADO ACTUAL FASE 2

### ✅ Implementado (Confirmado)

#### Backend

1. **Cambiar Mi Contraseña** ✅
   - Endpoint: `POST /api/v1/auth/change-password`
   - Controller: `AuthController.ChangePassword()`
   - Request: `ChangePasswordRequest(CurrentPassword, NewPassword, ConfirmPassword)`
   - Service: `IAuthService.ChangePasswordAsync()`
   - Policy: `WalosPolicies.CanonicalAuthenticated`

2. **Password Policy** ✅
   - Archivo: `Walos.Application/Security/PasswordPolicy.cs`
   - Validaciones de complejidad
   - Tests: `PasswordPolicyTests.cs`

3. **Access Token Validation** ✅
   - Service: `AccessTokenValidationService.cs`
   - Security stamp tracking
   - Tests: `AccessTokenValidationServiceTests.cs`

4. **Admin Password Reset** 🟡 (Parcial)
   - Controller: `AdminController` modificado
   - Validación: "Use change-password to update your own password"
   - Service: Modificaciones en `AdminService`, `UsersService`
   - Repository: Modificaciones en `AdminRepository`, `UsersRepository`

#### Frontend

1. **Password Utilities** ✅
   - Archivo: `frontend/src/utils/passwordPolicy.js`
   - Tests: `passwordPolicy.test.js`

2. **Profile Module** ✅
   - Directorio: `frontend/src/modules/profile/`
   - Componente de cambiar contraseña

3. **Auth Service** 🟡 (Modificado)
   - Archivo: `frontend/src/services/authService.js`
   - Integración con endpoint

4. **User Service** 🟡 (Modificado)
   - Archivo: `frontend/src/services/userService.js`
   - Tests: `userServicePassword.test.js`

---

## 🔍 TRABAJO PENDIENTE ESTIMADO

### 1. Completar Implementación Backend

#### 1.1 AuthService.ChangePasswordAsync()

**Revisar implementación**:
```csharp
Task<TokenResult> ChangePasswordAsync(
    long userId,
    long companyId,
    string currentPassword,
    string newPassword,
    string confirmPassword);
```

**Validaciones requeridas**:
- ✅ Usuario existe y pertenece a `companyId`
- ✅ `currentPassword` es correcta
- ✅ `newPassword != currentPassword`
- ✅ `newPassword == confirmPassword`
- ✅ `newPassword` cumple `PasswordPolicy`
- ✅ Hash con bcrypt
- ✅ Actualizar `password_hash` en `core.users`
- ✅ Invalidar refresh tokens anteriores
- ✅ Generar nuevos tokens
- ✅ Log de auditoría

**Estimación**: 1 hora (revisar + completar)

---

#### 1.2 Admin Password Reset

**Archivos a revisar**:
- `AdminController.cs`
- `AdminService.cs`
- `AdminRepository.cs`
- `UsersService.cs`
- `UsersRepository.cs`

**Validaciones requeridas**:
- ✅ Usuario actual tiene permiso `Users`
- ✅ Usuario objetivo pertenece al mismo `company_id`
- ✅ Usuario actual NO puede resetear su propia contraseña
- ✅ Nueva contraseña cumple `PasswordPolicy`
- ✅ Hash con bcrypt
- ✅ Actualizar `password_hash`
- ✅ Invalidar TODOS los refresh tokens del usuario objetivo
- ✅ Log de auditoría (quién reseteó a quién)

**Estimación**: 1 hora (revisar + hardening)

---

### 2. Completar Implementación Frontend

#### 2.1 Profile Module

**Componentes a revisar**:
- `frontend/src/modules/profile/ChangePasswordPage.jsx` (o similar)
- Formulario con validación
- Feedback de éxito/error
- Integración con `authService`

**Validaciones frontend**:
- ✅ Campos requeridos
- ✅ `newPassword == confirmPassword`
- ✅ Validación de complejidad (usando `passwordPolicy.js`)
- ✅ Feedback visual de requisitos
- ✅ Manejo de errores del backend

**Estimación**: 30 minutos (revisar + ajustes)

---

#### 2.2 Admin Password Reset UI

**Componentes a revisar**:
- `AdminUsersPage.jsx`
- `UsersPage.jsx`
- Modal de reset password

**Validaciones frontend**:
- ✅ No mostrar reset para usuario actual
- ✅ Tooltip explicativo
- ✅ Confirmación antes de reset
- ✅ Validación de nueva contraseña
- ✅ Feedback de éxito

**Estimación**: 30 minutos (revisar + ajustes)

---

### 3. Tests

#### 3.1 Backend Tests

**Archivos existentes a revisar**:
- `PasswordRepositoryIntegrationTests.cs`
- `PasswordRepositorySecurityTests.cs`
- `PasswordControllerTests.cs`
- `PasswordCreationPolicyTests.cs`
- `PasswordPolicyTests.cs`
- `AccessTokenValidationServiceTests.cs`
- `AccessTokenPipelineSecurityTests.cs`

**Tests faltantes estimados**:
- ✅ Cambiar contraseña: happy path
- ✅ Cambiar contraseña: contraseña actual incorrecta
- ✅ Cambiar contraseña: nueva == actual
- ✅ Cambiar contraseña: nueva != confirmación
- ✅ Cambiar contraseña: no cumple policy
- ✅ Cambiar contraseña: tenant isolation
- ✅ Reset admin: happy path
- ✅ Reset admin: mismo usuario (debe fallar)
- ✅ Reset admin: cross-tenant (debe fallar)
- ✅ Reset admin: sin permiso (debe fallar)
- ✅ Invalidación de tokens

**Estimación**: 1 hora (revisar + completar)

---

#### 3.2 Frontend Tests

**Archivos existentes a revisar**:
- `frontend/src/test/modules/profile/` (directorio)
- `frontend/src/test/modules/admin/AdminPasswordReset.test.jsx`
- `frontend/src/test/modules/users/` (directorio)
- `frontend/src/test/services/authService.test.js`
- `frontend/src/test/services/userServicePassword.test.js`
- `frontend/src/test/utils/passwordPolicy.test.js`

**Tests faltantes estimados**:
- ✅ Profile: formulario validación
- ✅ Profile: submit exitoso
- ✅ Profile: errores del backend
- ✅ Admin reset: modal
- ✅ Admin reset: validación
- ✅ Admin reset: usuario actual disabled

**Estimación**: 30 minutos (revisar + completar)

---

### 4. Documentación

#### 4.1 Actualizar Contrato

**Archivo**: `docs/WALOS-V1-MASTER-CONTRACT.md`

**Sección 7.1**: Cambiar Mi Contraseña
- Estado: 🟡 RELEASE GATE → ✅ DONE
- Endpoint: Documentado
- Validaciones: Listadas
- Tests: Referenciados

**Sección 7.2**: Reset Administrativo
- Estado: ✅ EXISTENTE → ✅ AUDITED
- Validaciones: Confirmadas
- Hardening: Aplicado

**Estimación**: 15 minutos

---

## 📋 CHECKLIST DE CONTINUACIÓN

### Pre-implementación
- [x] Backup branch creado
- [x] Estado actual documentado
- [ ] Review de diff completo
- [ ] Identificar archivos críticos

### Implementación
- [ ] Completar `AuthService.ChangePasswordAsync()`
- [ ] Auditar/hardening admin password reset
- [ ] Completar Profile module frontend
- [ ] Completar admin reset UI
- [ ] Ejecutar tests backend
- [ ] Ejecutar tests frontend
- [ ] Fix de tests fallidos

### Validación
- [ ] Build backend exitoso
- [ ] Build frontend exitoso
- [ ] Suite completa backend verde
- [ ] Suite completa frontend verde
- [ ] Manual testing de cambiar contraseña
- [ ] Manual testing de reset admin

### Commit
- [ ] Review final de cambios
- [ ] `git diff --check`
- [ ] Staging selectivo de archivos
- [ ] Commit message apropiado
- [ ] Verificar commit creado

---

## 🎯 PLAN DE EJECUCIÓN

### Paso 1: Review Completo (30 min)

```bash
# Ver diff de archivos críticos
git diff backend-dotnet/src/Walos.Application/Services/AuthService.cs
git diff backend-dotnet/src/Walos.Infrastructure/Repositories/AuthRepository.cs
git diff frontend/src/modules/profile/
git diff frontend/src/services/authService.js
```

**Objetivo**: Entender exactamente qué está implementado

---

### Paso 2: Completar Backend (2 horas)

1. **AuthService** (1h)
   - Implementar/revisar `ChangePasswordAsync()`
   - Validaciones completas
   - Invalidación de tokens
   - Auditoría

2. **Admin Reset** (1h)
   - Revisar implementación actual
   - Hardening de validaciones
   - Tests de seguridad

---

### Paso 3: Completar Frontend (1 hora)

1. **Profile Module** (30 min)
   - Revisar componente
   - Validaciones
   - UX

2. **Admin Reset UI** (30 min)
   - Revisar modal
   - Tooltip usuario actual
   - Confirmación

---

### Paso 4: Tests (1.5 horas)

1. **Backend** (1h)
   - Ejecutar suite
   - Fix tests fallidos
   - Agregar tests faltantes

2. **Frontend** (30 min)
   - Ejecutar suite
   - Fix tests fallidos

---

### Paso 5: Validación y Commit (30 min)

1. **Build** (10 min)
   - Backend Release
   - Frontend build

2. **Review Final** (10 min)
   - `git diff --stat`
   - `git diff --check`

3. **Commit** (10 min)
   - Staging selectivo
   - Mensaje descriptivo

---

## ⏱️ ESTIMACIÓN TOTAL

| Actividad | Tiempo |
|-----------|--------|
| Review completo | 30 min |
| Backend | 2 horas |
| Frontend | 1 hora |
| Tests | 1.5 horas |
| Validación y commit | 30 min |
| **TOTAL** | **5.5 horas** |

---

## 🚀 COMANDO PARA INICIAR

```bash
# 1. Verificar backup
git branch -v | grep backup

# 2. Ver estado actual
git status

# 3. Iniciar review
git diff --stat

# 4. Review de archivo crítico
git diff backend-dotnet/src/Walos.Application/Services/AuthService.cs
```

---

## ⚠️ RIESGOS Y MITIGACIONES

### Riesgo 1: Código no compila
**Mitigación**: Build temprano y frecuente

### Riesgo 2: Tests fallan
**Mitigación**: Ejecutar suite después de cada cambio significativo

### Riesgo 3: Mixing de fases
**Mitigación**: Review cuidadoso del diff antes de commit

### Riesgo 4: Perder trabajo
**Mitigación**: Backup branch ya creado

---

## ✅ CRITERIOS DE ÉXITO FASE 2

- [ ] Endpoint `POST /api/v1/auth/change-password` funcional
- [ ] Validaciones de password policy aplicadas
- [ ] Invalidación de tokens implementada
- [ ] Admin reset auditado y hardened
- [ ] Profile module funcional
- [ ] Admin reset UI funcional
- [ ] Tests backend verdes
- [ ] Tests frontend verdes
- [ ] Build exitoso
- [ ] Commit creado

---

**Fin del plan de continuación**
