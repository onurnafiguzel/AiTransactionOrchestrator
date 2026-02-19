# AI Transaction Orchestrator

**Distributed Transaction Processing with AI-Powered Fraud Detection**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](https://www.docker.com/)
[![Status](https://img.shields.io/badge/Status-95%25%20Complete-success)]()

> **Microservices architecture** with **DDD**, **CQRS**, **Saga Pattern**, and **Event-Driven** design for scalable transaction processing with AI fraud detection.

---

## 📋 Table of Contents

- [Quick Start](#-quick-start)
- [Services](#-services)
- [API Usage](#-api-usage)
- [Monitoring](#-monitoring--observability)
- [Architecture](#-architecture)
- [Documentation](#-documentation)

---

## 🚀 Quick Start

### Prerequisites
- **Docker** & **Docker Compose**
- (Optional) .NET 8 SDK for local development

### Start All Services

```bash
# Clone repository
git clone <repository-url>
cd AiTransactionOrchestrator

# Start everything with one command
docker-compose up -d

# View logs
docker-compose logs -f
```

**✅ That's it!** All services will automatically:
- Build from source
- Create database with schema
- Run migrations
- Start in dependency order
- Be ready in ~2 minutes

### Service URLs

| Service | URL | Credentials |
|---------|-----|-------------|
| **Transaction API** | http://localhost:5000/swagger | - |
| **Support API** | http://localhost:5040/swagger | - |
| **RabbitMQ Admin** | http://localhost:15672 | admin/admin |
| **Kibana Logs** | http://localhost:5601 | - |
| **Prometheus** | http://localhost:9090 | - |
| **Grafana** | http://localhost:3000 | admin/admin |
| **AlertManager** | http://localhost:9093 | - |

---

## 🏗️ Services

### Application Services (5)

| Service | Port | Purpose |
|---------|------|---------|
| **Transaction.Api** | 5000 | REST API with JWT auth |
| **Fraud.Worker** | 5010 | AI-powered fraud detection |
| **Transaction.Orchestrator** | 5020 | Saga orchestration |
| **Transaction.Updater** | 5030 | Status updates |
| **Support.Bot** | 5040 | Customer support API |

### Infrastructure (5)

| Component | Port | Purpose |
|-----------|------|---------|
| **PostgreSQL** | 5432 | ato/ato_pass |
| **RabbitMQ** | 5672, 15672 | admin/admin |
| **Redis** | 6379 | Caching |
| **Elasticsearch** | 9200 | Logging |
| **Kibana** | 5601 | Log visualization |

### Monitoring Stack (3)

| Component | Port | Purpose |
|-----------|------|---------|
| **Prometheus** | 9090 | Metrics collection |
| **Grafana** | 3000 | Metrics visualization (admin/admin) |
| **AlertManager** | 9093 | Alert routing |
| **Kibana** | 5601 | Log viewer |

---

## 🔑 API Usage

### 1. SignUp (Create Account)

```bash
curl -X POST http://localhost:5000/api/auth/signup \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass123!",
    "fullName": "John Doe"
  }'
```

### 2. Login (Get JWT Token)

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "password": "SecurePass123!"
  }'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "role": "Customer"
}
```

### 3. Create Transaction

```bash
curl -X POST http://localhost:5000/api/transaction \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 5000,
    "currency": "USD",
    "merchantId": "AMAZON_TR"
  }'
```

### 4. Get Transaction Status

```bash
curl http://localhost:5000/api/transaction/{id} \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

**Response:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "amount": 5000,
  "currency": "USD",
  "status": "Approved",
  "riskScore": 25,
  "explanation": "Low risk transaction from trusted merchant"
}
```

---

## 🏗️ Architecture

### Request Flow
```
Client (JWT Token)
  ↓
Transaction API (Validation, Rate Limiting)
  ↓
PostgreSQL (Save) → Outbox → RabbitMQ
  ↓
Saga Orchestrator (Workflow)
  ↓
Fraud Worker (4 AI Rules)
  ├─ High Amount Check
  ├─ Merchant Risk (Redis)
  ├─ Geographic Risk (Redis)
  └─ Velocity Check (Redis)
  ↓
Transaction Updater (Status Update + Cache Invalidation)
  ↓
Support Bot (Customer Queries)
```

### Key Patterns
- ✅ **Domain-Driven Design** - Aggregates, value objects
- ✅ **CQRS** - Command/query separation
- ✅ **Saga Pattern** - Distributed transactions
- ✅ **Outbox/Inbox** - Reliable messaging
- ✅ **Circuit Breaker** - Fault tolerance (Polly)
- ✅ **Rate Limiting** - 4 strategies
- ✅ **Cache Invalidation** - Event-driven

---

## � Monitoring & Observability

### Quick Access

| Tool | URL | Credentials | Purpose |
|------|-----|-------------|---------|
| **Grafana** | http://localhost:3000 | admin/admin | Metrics dashboards |
| **Prometheus** | http://localhost:9090 | - | Metrics collection |
| **AlertManager** | http://localhost:9093 | - | Alert routing |
| **Kibana** | http://localhost:5601 | - | Log analysis |

### Pre-configured Dashboards

1. **Overview Dashboard** - System health at a glance
2. **API Performance** - Request latency, throughput, errors
3. **Fraud Detection** - Detection rates, processing time
4. **System Resources** - DB, Cache, Message Queue metrics

### Quick Start

```bash
# Start monitoring stack (included in docker-compose)
docker-compose up -d

# Access Grafana
open http://localhost:3000

# View all metrics endpoints
curl http://localhost:5000/metrics  # Transaction API
curl http://localhost:5010/metrics  # Fraud Worker
curl http://localhost:5020/metrics  # Orchestrator
curl http://localhost:5030/metrics  # Updater
curl http://localhost:5040/metrics  # Support Bot
```

### Documentation

- **[METRICS_SETUP.md](METRICS_SETUP.md)** - 5-minute quick setup guide
- **[MONITORING_QUICKSTART.md](MONITORING_QUICKSTART.md)** - Common tasks reference
- **[MONITORING.md](MONITORING.md)** - Comprehensive monitoring guide
- **[DEVOPS_MONITORING.md](DEVOPS_MONITORING.md)** - DevOps/SRE operations guide

### Key Metrics Tracked

- ✅ HTTP request latency (p50, p95, p99)
- ✅ Error rates by service
- ✅ Throughput (requests/sec)
- ✅ Database connection pool usage
- ✅ Cache hit/miss ratio
- ✅ Message queue depths
- ✅ Fraud detection rates
- ✅ System resource utilization

### Alerting

Pre-configured alerts for:
- 🔴 Service down
- 🔴 High error rate (>5%)
- ⚠️ High latency (p95 >1s)
- ⚠️ Database connection pool exhaustion
- ⚠️ Low cache hit rate (<70%)
- ⚠️ High message queue depth (>1000)

Configure Slack/Email notifications in `scripts/alertmanager.yml`

---

## �📚 Documentation

- **[PROJECT_STATUS.md](PROJECT_STATUS.md)** - Current status, roadmap, missing features
- **[ARCHITECTURE.md](ARCHITECTURE.md)** - Detailed architecture diagrams
- **[AUTHENTICATION_GUIDE.md](AUTHENTICATION_GUIDE.md)** - JWT implementation details
- **[DOCKER_README.md](DOCKER_README.md)** - Docker deployment guide
- **[RESILIENCY_SCALABILITY_ANALYSIS.md](RESILIENCY_SCALABILITY_ANALYSIS.md)** - Advanced patterns

---

## 🔧 Common Commands

```bash
# View service status
docker-compose ps

# Follow logs for specific service
docker-compose logs -f transaction-api

# Stop all services
docker-compose down

# Rebuild and restart
docker-compose up -d --build

# Database shell
docker-compose exec postgres psql -U ato -d ato_db

# RabbitMQ management
open http://localhost:15672

# Check health
curl http://localhost:5000/health/ready
```

---

## 🎯 Current Status

**Overall:** ✅ 95% Complete  
**Missing:** ❌ Tests (0% coverage)  
**Timeline:** 2-3 weeks to production  

See **[PROJECT_STATUS.md](PROJECT_STATUS.md)** for detailed status.

---

## 📝 Features

### Implemented ✅
- ✅ 5 Microservices (API, Orchestrator, Updater, Fraud, Support)
- ✅ 5 Infrastructure services (PostgreSQL, RabbitMQ, Redis, Elasticsearch, Kibana)
- ✅ 3 Monitoring services (Prometheus, Grafana, AlertManager)
- ✅ JWT Authentication & Role-based Authorization
- ✅ AI-Powered Fraud Detection (4 rules)
- ✅ Rate Limiting (4 strategies)
- ✅ Cache Invalidation
- ✅ Input Validation (FluentValidation)
- ✅ Global Exception Handling
- ✅ Request/Response Logging
- ✅ Correlation ID Tracking
- ✅ Health Checks
- ✅ Metrics & Monitoring (OpenTelemetry + Prometheus)
- ✅ Alerting System (AlertManager)
- ✅ Docker Deployment

### Missing ❌
- ❌ Unit Tests (Critical)
- ❌ Integration Tests (Critical)
- ❌ Distributed Tracing

---

## 🤝 Contributing

This is a demonstration project showcasing microservices architecture patterns.

---

## 📄 License

MIT License - See LICENSE file for details

---

**Built with:** .NET 8, PostgreSQL, RabbitMQ, Redis, Docker, MassTransit, EF Core, Serilog, Polly


#### 📊 Observability
- Structured logging with Serilog
- Elasticsearch integration
- Kibana dashboards
- Correlation ID tracking
- Health check endpoints
- Request/response logging middleware

---

## 💻 Development

### Build Locally

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run specific service
dotnet run --project src/Transaction/Transaction.Api/Transaction.Api.csproj
```

### Run Tests

```bash
# Run all tests (when implemented)
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Database Migrations

```bash
# Add migration
dotnet ef migrations add MigrationName --project src/Transaction/Transaction.Infrastructure

# Update database
dotnet ef database update --project src/Transaction/Transaction.Infrastructure
```

### Docker Operations

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f [service-name]

# Stop services
docker-compose down

# Clean up (remove volumes)
docker-compose down -v

# Rebuild specific service
docker-compose up -d --build transaction-api
```

---

## 📚 Documentation

| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Complete system architecture & flow diagrams |
| [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) | Detailed component analysis |
| [MISSING_FEATURES.md](MISSING_FEATURES.md) | Missing features with timelines |
| [AUTHENTICATION_GUIDE.md](AUTHENTICATION_GUIDE.md) | JWT setup and usage |
| [DOCKER_README.md](DOCKER_README.md) | Docker deployment guide |

---

## 🗺️ Roadmap

### Phase 1: Testing & Quality (Week 1-2)
- Unit tests for all layers
- Integration tests for critical flows
- Cache invalidation implementation
- Performance benchmarking

### Phase 2: Production Readiness (Week 3-4)
- Rate limiting implementation
- API versioning
- Distributed tracing
- Load testing
- Security audit

### Phase 3: Advanced Features (Week 5-6)
- Pagination for all GET endpoints
- Batch processing API
- Webhook notifications
- Metrics & monitoring

### Phase 4: Management Tools (Week 7-8)
- Admin dashboard
- Fraud rules management UI
- Real-time alerting
- Advanced analytics

---

## 🤝 Contributing

Contributions are welcome! Please read the contributing guidelines before submitting PRs.

---

## 📄 License

This project is licensed under the MIT License.

---

**Status:** Active Development · **Version:** 1.0.0-beta · **Last Updated:** February 9, 2026
