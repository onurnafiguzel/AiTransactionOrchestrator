# Monitoring Quick Start

## 5-Minute Setup

```bash
# 1. Start the system
docker-compose up -d

# 2. Wait for health checks (~30 seconds)
docker-compose ps

# 3. Access dashboards
# - Grafana: http://localhost:3000 (admin/admin)
# - Prometheus: http://localhost:9090
# - AlertManager: http://localhost:9093
```

## Essential Dashboards

### Check System Health (30 seconds)
1. Grafana → Dashboards → **Overview**
2. Look for:
   - ✅ All green?
   - ⚠️ Any red or yellow?
   - 📊 Normal metrics values?

### Monitor API Performance (during traffic)
1. Grafana → Dashboards → **API Performance**
2. Watch:
   - Request latency (should be < 500ms p95)
   - Error rate (should be < 5%)
   - Which endpoints are slow?

### Check Fraud Detection
1. Grafana → Dashboards → **Fraud Detection**
2. Track:
   - Fraud detection rate (2-5% is normal)
   - Rules being triggered
   - Processing time

### System Resources
1. Grafana → Dashboards → **System Resources**
2. Monitor:
   - Database connections (< 180)
   - Redis memory (< 85%)
   - Cache hit rate (> 70%)
   - Queue depths (< 1000)

## Common Tasks

### Find a Slow API Endpoint
```
1. Go to API Performance dashboard
2. Look at "Errors by Endpoint" or "Response Time Distribution"
3. Identify the problem endpoint
4. Check Transaction API logs: docker-compose logs transaction-api
5. Optimize or scale if needed
```

### Check Why Fraud Detection is Slow
```
1. Go to Fraud Detection dashboard
2. Check "Average Processing Time"
3. View "Fraud Rules Hit Count"
4. If slow: Check Fraud Worker logs
   docker-compose logs fraud-worker
5. Profile the specific rules that are slow
```

### Investigate High Error Rate
```
1. Go to Overview dashboard
2. Check which service has errors (if shown)
3. Look at "Requests by Endpoint" → filter to errors
4. Check service logs:
   docker-compose logs <service-name>
5. Check Elasticsearch for detailed error logs:
   curl -X GET "localhost:9200/logs-*/_search?q=level:ERROR"
```

### Check Database Performance
```
1. Go to System Resources dashboard
2. Check "Database Query Performance" graph
3. If slow queries:
   - Check PostgreSQL query logs
   - Review indexes
   - Consider caching
4. Connection pool status:
   - Should stay < 180/200 (90%)
   - If jumping to max: Connection leak or high load
```

### Monitor Message Queue Health
```
1. Go to System Resources dashboard
2. Check "RabbitMQ Queue Depths"
3. If queue depth is high (> 1000):
   - Fraud Worker might be slow
   - Orchestrator might be blocked
   - Check worker service logs
4. Enable dead letter queue monitoring
```

## Alerting

### How to Get Alerted
1. **Email**: Configure in `scripts/alertmanager.yml`
2. **Slack**: Add webhook URL in `scripts/alertmanager.yml`
3. **Custom Webhook**: Implement in AlertManager

### Alert Severity Levels

**🔴 CRITICAL** - Immediate Action Required
- Any service is down
- Error rate > 5%
- Database pool exhausted

**⚠️ WARNING** - Monitor & Plan
- High latency (p95 > 1s)
- Memory usage > 85%
- Cache hit rate < 70%

### Suppress False Alarms
```yaml
# In scripts/alertmanager.yml
inhibit_rules:
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['alertname']
```

## Performance Benchmarks

### Healthy System Metrics
| Metric | Good | Warning | Critical |
|--------|------|---------|----------|
| p50 Latency | < 50ms | 50-100ms | > 100ms |
| p95 Latency | < 500ms | 500ms-1s | > 1s |
| Error Rate | < 1% | 1-5% | > 5% |
| Fraud Rate | 2-5% | 5-10% | > 10% |
| Cache Hit Rate | > 80% | 70-80% | < 70% |
| DB Connections | < 100 | 100-180 | > 180 |
| Queue Depth | < 100 | 100-1000 | > 1000 |

## Need Help?

See `MONITORING.md` for detailed guide and troubleshooting.
