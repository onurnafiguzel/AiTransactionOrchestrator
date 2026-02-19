# Monitoring & Alerting Rehberi

## Genel Bakış

AiTransaction sistemi, production-ready monitoring ve observability altyapısı ile donatılmıştır. Bu guide, sistemi nasıl monitoring yapacağınız, dashboards'ı nasıl kullanacağınız ve alerts'u nasıl ayarlayacağınızı açıklar.

## 📊 Monitoring Stack

### Bileşenler

| Araç | Port | Amaç | URL |
|------|------|------|-----|
| **Prometheus** | 9090 | Metrics collection & storage | http://localhost:9090 |
| **Grafana** | 3000 | Metrics visualization | http://localhost:3000 |
| **AlertManager** | 9093 | Alert routing & aggregation | http://localhost:9093 |
| **Elasticsearch** | 9200 | Log storage | http://localhost:9200 |
| **Kibana** | 5601 | Log visualization | http://localhost:5601 |

## 🚀 Başlangıç

### 1. Monitoring Stack'ı Başlat

```bash
# Tüm servisleri başlat
docker-compose up -d

# Logs'u izle
docker-compose logs -f prometheus grafana alertmanager

# Health check
curl http://localhost:9090/-/healthy
curl http://localhost:3000/api/health
curl http://localhost:9093/-/healthy
```

### 2. Grafana'ya Erişim

1. http://localhost:3000 açın
2. Login: `admin` / `admin`
3. İlk login'de password değişmesi istenir (optional)
4. **Dashboards** → **Browse** → Pre-configured dashboards'ı göreceksiniz

### 3. Prometheus'a Erişim

1. http://localhost:9090 açın
2. **Status** → **Targets** kısmında tüm servisleri görebilirsiniz
3. SQL editor'da queries yazabilirsiniz

## 📈 Dashboards

### 01 - Overview Dashboard
**Amaç:** Sistem health'inin genel durumunu görmek

**Key Metrics:**
- Total requests (req/sec)
- Error rate (%)
- Active services count
- Fraud detections (fraud/sec)
- Request rate timeline
- Error rate timeline
- Service health status table

**Kullanım:**
- Sistem down mu? → "Service Down" alert'ini kontrol edin
- Ne kadar trafik geçiyor? → "Total Requests" stat'ını kontrol edin
- Fraud oranı yüksek mi? → "Fraud Detections" stat'ını kontrol edin

**Threshold Values:**
- ⚠️ Error Rate > 5% = WARNING
- 🔴 Error Rate > 10% = CRITICAL
- ⚠️ Fraud Rate > 10% = INVESTIGATE

### 02 - API Performance Dashboard
**Amaç:** API endpoint performance'ını detaylı olarak izlemek

**Key Metrics:**
- Request latency (p50, p95, p99)
- Requests by endpoint
- Errors by endpoint
- Response time distribution
- HTTP status code distribution
- Concurrent requests

**Kullanım:**
- Slow API mi var? → "Request Latency" grafiklerine bakın
- Hangi endpoint'te problem? → "Errors by Endpoint" grafiklerine bakın
- Response time baseline nedir? → "Response Time Distribution" grafiklerine bakın

**HealthCheck SLA:**
- p50: < 100ms ✅
- p95: < 500ms ⚠️ (>500ms = WARNING)
- p99: < 1s ⚠️ (>1s = CRITICAL)

### 03 - Fraud Detection Dashboard
**Amaç:** Fraud detection engine'inin performansını ve sonuçlarını izlemek

**Key Metrics:**
- Fraud detection rate (fraud/sec)
- Approval rate (approved/sec)
- Decision distribution (pie chart)
- Risk score histogram
- Fraud rules hit count
- Average processing time
- Success metrics

**Kullanım:**
- Fraud detection dün kaç adet? → "Decisions Distribution" pie chart'ına bakın
- Hangi rule en çok trigger? → "Fraud Rules Hit Count" grafiklerine bakın
- Processing time normal mi? → "Average Processing Time" grafiklerine bakın

**Normal Ranges:**
- Fraud Rate: 2-5% ✅
- Fraud Rate > 10% = INVESTIGATE
- Processing Time: 50-200ms ✅
- Processing Time > 500ms = SLOW

### 04 - System Resources Dashboard
**Amaç:** Infrastructure health'ını izlemek (DB, Cache, Message Queue, etc.)

**Key Metrics:**
- PostgreSQL connections
- Redis memory usage
- Redis cache hit rate
- Redis keys count
- Elasticsearch cluster health
- RabbitMQ memory usage
- Database query performance
- Elasticsearch document count
- RabbitMQ queue depths

**Kullanım:**
- Database connection pool nearly full? → "PostgreSQL Connections" (>180)
- Redis memory critical? → "Redis Memory Usage" (>85%)
- Cache efficacy? → "Redis Cache Hit Rate" (<70% = LOW)
- Message queue backed up? → "RabbitMQ Queue Depths" (>1000 = HIGH)

**Resource Limits:**
- PostgreSQL connections: 200 max
- Redis memory: 512MB (docker-compose)
- Elasticsearch: 512MB heap
- RabbitMQ queue depth: < 1000 prefer

## 🚨 Alerting

### Alert Types

#### 1. Critical Alerts (Immediate Action Required)
```
- Service Down (any service)
- High Error Rate (>5%)
- Database Connection Pool Exhausted (>180 connections)
- Fraud Worker Down
- Orchestrator Down
- Updater Down
```

**Custom Action:**
1. AlertManager notification alırsınız
2. Slack (if configured) veya Email
3. Grafana dashboard'a bakın
4. `docker-compose logs <service>` ile logs kontrol edin
5. Problem source'unu identify edin

#### 2. Warning Alerts (Monitor & Plan)
```
- High API Latency (p95 > 1s)
- High Memory Usage (>85%)
- Low Cache Hit Rate (<70%)
- High Queue Depth (>1000)
- High Fraud Detection Rate (>10%)
- Slow Queries
```

**Custom Action:**
1. Bir sonraki deployment cycle'ında optimize edin
2. Capacity planning reviewu yapın
3. Performance profiling consider edin

### Alert Configuration

**File:** `scripts/alertmanager.yml`

```yaml
route:
  receiver: 'default'
  group_by: ['alertname', 'cluster', 'service']

receivers:
  - name: 'critical-alerts'
    email_configs:
      - to: 'admin@example.com'  # CUSTOMIZE
    slack_configs:
      - api_url: 'YOUR_SLACK_WEBHOOK'  # CUSTOMIZE
  
  - name: 'warning-alerts'
    slack_configs:
      - api_url: 'YOUR_SLACK_WEBHOOK'  # CUSTOMIZE
```

### Slack Integration (Optional)

1. Slack workspace'te webhook'u create edin:
   - https://api.slack.com/apps → Create App
   - Incoming Webhooks enable edin
   - Create Webhook URL (ör: https://hooks.slack.com/services/...)

2. `scripts/alertmanager.yml` güncelleyin:
```yaml
slack_configs:
  - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
    channel: '#critical-alerts'
```

3. Container'ı restart edin:
```bash
docker-compose restart alertmanager
```

## 📊 Custom Queries

### Prometheus Query Examples

**Request Rate (requests per second):**
```promql
sum(rate(http_server_request_duration_seconds_count[5m]))
```

**Error Rate (as percentage):**
```promql
sum(rate(http_server_request_duration_seconds_count{status=~"5.."}[5m])) 
/ 
sum(rate(http_server_request_duration_seconds_count[5m]))
```

**P95 Latency (milliseconds):**
```promql
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m])) * 1000
```

**Transaction Creation Rate:**
```promql
rate(transaction_created[5m])
```

**Fraud Detection Rate:**
```promql
rate(fraud_detected[5m])
```

**Cache Hit Ratio:**
```promql
rate(redis_keyspace_hits_total[5m]) 
/ 
(rate(redis_keyspace_hits_total[5m]) + rate(redis_keyspace_misses_total[5m]))
```

**Database Connection Pool Usage:**
```promql
pg_stat_activity_count / 200  # Shows as percentage
```

## 🔍 Troubleshooting

### Problem: "No data" in Prometheus

**Causes:**
1. Metrics endpoint not exposed
2. Service down
3. Prometheus scrape config wrong

**Solution:**
```bash
# 1. Check if service is healthy
curl http://localhost:5000/health/live
# 2. Check if metrics endpoint exists
curl http://localhost:5000/metrics
# 3. Check Prometheus targets
# http://localhost:9090/targets → look for RED status
# 4. Re-check docker-compose.yml prometheus service
```

### Problem: "Alerts not firing"

**Causes:**
1. Alert rules not loaded
2. AlertManager down
3. Wrong threshold values

**Solution:**
```bash
# 1. Check Prometheus alert status
# http://localhost:9090/alerts

# 2. Check if AlertManager is running
docker ps | grep alertmanager

# 3. Check AlertManager logs
docker-compose logs alertmanager

# 4. Reload AlertManager config
docker-compose restart alertmanager
```

### Problem: "Missing metrics for specific service"

**Causes:**
1. Service doesn't have OpenTelemetry enabled
2. Service port wrong in prometheus.yml
3. Service health check failing

**Solution:**
```bash
# 1. Verify service is healthy
curl http://service-name:port/health/live

# 2. Check if metrics endpoint exists
curl http://service-name:port/metrics

# 3. Check prometheus.yml for correct config:
#   - job_name: correct?
#   - targets: correct IP:port?
#   - metrics_path: /metrics?

# 4. Restart Prometheus
docker-compose restart prometheus
```

## 🎯 Best Practices

### 1. Regular Dashboard Review
- Günde en az 1 kez Overview dashboard'a bakın
- Weekly API Performance review yapın
- Anomalies investigate edin

### 2. Alert Tuning
- Alert thresholds'u business requirements'a göre ayarlayın
- False positive'ları minimize edin
- Yüksek severitysi olan alert'ları prioritize edin

### 3. Metrics Retention
- Default: 30 days (docker-compose.yml)
- Production'da daha uzun period consider edin (90+ days)
- Grafana datasource'unda retention policy belirleyin

### 4. Performance Baselines
- Normal koşullarda metric values'ları kaydedin
- Seasonal variation'ları identify edin
- Scaling decisions için historical data kullanın

### 5. Security
- Grafana admin password'u değiştirin
- Prometheus'a authentication eklemek consider edin
- AlertManager credentials'ı secure tutun
- Logs'u regular basis'te review edin

## 📚 Additional Resources

- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [AlertManager Documentation](https://prometheus.io/docs/alerting/latest/overview/)
- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
