# Deploy StockControl

Arquitectura recomendada para hotelesdepeten.com:

- Frontend: `https://stock.hotelesdepeten.com` en Vercel.
- Backend: `https://api-stock.hotelesdepeten.com` en VPS.
- Base de datos: SQL Server en Docker, escuchando solo en `127.0.0.1:1433`.

## DNS

Configura:

- `stock.hotelesdepeten.com` en Vercel. Vercel indicara el record exacto; normalmente para subdominio es un CNAME.
- `api-stock.hotelesdepeten.com` apuntando a la IP publica del VPS.

Si usas Cloudflare, deja el backend en modo HTTPS Full/Strict cuando el certificado ya este activo.

## VPS

Requisitos:

- Docker y Docker Compose.
- Nginx.
- Certbot si vas a emitir SSL con Let's Encrypt.

## SQL Server Docker

En el VPS, copia `deploy/docker-compose.sqlserver.yml` a una carpeta como `/opt/stockcontrol/db`.

Ejemplo:

```bash
export MSSQL_SA_PASSWORD='CAMBIAR_PASSWORD_SQL_FUERTE'
docker compose -f docker-compose.sqlserver.yml up -d
```

El compose publica SQL Server solo en `127.0.0.1:1433`, asi la base no queda expuesta a internet.

## Variables del backend

Copia `deploy/stockcontrol-api.env.example` a:

```bash
/etc/stockcontrol/stockcontrol-api.env
```

Cambia como minimo:

- `ConnectionStrings__Default`
- `Jwt__Key`
- `Seed__AdminPassword`

Genera secretos fuertes, por ejemplo:

```bash
openssl rand -base64 48
```

La app aplica migraciones automaticamente al arrancar. Si la base esta vacia, tambien crea roles, hoteles base, unidades y el admin inicial.

## Publicar backend

Desde el proyecto, publica self-contained para Linux para no depender del runtime instalado en el VPS:

```bash
dotnet publish StockBackend/StockControl.Api/StockControl.Api.csproj -c Release -r linux-x64 --self-contained true -o publish/stockcontrol-api
```

Copia el contenido de `publish/stockcontrol-api` al VPS en:

```bash
/opt/stockcontrol/api
```

Despues de descomprimirlo en el VPS:

```bash
sudo chmod +x /opt/stockcontrol/api/StockControl.Api
sudo chown -R www-data:www-data /opt/stockcontrol/api
```

Instala systemd:

```bash
sudo cp deploy/stockcontrol-api.service.example /etc/systemd/system/stockcontrol-api.service
sudo systemctl daemon-reload
sudo systemctl enable stockcontrol-api
sudo systemctl start stockcontrol-api
sudo systemctl status stockcontrol-api
```

Logs:

```bash
journalctl -u stockcontrol-api -f
```

## Nginx y SSL

Copia `deploy/nginx.stockcontrol.conf.example` a:

```bash
/etc/nginx/sites-available/stockcontrol-api
```

Activalo:

```bash
sudo ln -s /etc/nginx/sites-available/stockcontrol-api /etc/nginx/sites-enabled/stockcontrol-api
sudo nginx -t
sudo systemctl reload nginx
```

Emite certificado:

```bash
sudo certbot --nginx -d api-stock.hotelesdepeten.com
```

Valida:

```bash
curl https://api-stock.hotelesdepeten.com/health
```

Debe responder `Healthy`.

## Vercel

Configura el proyecto con:

- Root Directory: `StockFront`
- Build Command: `npm run build`
- Output Directory: `dist`
- Environment Variable: `VITE_API_URL=https://api-stock.hotelesdepeten.com`
- Domain: `stock.hotelesdepeten.com`

El archivo `StockFront/vercel.json` ya deja listo el fallback de rutas SPA para que URLs como `/inventario` o `/reportes` no den 404 al refrescar.

## Checklist antes de usuarios reales

- `https://api-stock.hotelesdepeten.com/health` responde `Healthy`.
- Login del admin inicial funciona.
- Crear usuario operativo nuevo.
- Cambiar o desactivar el admin inicial si corresponde.
- Crear proveedor, producto y documento de prueba.
- Recibir documento y revisar inventario.
- Ver alertas, auditoria, reportes y cierre mensual.
- Configurar backup automatico de SQL Server.
