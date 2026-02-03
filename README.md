# AI Transaction Orchestrator

Distributed transaction processing system with microservices architecture.

## Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 8 SDK (for local development)

### Run with Docker

```bash
docker-compose up -d
```

That's it! All services will:
- ✅ Build from source
- ✅ Create PostgreSQL database with schema
- ✅ Run EF Core migrations automatically
- ✅ Start in correct dependency order
- ✅ Be ready to process transactions

### Service URLs

| Service | URL | Purpose |
|---------|-----|---------|
| **Transaction API** | http://localhost:5000 | Create/query transactions |
| **Swagger UI** | http://localhost:5000/swagger | API documentation |
| **RabbitMQ Admin** | http://localhost:15672 | Message broker (admin/admin) |
| **Kibana** | http://localhost:5601 | Log visualization |

### Development

Build locally:
```bash
dotnet build
```

Run tests:
```bash
dotnet test
```

### Architecture

**5 Application Services:**
- `Transaction.Api` - REST API entry point
- `Transaction.Orchestrator.Worker` - Saga orchestration engine
- `Transaction.Updater.Worker` - Status update consumer
- `Fraud.Worker` - Fraud detection processor
- `Support.Bot` - Customer support API

**Infrastructure:**
- PostgreSQL - Transaction & Saga state storage
- RabbitMQ - Async messaging
- Elasticsearch + Kibana - Structured logging

### Key Features

- ✅ Automatic database migrations on startup
- ✅ Domain-driven design with aggregate roots
- ✅ Saga pattern for distributed transactions
- ✅ Outbox/Inbox for reliable messaging
- ✅ Structured logging to Elasticsearch
- ✅ Health checks with dependencies

### Docker Cleanup

Stop and remove all containers:
```bash
docker-compose down -v
```

View logs:
```bash
docker-compose logs -f [service-name]
```

---

**Need help?** Check container logs: `docker-compose logs -f transaction-api`
