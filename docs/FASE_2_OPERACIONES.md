# Requisitos de usuario de la fase 2

Fuentes: **INF 442 SIG 2026-10-15** (alcance escrito) y **AlcanceProyecto_2_2026** (capturas web y Android, diapositivas 3 a 19).

| ID | Actor | Necesidad y criterio de aceptación | Cobertura en el código |
| --- | --- | --- | --- |
| RU-01 | Administrador, cortador y reconector | Iniciar sesión con usuario y contraseña, ver el nombre de usuario y cerrar sesión. | Autenticación SQL existente y navegación por rol. El trabajador entra a **Mis tareas**. |
| RU-02 | Administrador | Consultar el total de conexiones y su distribución por estado. | **Panel principal** consulta SQL por estado: Normal, Para corte, Cortado, Baja parcial y Baja total. |
| RU-03 | Administrador o consultor | Consultar conexiones coloreadas, filtrar por estado y buscar por código fijo o cliente. | Visor Leaflet con filtro SQL para puntos, leyenda y búsqueda existente. En Registros se conserva la ficha geográfica. |
| RU-04 | Administrador o consultor | Ver manzanas, lotes, vías, dimensiones y un mapa base en 2D y 3D. | OpenStreetMap, vista 2D, vista inclinable con MapLibre y dimensiones de segmentos exteriores en 2D desde zoom 18. Las medidas se calculan a partir de la geometría y son aproximadas. |
| RU-05 | Administrador | Elegir cortador o reconector, indicar cantidad y punto de partida, previsualizar y guardar la ruta numerada. | **Asignaciones** filtra trabajadores por rol activo, admite 1–50 tareas y calcula el orden con Dijkstra sobre la capa Vías. |
| RU-06 | Administrador | Ver el avance y la ubicación del trabajador durante el recorrido. | **Monitoreo** actualiza cada 8 segundos y separa la ruta planificada de las posiciones recibidas. Incluye filtros de fecha, tipo y trabajador, y reproducción del recorrido, inferidos de las capturas. |
| RU-07 | Cortador o reconector | Consultar sus tareas en el orden calculado y abrir un punto asignado. | **Mis tareas** lista las asignaciones propias, ordena las paradas y muestra el resultado de las tareas registradas. |
| RU-08 | Cortador o reconector | Obtener GPS, ver destino y distancia, y abrir navegación de moto, a pie o vehículo. | Android usa GPS nativo y abre Google Maps. La distancia que muestra la ficha es en línea recta. El modo y las indicaciones dependen de la cobertura del proveedor. |
| RU-09 | Cortador o reconector | Registrar medidor, lectura y evidencia de una operación realizada, u observación si no se pudo completar. | Formulario móvil con hasta tres fotos, límite inferido de las capturas, y resultado Realizado u Observado. |
| RU-10 | Cortador o reconector | Registrar automáticamente fecha, hora y posición, incluso cuando se informa «casa cerrada». | El servidor registra la hora UTC y exige coordenadas, precisión de hasta 100 m y distancia de hasta 150 m al destino. Son umbrales de implementación. El GPS verifica proximidad reportada y puede ser simulado por el dispositivo. |
| RU-11 | Administrador | Clasificar a los trabajadores y preparar conexiones para la operación. | Requisito derivado del flujo. En **Administración** se agregan los roles operativos a cuentas activas. En **Registros** se puede marcar una conexión normal para corte. |
| RU-12 | Cortador o reconector | Leer automáticamente el medidor a partir de una foto. | OCR opcional. La captura y la lectura manual están incluidas. |
| RU-13 | Equipo de entrega | Entregar un enlace con documentación, base y proyecto completo. | Entregable académico separado del código. La publicación de un respaldo con datos de clientes requiere autorización y un enlace con acceso controlado. |

Las capturas también muestran entradas de menú para lecturación y órdenes de trabajo. El objetivo escrito delimita esta fase a cortes y reconexiones, por lo que esas entradas quedan como alcance por confirmar.

## Preparación

1. Con autorización para cambios de esquema, aplicar `database/extensions/10_OperacionesAgua.sql` a la base existente. El script agrega tablas de operaciones y registra los roles oficiales ausentes. La aplicación no ejecuta el script al iniciar.
2. En **Administración**, asignar **Cortador** o **Reconector** a una cuenta activa. SQL conserva el nombre oficial `Reconexion`. El trabajador debe iniciar sesión nuevamente.
3. En **Registros → Códigos Fijos**, abrir una conexión Normal autorizada para la prueba y pulsar **Marcar para corte**. Las reconexiones se planifican con conexiones en estado Cortado. En la revisión local, las 6.271 conexiones estaban en estado Normal.
4. Reiniciar la Web y usar la APK de esta versión. También se puede usar la PWA por HTTPS para capturar fotos y obtener GPS.

## Prueba de aceptación

1. Como administrador, abrir **Asignaciones**, seleccionar el trabajador, indicar una cantidad y hacer clic en una calle para elegir la partida. Calcular la ruta, comprobar la numeración y guardarla.
2. En Android, iniciar sesión como ese trabajador. Comprobar el orden, iniciar recorrido, abrir una tarea, obtener ubicación y abrir navegación externa. Confirmar en **Monitoreo** que llegan muestras nuevas durante el desplazamiento.
3. En el sitio de prueba autorizado, capturar la evidencia y guardar el resultado. Un corte realizado cambia la conexión a Cortado y una reconexión realizada a Normal. Una observación conserva el estado. Comprobar la marca de tiempo, ubicación y evidencia en **Monitoreo**.
4. Verificar que otro trabajador no puede ver ni modificar la asignación. Probar también GPS denegado, baja precisión y falta de foto al reportar como realizado.

La planificación elige iterativamente el destino alcanzable de menor coste y usa Dijkstra para cada tramo. Conecta intersecciones geométricas y ajusta los puntos a la vía cercana dentro de 250 m. Usa las geometrías disponibles en SQL, que no incluyen sentidos, giros ni niveles de paso. El trazado se limita a las vías cargadas y no garantiza el óptimo global de todas las paradas.

Android 8 o superior usa un servicio de ubicación en primer plano para continuar enviando posiciones mientras el trabajador usa el navegador de rutas. Se puede detener desde **Mis tareas** o desde el control del sistema. La PWA envía muestras con la pantalla de campo visible. El flujo debe verificarse en un teléfono real.
