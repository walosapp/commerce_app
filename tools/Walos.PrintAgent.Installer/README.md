# Walos Agent — distribución Windows V1

El comercio recibe un instalador `.exe`; no necesita PowerShell, .NET ni herramientas de desarrollo. El ejecutable del agente se publica `win-x64`, self-contained y single-file. El instalador se construye con Inno Setup 6.4.3 obtenido desde el paquete comunitario `Tools.InnoSetup` de NuGet, fijado por versión y SHA-256 (`9D67A2B...92A5`); cada build vuelve a extraer el compilador desde ese paquete verificado y no depende de una instalación global. El proceso es repetible con entradas fijadas, aunque NuGet es un redistribuidor y no la fuente oficial del binario.

## Construir

Desde la raíz del repositorio:

```powershell
.\tools\Walos.PrintAgent.Installer\Build-Installer.ps1 `
  -Version 1.0.1
```

El instalador siempre incluye de forma explícita el origen oficial `https://commerce-app-red.vercel.app` y los orígenes locales `http://localhost:5173` y `http://127.0.0.1:5173`. No requiere configuración manual del comercio. `-AllowedOrigins` es opcional y solo agrega orígenes HTTPS separados por `;`; nunca reemplaza los orígenes canónicos.

Artefacto no versionado:

```text
tools/Walos.PrintAgent.Installer/artifacts/installer/Walos-Agent-Setup.exe
```

El pipeline de release debe publicar ese archivo en un asset HTTPS estable y configurar `VITE_WALOS_AGENT_DOWNLOAD_URL` en el build del frontend. No se necesita un nuevo backend ni un updater para V1.

## Instalación

- Ruta por defecto: `%ProgramFiles%\Walos\PrintAgent`.
- Autoarranque normal: `HKLM\Software\Microsoft\Windows\CurrentVersion\Run\WalosPrintAgent`.
- Al finalizar, inicia el tray como el usuario que lanzó el instalador.
- Upgrade/reinstalación: usa un `AppId` estable y conserva `%LOCALAPPDATA%\Walos\PrintAgent`.
- Desinstalación interactiva: pregunta explícitamente si debe borrar pairing/configuración.
- Desinstalación silenciosa: conserva configuración por defecto; `/CLEANCONFIG` solicita borrarla expresamente.

El agente conserva el listener exclusivo `127.0.0.1:17831`; el instalador no crea reglas de firewall. El navegador conserva el pairing de esta estación en `localStorage`, mientras el agente conserva su copia protegida con DPAPI `CurrentUser`, de modo que cerrar el navegador o reiniciar Windows no exige vincular nuevamente. Como cualquier token accesible a JavaScript, esto exige mantener controlado el origen Walos y prevenir XSS. Un mutex global evita abrir dos agentes. V1 está orientada a una terminal POS Windows con un único usuario interactivo; escenarios RDS/multiusuario requieren una decisión posterior.

## Smoke test controlado

Cerrá manualmente cualquier agente que ya esté escuchando en el puerto 17831. Luego:

```powershell
.\tools\Walos.PrintAgent.Installer\Smoke-Test-Installer.ps1 `
  -Version 1.0.1 `
  -ConfirmDisposableUserProfile
```

La prueba **se niega a ejecutarse** si encuentra estado o autoarranque de Walos y debe correrse únicamente en una cuenta descartable o VM. Usa una variante separada, per-user y no distribuible del instalador; instala silenciosamente en una carpeta aislada, comprueba el arranque postinstall, versión, binding loopback, reinstalación, preservación de un estado centinela, comando de autoarranque HKCU y desinstalación normal. No copia ni borra configuración de una cuenta operativa. El `.exe` de smoke vive fuera de `artifacts/installer` para impedir publicarlo por error.

El smoke sandbox NO demuestra UAC, instalación real en Program Files, HKLM Run, limpieza explícita ni arranque después de un inicio de sesión. Esos gates requieren una VM limpia elevada: instalar normalmente, verificar `%ProgramFiles%\Walos\PrintAgent`, cerrar sesión/entrar y luego desinstalar primero conservando estado y después con `/CLEANCONFIG`.

## Firma y publicación

El artefacto local es deliberadamente reproducible pero no queda firmado sin un certificado corporativo. Antes de publicarlo al comercio es gate obligatorio:

```powershell
Get-AuthenticodeSignature .\tools\Walos.PrintAgent.Installer\artifacts\installer\Walos-Agent-Setup.exe
```

`Status` debe ser `Valid` y el certificado debe pertenecer al publicador aprobado de Walos. Un instalador `NotSigned` sirve para validación interna, pero es **NO-GO para distribución pública**.
