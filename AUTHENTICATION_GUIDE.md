# Authentication & Authorization Implementation Guide

## 🎯 Overview

Complete authentication and authorization system has been added to the AI Transaction Orchestrator with:

- ✅ **User Domain Entity** - DDD pattern with soft delete
- ✅ **JWT Authentication** - Token-based security
- ✅ **Role-Based Authorization** - Admin & Customer roles
- ✅ **Transaction User Tracking** - Every transaction linked to a user
- ✅ **Saga State User Context** - UserId tracked throughout orchestration
- ✅ **Admin-Only Support Bot** - Protected customer support endpoints

---

## 📋 Database Migration Required

**IMPORTANT:** Before running the application, you must create and apply migrations for the new schema changes.

### Migration Steps

#### 1. Create Migration for Transaction Database

```powershell
# Navigate to Transaction.Api project
cd src/Transaction/Transaction.Api

# Create migration
dotnet ef migrations add AddUserIdToTransactions `
  --project ../Transaction.Infrastructure/Transaction.Infrastructure.csproj `
  --startup-project Transaction.Api.csproj `
  --context TransactionDbContext `
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update `
  --project ../Transaction.Infrastructure/Transaction.Infrastructure.csproj `
  --startup-project Transaction.Api.csproj `
  --context TransactionDbContext
```

#### 2. Create Migration for Saga Orchestrator Database

```powershell
# Navigate to Orchestrator Worker
cd src/Transaction/Transaction.Orchestrator.Worker

# Create migration
dotnet ef migrations add AddUserIdToSagaState `
  --context OrchestratorSagaDbContext `
  --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update --context OrchestratorSagaDbContext
```

---

## 🔐 API Endpoints

### Authentication Endpoints

#### 1. SignUp (Create Account)
```http
POST http://localhost:5000/api/auth/signup
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "fullName": "John Doe"
}

Response 201:
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com"
}
```

#### 2. Login
```http
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}

Response 200:
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "fullName": "John Doe",
  "role": "Customer",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAtUtc": "2026-02-08T10:30:00Z"
}
```

#### 3. Get Current User
```http
GET http://localhost:5000/api/auth/me
Authorization: Bearer {token}

Response 200:
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "email": "user@example.com",
  "role": "Customer"
}
```

#### 4. Deactivate Account (Soft Delete)
```http
DELETE http://localhost:5000/api/auth/me
Authorization: Bearer {token}

Response 204: No Content
```

---

### Transaction Endpoints (Protected)

#### Create Transaction (Requires JWT)
```http
POST http://localhost:5000/api/transaction
Authorization: Bearer {token}
Content-Type: application/json

{
  "amount": 1500.00,
  "currency": "USD",
  "merchantId": "AMAZON_TR"
}

Response 201:
{
  "transactionId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "correlationId": "abc123def456"
}
```

---

### Support Bot Endpoints (Admin Only)

#### Get Transaction Details (Admin Only)
```http
GET http://localhost:5040/api/support/transactions/{transactionId}
Authorization: Bearer {admin_token}

Response 200:
{
  "transactionId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Approved",
  "reason": "Low risk transaction",
  // ... full transaction report
}

Response 401: Unauthorized (if not admin)
Response 403: Forbidden (if customer role)
```

---

## 🔑 JWT Configuration

Both **Transaction.Api** and **Support.Bot** use the same JWT configuration:

### appsettings.json
```json
{
  "Jwt": {
    "SecretKey": "your-256-bit-secret-key-min-32chars-change-this-in-production!",
    "Issuer": "AiTransactionOrchestrator",
    "Audience": "AiTransactionOrchestrator",
    "ExpirationMinutes": "60"
  }
}
```

**⚠️ IMPORTANT:** Change the `SecretKey` in production! Minimum 32 characters required.

---

## 👤 User Roles

### Customer (Default)
- Can create transactions
- Can view own transactions
- Can deactivate own account

### Admin
- All Customer permissions
- Can access Support Bot endpoints
- Can view all transactions
- Can query incidents and fraud data

---

## 🗄️ Database Schema Changes

### New Table: `users`
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY,
    email VARCHAR(256) UNIQUE NOT NULL,
    password_hash VARCHAR(512) NOT NULL,
    full_name VARCHAR(256) NOT NULL,
    role INT NOT NULL, -- 1=Customer, 2=Admin
    status INT NOT NULL, -- 1=Active, 2=Inactive, 3=Suspended
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    created_at_utc TIMESTAMP NOT NULL,
    updated_at_utc TIMESTAMP NOT NULL,
    last_login_at_utc TIMESTAMP,
    deactivated_at_utc TIMESTAMP,
    deactivation_reason VARCHAR(500)
);

CREATE UNIQUE INDEX ix_users_email ON users(email);
```

### Updated Table: `transactions`
```sql
ALTER TABLE transactions 
ADD COLUMN user_id UUID NOT NULL;

CREATE INDEX idx_transactions_user_id ON transactions(user_id);
CREATE INDEX idx_transactions_user_id_created_at ON transactions(user_id, created_at_utc);
```

### Updated Table: `transaction_orchestrations` (Saga)
```sql
ALTER TABLE transaction_orchestrations 
ADD COLUMN user_id UUID NOT NULL;
```

---

## 🚀 Testing Workflow

### 1. Create Admin User (Manual)
```sql
-- After migrations, manually create an admin user
INSERT INTO users (id, email, password_hash, full_name, role, status, is_deleted, created_at_utc, updated_at_utc)
VALUES (
  gen_random_uuid(),
  'admin@example.com',
  '-- use SignUp endpoint to generate hash --',
  'System Admin',
  2, -- Admin role
  1, -- Active status
  false,
  NOW(),
  NOW()
);
```

**OR** use the SignUp endpoint and manually update the role:
```sql
UPDATE users SET role = 2 WHERE email = 'admin@example.com';
```

### 2. Complete Transaction Flow

```bash
# 1. Sign up
POST /api/auth/signup
{"email": "customer@test.com", "password": "Test123!", "fullName": "Test User"}

# 2. Login
POST /api/auth/login
{"email": "customer@test.com", "password": "Test123!"}
# Save the token from response

# 3. Create transaction
POST /api/transaction
Authorization: Bearer {token}
{"amount": 500, "currency": "USD", "merchantId": "TEST_MERCHANT"}

# 4. Admin login
POST /api/auth/login
{"email": "admin@example.com", "password": "Admin123!"}

# 5. Check transaction details (admin only)
GET /api/support/transactions/{transactionId}
Authorization: Bearer {admin_token}
```

---

## 🛡️ Security Features

### Password Hashing
- **Algorithm:** PBKDF2 with SHA256
- **Iterations:** 100,000
- **Salt:** 128-bit random
- **Key:** 256-bit derived key

### JWT Tokens
- **Algorithm:** HMAC-SHA256
- **Expiration:** 60 minutes (configurable)
- **Claims:** UserId, Email, Role
- **Validation:** Issuer, Audience, Lifetime

### Soft Delete
- Users are **never physically deleted**
- `IsDeleted` flag set to `true`
- `Status` changed to `Inactive`
- `DeactivationReason` logged
- Global query filter excludes deleted users

---

## 📊 Data Flow

```
1. User SignUp
   ↓
2. User Login (JWT issued)
   ↓
3. POST /api/transaction (JWT validated → UserId extracted)
   ↓
4. Transaction.Create(userId, amount, currency, merchantId)
   ↓
5. TransactionCreatedDomainEvent(transactionId, userId)
   ↓
6. Saga receives UserId
   ↓
7. FraudCheckRequested(transactionId, userId, ...)
   ↓
8. Fraud analysis with user context
   ↓
9. Admin views details via Support Bot
```

---

## ✅ Implementation Checklist

- [x] User Domain Entity with DDD pattern
- [x] User Repository & EF Configuration
- [x] JWT Token Generator
- [x] Password Hasher (PBKDF2)
- [x] SignUp Command & Handler
- [x] Login Command & Handler
- [x] Deactivate User Command & Handler
- [x] Auth Controller (SignUp, Login, Delete, Me)
- [x] Transaction.Domain UserId property
- [x] Transaction.Create updated with UserId
- [x] TransactionCreatedDomainEvent with UserId
- [x] FraudCheckRequested with UserId
- [x] Saga State UserId tracking
- [x] Transaction API JWT authentication
- [x] Transaction Controller [Authorize] attribute
- [x] Support Bot JWT authentication
- [x] Support Controller [Authorize(Policy="AdminOnly")]
- [x] Database indexes for user queries
- [x] Soft delete implementation
- [x] Configuration files updated

---

## 🔧 Next Steps

1. **Run Migrations** (See Migration Steps above)
2. **Update JWT SecretKey** in production
3. **Create Admin User** manually or via script
4. **Test Authentication Flow** with Postman/Swagger
5. **Monitor Logs** for authentication events

---

## 📝 Notes

- **UserId is mandatory** for all new transactions
- **JWT token required** for POST /api/transaction
- **Admin role required** for all Support Bot endpoints
- **Passwords are hashed** - never stored in plain text
- **Users are soft deleted** - data preserved for audit
- **Correlation IDs** still track end-to-end flow

---

**Implementation completed successfully! 🎉**

Run migrations and test the authentication flow.
