# 🚀 Metrics & Monitoring Quick Setup Guide

## Step 1: Build & Start (5 minutes)

```bash
# Build all services
dotnet build

# Start all services including monitoring stack
docker-compose up -d

# Wait for all services to be healthy (~1-2 minutes)
docker-compose ps

# Expected output: All services should show (healthy)
```

## Step 2: Access Dashboards (30 seconds)

### Grafana - Main Dashboard
```
URL: http://localhost:3000
Username: admin
Password: admin

1. First login: Skip password change or set new password
2. Navigate to: Dashboards → Browse
3. You'll see 4 pre-configured dashboards:
   - 01 - Overview
   - 02 - API Performance
   - 03 - Fraud Detection
   - 04 - System Resources
```

### Prometheus - Raw Metrics
```
URL: http://localhost:9090

1. Click "Status" → "Targets"
2. Verify all services are UP (blue/green)
3. Try a query: rate(http_server_request_duration_seconds_count[5m])
```

### AlertManager - Alert Status
```
URL: http://localhost:9093

1. Check if any alerts are currently firing
2. View silenced alerts
3. Configure notification routing
```

## Step 3: Generate Test Traffic (Optional)

```bash
# Get JWT token
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!",
    "role": "Customer"
  }'

# Save the token from response
TOKEN="<your-jwt-token>"

# Create transaction
curl -X POST http://localhost:5000/api/transactions \
  -H "Authorization: Bearer $TOKEN" \
   -H "X-Idempotency-Key: 8f1c2d3e4b5a6978c9d0e1f2a3b4c5d6" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.00,
    "currency": "USD",
    "merchantId": "MERCHANT_001",
    "description": "Test transaction",
    "countryCode": "US"
  }'

# Repeat 10-20 times to generate metrics
```

## Step 4: View Metrics in Grafana

### Overview Dashboard
```
1. Go to: Dashboards → Overview
2. You should see:
   ✅ Total Requests: Non-zero value
   ✅ Error Rate: Should be 0% (or very low)
   ✅ Active Services: Should show 5/5
   ✅ Request Rate: Graph showing traffic
```

### API Performance Dashboard
```
1. Go to: Dashboards → API Performance
2. Check:
   - Request latency (should be < 100ms p50)
   - Requests by endpoint (POST /api/transactions should show activity)
   - HTTP status codes (mostly 200/201)
```

### Fraud Detection Dashboard
```
1. Go to: Dashboards → Fraud Detection
2. Check:
   - Fraud detection rate (transactions being analyzed)
   - Decisions distribution (approved vs fraud)
   - Processing time (should be < 200ms)
```

### System Resources Dashboard
```
1. Go to: Dashboards → System Resources
2. Monitor:
   - PostgreSQL connections (should be < 20 for light load)
   - Redis memory (should be minimal)
   - Cache hit rate (will improve as cache warms up)
   - Queue depths (should be 0 or very low)
```

## Step 5: Test Alerting (Optional)

### Trigger a High Error Rate Alert

```bash
# Cause errors by making invalid requests
for i in {1..100}; do
  curl -X POST http://localhost:5000/api/transactions \
    -H "Authorization: Bearer INVALID_TOKEN" \
      -H "X-Idempotency-Key: 9c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f" \
    -H "Content-Type: application/json" \
    -d '{"amount": 100}' &
done

# Wait 2-3 minutes, then check AlertManager
# http://localhost:9093 should show "HighErrorRate" alert
```

### Silence the Alert

```bash
# Via AlertManager UI:
1. Go to http://localhost:9093
2. Click on the alert
3. Click "Silence"
4. Set duration (e.g., 1 hour)
5. Add comment: "Testing alert system"
6. Click "Create"
```

## Common Queries to Try in Prometheus

### Request Rate (requests per second)
```promql
sum(rate(http_server_request_duration_seconds_count[5m]))
```

### Error Rate (as percentage)
```promql
sum(rate(http_server_request_duration_seconds_count{status=~"5.."}[5m])) 
/ 
sum(rate(http_server_request_duration_seconds_count[5m])) * 100
```

### P95 Latency (seconds)
```promql
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))
```

### Database Connections
```promql
pg_stat_activity_count
```

### Redis Memory Usage (percentage)
```promql
redis_memory_used_bytes / redis_memory_max_bytes * 100
```

## Troubleshooting

### Problem: No metrics in Grafana

**Solution:**
```bash
# 1. Check if services are exposing metrics
curl http://localhost:5000/metrics
curl http://localhost:5010/metrics

# 2. Check Prometheus targets
# Go to http://localhost:9090/targets
# All services should show "UP"

# 3. Check Prometheus logs
docker-compose logs prometheus

# 4. Restart Prometheus
docker-compose restart prometheus
```

### Problem: Grafana dashboards showing "N/A"

**Solution:**
```bash
# 1. Check data source configuration
# Grafana → Configuration → Data Sources → Prometheus
# URL should be: http://prometheus:9090

# 2. Test connection by clicking "Test" button
# Should show green "Data source is working"

# 3. Generate some traffic if no data exists yet
```

### Problem: Alerts not appearing

**Solution:**
```bash
# 1. Check AlertManager is running
docker ps | grep alertmanager

# 2. Check alert rules are loaded
# Go to http://localhost:9090/alerts
# Should show configured alert rules

# 3. Check AlertManager configuration
docker-compose logs alertmanager

# 4. Restart AlertManager
docker-compose restart alertmanager
```

## Next Steps

1. **Configure Slack Notifications:**
   - Edit `scripts/alertmanager.yml`
   - Add your Slack webhook URL
   - Restart AlertManager

2. **Customize Dashboards:**
   - Grafana → Dashboards → Select a dashboard
   - Click "Dashboard settings" (gear icon)
   - Make edits and save
   - Export JSON for version control

3. **Add Custom Metrics:**
   - See `src/BuildingBlocks/BuildingBlocks.Contracts/Observability/AiTransactionMetrics.cs`
   - Add your custom counters, gauges, or histograms
   - Rebuild and redeploy

4. **Review Full Documentation:**
   - `MONITORING.md` - Comprehensive guide
   - `DEVOPS_MONITORING.md` - DevOps/SRE guide
   - `MONITORING_QUICKSTART.md` - Quick reference

## Health Check URLs

```bash
# All services health
curl http://localhost:5000/health/ready  # Transaction API
curl http://localhost:5010/health/live   # Fraud Worker
curl http://localhost:5020/health/live   # Orchestrator
curl http://localhost:5030/health/live   # Updater
curl http://localhost:5040/health/ready  # Support Bot

# Monitoring stack health
curl http://localhost:9090/-/healthy     # Prometheus
curl http://localhost:3000/api/health    # Grafana
curl http://localhost:9093/-/healthy     # AlertManager
```

## Success Criteria

After completing this guide, you should have:

✅ All services running and healthy
✅ Grafana accessible with 4 pre-configured dashboards
✅ Prometheus collecting metrics from all services
✅ AlertManager configured and ready
✅ Test traffic generating visible metrics
✅ Basic understanding of the monitoring stack

## Need Help?

- Check logs: `docker-compose logs <service-name>`
- Review configuration files in `scripts/` directory
- See detailed guides: `MONITORING.md` and `DEVOPS_MONITORING.md`
- Check for errors: `docker-compose ps` (all should be healthy)
