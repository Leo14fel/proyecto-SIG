# Pruebas

Ejecute `dotnet test VisorDatosSIG.sln`.

## Ejecución auditada 2026-11-12

- `dotnet test VisorDatosSIG.sln`: correcto, 18 de 18 pruebas superadas.
- `dotnet build src/VisorDatosSIG.Web/VisorDatosSIG.Web.csproj --no-restore`: correcto, sin advertencias ni errores.
- Las tres pruebas PWA verifican manifiesto instalable, exclusión de rutas privadas en el Service Worker y botón de instalación en Login.
- `dotnet list src/VisorDatosSIG.Infrastructure/VisorDatosSIG.Infrastructure.csproj package --include-transitive --vulnerable`: sin paquetes vulnerables en `nuget.org`.
- SQL Server, aplicación de `10_OperacionesAgua.sql`, HTTP autenticado, navegador HTTPS y teléfono físico no se ejecutaron durante esta auditoría. No se declaran validados.

El repositorio incluye `NuGet.Config` para evitar fuentes y carpetas de reserva locales de DevExpress que no usa el proyecto. La restauración se realiza exclusivamente desde `nuget.org`.

Las pruebas unitarias cubren verificación y generación PBKDF2-SHA256, reconocimiento de capas oficiales, diagnóstico de componentes ausentes y rechazo de capas arbitrarias. La comprobación manual de autenticación requiere SQL Server local y una cuenta oficial activa; no modifica la base.

Valores de inyección como `'`, `' OR 1=1 --`, `%` y `AGUILAR'--` se transmiten únicamente como parámetros de LINQ/EF Core; no se construye SQL con nombres ni valores del cliente. Falta una prueba de integración con SQL aislado que lo demuestre en ejecución.

Pendiente: pruebas de integración HTTP con una base SQL aislada para búsqueda, exportación, usuario inactivo, logout y autorización Administrador/Consultor. Las pruebas responsive, autenticadas y de rendimiento requieren navegador y base local; no se han registrado resultados para 360, 768 o 1366 px.

## Registros geográficos

Se verificó sin sesión que `/Registros` y el deep link de Visor redirigen a Login, y que `/api/registros` devuelve 401. La validación autenticada de datos reales, la selección visual de geometría y los tamaños 360/390/768/1366 requieren una sesión de prueba en navegador y quedan pendientes de una prueba manual registrada.

## Evidencia local

- La cuenta local `demo.consultor` se creó fuera del repositorio con PBKDF2-SHA256, 100.000 iteraciones, salt aleatorio y rol `Consultor` activo.
- Mediante HTTP autenticado, Consultor recibió 200 en Inicio, Visor, Mi Perfil y `/api/capas`. La solicitud directa a `/Administracion/Usuarios` fue rechazada por la autorización de rol.
- Datos migrados verificados: Manzanas 863/863; Lotes 15.281 SHP, 1 geometría vacía omitida, 15.280 SQL; Códigos Fijos 6.271/6.271; Vías 578/578. SRID incorrecto 0 y `STIsValid() = 0` en las cuatro capas. La geometría vacía no fue reparada ni inventada.

## Rendimiento

| Operación | Resultado |
| --- | --- |
| Dashboard | Pendiente de medición repetida en navegador |
| Visor inicial | Pendiente de medición repetida en navegador |
| AGUILAR | Pendiente de medición repetida en navegador |
| Lote | Pendiente de medición repetida en navegador |
| GeoJSON Manzanas | Pendiente de medición repetida en navegador |
| Lotes por bbox | Pendiente de medición repetida en navegador |

## PWA

- Build y pruebas unitarias correctos después de incorporar manifest, Service Worker y activos PWA.
- Pendiente de prueba física: instalación Chrome Android, Edge y Safari iOS; modo standalone, `beforeinstallprompt`, flujo PIN de Cloudflare, GPS, cámara, gestos táctiles, exportación CSV móvil y contingencia offline tras la instalación del Service Worker.
- No se considera que la consulta GIS funcione offline: el Service Worker devuelve `offline.html` únicamente cuando falla una navegación de red.

## Pruebas de privacidad y caché PWA (2026-11-12)

El Service Worker usa `visordatos-static-v6`: limita el almacenamiento a extensiones públicas dentro de `/css/`, `/js/`, `/icons/` y `/lib/`; respeta `Cache-Control: private/no-store` y elimina solo versiones antiguas de su propia caché.

Pruebas adicionales con Node.js 18+ (sin dependencias externas):

```powershell
node --test tests/pwa-service-worker.test.cjs
```

Los siete casos cubren exclusión de rutas privadas, funcionamiento sin conexión de CSS público, cabeceras de privacidad, navegación offline, limpieza de cachés antiguos, orígenes externos y aislamiento entre aplicaciones. Estas pruebas simulan el navegador; deben ejecutarse en el entorno del proyecto y no sustituyen la validación en Android/iOS. La cifra de 18/18 pruebas .NET registrada anteriormente no corresponde a esta nueva versión del código y requiere repetición.

## PWA de campo: interfaz responsive (2026-11-12)

La pantalla **Mis tareas** presenta cada asignación con un progreso de tareas registradas y tarjetas por parada (Pendiente, Realizado u Observado). Las acciones de recorrido y apertura de tareas se conservan. El formulario muestra los nombres de fotos elegidas y solicita confirmación antes de enviar el resultado; no cambia las validaciones del servidor, GPS, SQL ni Dijkstra.

**Pruebas de aceptación pendientes en navegador y teléfono físico:** 320, 360, 390, 768 y 1366 px; nombres largos; varias asignaciones; tarjetas pendientes/realizadas/observadas; iniciar y detener seguimiento; abrir una tarea; cancelar confirmación sin guardar; guardar una observación y un trabajo realizado con fotos; revisar el resultado en SQL Server. El rediseño es una mejora visual, no una prueba integral ejecutada.

La aplicación móvil oficial sigue siendo **PWA**, no APK ni MAUI.
