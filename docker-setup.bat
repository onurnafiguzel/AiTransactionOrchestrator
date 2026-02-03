@echo off
REM AI Transaction Orchestrator - Docker Setup for Windows
REM Usage: docker-setup.bat

setlocal enabledelayedexpansion

cls
echo.
echo ╔════════════════════════════════════════════════════════════════╗
echo ║   AI Transaction Orchestrator - Docker Multi-Container Setup   ║
echo ╚════════════════════════════════════════════════════════════════╝
echo.

REM Check Docker installation
docker --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker is not installed. Please install Docker Desktop for Windows.
    pause
    exit /b 1
)

docker-compose --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker Compose is not installed.
    pause
    exit /b 1
)

echo ✅ Docker and Docker Compose found
echo.

REM Create scripts directory if not exists
if not exist "scripts\" mkdir scripts

echo 🔍 Checking required configuration files...
setlocal enabledelayedexpansion
set "missing=0"

for %%F in (
    "docker-compose.yml"
    "Dockerfile"
    ".dockerignore"
    "scripts\init-db.sql"
    "scripts\rabbitmq.conf"
) do (
    if not exist %%F (
        echo ❌ Missing: %%F
        set "missing=1"
    )
)

if !missing! equ 1 (
    echo.
    echo Some required files are missing. Please ensure all files are in place.
    pause
    exit /b 1
)

echo ✅ All required files found
echo.

REM Stop and remove existing containers
echo 🧹 Cleaning up existing containers...
docker-compose down --remove-orphans >nul 2>&1
echo ✅ Cleaned up
echo.

REM Build images
echo 🔨 Building Docker images (this may take 5-10 minutes)...
docker-compose build --no-cache
if errorlevel 1 (
    echo ❌ Build failed!
    pause
    exit /b 1
)

echo.
echo 🚀 Starting services...
docker-compose up -d

echo.
echo ⏳ Waiting for services to be healthy (this may take 1-2 minutes)...

REM Wait for PostgreSQL
set "count=0"
:wait_postgres
if !count! equ 60 (
    echo PostgreSQL timeout
    goto skip_postgres
)
docker-compose exec -T postgres pg_isready -U ato >nul 2>&1
if errorlevel 1 (
    set /a count=!count!+1
    timeout /t 1 /nobreak >nul
    cls
    echo ⏳ Waiting for PostgreSQL... (!count!/60)
    goto wait_postgres
)
:skip_postgres
echo ✅ PostgreSQL ready
echo.

REM Wait for RabbitMQ
set "count=0"
:wait_rabbitmq
if !count! equ 60 (
    echo RabbitMQ timeout
    goto skip_rabbitmq
)
docker-compose exec -T rabbitmq rabbitmq-diagnostics ping >nul 2>&1
if errorlevel 1 (
    set /a count=!count!+1
    timeout /t 1 /nobreak >nul
    goto wait_rabbitmq
)
:skip_rabbitmq
echo ✅ RabbitMQ ready

REM Wait for Elasticsearch
set "count=0"
:wait_elastic
if !count! equ 60 (
    echo Elasticsearch timeout
    goto skip_elastic
)
docker exec ato-elasticsearch curl -s http://localhost:9200/_cluster/health >nul 2>&1
if errorlevel 1 (
    set /a count=!count!+1
    timeout /t 1 /nobreak >nul
    goto wait_elastic
)
:skip_elastic
echo ✅ Elasticsearch ready
echo.

echo ╔════════════════════════════════════════════════════════════════╗
echo ║                    Setup Complete!                             ║
echo ╚════════════════════════════════════════════════════════════════╝
echo.
echo 📍 Service URLs:
echo    • Transaction API:      http://localhost:5000
echo    • Swagger UI:           http://localhost:5000/swagger
echo    • RabbitMQ Admin:       http://localhost:15672 (admin/admin)
echo    • Kibana:               http://localhost:5601
echo.
echo 🔧 Database:
echo    • Host: localhost
echo    • Port: 5432
echo    • User: ato
echo    • Password: ato_pass
echo    • Database: ato_db
echo.
echo 📝 Useful Commands:
echo    • View logs:     docker-compose logs -f [service-name]
echo    • Restart:       docker-compose restart
echo    • Stop:          docker-compose stop
echo    • Start:         docker-compose start
echo    • Remove all:    docker-compose down -v
echo.

docker-compose ps
echo.

pause
