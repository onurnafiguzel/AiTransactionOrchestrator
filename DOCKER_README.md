# 🐳 AI Transaction Orchestrator - Docker Deployment

[![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)](https://www.docker.com)
[![Status](https://img.shields.io/badge/Status-Production%20Ready-green.svg)](.)

Tüm servisleri **tek komutla** başlayın - Docker ve docker-compose otomatik olarak:
- ✅ Veritabanını kurar ve migrasyonları çalıştırır
- ✅ RabbitMQ message broker'ı başlatır
- ✅ Elasticsearch ve Kibana'yı kurar
- ✅ Grafana dashboards'u yapılandırır
- ✅ Tüm .NET uygulamalarını derleme ve çalıştırır
- ✅ Health checks ile durumu doğrular

---

## 🚀 QuickStart (Önerilen)

### **Linux/macOS:**
```bash
chmod +x docker-setup.sh && ./docker-setup.sh
```

### **Windows (PowerShell):**
```powershell
.\docker-setup.bat
```

### **Makefile ile (tüm OS'ler):**
```bash
make setup
# or
make dev
```

---

## ⏱️ Başlangıç Süresi
- İlk kurulum: **5-10 dakika** (Docker image build)
- Sonraki başlamalar: **1-2 dakika**

---

## 📍 Servis URL'leri

| Servis | URL | Credentials |
|--------|-----|-------------|
| **Transaction API** | http://localhost:5000 | - |
| **Swagger Docs** | http://localhost:5000/swagger | - |
| **RabbitMQ Admin** | http://localhost:15672 | `admin` / `admin` |
| **Kibana** | http://localhost:5601 | - |
| **Grafana** | http://localhost:3000 | `admin` / `admin` |
| **Prometheus** | http://localhost:9090 | - |
| **PostgreSQL** | localhost:5432 | ato / ato_pass |

---

## 📊 Mimari

```
┌─────────────────────────────────────────────────────────┐
│                 Docker Compose Network                  │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │   Postgres   │  │   RabbitMQ   │  │ Elasticsearch│ │
│  │   (Port 5432)│  │ (Port 5672)  │  │  (Port 9200) │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│         │                  │                 │         │
│  ┌──────────────────────────────────────────────────┐  │
│  │                                                  │  │
│  │  Transaction API (5000) ◄─────── Messages ──────┼──┤
│  │  Fraud Worker (5010)                            │  │
│  │  Orchestrator (5020)                            │  │
│  │  Updater (5030)                                 │  │
│  │  Support Bot (5040)                             │  │
│  │                                                  │  │
│  └──────────────────────────────────────────────────┘  │
│         │                  │                           │
│         └────► Kibana ◄────┴────► Prometheus          │
│                  │                      │              │
│                  └──────► Grafana ◄─────┘              │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

---

## 🔧 Yaygın Komutlar

```bash
# Durumu göster
docker-compose ps
make health

# Logs göster
docker-compose logs -f
docker-compose logs -f transaction-api

# Veritabanı shell'i
docker-compose exec postgres psql -U ato -d ato_db

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
