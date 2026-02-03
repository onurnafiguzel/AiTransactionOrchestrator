.PHONY: help build up down logs restart clean test health

help:
	@echo "AI Transaction Orchestrator - Makefile Commands"
	@echo ""
	@echo "docker:"
	@echo "  make build           - Build all Docker images"
	@echo "  make up              - Start all services"
	@echo "  make down            - Stop all services"
	@echo "  make restart         - Restart all services"
	@echo "  make logs            - View all logs (tail -f)"
	@echo "  make logs-api        - View API logs"
	@echo "  make clean           - Remove containers and volumes"
	@echo "  make health          - Check health status"
	@echo ""
	@echo "development:"
	@echo "  make rebuild-api     - Rebuild and restart API only"
	@echo "  make rebuild-workers - Rebuild and restart all workers"
	@echo "  make shell-api       - Open shell in API container"
	@echo "  make db-migrate      - Run database migrations"
	@echo ""
	@echo "testing:"
	@echo "  make test            - Run all tests"
	@echo "  make test-unit       - Run unit tests"
	@echo "  make test-integration - Run integration tests"

# Docker targets
build:
	@echo "Building Docker images..."
	docker-compose build --no-cache

up:
	@echo "Starting all services..."
	docker-compose up -d
	@echo "Waiting for services to be healthy..."
	@sleep 5
	@make health

down:
	@echo "Stopping all services..."
	docker-compose down

restart:
	@echo "Restarting all services..."
	docker-compose restart

logs:
	docker-compose logs -f

logs-api:
	docker-compose logs -f transaction-api

logs-fraud:
	docker-compose logs -f fraud-worker

logs-orchestrator:
	docker-compose logs -f transaction-orchestrator

clean:
	@echo "Removing containers and volumes..."
	docker-compose down -v
	@echo "Cleaned!"

health:
	@echo "Checking service health..."
	@docker-compose ps
	@echo ""
	@echo "Testing endpoints:"
	@curl -s http://localhost:5000/health/live && echo "✅ API: OK" || echo "❌ API: Not responding"
	@curl -s http://localhost:5601/api/status > /dev/null && echo "✅ Kibana: OK" || echo "❌ Kibana: Not responding"

# Development targets
rebuild-api:
	@echo "Rebuilding Transaction.Api..."
	docker-compose build --no-cache transaction-api
	docker-compose up -d transaction-api
	@sleep 2
	docker-compose logs -f transaction-api

rebuild-workers:
	@echo "Rebuilding all worker services..."
	docker-compose build --no-cache fraud-worker transaction-orchestrator transaction-updater
	docker-compose up -d fraud-worker transaction-orchestrator transaction-updater
	@sleep 2
	@docker-compose logs -f

shell-api:
	docker-compose exec transaction-api bash

shell-db:
	docker-compose exec postgres psql -U ato -d ato_db

db-migrate:
	@echo "Running database migrations..."
	@echo "Note: Migrations run automatically on container startup"
	docker-compose exec -T transaction-api dotnet ef database update --project src/Transaction/Transaction.Infrastructure

# Testing targets
test:
	@echo "Running all tests..."
	docker-compose exec -T transaction-api dotnet test

test-unit:
	@echo "Running unit tests..."
	find . -name "*.Tests.csproj" -exec docker-compose exec -T {} dotnet test --filter="Category=Unit" \;

test-integration:
	@echo "Running integration tests..."
	find . -name "*.Tests.csproj" -exec docker-compose exec -T {} dotnet test --filter="Category=Integration" \;

# Quick setup
setup:
	@if [ -x "./docker-setup.sh" ]; then \
		./docker-setup.sh; \
	else \
		echo "Running Docker setup..."; \
		make build && make up && make health; \
	fi

# Development quick start
dev: build up
	@echo ""
	@echo "✅ Development environment ready!"
	@echo ""
	@echo "API: http://localhost:5000"
	@echo "Swagger: http://localhost:5000/swagger"
	@echo "RabbitMQ: http://localhost:15672 (admin/admin)"
	@echo "Kibana: http://localhost:5601"
	@echo ""

# Production-like test
prod-test: clean build
	@echo "Starting production-like environment..."
	ASPNETCORE_ENVIRONMENT=Production docker-compose up -d
	@make health

.DEFAULT_GOAL := help
