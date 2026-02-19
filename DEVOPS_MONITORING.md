# DevOps & SRE Guide - Monitoring

## Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                    Applications                          │
├──────────────────────────────────────────────────────────┤
│ Transaction API     Fraud Worker    Support Bot         │
│ (port 5000)         (port 5010)     (port 5040)        │
│ [/metrics]           [/metrics]      [/metrics]         │
└────────┬──────────────┬──────────────┬──────────────────┘
         │              │              │
         └──────────────┼──────────────┘
                        │
         ┌──────────────▼──────────────┐
         │  Prometheus (port 9090)    │
         │  - Scrapes /metrics        │
         │  - Interval: 5-15s         │
         │  - Retention: 30 days      │
         │  - Custom rules loaded     │
         └──────────────┬──────────────┘
                        │
        ┌───────────────┼───────────────┐
        │               │               │
   ┌────▼────┐    ┌────▼────┐    ┌────▼────┐
   │ Grafana  │    │AlertMgr  │    │Queries  │
   │ (3000)   │    │ (9093)   │    │(manual) │
   │ Dashboards   │ Routes   │    │         │
   └──────────┘    └────┬────┘    └─────────┘
                        │
           ┌────────────┼────────────┐
           │            │            │
      ┌────▼──┐  ┌─────▼────┐  ┌──▼─────┐
      │ Email │  │  Slack   │  │Webhook │
      │       │  │ (if cfg) │  │(custom)│
      └───────┘  └──────────┘  └────────┘
```

## 📋 Prometheus Configuration

**File:** `scripts/prometheus.yml`

### Scrape Intervals

```yaml
global:
  scrape_interval: 15s          # Default
  evaluation_interval: 15s       # Alert rules eval

scrape_configs:
  - job_name: 'transaction-api'
    scrape_interval: 5s          # API is fast-changing
  
  - job_name: 'elasticsearch'
    scrape_interval: 30s         # Slow-changing
```

**Decision Matrix:**
| Service Type | Interval | Reason |
|-------------|----------|--------|
| API/Web | 5s | High frequency changes |
| Worker | 10s | Medium frequency |
| Database | 30s | Slow changes |
| Message Queue | 30s | Slow changes |
| Infrastructure | 30s | Slow changes |

### Alert Rules

**File:** `scripts/alert-rules.yml`

Rules are evaluated every 15 seconds. Alert fires after `for` duration:

```yaml
- alert: HighErrorRate
  expr: rate(...) > 0.05
  for: 2m              # Fire after 2 minutes of condition
```

## 🔧 Prometheus Operations

### Backup Metrics

```bash
# Prometheus stores in /prometheus directory
# For Docker:
docker cp ato-prometheus:/prometheus ./prometheus_backup_$(date +%Y%m%d)

# Restore
docker cp prometheus_backup_20240218/ ato-prometheus:/prometheus
docker-compose restart prometheus
```

### Query Language (PromQL)

**Common Patterns:**

```promql
# Instant vector (current value)
http_server_request_duration_seconds_count

# Range vector (1 hour of data)
http_server_request_duration_seconds_count[1h]

# Rate calculation (per second)
rate(http_server_request_duration_seconds_count[5m])

# Sum across all labels
sum(rate(...[5m]))

# Sum by specific label
sum(rate(...[5m])) by (job, method)

# Filtering
http_server_request_duration_seconds_count{job="transaction-api"}

# Histogram percentiles
histogram_quantile(0.95, rate(http_server_request_duration_seconds_bucket[5m]))

# Comparison
rate(...[5m]) > 0.5

# Time-series aggregation
topk(5, ...)    # Top 5 by value
```

## 📊 Grafana Administration

### 1. Data Source Management

**Configure Prometheus:**
```
Settings → Data Sources → Prometheus
URL: http://prometheus:9090
Access: Server (default)
```

**Configure Elasticsearch:**
```
Settings → Data Sources → Elasticsearch
URL: http://elasticsearch:9200
Index: logs-*
Time Field: @timestamp
Log Level Field: level
```

### 2. Dashboard Management

#### Provision Dashboards via Files
```bash
# Dashboards in: scripts/grafana-dashboards/*.json
# Auto-loaded from provisioning config
# Changes reflected every 10 seconds
```

#### Create Custom Dashboard
```
1. Home → Create → Dashboard
2. Add panels (manually or from existing)
3. Save dashboard
4. Export as JSON if want to version control
```

#### Import Pre-built Dashboards
```
Grafana UI → + → Import Dashboard
ID or JSON
Popular: 3662 (Prometheus 2.0), 1860 (Node Exporter)
```

### 3. User Management
```
Admin → Users
- Add users (if needed)
- Assign organizations
- Set roles (Viewer, Editor, Admin)
```

### 4. Notifications
```
Settings → Notification channels
- Email
- Slack
- PagerDuty
- Webhook
```

## 🚨 AlertManager Administration

### 1. Configuration

**File:** `scripts/alertmanager.yml`

```yaml
global:
  resolve_timeout: 5m              # How long to wait before alert resolved

route:
  receiver: 'default'
  group_by: ['alertname']          # Group related alerts
  group_wait: 10s                  # Wait before sending group
  group_interval: 10s              # Check interval
  repeat_interval: 12h             # Re-send resolved alert

receivers:
  - name: 'critical-alerts'
    email_configs: [...]
    slack_configs: [...]

inhibit_rules:
  - source_match: {severity: critical}
    target_match: {severity: warning}
    # Suppress warnings if critical alert exists
```

### 2. Testing Alerts

```bash
# Send test alert
docker exec ato-alertmanager ./alertmanager 

# Or manually:
curl -X POST http://localhost:9093/api/v1/alerts \
  -H "Content-Type: application/json" \
  -d '[{
    "labels": {
      "alertname": "TestAlert",
      "severity": "critical"
    },
    "annotations": {
      "summary": "This is a test"
    }
  }]'
```

### 3. Silence Alerts

```bash
# Temporarily silence an alert (via UI)
Alerting → Silences → New Silence
- Match labels (alertname, job, etc.)
- Duration: 1h, 1d, etc.

# Via API:
curl -X POST http://localhost:9093/api/v1/silences \
  -H "Content-Type: application/json" \
  -d '{
    "matchers": [
      {"name": "alertname", "value": "HighErrorRate"}
    ],
    "duration": "1h",
    "comment": "Deployment in progress"
  }'
```

## 🔍 Metrics Collection

### OpenTelemetry Instrumentation

Each service automatically exports metrics to Prometheus:

```csharp
// Transaction API (ASP.NET Core)
builder.AddOpenTelemetryHttp("Transaction.Api");
app.MapPrometheusMetrics();  // Endpoint: /metrics

// Fraud Worker (Host Service)
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m.AddPrometheusExporter())
builder.Services.AddHostedService<PrometheusMetricsHostedService>();
```

### Custom Metrics

```csharp
using var meter = new Meter("MyApp");

// Counter
var counter = meter.CreateCounter<long>("requests.total");
counter.Add(1, new KeyValuePair<string, object?>("endpoint", "/api/tx"));

// Histogram
var histogram = meter.CreateHistogram<double>("latency.ms");
histogram.Record(125, ...);

// Observable Gauge
var gauge = meter.CreateObservableGauge(
    "active_connections",
    () => new Measurement<long>(activeCount));
```

### Metrics Exported

**By Default (ASP.NET Core):**
- `http_server_request_duration_seconds` (histogram)
- `http_server_request_duration_seconds_count` (counter)
- `process_cpu_seconds_total` (counter)
- `process_resident_memory_bytes` (gauge)

**By Custom Application:**
- `transaction_created` (counter)
- `fraud_detected` (counter)
- `fraud_processing_duration_seconds` (histogram)

## 🛡️ Security

### 1. Prometheus Access Control
```bash
# Consider using nginx reverse proxy:
docker run -d \
  -v /path/to/nginx.conf:/etc/nginx/nginx.conf:ro \
  -p 9090:9090 \
  nginx:latest
```

### 2. AlertManager Authentication
```yaml
# Use basic auth or OAuth2 proxy
# In docker-compose, expose only via reverse proxy
```

### 3. Grafana Security
```
Admin → Settings → Security
- Change default password immediately
- Enable HTTPS (in production)
- Consider SAML/OAuth2 for enterprise
- Limit dashboard editing permissions
```

### 4. Data Privacy
```
- Logs may contain PII (customer IDs, emails)
- Implement log filtering if needed
- Retention policy: Follow GDPR (typically 30-90 days)
```

## 🚀 Scaling Monitoring

### For Growing Systems

**1. Horizontal Scaling (Multiple Prometheus)**
```yaml
# Prometheus federation
global:
  external_labels:
    replica: '1'  # Different for each instance
  
# Remote storage
remote_write:
  - url: https://prometheus-longterm.example.com/api/write
```

**2. Long-term Storage**
- Prometheus default: 30 days
- For production: S3, GCS, or dedicated time-series DB
- Tools: Thanos, Cortex, Mimir

**3. Log Aggregation**
- Current: Elasticsearch + Kibana
- Alternative: Loki stack (lighter weight)
- Cloud: Datadog, New Relic, Splunk

```yaml
# Add Loki to docker-compose.yml
loki:
  image: grafana/loki:latest
  ports:
    - "3100:3100"
  # Scrape logs from all services
```

## 📈 SLO/SLI Setup

### Define SLOs (Service Level Objectives)

```promql
# SLI: Error rate < 0.1% (99.9% availability)
# SLI: p99 latency < 1s

# Alert if we're consuming error budget too fast
increase(errors_total[30d]) / operation:rate30d
> 0.001  # 0.1% error budget
```

### Example SLO Configuration

```yaml
# scripts/alert-rules.yml
- alert: ErrorBudgetExhausted
  expr: |
    sum(increase(errors_total[30d])) / 
    sum(increase(requests_total[30d])) > 0.001
  for: 1h
  labels:
    severity: critical
```

## 📚 Operational Playbooks

### When Alert Fires: "ServiceDown"

1. **Immediate Check:**
   ```bash
   docker ps | grep <service>
   docker-compose logs <service> | tail -50
   ```

2. **Investigate:**
   - Crash reason?
   - Resource exhaustion (OOM)?
   - Dependency down?

3. **Recovery:**
   ```bash
   docker-compose restart <service>
   ```

4. **Prevention:**
   - Check health checks
   - Review resource limits
   - Plan capacity

### When Alert Fires: "HighErrorRate"

1. **Identify:**
   - Which endpoint?
   - Error type (5xx, 4xx)?
   - Recent changes?

2. **Investigate:**
   ```bash
   # Check Elasticsearch logs
   curl -X GET "localhost:9200/logs-*/_search?q=level:ERROR&size=20"
   
   # Check specific service
   docker-compose logs <service> | grep ERROR
   ```

3. **Mitigate:**
   - Silence alert if known issue
   - Restart affected service
   - Rollback recent changes

4. **Resolve:**
   - Fix root cause
   - Test
   - Deploy

### When Alert Fires: "HighLatency"

1. **Check:**
   - Which endpoint?
   - Database slow?
   - Network issue?
   - Resource constrained?

2. **Troubleshoot:**
   ```bash
   # Query Prometheus
   histogram_quantile(0.95, 
     rate(http_server_request_duration_seconds_bucket{job="transaction-api"}[5m])
   ) by (route)
   
   # Database slow queries
   docker exec ato-postgres psql -U ato -d ato_db -c \
     "SELECT * FROM pg_stat_statements ORDER BY total_time DESC LIMIT 10;"
   ```

3. **Optimize:**
   - Add indexes
   - Cache results
   - Optimize query
   - Scale service

## 🎓 Learning Resources

- [Prometheus Best Practices](https://prometheus.io/docs/practices/rules/)
- [Grafana Best Practices](https://grafana.com/docs/grafana/latest/dashboards/best-practices/)
- [AlertManager Routing](https://prometheus.io/docs/alerting/latest/configuration/)
- [SRE Book - Monitoring](https://sre.google/sre-book/monitoring-distributed-systems/)
