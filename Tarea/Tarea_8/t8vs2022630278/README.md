# Sistema E-commerce Serverless - Tarea 8

Sistema de comercio electrónico serverless desarrollado con Azure Functions, .NET 8.0 y MySQL.

## 🚀 Despliegue en Azure

**URL del Sistema:** https://t8ap2022630278-f9hahsd6graeanef.canadacentral-01.azurewebsites.net/api/Get?nombre=/prueba.html

## 📋 Descripción

Sistema completo de e-commerce implementado como aplicación serverless en Azure Functions, que incluye gestión de usuarios, catálogo de productos, carrito de compras y procesamiento de órdenes. El sistema utiliza MySQL como base de datos y proporciona una interfaz web para interactuar con todas las funcionalidades.

## 🏗️ Arquitectura

### Tecnologías Utilizadas

- **Backend**: Azure Functions v4 con .NET 8.0
- **Base de Datos**: MySQL 9.5
- **Frontend**: HTML5, CSS3, JavaScript (Vanilla)
- **Cloud Provider**: Microsoft Azure
- **Autenticación**: Sistema de tokens personalizado

### Componentes Principales

1. **Azure Functions (HTTP Triggers)**
2. **Base de Datos MySQL**
3. **Cliente Web (SPA)**
4. **Sistema de Archivos Estáticos**

## 📁 Estructura del Proyecto

```
t8vs2022630278/
├── *.cs                          # Azure Functions (endpoints)
├── database_setup.sql            # Script de inicialización DB
├── Program.cs                    # Configuración de la aplicación
├── t8vs2022630278.csproj        # Configuración del proyecto .NET
├── front-end/                    # Aplicación cliente
│   ├── prueba.html              # Interfaz principal
│   └── WSClient.js              # Cliente de servicios web
├── Properties/
│   └── launchSettings.json      # Configuración de ejecución local
└── bin/                          # Archivos compilados
```

## 🔌 Endpoints Disponibles

### Gestión de Usuarios

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/alta_usuario` | POST | Registro de nuevo usuario |
| `/api/login` | POST | Autenticación de usuario |
| `/api/consulta_usuario` | POST | Consulta información del usuario |
| `/api/modifica_usuario` | POST | Actualización de datos del usuario |
| `/api/borra_usuario` | POST | Eliminación de cuenta |

### Gestión de Artículos

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/alta_articulo` | POST | Crear nuevo artículo |
| `/api/consulta_articulos` | GET/POST | Listar todos los artículos |

### Carrito de Compras

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/compra_articulo` | POST | Agregar artículo al carrito |
| `/api/consulta_carrito` | POST | Ver contenido del carrito |
| `/api/modifica_carrito_compra` | POST | Modificar cantidad en carrito |
| `/api/elimina_articulo_carrito_compra` | POST | Eliminar artículo del carrito |
| `/api/elimina_carrito_compra` | POST | Vaciar carrito completo |

### Órdenes

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/finaliza_compra` | POST | Procesar orden de compra |

### Archivos Estáticos

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/Get?nombre={path}` | GET | Servir archivos estáticos del frontend |

## 🗄️ Modelo de Base de Datos

### Tablas Principales

1. **usuarios**
   - Información de usuarios registrados
   - Campos: id_usuario, email, password, nombre, apellido_paterno, apellido_materno, fecha_nacimiento, telefono, genero, token

2. **fotos_usuarios**
   - Imágenes de perfil (BLOB)
   - Relación: N:1 con usuarios

3. **stock**
   - Catálogo de productos
   - Campos: id_articulo, nombre, descripcion, precio, cantidad

4. **fotos_articulos**
   - Imágenes de productos (BLOB)
   - Relación: N:1 con stock

5. **carrito_compra**
   - Artículos en carrito por usuario
   - Relación: N:N entre usuarios y stock

6. **ordenes**
   - Cabecera de órdenes de compra
   - Campos: id_orden, id_usuario, fecha, total

7. **orden_detalle**
   - Detalle de artículos por orden
   - Relación: N:1 con ordenes

## 🛠️ Configuración Local

### Prerrequisitos

- .NET SDK 8.0 o superior
- Azure Functions Core Tools
- MySQL Server 8.0+
- Visual Studio 2022 o VS Code

### Pasos de Instalación

1. **Clonar el repositorio**
   ```bash
   git clone <repository-url>
   cd t8vs2022630278
   ```

2. **Configurar Base de Datos**
   ```bash
   mysql -u root -p < database_setup.sql
   ```

3. **Configurar Variables de Entorno**
   
   Crear/editar `local.settings.json`:
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "DB_SERVER": "localhost",
       "DB_PORT": "3306",
       "DB_DATABASE": "servicio_web",
       "DB_USER": "your_mysql_user",
       "DB_PASSWORD": "your_mysql_password"
     }
   }
   ```

4. **Restaurar Dependencias**
   ```bash
   dotnet restore
   ```

5. **Compilar el Proyecto**
   ```bash
   dotnet build
   ```

6. **Ejecutar Localmente**
   ```bash
   func start
   ```

   El sistema estará disponible en: `http://localhost:7071/api/Get?nombre=/prueba.html`

## 🚢 Despliegue en Azure

### Opción 1: Desde Visual Studio Code

1. Instalar la extensión "Azure Functions"
2. Click derecho en el proyecto → "Deploy to Function App..."
3. Seleccionar o crear una nueva Function App
4. Configurar las variables de entorno en Azure Portal

### Opción 2: Desde Azure CLI

```bash
# Publicar la aplicación
dotnet publish --configuration Release

# Crear Function App (si no existe)
az functionapp create \
  --resource-group <resource-group> \
  --consumption-plan-location canadacentral \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --name t8ap2022630278 \
  --storage-account <storage-account>

# Desplegar
func azure functionapp publish t8ap2022630278
```

### Configuración de Variables de Entorno en Azure

En Azure Portal → Function App → Configuration → Application Settings:

```
DB_SERVER=<mysql-server-host>
DB_PORT=3306
DB_DATABASE=servicio_web
DB_USER=<mysql-username>
DB_PASSWORD=<mysql-password>
```

## 📦 Dependencias NuGet

```xml
<PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.50.0" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore" Version="2.1.0" />
<PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" Version="2.50.0" />
<PackageReference Include="MySql.Data" Version="9.5.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

## 🧪 Pruebas

### Prueba de Registro de Usuario

```bash
curl -X POST https://t8ap2022630278-f9hahsd6graeanef.canadacentral-01.azurewebsites.net/api/alta_usuario \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "password123",
    "nombre": "Juan",
    "apellido_paterno": "Pérez",
    "apellido_materno": "García",
    "fecha_nacimiento": "1990-01-01",
    "telefono": 5551234567,
    "genero": "M"
  }'
```

### Prueba de Login

```bash
curl -X POST https://t8ap2022630278-f9hahsd6graeanef.canadacentral-01.azurewebsites.net/api/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "password123"
  }'
```

### Prueba de Consulta de Artículos

```bash
curl https://t8ap2022630278-f9hahsd6graeanef.canadacentral-01.azurewebsites.net/api/consulta_articulos
```

## 🔒 Seguridad

- **Autenticación**: Sistema basado en tokens generados al login
- **Validación**: Todos los endpoints validan el token de usuario
- **Passwords**: Se recomienda implementar hashing (bcrypt/PBKDF2)
- **HTTPS**: Todas las comunicaciones utilizan HTTPS en producción
- **CORS**: Configurado para permitir acceso desde el frontend

## 📊 Características Principales

### Funcionalidades de Usuario
- ✅ Registro e inicio de sesión
- ✅ Gestión de perfil (consulta, modificación, eliminación)
- ✅ Soporte para fotos de perfil (BLOB)
- ✅ Autenticación con tokens

### Funcionalidades de Productos
- ✅ Catálogo de artículos con imágenes
- ✅ Alta de nuevos productos
- ✅ Gestión de stock en tiempo real

### Funcionalidades de Carrito
- ✅ Agregar artículos al carrito
- ✅ Modificar cantidades
- ✅ Eliminar artículos individuales
- ✅ Vaciar carrito completo
- ✅ Visualización del carrito con totales

### Funcionalidades de Compra
- ✅ Finalización de compra
- ✅ Generación de órdenes
- ✅ Actualización automática de stock
- ✅ Historial de compras

## 🎨 Interfaz de Usuario

La aplicación incluye una interfaz web moderna y responsiva con:
- Diseño limpio y minimalista
- Navegación intuitiva entre secciones
- Formularios validados
- Feedback visual de operaciones
- Visualización de productos con imágenes
- Gestión completa del carrito de compras

## 📝 Tareas de VS Code

El proyecto incluye las siguientes tareas configuradas:

- `clean (functions)`: Limpiar artefactos de compilación
- `build (functions)`: Compilar el proyecto
- `publish (functions)`: Publicar en modo Release
- `func: host start`: Iniciar Azure Functions localmente

## 🤝 Contribuciones

Sistema desarrollado como parte de la Tarea 8 del curso de Sistemas Distribuidos.

**Estudiante**: 2022630278  
**Institución**: [Nombre de la Universidad]  
**Curso**: Sistemas Distribuidos  
**Profesor**: Carlos Pineda Guerrero

## 📄 Licencia

Este proyecto es de uso académico.

## 🐛 Problemas Conocidos

- La autenticación actualmente usa tokens en memoria (considerar JWT para producción)
- Las contraseñas se almacenan en texto plano (implementar hashing)
- No hay validación de imágenes al cargar fotos

## 🔮 Mejoras Futuras

- [ ] Implementar JWT para autenticación
- [ ] Agregar hashing de contraseñas (bcrypt)
- [ ] Implementar caché para consultas frecuentes
- [ ] Agregar paginación en listado de productos
- [ ] Implementar sistema de búsqueda y filtros
- [ ] Agregar validación de imágenes
- [ ] Implementar tests unitarios y de integración
- [ ] Agregar logging estructurado
- [ ] Implementar rate limiting
- [ ] Agregar métricas y monitoreo

## 📞 Contacto

Para preguntas o soporte relacionado con este proyecto académico, contactar al profesor del curso.

---

**Última actualización**: Enero 2026  
**Versión**: 1.0.0
