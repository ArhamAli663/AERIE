# AERIE — Comprehensive Local Setup & Operational Runbook

**AERIE:** An Intelligent, Multi-Tenant SaaS ERP Ecosystem for End-to-End Poultry Supply Chain Optimization  
**Scope Phase 1:** Farmer Module & Broker Module with Shared Multi-Tenant Relational Core

---

## 1. System Prerequisites

Ensure the following tools are installed on your workstation:

| Prerequisite | Minimum Required Version | Verification Command | Purpose |
| :--- | :--- | :--- | :--- |
| **.NET SDK** | `9.0.100` or higher | `dotnet --version` | ASP.NET Core Web API, Razor Pages Web App, EF Core |
| **PostgreSQL** | `15.0` or higher (16 recommended) | `psql --version` | Shared Multi-Tenant Relational Database |
| **Flutter SDK** | `3.22.0` or higher | `flutter --version` | Farmer Mobile Client with Offline SQLite |
| **Web Browser** | Chrome, Edge, or Firefox | — | Accessing Razor Pages Web Portal & Swagger |

---

## 2. PostgreSQL Database Setup

### Option A: Local PostgreSQL or Docker Container
If using Docker, run a clean PostgreSQL 16 container:
```powershell
docker run --name aerie-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=aerie_db -p 5432:5432 -d postgres:16-alpine
```

### Option B: Local PostgreSQL Service
Create a database named `aerie_db` using `psql` or pgAdmin:
```sql
CREATE DATABASE aerie_db;
```

### Applying the Database Schema
You can initialize the database using either EF Core migrations or the included idempotent DDL script:

**Method 1: Using EF Core Migrations (Recommended)**
From the solution root (`e:\FYP Proect\AERIE`):
```powershell
dotnet ef database update --project src/AERIE.Infrastructure --startup-project src/AERIE.Api
```

**Method 2: Using the pre-generated `schema.sql` script**
```powershell
psql -U postgres -d aerie_db -f schema.sql
```

The schema creates all 28 tables, foreign keys, decimal precisions, and indexes including tenant isolation filters.

---

## 3. Configuration & Connection Strings

Both `src/AERIE.Api/appsettings.json` and `src/AERIE.Web/appsettings.json` are pre-configured to connect to PostgreSQL:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=aerie_db;Username=postgres;Password=postgres"
  },
  "JwtSettings": {
    "Secret": "AERIE_SUPER_SECRET_KEY_FOR_ENTERPRISE_JWT_AUTHENTICATION_MIN_32_CHARS_2026",
    "Issuer": "AeriePoultrySaaS",
    "Audience": "AeriePoultryClients",
    "ExpiryMinutes": 1440
  }
}
```
*Note: If your local PostgreSQL uses a different password or port, update the `DefaultConnection` string in both `appsettings.json` files.*

---

## 4. Running the Solution

### 4.1 Running the Backend Web API (`AERIE.Api`)
Open a terminal in `src/AERIE.Api` and execute:
```powershell
dotnet run
```
- **Web API Gateway URL:** `http://localhost:5000`
- **Interactive Swagger Documentation:** `http://localhost:5000/swagger`
- **SignalR Hub Endpoint:** `http://localhost:5000/hubs/aerie`

### 4.2 Running the Web Application (`AERIE.Web`)
Open a second terminal in `src/AERIE.Web` and execute:
```powershell
dotnet run
```
- **Web Application Portal:** `http://localhost:5001`
- Built with Google Stitch-inspired design tokens, responsive cards, interactive modals, and real-time market data tickers.

### 4.3 Running the Farmer Mobile Client (`AERIE.Farmer.Mobile`)
Open a third terminal in `src/AERIE.Farmer.Mobile`:
```powershell
flutter pub get
flutter run -d chrome
# Or to target Android emulator:
# flutter run -d emulator-5554
```
- Embedded SQLite (`sqflite`) for local observation logging in remote sheds.
- Automatic network connectivity detection with 5-second reconnect sync trigger.
- Idempotent push synchronization with server-wins conflict resolution.

---

## 5. Pre-Seeded Test Credentials & Tenant Profiles

For grading, demonstrations, and automated workflows, the following tenant accounts are pre-configured:

| Role | Tenant Name | Email Account | Password | Accessible Areas |
| :--- | :--- | :--- | :--- | :--- |
| **Farmer** | *Al-Baraka Poultry Farms* | `farmer@albaraka.com` | `Farmer@123456` | Farms, Sheds, Batches, Observations, Listings, Received Bids |
| **Broker** | *Punjab Poultry Traders* | `broker@punjabtraders.com` | `Broker@123456` | Marketplace Bidding, Deal Contracts, Weighbridge, Settlements, Ledger |
| **System Admin**| *AERIE SaaS Platform* | `admin@aerie.io` | `Admin@123456` | Cross-Tenant Oversight & Global Configuration |

---

## 6. Step-by-Step Verification Runbook

Follow this 8-step verification sequence to validate the complete business lifecycle from chick placement to financial settlement:

### Step 1: Inspect Farms & Sheds (Farmer)
1. Open your browser and navigate to `http://localhost:5001/Farmer/Farms`.
2. Observe the pre-registered farms (*Rawalpindi Farm #1*) and climate-controlled sheds (*House A1 — Broiler Tunnel*).
3. Click **"Register New Poultry Shed"** to add a shed with capacity of 10,000 birds.

### Step 2: Manage Flock Batches & Placements
1. Navigate to `http://localhost:5001/Farmer/Batches`.
2. Inspect the active batch `COBB-2026-B1` (5,000 Cobb 500 birds placed).
3. Click **"Record Observation"** on the batch card.
4. Input today's field readings:
   - Mortality: `12` birds
   - Feed Consumed: `480.0` kg
   - Water Consumed: `950.0` Liters
   - Sample Body Weight: `1,920` grams
5. Click **"Save Observation & Compute KPIs"**.
6. Observe that **FCR (Feed Conversion Ratio)**, **ADG (Average Daily Gain)**, **Mortality %**, and **EPEF (European Production Efficiency Factor)** are recalculated instantly via the pure domain calculator.

### Step 3: Transition Flock & Publish to Marketplace
1. On the same batch card, click **"Ready for Sale"**.
2. Click **"Publish to Marketplace"**.
3. Specify:
   - Quantity Offered: `4,850` birds
   - Expected Price: `PKR 380` / kg
   - Available From: Today's date
4. Submit. The listing is now published across tenant boundaries.

### Step 4: Broker Marketplace Discovery & Proposal Submission
1. Navigate to `http://localhost:5001/Broker/Marketplace`.
2. As a broker, browse live published broiler listings across cities (Rawalpindi, Lahore, Faisalabad).
3. Find the flock published in Step 3 and click **"Submit Purchase Proposal"**.
4. Specify:
   - Quantity Requested: `4,850` birds
   - Offered Rate: `PKR 380.00` / kg
   - Vehicle Registration: `LES-8942`
   - Planned Lifting Date: Tomorrow
5. Submit proposal.

### Step 5: Farmer Acceptance & Atomic Reservation
1. Navigate to `http://localhost:5001/Farmer/Listings`.
2. View the incoming proposal from *Punjab Poultry Traders*.
3. Click **"Accept Proposal & Create Contract"**.
4. The system validates quantity availability and atomically reserves the flock, creating a formal trade contract (`Deal`). Competing proposals exceeding capacity are automatically prevented from overselling.

### Step 6: Weighbridge Lifting Ticket Entry
1. Navigate to `http://localhost:5001/Broker/Deals`.
2. Locate the active deal and advance status from `Accepted` to `InTransit`.
3. Click **"Record Lifting Weighbridge"**.
4. Enter official weighbridge scale readings:
   - Gross Vehicle Weight: `18,450.00` kg
   - Tare (Empty Vehicle) Weight: `9,235.00` kg
   - Net Bird Weight: `9,215.00` kg
   - Actual Birds Loaded: `4,850`
   - Average Bird Weight: `1.90` kg
5. Submit ticket. Status updates to `Delivered`.

### Step 7: Commission & Multi-Party Settlement Engine
1. On the delivered deal card, click **"Generate Multi-Party Settlement"**.
2. Review the automated breakdown:
   - Gross Amount: `9,215 kg × PKR 380 = PKR 3,501,700`
   - Broker Commission Deduction: `PKR 3.00/kg = PKR 27,645`
   - Freight / Transport Allowance: `PKR 45,000`
   - Net Farmer Payout: `PKR 3,429,055`
3. Click **"Generate Commercial Invoice"**.
4. Click **"Record Payment Execution"** (Payment method: Bank Transfer).

### Step 8: Double-Entry Bookkeeping Audit Trail
1. Navigate to `http://localhost:5001/Broker/Ledger`.
2. Inspect the immutable audit log:
   - `Debit`: Accounts Receivable / Cash Asset
   - `Credit`: Broiler Sales Revenue
   - `Debit`: Broker Commission Expense / Income
   - `Debit`: Transport & Freight Expense
3. Every rupee is accounted for with balanced debits and credits.

---

## 7. Running the Automated Test Suite

AERIE includes a comprehensive automated test suite covering unit math formulas, tenant security isolation, and the complete end-to-end integration workflow.

Execute from the root directory:
```powershell
dotnet test AERIE.sln --logger "console;verbosity=normal"
```

Expected output:
```text
Passed AERIE.Tests.Unit (11 tests, 0 failed, Duration: ~118 ms)
Passed AERIE.Tests.Integration (6 tests, 0 failed, Duration: ~10 s)
Total: 17 Passed, 0 Failed, 0 Skipped (100% Success)
```
