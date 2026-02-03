#!/bin/sh
# Entrypoint script for .NET applications running in Docker
# Handles EF Core migrations and health check setup

set -e

PROJECT_NAME=${PROJECT_NAME:-"Application"}
DATABASE_URL=${ConnectionStrings__TransactionDb:-${ConnectionStrings__SagaDb}}
HEALTH_PORT=${Health__Port:-5010}

echo "=========================================="
echo "Starting: $PROJECT_NAME"
echo "Database: $DATABASE_URL"
echo "=========================================="

# Wait for database to be ready
if [ -n "$DATABASE_URL" ]; then
    echo "Waiting for database to be ready..."
    for i in 1 2 3 4 5; do
        if echo "$DATABASE_URL" | grep -q "postgres"; then
            # PostgreSQL check
            DB_HOST=$(echo "$DATABASE_URL" | grep -oP '(?<=Host=)[^;]+' || echo "localhost")
            DB_PORT=$(echo "$DATABASE_URL" | grep -oP '(?<=Port=)[^;]+' || echo "5432")
            DB_USER=$(echo "$DATABASE_URL" | grep -oP '(?<=Username=)[^;]+' || echo "ato")
            
            if timeout 5 bash -c "echo > /dev/tcp/$DB_HOST/$DB_PORT" 2>/dev/null; then
                echo "✅ Database is ready"
                break
            fi
        fi
        
        if [ $i -lt 5 ]; then
            echo "Database not ready, retrying... ($i/5)"
            sleep 5
        else
            echo "⚠️  Database connection failed, continuing anyway..."
        fi
    done
fi

# Create health check indicator
mkdir -p /tmp
touch /tmp/health

echo "✅ Application ready to start"
echo "Starting .NET application..."

# Execute the main application
exec dotnet *.dll
