# Cancun Resort Price Tracker

A fully automated, resilient price‑tracking pipeline for the  
**Grand Fiesta Americana Coral Beach Cancun Resort**.

This system monitors real‑time room rates, logs historical prices, and sends Gmail alerts when the price drops below your target threshold.

---

## Live Dashboard
<!-- START_DASHBOARD -->

<!-- END_DASHBOARD -->

---

## Key Features

- Real‑time scraping via Playwright (Synxis booking engine)
- Deep‑link navigation (no UI clicking; instant date/room targeting)
- Regex‑based price extraction for maximum stability
- PostgreSQL logging with EF Core (`HasPrecision(18,2)`)
- Gmail SMTP alerts when price < target
- Automated GitHub Actions runner (daily or scheduled)
- Auto‑generated price trend graph embedded in README

---

## Tech Stack

- .NET 8  
- Playwright (Chromium)  
- PostgreSQL + EF Core  
- System.Net.Mail (Gmail App Password)  
- GitHub Actions (CI Scheduler)

---

## Architecture Overview

### Scraper Engine
- Builds Synxis deep‑link URLs with parameters (`adult`, `arrive`, `depart`, `hotel`, etc.)
- Loads page via Playwright (headless Chromium)
- Extracts raw text using `InnerTextAsync()` for SPA stability
- Locates target room section and parses price via Regex

### Background Worker
- Runs every 4 hours using `.NET BackgroundService`
- Creates scoped DbContext instances via `IServiceScopeFactory`
- Logs price history to PostgreSQL
- Sends Gmail alerts when price < threshold
- Updates README dashboard + price trend graph

### Database Layer
- `HotelPriceLog` entity with:
  - `HotelName`
  - `Price` (precision 18,2)
  - `CheckInDate` / `CheckOutDate`
  - `Source`
  - `CreatedAt` (UTC)
- Indexed timestamps for fast historical queries

---

## Setup

### 1. Clone
```bash
git clone https://github.com/scottlee-dev/CancunPriceTracker.git
cd CancunPriceTracker
```

### 2. Environment Variables
Configure via GitHub Actions or local `.env`:

```
EmailSettings__SenderEmail=your@gmail.com
EmailSettings__SenderPassword=your_app_password
EmailSettings__RecipientEmail=your@gmail.com
ConnectionStrings__DefaultConnection=your_postgres_connection
```

### 3. Apply Migrations
```bash
dotnet ef database update
```

### 4. Run
```bash
dotnet run
```

---

## Database Schema

| Column        | Type              | Notes              |
|---------------|-------------------|--------------------|
| Id            | bigint            | Primary Key        |
| HotelName     | text              | Required           |
| Price         | numeric(18,2)     | Required           |
| CheckInDate   | timestamp         | Required           |
| CheckOutDate  | timestamp         | Required           |
| Source        | text              | Required           |
| CreatedAt     | timestamp (UTC)   | Auto‑generated     |

---

## Roadmap

- [x] Playwright Scraper Engine  
- [x] PostgreSQL Logging  
- [x] Gmail SMTP Alerts  
- [x] README Auto‑Dashboard  
- [ ] Docker Deployment  
- [ ] Web Dashboard (Blazor / Grafana)

---

## Author

**Scott Lee**  
Software Engineer • Automation & Systems Development  
[LinkedIn](https://www.linkedin.com/in/scott-lee-dev/) • [GitHub](https://github.com/scottlee-dev)
