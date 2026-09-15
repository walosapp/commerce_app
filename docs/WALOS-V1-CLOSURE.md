# WALOS V1 — CIERRE DE PLATAFORMA

> **Fecha de corte:** 2026-09-15
> **Fuente:** código actual, migraciones y UAT reportada.
> **Estado:** alcance V1 definido, hardware V1 cerrado y gates finales verificados en verde.

Este documento reemplaza como referencia de cierre las afirmaciones obsoletas que describían el Print Agent, el cajón o la caja como no implementados o trasladados a V2. No convierte planes o tests no ejecutados en funcionalidad validada.

## 1. Auditoría inicial

La revisión del baseline encontró estos puntos relevantes:

- El modelo de suscripciones existente no resolvía el encendido operativo de módulos por comercio. Se eligió un catálogo y una relación explícita por `company_id`, sin agregar booleanos a `core.companies` (`supabase/migrations/019_company_features.sql`).
- El rol técnico `dev`, la administración global y los roles operativos tenant requerían fronteras explícitas. La autorización quedó centralizada en `WalosAuthorizationExtensions.AddWalosAuthorization()` y los roles canónicos en `WalosAuthorization.cs`.
- Había que impedir que roles legacy o desconocidos recibieran tokens o aprovecharan endpoints con autorización genérica. `AuthService.LoginAsync()` y `RefreshTokenAsync()` validan la allow-list canónica antes de emitir o rotar credenciales; la `DefaultPolicy` falla cerrada para principales no operativos.
- El fallback PWA debía sobrevivir a logos nulos, legacy faltantes, referencias externas o referencias administradas inválidas. La resolución se concentra en `PwaController.GetIcon()`/`OpenLogoStreamAsync()` y usa el PNG embebido de Walos.
- El comportamiento de devoluciones sí estaba implementado para productos simples, dinero, crédito e inventario dentro de una transacción. La auditoría identificó una deuda real para productos preparados: el reintegro consulta la receta vigente, no una fotografía histórica de la receta consumida (`RefundRepository.RestoreInventoryAsync()`).
- El supuesto error de login HTTP 422 no se reprodujo: un login inválido contra producción devolvió el 422 de negocio esperado, con cuerpo de credenciales inválidas y CORS. No se introdujo un cambio especulativo en el binding JSON ni en `Program.cs` por ese reporte.
- El rate limiter estaba registrado y el middleware presente, pero la policy nombrada `api` no está aplicada a endpoints ni configurada como limiter global (`Program.cs`: `AddRateLimiter()`/`UseRateLimiter()`). Se conserva como deuda, no como protección activa.

## 2. Decisiones de V1

1. `dev` es un superusuario **técnico confiable**: puede probar capacidades operativas y de plataforma, pero solo cuando el JWT incluye el claim firmado `platformAdmin=true` derivado de la identidad de sistema.
2. `platform_admin` administra la plataforma; no obtiene acceso operativo silencioso a ventas, caja o inventario de los tenants.
3. Los roles tenant continúan siendo `super_admin`, `manager`, `cashier` y `waiter`, con policies por capacidad. El código legacy `admin` no es un rol autenticable ni una vía alternativa de autorización.
4. La disponibilidad funcional es la intersección de **rol/capacidad + feature del comercio**. Ocultar navegación nunca sustituye la validación backend.
5. Desactivar un módulo no elimina datos. Al reactivarlo se recupera el acceso a la información previa.
6. `dashboard` es obligatorio y siempre está activo. `ai` inicia desactivado.
7. Restaurante y POS-Deli son features independientes.
8. La facturación POS, el billing SaaS de Walos y la futura facturación electrónica fiscal son dominios distintos. Esta última queda fuera de V1.
9. H1, H2, H3 y H3.1 conforman el hardware V1. No se inicia H4.

## 3. Roles y fronteras de autorización

### `dev`

- Rol canónico: `WalosRoles.Dev`.
- Puede satisfacer policies operativas mediante `IsTrustedDev()` y la policy de plataforma mediante `IsTrustedPlatformPrincipal()` (`WalosAuthorizationExtensions.cs`).
- El claim de plataforma no se confía desde un rol arbitrario: `AuthService.GenerateJwtToken()` lo deriva de la identidad de sistema canónica `WALOS-SYSTEM-001` (`WalosSystemIdentity.CompanyTaxId`).
- Las operaciones sobre usuarios técnicos están protegidas también en SQL. `UsersRepository.UpdateAsync()`, `SetStatusAsync()`, `SoftDeleteAsync()` y `ResetPasswordAsync()` excluyen objetivos cuyo rol actual sea `dev`; esto cierra cambios por ID directo y carreras entre lectura y escritura.

### `platform_admin`

- Rol canónico y de alcance global, autorizado por `WalosPolicies.PlatformAdmin` solo junto con el claim confiable de plataforma.
- Puede administrar comercios, módulos y sucursales mediante controllers bajo `/api/v1/platform/admin`.
- No satisface la `DefaultPolicy` operativa ni las policies tenant. Su logout utiliza deliberadamente `WalosPolicies.CanonicalAuthenticated` (`AuthController.Logout()`), sin ampliar el acceso a operaciones de negocio.

### Roles tenant

- `super_admin`, `manager`, `cashier` y `waiter` son los únicos roles operativos canónicos.
- Las policies separan, entre otras, administración tenant, settings, usuarios, finanzas, compras/proveedores, escritura de inventario, venta, caja y delivery (`WalosAuthorization.cs`, `WalosAuthorizationExtensions.cs`).
- Compras y Proveedores protegen también sus endpoints de lectura con `PurchasesRead` y `SuppliersRead`; no dependen de un `[Authorize]` genérico.
- La falta de permiso produce autorización denegada; un módulo apagado produce `feature_not_enabled` mediante `CompanyFeatureMiddleware`. Son causas distintas.

## 4. Features por comercio

### Esquema y migración

La migración aditiva `supabase/migrations/019_company_features.sql` crea:

- `platform.features`: catálogo canónico, default, obligatoriedad, orden y auditoría.
- `platform.company_features`: PK `(company_id, feature_code)`, `is_enabled`, `created_at`, `updated_at` y `updated_by`.

La migración hace preflight del esquema, rechaza estados parciales o definiciones canónicas incompatibles y luego realiza backfill. Los diez módulos son:

`dashboard`, `inventory`, `restaurant`, `pos`, `cash`, `purchases`, `suppliers`, `delivery`, `finance`, `ai`.

Compatibilidad:

- comercios existentes reciben todos los módulos operativos en ON;
- `dashboard` queda ON y no puede apagarse por constraint ni por servicio;
- `ai` queda OFF;
- un comercio nuevo sin override resuelve los mismos defaults mediante `COALESCE(cf.is_enabled, f.default_enabled)` en `CompanyFeatureRepository`; la primera modificación persiste su fila con upsert. La migración hace backfill físico solo de los comercios existentes al momento de aplicarla.

**Estado DB de desarrollo:** la migración 019 ya fue aplicada y verificada en DEV. En esa verificación había 4 comercios y 40 relaciones; `dashboard` estaba activo en 4/4 y `ai` inactivo en 4/4. Esto no afirma aplicación en producción.

### Aplicación backend

- `GET /api/v1/features`: estado del comercio autenticado (`FeaturesController`).
- `GET /api/v1/platform/admin/features`: catálogo (`PlatformFeaturesController`).
- `GET /api/v1/platform/admin/companies/{companyId}/features`: configuración por comercio.
- `PUT /api/v1/platform/admin/companies/{companyId}/features/{featureCode}`: cambio auditado.
- `RequireFeatureAttribute`/`RequireAnyFeatureAttribute` agregan metadata reutilizable.
- `CompanyFeatureMiddleware.InvokeAsync()` resuelve el tenant, consulta estados en bloque y falla cerrado antes de ejecutar el endpoint.

### UI

En **Comercios → Módulos**, `TenantsPage.jsx` abre `CompanyFeaturesPanel.jsx` para consultar y modificar features. En **Comercios → Sucursales**, abre `CompanyBranchesPanel.jsx` para listar, crear, editar y activar/desactivar sucursales mediante `PlatformBranchesController`.

La desactivación concurrente de sucursales usa `IsolationLevel.ReadCommitted` y conserva el lock `FOR UPDATE` por empresa en `AdminRepository.UpdateBranchAsync()`. Así se serializa el invariante de última sucursal activa sin retener el snapshot obsoleto que, bajo `Serializable`, producía PostgreSQL `40001 serialization_failure`.

La navegación y las rutas usan configuración canónica de features (`frontend/src/config/companyFeatures.js`, `useCompanyFeatures.js` y guards bajo `components/routing`). Una feature apagada no solo se oculta: se evita montar/cargar el módulo y la URL directa queda protegida. El backend sigue siendo la autoridad final.

## 5. PWA y branding

- `PwaController.GetManifest()` genera el manifest tenant-aware.
- `PwaController.GetIcon()` acepta únicamente tamaños permitidos y delega la lectura a `OpenLogoStreamAsync()`.
- Si el logo es nulo, default, legacy inexistente, externo, inválido o no corresponde al tenant, se renderiza `Assets/walos-default.png`; no se devuelve 404 por esa causa.
- `IFileStorage.TryGetManagedObjectKey()` y `SupabaseFileStorage` validan referencias administradas en lugar de descargar URLs arbitrarias.
- El frontend incluye iconos PNG instalables en `frontend/public/icons/` y el manifest base en `frontend/public/manifest.webmanifest`.

## 6. Devoluciones

### Comportamiento confirmado por código

`RefundService.CreateRefundAsync()` valida `full`/`partial`, normaliza ítems y construye un fingerprint. `RefundRepository.ProcessAsync()` ejecuta bajo una única transacción:

1. bloquea/valida la orden por `company_id` y `branch_id`;
2. resuelve replay por idempotency key y fingerprint;
3. calcula cantidades netas ya devueltas y el monto disponible;
4. distribuye la devolución entre reducción de crédito y dinero retornable;
5. exige y bloquea caja cuando existe componente efectivo;
6. inserta `sales.refunds` y `sales.refund_items`;
7. restaura inventario y registra movimientos;
8. ajusta crédito, registra salida de caja y actualiza `refund_status` de la orden;
9. confirma la transacción, o hace rollback ante cualquier excepción.

Esto cubre devolución total y parcial de productos simples, ajuste de caja según la porción efectivamente pagada, reducción de crédito, replay seguro y atomicidad. Los accesos y cálculos conservan `company_id` y `branch_id`; una sucursal nula no entra al flujo operativo.

### Deuda HIGH: preparados y recetas históricas

`RefundRepository.RestoreInventoryAsync()` consulta `inventory.recipes` al momento de devolver un producto `prepared` y genera movimientos `refund_recipe`. Por lo tanto, restaura correctamente **la receta actual**, pero no necesariamente los ingredientes y cantidades consumidos al vender si la receta cambió después.

Esta deuda no debe maquillarse con más tests sobre la receta vigente. La corrección requiere persistir una fotografía del consumo real por ítem/venta y usarla como fuente del refund. Es un cambio de modelo y queda fuera de este cierre V1.

## 7. Asistente IA

- Feature `ai`: OFF por defecto en migración y backfill.
- `IAiCapabilityGuard` cruza cada capacidad con las features del comercio y falla cerrado.
- Las sesiones se aíslan por empresa y usuario; el fingerprint de capacidades invalida contexto incompatible (`AiSessionRepository`, `OrchestratorService`).
- V1 conserva consultas y preparación informativa permitidas, pero no mutaciones operativas.
- La acción `add_stock` está deshabilitada explícitamente en `OrchestratorService`: devuelve una respuesta de seguridad y no escribe inventario. Los endpoints legacy de IA tampoco ejecutan esas mutaciones (`backend-dotnet/README.md`, “Mutaciones deshabilitadas”).

**Deuda:** diseñar una primitiva de entrada de stock atómica, idempotente y auditable antes de volver a habilitar `add_stock`; no debe reutilizarse una vía de escritura débil desde el LLM.

## 8. Hardware POS V1

El hardware V1 está cerrado funcional y físicamente. La evidencia técnica detallada permanece en `docs/hardware-pos.md`.

- **H1:** Print Agent .NET 10 WinForms/tray, API local exclusivamente en `127.0.0.1:17831`, pairing, secreto protegido con DPAPI, CORS por allow-list, payloads tipados, spooler RAW, ESC/POS 58 mm e idempotencia durable. La DIG-58IIA imprimió sin diálogo y la DIG-4101 abrió una sola vez; el replay no repitió el efecto físico.
- **H2:** `ReceiptPreview` imprime el comprobante persistido obtenido de `GET /api/v1/sales/orders/{id}/receipt`; una reimpresión manual nunca abre el cajón.
- **H3:** después de persistir la venta, Restaurante y POS-Deli consultan el recibo canónico, imprimen como post-action y abren el cajón únicamente si existe componente efectivo. Fallos de impresión/cajón no revierten ni repiten checkout, inventario o pagos. Los job IDs de venta, impresión y cajón permanecen separados.
- **H3.1:** instalador Windows self-contained, tray y autoarranque; pairing/configuración sobreviven reinstalación en `%LOCALAPPDATA%\Walos\PrintAgent`; la aplicación se instala bajo Program Files. El origin oficial de producción y los origins locales están autorizados explícitamente, sin `*`, y el binding continúa en loopback.

No forman parte de V1: H4, auto-update, servicio Windows, telemetría, báscula, lectores adicionales, personalización de ticket ni nuevas capacidades de hardware. Authenticode sigue fuera del alcance del instalador V1.

## 9. Seguridad de configuración y seeds

- `JwtSecretValidator.ValidateOrThrow()` se invoca en startup antes de registrar JWT. Rechaza secretos vacíos, menores de 32 bytes, placeholders conocidos, baja diversidad de clases o pocos caracteres distintos (`Program.cs`, `JwtSecretValidator.cs`). `appsettings.json` no contiene un secreto funcional.
- Login y refresh aplican allow-list de roles canónicos antes de emitir credenciales (`AuthService.EnsureCanonicalRole()`).
- La `DefaultPolicy` permite solamente roles tenant canónicos o `dev` confiable. `platform_admin` usa exclusivamente policies de plataforma, salvo la policy mínima de logout.
- Las migraciones automáticas `800_seed_initial_data.sql` y `900_seed_dev_user.sql` son marcadores de compatibilidad y no contienen credenciales ni crean usuarios.
- Los scripts manuales `supabase/scripts/bootstrap_demo_data.sql`, `bootstrap_walos_system.sql` y `cleanup_data_keep_inventory.sql` fallan cerrado antes de escrituras destructivas si el operador no proporciona los hashes BCrypt mediante settings de sesión. No contienen hashes conocidos ni contraseñas por defecto.
- `999_cleanup_data_keep_inventory.sql` es también un marcador no destructivo; el cleanup real es deliberadamente un script manual.

## 10. Módulos incluidos y excluidos

### Incluidos en V1

- autenticación, tenants, usuarios y roles;
- dashboard;
- inventario, productos, recetas y movimientos;
- Restaurante;
- POS-Deli;
- caja;
- compras y proveedores;
- delivery;
- finanzas;
- devoluciones y créditos;
- PWA/branding con fallback;
- features por comercio y administración de sucursales;
- asistente IA de lectura, opt-in y sin mutaciones;
- hardware H1/H2/H3/H3.1.

### Fuera de V1

- WhatsApp;
- clientes V2;
- facturación electrónica fiscal y notas crédito fiscales;
- personalización de tickets;
- báscula y lectores adicionales;
- empleados;
- auto-update del agente;
- Authenticode;
- H4 y cualquier feature V2;
- upgrades, checkout de planes, límites de uso y billing automático.

`platform.billing_invoices` y cualquier billing SaaS existente no equivalen a facturación fiscal ni prueban integración DIAN.

## 11. Riesgos y deuda restante

| Severidad | Deuda | Consecuencia / acción |
|---|---|---|
| **HIGH** | Refund de preparados usa la receta vigente. | Una receta modificada puede devolver ingredientes/cantidades distintos a los consumidos. Persistir snapshot histórico antes de corregir. |
| **HIGH** | `add_stock` por IA no tiene una primitiva segura. | Mantener la mutación deshabilitada hasta contar con atomicidad, idempotencia y auditoría. |
| **MEDIUM** | Limiter `api` registrado pero no aplicado. | Agregar metadata `RequireRateLimiting("api")` o un `GlobalLimiter` probado; hoy no debe declararse protección activa. |
| **MEDIUM** | Migración 019 verificada en DEV, no declarada aplicada en producción. | Ejecutar el proceso de release/migración de producción con evidencia separada. |
| **MEDIUM** | Instalador sin Authenticode. | Firmar antes de distribución comercial pública; no bloquea el alcance interno ya aceptado de H3.1. |
| **LOW** | `CanonicalAuthenticated` permite logout a un `platform_admin` aunque el claim de plataforma no sea `true`. | Alcance limitado a invalidar su propia sesión; mantener monitoreado, sin convertirlo en acceso operativo. |
| **INFO** | Reporte de login 422. | **NOT REPRODUCED**; conservar observabilidad y capturar request/response reales si reaparece. |

## 12. Gates finales del corte

**VERIFICADOS EN VERDE:**

- integraciones backend afectadas por el cierre V1: **29/29**, sin fallos ni omitidos;
- suite backend completa: **762/762**, sin fallos ni omitidos;
- build backend Release: **OK**, 0 errores y 17 warnings existentes;
- suite frontend completa: **248/248**, sin fallos;
- build frontend de producción: **OK**;
- migración `019_company_features.sql`: aplicada y verificada en **DEV**; no se afirma aplicación en producción;
- `git diff --check`: **OK**.

Con estos resultados, el veredicto técnico del corte es:

**WALOS V1 PLATFORM CLOSURE APROBABLE**
