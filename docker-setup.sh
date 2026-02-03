#!/bin/bash

# AI Transaction Orchestrator - One-Liner Docker Setup Script
# Usage: ./docker-setup.sh

set -e

echo "╔════════════════════════════════════════════════════════════════╗"
echo "║   AI Transaction Orchestrator - Docker Multi-Container Setup   ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""

# Check Docker installation
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not installed. Please install Docker first."
    exit 1
fi

if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose first."
    exit 1
fi

echo "✅ Docker and Docker Compose found"
echo ""

# Check if scripts directory exists
if [ ! -d "./scripts" ]; then
    echo "⚠️  Creating scripts directory..."
    mkdir -p ./scripts
fi

# Verify required files
echo "🔍 Checking required configuration files..."
required_files=(
    "docker-compose.yml"
    "Dockerfile"
    ".dockerignore"
    "scripts/init-db.sql"
    "scripts/rabbitmq.conf"
)

for file in "${required_files[@]}"; do
    if [ ! -f "$file" ]; then
        echo "❌ Missing: $file"
        exit 1
    fi
done

echo "✅ All required files found"
echo ""

# Stop and remove existing containers if running
echo "🧹 Cleaning up existing containers..."
docker-compose down --remove-orphans 2>/dev/null || true
echo "✅ Cleaned up"
echo ""

# Build images
echo "🔨 Building Docker images (this may take 5-10 minutes)..."
docker-compose build --no-cache

echo ""
echo "🚀 Starting services..."
docker-compose up -d

echo ""
echo "⏳ Waiting for services to be healthy (this may take 1-2 minutes)..."

# Wait for PostgreSQL
echo -n "  PostgreSQL: "
for i in {1..60}; do
    if docker-compose exec -T postgres pg_isready -U ato > /dev/null 2>&1; then
        echo "✅ Ready"
        break
    fi
    echo -n "."
    sleep 1
done

# Wait for RabbitMQ
echo -n "  RabbitMQ: "
for i in {1..60}; do
    if docker-compose exec -T rabbitmq rabbitmq-diagnostics ping > /dev/null 2>&1; then
        echo "✅ Ready"
        break
    fi
    echo -n "."
    sleep 1
done

# Wait for Elasticsearch
echo -n "  Elasticsearch: "
for i in {1..60}; do
    if docker exec ato-elasticsearch curl -s http://localhost:9200/_cluster/health > /dev/null 2>&1; then
        echo "✅ Ready"
        break
    fi
    echo -n "."
    sleep 1
done

echo ""
echo "✅ All services are healthy!"
echo ""

# Display URLs
echo "╔════════════════════════════════════════════════════════════════╗"
echo "║                    🎉 Setup Complete! 🎉                       ║"
echo "╚════════════════════════════════════════════════════════════════╝"
echo ""
echo "📍 Service URLs:"
echo "   • Transaction API:      http://localhost:5000"
echo "   • Swagger UI:           http://localhost:5000/swagger"
echo "   • RabbitMQ Admin:       http://localhost:15672 (admin/admin)"
echo "   • Kibana:               http://localhost:5601"
echo ""
echo "🔧 Service Health Checks:"
docker-compose ps
echo ""
echo "📊 Database:"
echo "   • Host: localhost"
echo "   • Port: 5432"
echo "   • User: ato"
echo "   • Password: ato_pass"
echo "   • Database: ato_db"
echo ""
echo "📝 Useful Commands:"
echo "   • View logs:     docker-compose logs -f [service-name]"
echo "   • Restart all:   docker-compose restart"
echo "   • Stop all:      docker-compose stop"
echo "   • Start all:     docker-compose start"
echo "   • Remove all:    docker-compose down -v"
echo ""
echo "✨ Ready to process transactions!"
