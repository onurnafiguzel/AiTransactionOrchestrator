# 🐳 Docker Setup Guide - AI Transaction Orchestrator

Projeyi Docker ile bir komutla kurabilirsiniz. Tüm servisleri otomatik olarak başlar ve veritabanı migrasyonları uygulanır.

## 📋 Ön Gereksinimler

- **Docker Desktop** (v20.10+)
- **Docker Compose** (v2.0+)
- **RAM**: Minimum 8GB (önerilen 16GB)
- **Disk**: Minimum 10GB boş alan

### Kurulum

**macOS/Linux**:
```bash
brew install docker docker-compose
```

**Windows**:
- Docker Desktop for Windows'u indir: https://www.docker.com/products/docker-desktop
- WSL2 backend kullan

## 🚀 Hızlı Başlangıç (One-Liner)

### Linux/macOS:
```bash
chmod +x docker-setup.sh && ./docker-setup.sh
```

### Windows:
```cmd
docker-setup.bat
```

## 📊 Servisler

| Servis | Port | URL | Açıklama |
|--------|------|-----|----------|
| **Transaction API** | 5000 | http://localhost:5000 | Ana API |
| **Swagger** | 5000 | http://localhost:5000/swagger | API Dokumentasyonu |
| **PostgreSQL** | 5432 | localhost | Veritabanı |
| **RabbitMQ** | 5672, 15672 | http://localhost:15672 | Message Broker |
| **Elasticsearch** | 9200 | http://localhost:9200 | Log Storage |
| **Kibana** | 5601 | http://localhost:5601 | Log Viewer |
| **Prometheus** | 9090 | http://localhost:9090 | Metrics |
| **Grafana** | 3000 | http://localhost:3000 | Dashboards |

## 🔐 Credentials

### PostgreSQL
```
Host: localhost
Port: 5432
Database: ato_db
Username: ato
Password: ato_pass
```

### RabbitMQ Management
```
URL: http://localhost:15672
Username: admin
Password: admin
```

### Grafana
```
URL: http://localhost:3000
Username: admin
Password: admin
```

## 🔧 Manuel Kurulum

Setup script'lerini çalıştırmak istemiyorsanız:

```bash
# 1. Build images
docker-compose build

# 2. Start services
docker-compose up -d

# 3. Check status
docker-compose ps

# 4. View logs
docker-compose logs -f

# 5. Health check
docker-compose exec postgres pg_isready -U ato
docker-compose exec rabbitmq rabbitmq-diagnostics ping
```

## 📝 Veritabanı Migrasyonları

Migrasyonlar otomatik olarak çalışır. Manuel olarak çalıştırmak için:

```bash
# Transaction.Api migrations
docker-compose exec transaction-api dotnet ef database update \
  --project src/Transaction/Transaction.Infrastructure

# Orchestrator migrations
docker-compose exec transaction-orchestrator dotnet ef database update \
  --project src/Transaction/Transaction.Orchestrator.Worker
```

## 🔍 Health Checks

Tüm servislerin sağlığını kontrol et:

```bash
# All containers
docker-compose ps

# API health
curl http://localhost:5000/health/live

# PostgreSQL
docker-compose exec postgres pg_isready -U ato

# RabbitMQ
docker-compose exec rabbitmq rabbitmq-diagnostics ping

# Elasticsearch
curl http://localhost:9200/_cluster/health
```

## 📊 Logs

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f transaction-api

# Last N lines
docker-compose logs --tail=50 transaction-api

# With timestamps
docker-compose logs -f -t transaction-api
```

## 🛠️ Yaygın Sorunlar

### "Port already in use"
```bash
# Kullanılan portları bul
lsof -i :5000

# Or change ports in docker-compose.yml
```

### "Container exits immediately"
```bash
# Logs'u kontrol et
docker-compose logs transaction-api

# Rebuild
docker-compose build --no-cache
```

### "Database migration failed"
```bash
# Container'ı temizle
docker-compose down -v

# Yeniden başlat
docker-compose up -d
```

### "RabbitMQ connection refused"
```bash
# RabbitMQ'nun sağlıklı olduğunu doğrula
docker-compose exec rabbitmq rabbitmq-diagnostics status

# Restart if needed
docker-compose restart rabbitmq
```

## 🧹 Cleanup

```bash
# Stop all services
docker-compose stop

# Remove containers
docker-compose rm -f

# Remove volumes (data loss!)
docker-compose down -v

# Remove images
docker rmi $(docker images 'ato-*' -a -q)
```

## 🚀 Development Workflow

### Koda değişiklik yapıldığında:

```bash
# 1. Rebuild only changed service
docker-compose build transaction-api

# 2. Restart service
docker-compose up -d transaction-api

# 3. View logs
docker-compose logs -f transaction-api
```

### Interactive debugging:

```bash
# Open shell in container
docker-compose exec transaction-api bash

# Run dotnet commands inside
docker-compose exec transaction-api dotnet ef migrations list
```

## 📈 Performance Tuning

### PostgreSQL
```yaml
# docker-compose.yml
POSTGRES_INITDB_ARGS: "-c shared_buffers=512MB -c max_connections=400"
```

### Elasticsearch
```yaml
ES_JAVA_OPTS: "-Xms1g -Xmx1g"
```

### RabbitMQ
```yaml
RABBITMQ_CHANNEL_MAX: 4096
```

## 🔄 Continuous Integration

### GitHub Actions örneği:
```yaml
- name: Build Docker images
  run: docker-compose build

- name: Start services
  run: docker-compose up -d

- name: Wait for health
  run: |
    timeout 60 bash -c 'until curl -f http://localhost:5000/health/live; do sleep 1; done'

- name: Run tests
  run: docker-compose exec -T transaction-api dotnet test
```

## 📚 Kaynaklar

- [Docker Documentation](https://docs.docker.com)
- [Docker Compose Specification](https://docs.docker.com/compose/compose-file)
- [PostgreSQL Docker Image](https://hub.docker.com/_/postgres)
- [RabbitMQ Docker Image](https://hub.docker.com/_/rabbitmq)
- [Elasticsearch Docker](https://www.docker.elastic.co)

---

**Sorunlar?** 
- Logs kontrol et: `docker-compose logs -f [service]`
- Health checks: `docker-compose ps`
- Temizle ve yeniden başla: `docker-compose down -v && docker-compose up -d`
