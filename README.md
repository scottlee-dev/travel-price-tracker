# Cancun Resort Price Tracker

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)
![Playwright](https://img.shields.io/badge/Playwright-Chromium-green.svg)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16%2B-blue.svg)
![EF Core](https://img.shields.io/badge/EF%20Core-8.0-blueviolet.svg)
![Status](https://img.shields.io/badge/Status-Production%20Ready-success.svg)

An automated, highly resilient web scraping pipeline built to monitor real-time prices for the **Grand Fiesta Americana Coral Beach Cancun Resort**.

This system continuously tracks targeted room prices (Ocean Front Suite Double - 2 Queen) via the Synxis booking engine, logs price history into a PostgreSQL database, and triggers instant email alerts when prices drop below a defined budget threshold ($950).

---

## Getting Started: Local Setup

You can clone and run this application locally on your machine in under 3 minutes.

### Prerequisites
* **.NET 8.0 SDK+**
* **PostgreSQL 16+**
* **Git**

### 1. Database Configuration
Open your PostgreSQL terminal (or pgAdmin/DBeaver) and create an empty database named `CancunTravelDb`:

```sql
CREATE DATABASE "CancunTravelDb";
```

### 2. Configure Application Settings
Update `appsettings.json` in the project root directory:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=CancunTravelDb;Username=postgres;Password=YOUR_DB_PASSWORD"
  },
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "SenderName": "Cancun Price Tracker",
    "SenderEmail": "your_email@gmail.com",
    "SenderPassword": "YOUR_16_DIGIT_APP_PASSWORD",
    "RecipientEmail": "recipient_email@gmail.com"
  }
}
```
*(Note: Use a Google Account "App Password" for `SenderPassword`, not your standard account password.)*

### 3. Clone, Build, and Run

Open your terminal and execute the following commands:

```bash
# Clone the repository
git clone [https://github.com/scottlee-dev/CancunPriceTracker.git](https://github.com/scottlee-dev/CancunPriceTracker.git)

cd CancunPriceTracker

# Apply Entity Framework Core database migrations
dotnet ef database update

# Run the background worker service
dotnet run
```

---

## Core Architecture & Scraping Logic

The scraping pipeline is optimized for stability against heavy Single Page Applications (SPAs):

1. **Deep Link Parameterization:** Directly constructs direct booking URLs with target parameters (`adult=3`, `arrive=2027-03-22`, `depart=2027-03-26`, `hotel=56627`), bypassing front-page UI date pickers and initial searches.
2. **Text-First & Regex Extraction:** Bypasses fragile DOM traversal by fetching `body.innerText` directly once rendered. It isolates the target room heading (`Ocean Front Suite Double (2 Queen)`) using `Substring` slicing and executes a non-greedy regular expression (`[\s\S]*?\$([0-9,]+)`) to accurately capture the rate for `I Prefer Member Rate`.
3. **Threshold Alerting & Rate-Limit Defense:** Operates on a 4-hour polling interval inside a .NET `BackgroundService` to prevent IP bans. Automated SMTP alerts are dispatched only when the scraped rate falls below the `$950.00` target threshold.

---

## Engineering Highlights & Architectural Decisions

### 1. Text-First Regex Parsing over Brittle DOM Traversal
* **Decision:** Replaced nested CSS/XPath locators and `.Filter()` chains with direct raw text extraction (`InnerTextAsync()`) combined with Regex pattern matching.
* **Reason:** Dynamic booking portals (like Synxis) frequently re-render DOM nodes, causing 30-second Playwright `TimeoutException`s. Text-slicing and Regex run near-instantly, rendering the pipeline completely immune to DOM structural changes and hydration delays.

### 2. Scoped Service Management in Hosted Worker
* **Decision:** Injected `IServiceScopeFactory` into `Worker` (`BackgroundService`) to explicitly construct asynchronous scopes (`CreateAsyncScope()`) per polling cycle.
* **Reason:** `DbContext` is a scoped dependency, whereas `BackgroundService` is a singleton. Constructing explicit scopes prevents context reuse issues, eliminates memory leaks, and guarantees proper resource disposal across long-running background tasks.

### 3. Financial Precision & UTC Standardization
* **Decision:** Enforced `HasPrecision(18, 2)` on database entities and strictly assigned `DateTimeKind.Utc` to all check-in/out timestamps before persisting.
* **Reason:** Prevents rounding discrepancies in price logs and avoids timezone shifts between PostgreSQL storage and .NET runtime execution.

---

## Relational Database Schema

| Table | Description | Key Columns |
| --- | --- | --- |
| **`HotelPrices`** | Logs historical price snapshots captured by the background scraper. | `Id`, `HotelName`, `Price`, `CheckInDate`, `CheckOutDate`, `Source`, `ScrapedAt` |

---

## Roadmap (Next Steps)

- [x] **Phase 1-4:** Playwright Scraper Engine & PostgreSQL / EF Core Integration
- [x] **Phase 5:** Automated Gmail SMTP Price Drop Email Alerts
- [ ] **Phase 6:** Docker Containerization & Cloud/Server Deployment (24/7 Uptime)
- [ ] **Phase 7:** Interactive Web UI / Analytics Dashboard for Price Trends (Blazor / Grafana)

---

## Author

**Scott Lee**

* Software Engineer | Automation & Systems Development
* Connect on **[LinkedIn](https://www.linkedin.com/in/scott-lee-dev/)** | **[GitHub](https://github.com/scottlee-dev)**