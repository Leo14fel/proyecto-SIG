# Cliente móvil PWA

## Iniciar el servidor

La cadena `ConnectionStrings:DefaultConnection` debe existir en User Secrets del proyecto Web. El script no la modifica ni la muestra.

```powershell
.\scripts\iniciar-servidor-mobile.ps1
```

El script comprueba que el puerto 8080 esté libre y muestra la URL LAN que debe usar el teléfono.

Abra la URL HTTPS de la Web en el navegador del teléfono e inicie sesión. En Login, pulse **Instalar aplicación** y confirme el aviso del navegador. Si no hay aviso, use el menú de Chrome/Edge y elija **Instalar aplicación**. En Safari iOS, use **Compartir -> Agregar a pantalla de inicio**.

La PWA usa la misma URL del servidor Web, la cookie autenticada y los endpoints existentes. No descarga APK ni se conecta directamente a SQL Server.

## SQL Server

El script valida que `ConnectionStrings:DefaultConnection` exista en User Secrets o como variable de entorno, sin mostrar su valor. SQL Server solo es usado por la Web; la PWA no se conecta directamente a la base de datos.

## Proyecto MAUI histórico

`src/VisorDatosSIG.Mobile` se conserva como referencia y no se elimina. Está fuera de `VisorDatosSIG.sln`, no requiere compilarse para esta entrega y no representa el cliente móvil oficial.
