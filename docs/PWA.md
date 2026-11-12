# PWA de VisorDatosSIG

## Arquitectura

La PWA se implementa dentro de `src/VisorDatosSIG.Web`. Conserva ASP.NET Core MVC, autenticación por cookies, SQL Server y Leaflet; no existe una base de datos ni backend alternativo.

- `wwwroot/manifest.webmanifest`: nombre, modo `standalone`, colores e iconos locales.
- `wwwroot/icons/`: iconos PNG de 192, 512 y 512 maskable, más su fuente SVG.
- `wwwroot/service-worker.js`: caché público y pantalla de contingencia.
- `wwwroot/offline.html`: experiencia offline autocontenida con botón de reintento.
- `wwwroot/js/site.js`: registro seguro del Service Worker e instalación opcional.

## Caché y seguridad

El Service Worker cachea únicamente archivos públicos estáticos: CSS, JavaScript, Bootstrap e iconos. Las navegaciones HTML siempre se solicitan a la red y, si la red falla, muestran `offline.html`.

No se cachean `/Cuenta`, `/Administracion`, `/api`, `/Visor`, respuestas GeoJSON, CSV, cookies, tokens ni páginas autenticadas. La PWA no ofrece consultas GIS sin conexión ni almacena credenciales.

Leaflet se mantiene desde su CDN actual y el mapa base de OpenStreetMap se consulta bajo demanda. Ninguno se cachea en masa: el visor necesita conectividad para librerías remotas no presentes y para los mosaicos del mapa base.

La Web confía en `X-Forwarded-For` y `X-Forwarded-Proto` solo si el proxy es loopback. Esto permite que `cloudflared` comunique el esquema HTTPS original sin aceptar cabeceras reenviadas de clientes arbitrarios.

## Instalación

### Android (Chrome)

1. Abra la URL HTTPS establecida para VisorDatosSIG.
2. Complete cualquier PIN de Cloudflare antes de llegar al Login.
3. Cuando Chrome habilite la instalación, use **INSTALAR VISORDATOSSIG** o el menú de Chrome, **Instalar aplicación**.
4. Abra el icono **VisorSIG** desde la pantalla de inicio.

La instalación requiere HTTPS (o `localhost` para desarrollo). Si no hay sesión, la ruta inicial redirige al Login normal.

### iPhone/iPad

En Safari, use **Compartir → Agregar a pantalla de inicio**. iOS no expone `beforeinstallprompt`, por lo que la aplicación muestra esa instrucción en lugar de un diálogo artificial.

## Cloudflare Quick Tunnel

El manifest y el Service Worker usan rutas relativas y nunca contienen un dominio `trycloudflare.com`. Un Quick Tunnel cambia de hostname al reiniciarse: una PWA instalada puede permanecer asociada al origen anterior, por lo que se debe abrir la nueva URL e instalar de nuevo si el túnel cambia.

Para una demostración temporal protegida por PIN:

```powershell
& "$env:LOCALAPPDATA\Cloudflared\cloudflared.exe" tunnel --url http://127.0.0.1:8080 --allowed-mail "correo-permitido@ejemplo.com"
```

El proceso debe permanecer en ejecución. El PIN de Cloudflare y la cookie de VisorDatosSIG son autenticaciones independientes. Un Quick Tunnel no tiene disponibilidad garantizada y no reemplaza un despliegue permanente.

Para un dominio estable, cree un túnel administrado con una cuenta Cloudflare y un dominio propio, agregue una ruta pública HTTPS al origen local, aplique Cloudflare Access y ejecute el túnel con su token o credenciales fuera del repositorio. No guarde tokens, certificados ni URLs temporales en Git.

## Pruebas realizadas

- Manifest, iconos, Service Worker y página offline: comprobación HTTP local.
- Build y pruebas unitarias: ejecutados localmente.
- Login, Dashboard, API de capas, búsqueda `AGUILAR`, GeoJSON y Logout: comprobados previamente contra SQL local.

Pendiente de una URL HTTPS funcional y de un dispositivo físico: instalación desde Chrome Android, modo standalone, PIN de Cloudflare, exportación CSV móvil, interacción táctil, pruebas en otra red y navegación offline con un Service Worker ya instalado.
