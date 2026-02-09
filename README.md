# AI Transaction Orchestrator

**Production-Ready Distributed Transaction Processing System**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker)](https://www.docker.com/)
[![Microservices](https://img.shields.io/badge/Architecture-Microservices-orange)]()
[![DDD](https://img.shields.io/badge/Pattern-DDD-blue)]()  
[![Status](https://img.shields.io/badge/Status-90%25%20Complete-success)]()  

> Advanced microservices architecture implementing DDD, CQRS, Saga Pattern, and Event-Driven Architecture for scalable transaction processing with AI-powered fraud detection.

---

## 📋 Table of Contents

- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Project Status](#project-status)
- [Features](#features)
- [Development](#development)
- [Documentation](#documentation)

---

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)
- Git

### Run with Docker (Recommended)

```bash
# Clone repository
git clone <repository-url>
cd AiTransactionOrchestrator

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f
```

**That's it!** All services will:
- ✅ Build from source automatically
- ✅ Create PostgreSQL database with schema
- ✅ Run EF Core migrations
- ✅ Start in correct dependency order
- ✅ Be ready to process transactions

### Service URLs

| Service | URL | Credentials | Purpose |
|---------|-----|-------------|---------|  
| **Transaction API** | http://localhost:5000 | JWT Required | Create/query transactions |
| **Swagger UI** | http://localhost:5000/swagger | - | API documentation |
| **Support API** | http://localhost:5040 | JWT Required | Support queries |
| **RabbitMQ Admin** | http://localhost:15672 | admin/admin | Message broker UI |
| **Kibana** | http://localhost:5601 | - | Log visualization |

---

## 🏗️ Architecture Overview

### Microservices (5 Services)

| Service | Port | Type | Status |
|---------|------|------|--------|
| **Transaction.Api** | 5000 | REST API | ✅ Complete |
| **Transaction.Orchestrator.Worker** | 5020 | Background Worker | ✅ Complete |
| **Transaction.Updater.Worker** | 5030 | Background Worker | ✅ Complete |
| **Fraud.Worker** | 5010 | Background Worker | ✅ Complete |
| **Support.Bot** | 5040 | REST API | ✅ Complete |

### Infrastructure Components

| Component | Version | Purpose | Status |
|-----------|---------|---------|--------|
| **PostgreSQL** | 16-Alpine | Transaction & Saga state | ✅ Complete |
| **RabbitMQ** | 3.13 | Message broker | ✅ Complete |
| **Redis** | 7-Alpine | Caching layer | ✅ Complete |
| **Elasticsearch** | 8.13.4 | Log storage | ✅ Complete |
| **Kibana** | 8.13.4 | Log visualization | ✅ Complete |

### Design Patterns Implemented

- ✅ **Domain-Driven Design (DDD)** - Aggregate roots, value objects, domain events
- ✅ **CQRS** - Command/Query separation with MediatR
- ✅ **Saga Pattern** - Distributed transaction orchestration with MassTransit
- ✅ **Outbox Pattern** - Reliable event publishing
- ✅ **Inbox Pattern** - Idempotent message processing
- ✅ **Circuit Breaker** - Fault tolerance (Polly)
- ✅ **Repository Pattern** - Data access abstraction
- ✅ **Unit of Work** - Transaction management

---

## 📊 Project Status

**Overall Completion: 90%**

### ✅ Completed Features (85%)

- ✅ Core microservices architecture
- ✅ Domain-driven design implementation
- ✅ Event-driven communication
- ✅ Fraud detection with 4 AI-powered rules
- ✅ Redis caching (3 strategies: STRING, SET, HASH)
- ✅ JWT authentication & authorization
- ✅ Global exception handling
- ✅ Request/response logging
- ✅ Structured logging to Elasticsearch
- ✅ Health checks (liveness & readiness)
- ✅ Docker containerization
- ✅ Database migrations
- ✅ IP-based fraud detection
- ✅ Circuit breaker for external services
- ✅ FluentValidation for input validation

### ⚠️ In Progress (5%)

- 🔄 Cache invalidation on status updates
- 🔄 Extended health checks for Support.Bot

### ❌ Not Started (10%)

**Critical (4 items):**
- ❌ Unit tests (0% coverage)
- ❌ Integration tests
- ❌ Performance tests
- ❌ Load tests

**Medium Priority (5 items):**
- ❌ Rate limiting (API protection)
- ❌ Pagination for endpoints
- ❌ Distributed tracing (Jaeger/OpenTelemetry)
- ❌ API versioning
- ❌ Metrics & monitoring (Prometheus)

**Low Priority (6 items):**
- ❌ Batch processing API
- ❌ Webhook notifications
- ❌ Admin dashboard UI
- ❌ Transaction search API
- ❌ Fraud rules management UI
- ❌ Real-time alerts

---

## ✨ Features

### Core Capabilities

#### 🔒 Security
- JWT authentication with role-based authorization
- IP address tracking for fraud detection
- Secure configuration management
- CORS policy configuration

#### 🎯 Transaction Processing
- RESTful transaction creation
- Asynchronous fraud detection
- Saga-based workflow orchestration
- Automatic status updates
- Timeline tracking

#### 🛡️ Fraud Detection (4 Rules)
1. **High Amount Rule** - Blocks transactions > $10,000
2. **Merchant Risk Rule** - Blacklist/whitelist checking (Redis SET)
3. **Geographic Risk Rule** - Country-based risk scoring (Redis HASH)
4. **Velocity Check Rule** - Failed transaction counter (Redis STRING + LIST)

#### 💾 Caching Strategy
- Transaction caching (10 min TTL)
- Merchant risk cache
- Geographic risk cache
- Velocity check cache
- Support incident summary cache (30 min TTL)

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
