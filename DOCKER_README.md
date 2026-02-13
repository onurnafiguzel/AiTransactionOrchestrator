# 🐳 Docker Deployment Guide - AI Transaction Orchestrator

[![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)](https://www.docker.com)
[![Status](https://img.shields.io/badge/Status-Production%20Ready-green.svg)](.)

Start all services with **one command** - Docker automatically:
- ✅ Creates database and runs migrations
- ✅ Starts RabbitMQ message broker
- ✅ Sets up Elasticsearch and Kibana
- ✅ Builds and runs all .NET applications
- ✅ Verifies health checks

---

## 📋 Prerequisites

- **Docker Desktop** (v20.10+)
- **Docker Compose** (v2.0+)
- **RAM**: Minimum 8GB (16GB recommended)
- **Disk**: Minimum 10GB free space

---

## 🚀 Quick Start

### Linux/macOS:
```bash
chmod +x docker-setup.sh && ./docker-setup.sh
```

### Windows (PowerShell):
```powershell
.\docker-setup.bat
```

### Or use Makefile:
```bash
make setup
# or
make dev
```

### Or use Docker Compose directly:
```bash
docker-compose up -d
```

---

## ⏱️ Startup Time
- **First run:** 5-10 minutes (builds Docker images)
- **Subsequent runs:** 1-2 minutes

---

## 📍 Service URLs

| Service | URL | Credentials | Purpose |
|---------|-----|-------------|---------|
| **Transaction API** | http://localhost:5000/swagger | - | Create/query transactions |
| **Support API** | http://localhost:5040/swagger | - | Support queries |
| **RabbitMQ Admin** | http://localhost:15672 | admin/admin | Message broker UI |
| **Kibana** | http://localhost:5601 | - | Log visualization |
| **PostgreSQL** | localhost:5432 | ato/ato_pass | Database |
| **Redis** | localhost:6379 | - | Cache |
| **Elasticsearch** | http://localhost:9200 | - | Log storage |

---

## 📊 Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                 Docker Compose Network                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │
│  │  PostgreSQL  │  │   RabbitMQ   │  │    Redis     │     │
│  │  (Port 5432) │  │ (Port 5672)  │  │ (Port 6379)  │     │
│  └──────────────┘  └──────────────┘  └──────────────┘     │
│         │                  │                 │             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Application Services                    │  │
│  │  - Transaction API (5000)                           │  │
│  │  - Fraud Worker (5010)                              │  │
│  │  - Orchestrator (5020)                              │  │
│  │  - Updater (5030)                                   │  │
│  │  - Support Bot (5040)                               │  │
│  └──────────────────────────────────────────────────────┘  │
│         │                  │                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Observability Stack                     │  │
│  │  - Elasticsearch (9200)                             │  │
│  │  - Kibana (5601)                                    │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 Common Commands

```bash
# Check service status
docker-compose ps

# View logs
docker-compose logs -f
docker-compose logs -f transaction-api

# Stop all services
docker-compose stop

# Start all services
docker-compose start

# Restart specific service
docker-compose restart transaction-api

# Database shell
docker-compose exec postgres psql -U ato -d ato_db

# RabbitMQ diagnostics
docker-compose exec rabbitmq rabbitmq-diagnostics ping

# Redis CLI
docker-compose exec redis redis-cli

# Clean up (remove containers)
docker-compose down

# Clean up (remove volumes too - DATA LOSS!)
docker-compose down -v
```

---

## 🔍 Health Checks

Check the health of all services:

```bash
# All containers
docker-compose ps

# Transaction API
curl http://localhost:5000/health/ready
curl http://localhost:5000/health/live

# PostgreSQL
docker-compose exec postgres pg_isready -U ato

# RabbitMQ
docker-compose exec rabbitmq rabbitmq-diagnostics status

# Elasticsearch
curl http://localhost:9200/_cluster/health

# Redis
docker-compose exec redis redis-cli ping
```

---

## 📋 Database Migrations

Migrations run automatically on startup. To run manually:

```bash
# Transaction.Api migrations
docker-compose exec transaction-api dotnet ef database update

# Check migration status
docker-compose exec transaction-api dotnet ef migrations list
```

---

## 🛠️ Troubleshooting

### "Port already in use"
```bash
# Find which process is using the port
lsof -i :5000  # macOS/Linux
netstat -ano | findstr :5000  # Windows

# Option 1: Stop the process
# Option 2: Change port in docker-compose.yml
```

### "Container exits immediately"
```bash
# Check logs
docker-compose logs transaction-api

# Rebuild without cache
docker-compose build --no-cache

# Remove and recreate
docker-compose down
docker-compose up -d
```

### "Database migration failed"
```bash
# Clean volumes and restart
docker-compose down -v
docker-compose up -d

# Check PostgreSQL logs
docker-compose logs postgres
```

### "RabbitMQ connection refused"
```bash
# Check RabbitMQ health
docker-compose exec rabbitmq rabbitmq-diagnostics status

# Restart RabbitMQ
docker-compose restart rabbitmq

# Wait for RabbitMQ to be ready (can take 30-60 seconds)
```

### "Out of memory"
```bash
# Increase Docker Desktop memory limit
# Settings > Resources > Memory > 8GB+

# Or reduce running services
docker-compose stop kibana elasticsearch
```

---

## 🧪 Development Workflow

```bash
# Start infrastructure only
docker-compose up -d postgres rabbitmq redis elasticsearch kibana

# Run app services locally (for debugging)
cd src/Transaction/Transaction.Api
dotnet run

# Or rebuild specific service
docker-compose build transaction-api
docker-compose up -d transaction-api
```

---

## 🧹 Cleanup

```bash
# Stop all services
docker-compose stop

# Remove containers
docker-compose rm -f

# Remove volumes (WARNING: Data loss!)
docker-compose down -v

# Remove images
docker-compose down --rmi all

# Full cleanup
docker system prune -a --volumes
```

---

## 🔐 Security Considerations

### Production Deployment

**⚠️ Before deploying to production:**

1. **Change default passwords** in docker-compose.yml:
   - PostgreSQL: `ato_pass`
   - RabbitMQ: `admin`

2. **Use secrets** instead of environment variables:
   ```yaml
   secrets:
     db_password:
       file: ./secrets/db_password.txt
   ```

3. **Enable HTTPS** with reverse proxy (nginx, traefik)

4. **Restrict CORS** in appsettings.json

5. **Use environment-specific configs**:
   ```bash
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   ```

---

## 📊 Monitoring

View logs in Kibana:
1. Open http://localhost:5601
2. Go to **Discover**
3. Create index pattern: `logstash-*`
4. Filter by service: `SourceContext:*Transaction.Api*`

---

## 🚀 Performance Tuning

### PostgreSQL
```yaml
environment:
  POSTGRES_INITDB_ARGS: "-c shared_buffers=512MB -c max_connections=200"
```

### RabbitMQ
```yaml
environment:
  RABBITMQ_VM_MEMORY_HIGH_WATERMARK: 0.7
```

### Elasticsearch
```yaml
environment:
  ES_JAVA_OPTS: "-Xms1g -Xmx1g"
```

---

## 📚 Additional Resources

- **[README.md](README.md)** - Quick start guide
- **[PROJECT_STATUS.md](PROJECT_STATUS.md)** - Current project status
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Architecture diagrams
- **[AUTHENTICATION_GUIDE.md](AUTHENTICATION_GUIDE.md)** - JWT setup

---

**Last Updated:** February 13, 2026


# API shell'i
docker-compose exec transaction-api bash

# Migrasyonları çalıştır
docker-compose exec -T transaction-api dotnet ef database update

# Services'i yeniden başlat
docker-compose restart
make restart

# Tüm verileri sil ve temizle
docker-compose down -v
```

---

## 🔐 Varsayılan Credentials

```
PostgreSQL:
  Host: localhost
  Port: 5432
  Database: ato_db
  User: ato
  Password: ato_pass

RabbitMQ:
  User: admin
  Password: admin
  Management: http://localhost:15672

Grafana:
  User: admin
  Password: admin
  Url: http://localhost:3000
```

---

## 📈 Database Migrasyonları

Migrasyonlar otomatik olarak container başlangıcında çalışır. Manual çalıştırmak için:

```bash
# Transaction.Api migrations
docker-compose exec -T transaction-api dotnet ef database update \
  --project src/Transaction/Transaction.Infrastructure

# Orchestrator migrations  
docker-compose exec -T transaction-orchestrator dotnet ef database update \
  --project src/Transaction/Transaction.Orchestrator.Worker
```

---

## 🧪 Test Etme

### Health Check
```bash
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready
```

### API Test
```bash
curl -X POST http://localhost:5000/transactions \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.50,
    "currency": "USD",
    "merchantId": "MERCHANT123"
  }'
```

### Logs İnceleme
```bash
# Tüm logs
docker-compose logs

# Belirli service
docker-compose logs -f fraud-worker

# Son 50 satır
docker-compose logs --tail=50 transaction-api

# Zaman damgası ile
docker-compose logs -f -t
```

---

## 🛠️ Development Workflow

### Code değişikliğinde hızlı rebuild:
```bash
# Only rebuild API
make rebuild-api

# Rebuild all workers
make rebuild-workers

# Watch logs
docker-compose logs -f transaction-api
```

### Database değişikliklerinde:
```bash
# Add new migration
docker-compose exec transaction-api dotnet ef migrations add MigrationName \
  --project src/Transaction/Transaction.Infrastructure \
  -o Persistence/Migrations

# Apply migrations
docker-compose exec -T transaction-api dotnet ef database update \
  --project src/Transaction/Transaction.Infrastructure
```

---

## 🚨 Sorun Giderme

### Port zaten kullanımda
```bash
# Kullanılan port'u bul
lsof -i :5000

# docker-compose.yml'de port'u değiştir
```

### Veritabanı bağlantısı hatası
```bash
# Container'ı kontrol et
docker-compose ps postgres

# Logs göster
docker-compose logs postgres

# Temizle ve yeniden başlat
docker-compose down -v
docker-compose up -d
```

### Services startup sırasında crash
```bash
# Detaylı logs
docker-compose logs --tail=100 [service-name]

# Rebuild
docker-compose build --no-cache
docker-compose up -d
```

### RabbitMQ connection refused
```bash
# RabbitMQ'nun çalışıp çalışmadığını kontrol et
docker-compose exec rabbitmq rabbitmq-diagnostics status

# Restart RabbitMQ
docker-compose restart rabbitmq
```

---

## 📊 Monitoring

### Grafana Dashboards
1. http://localhost:3000 açın (admin/admin)
2. **Configuration > Data Sources** → Prometheus eklenmiş olmalı
3. **+ Create Dashboard** → Prometheus sorguları yazın

### Prometheus Metrics
- URL: http://localhost:9090
- Query örneği: `rate(http_requests_total[5m])`

### Kibana Logs
1. http://localhost:5601 açın
2. **Management > Stack Management > Index Patterns**
3. `aitransaction-logs-*` pattern'i oluşturun
4. **Analytics > Discover** → Logs'u görün

---

## 🔄 Environment Variables

`.env.example` dosyasını `.env` olarak kopyalayın:

```bash
cp .env.example .env
```

Sonra ihtiyaçlarınıza göre düzenleyin:

```env
ASPNETCORE_ENVIRONMENT=Production
FraudExplanation__Enabled=true
FraudExplanation__TimeoutSeconds=10
```

---

## 📚 Dosya Yapısı

```
.
├── docker-compose.yml          # Docker services konfigürasyonu
├── Dockerfile                  # Multi-stage .NET build
├── .dockerignore               # Docker ignore patterns
├── DOCKER_SETUP.md             # Detaylı Docker guide
├── docker-setup.sh             # Linux/macOS setup script
├── docker-setup.bat            # Windows setup script
├── Makefile                    # Convenience commands
├── .env.example                # Environment variables örneği
├── scripts/
│   ├── init-db.sql             # PostgreSQL initialization
│   ├── rabbitmq.conf           # RabbitMQ config
│   ├── prometheus.yml          # Prometheus config
│   ├── grafana-datasources.yml # Grafana datasources
│   ├── grafana-dashboards.yml  # Grafana dashboards
│   └── docker-entrypoint.sh    # Container entrypoint
└── src/
    ├── Transaction/
    │   ├── Transaction.Api/
    │   ├── Transaction.Domain/
    │   ├── Transaction.Application/
    │   ├── Transaction.Infrastructure/
    │   ├── Transaction.Orchestrator.Worker/
    │   └── Transaction.Updater.Worker/
    ├── Fraud/
    │   └── Fraud.Worker/
    ├── Support/
    │   └── Support.Bot/
    └── BuildingBlocks/
        └── BuildingBlocks.Contracts/
```

---

## ⚡ Performance Tips

### Slow build?
```bash
# Clean build cache
docker system prune -a

# Rebuild without cache
docker-compose build --no-cache
```

### High memory usage?
Edit `docker-compose.yml`:
```yaml
elasticsearch:
  environment:
    - ES_JAVA_OPTS=-Xms256m -Xmx256m  # Reduce from 512m
```

### Network issues?
```bash
# Recreate network
docker-compose down
docker network rm ato-network || true
docker-compose up -d
```

---

## 📖 Kaynaklar

- [Docker Documentation](https://docs.docker.com)
- [Docker Compose Guide](https://docs.docker.com/compose)
- [.NET Docker Images](https://github.com/dotnet/dotnet-docker)
- [PostgreSQL Docker](https://hub.docker.com/_/postgres)
- [RabbitMQ Docker](https://hub.docker.com/_/rabbitmq)

---

## 🆘 Destek

Sorular veya sorunlar için:

1. **Logs kontrol et**: `docker-compose logs -f [service]`
2. **Health check**: `docker-compose ps`
3. **Container debug**: `docker-compose exec [service] bash`
4. **Clean restart**: `docker-compose down -v && docker-compose up -d`

---

**Happy coding! 🚀**
