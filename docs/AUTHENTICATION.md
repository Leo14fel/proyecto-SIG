# Autenticación

La Web utiliza Cookie Authentication de ASP.NET Core con el nombre `VisorDatosSIG.Auth`. La cookie es HttpOnly, `SameSite=Lax`, expira a los 30 minutos y usa expiración deslizante. En desarrollo utiliza `SameAsRequest`; en producción requiere HTTPS.

## Esquema oficial utilizado

- `dbo.Usuarios`: `IdUsuario`, `Login`, `Nombre`, `PasswordHash`, `PasswordSalt`, `Iteraciones`, `Activo`, `FechaRegistro`.
- `dbo.Roles`: `IdRol`, `NombreRol`, `Descripcion`, `Estado`.
- `dbo.UsuariosRoles`: relación N:N por `IdUsuario` e `IdRol`, con restricción única.
- `dbo.MenuOpciones` y `dbo.UsuarioMenu`: permisos de menú existentes; no se usan aún para autorizar rutas.

Las contraseñas se verifican con PBKDF2-SHA256 usando la sal, las iteraciones y el hash de 32 bytes del esquema. No se usa ASP.NET Core Identity ni se crea una tabla paralela.

## Configuración local

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=VisorDatosSIG;Trusted_Connection=True;TrustServerCertificate=True" --project src/VisorDatosSIG.Web
```

También puede configurar `ConnectionStrings__DefaultConnection` como variable de entorno. El script oficial documenta el usuario inicial y su mecanismo PBKDF2; no se almacena ninguna contraseña en este repositorio.

## Rutas

- `GET /Cuenta/Login`
- `POST /Cuenta/Login`
- `POST /Cuenta/Logout`
- `GET /Cuenta/AccesoDenegado`

`/`, `/Home` y `/Visor` requieren sesión. La política `Administrador` queda preparada para rutas administrativas futuras. No existe bitácora de login/logout porque el esquema oficial no define una tabla física compatible.
