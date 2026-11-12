# Uso móvil local

## Iniciar el servidor

La cadena `ConnectionStrings:DefaultConnection` debe existir en User Secrets del proyecto Web. El script no la modifica ni la muestra.

```powershell
.\scripts\iniciar-servidor-mobile.ps1
```

El script comprueba que el puerto 8080 esté libre y muestra la URL LAN que debe usar el teléfono.

## Instalar la APK Debug

Con el teléfono vinculado por ADB:

```powershell
& "C:\Users\brand\AppData\Local\Android\Sdk\platform-tools\adb.exe" install -r "src\VisorDatosSIG.Mobile\bin\Debug\net10.0-android\publish\com.visordatos.sig.mobile-Signed.apk"
```

La APK Debug usa inicialmente `http://192.168.0.17:8080` y conserva una dirección ya guardada por el usuario. Para cambiarla, usa **Servidor** en la cabecera de la aplicación.

## SQL Server

El script valida que `ConnectionStrings:DefaultConnection` exista en User Secrets o como variable de entorno, sin mostrar su valor. SQL Server solo es usado por la Web; la APK no se conecta directamente a la base de datos.
