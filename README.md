# Cancun Resort Price Tracker

An automated, highly resilient web scraping pipeline built to monitor real-time prices for the **Grand Fiesta Americana Coral Beach Cancun Resort**. 

This system continuously tracks targeted room prices (e.g., Ocean Front Suite) and sends an automated email alert the moment the price drops below a defined budget threshold ($950).

## Key Features

- **Deep Link Navigation:** Directly accesses the Synxis booking engine via URL parameters, bypassing complex UI interactions and date picking.
- **Timeout-Proof Scraping (Text-First Parsing):** Bypasses brittle DOM traversal (which causes 30s timeouts on SPA sites) by instantly extracting raw page text and parsing it via highly optimized Regular Expressions (Regex).
- **Persistent Storage:** Logs all price history to a **PostgreSQL** database using **Entity Framework Core** with strict `HasPrecision(18,2)` financial data standards.
- **Automated Email Alerts:** Integrates with Gmail SMTP to push instant notifications straight to your smartphone when the price drops below the target threshold.
- **24/7 Background Worker:** Runs as a headless .NET Background Service, silently polling prices every 4 hours to avoid IP bans.

## Tech Stack

- **Framework:** .NET 8 (C#) / Hosted Service Worker
- **Scraping Engine:** Microsoft Playwright (Headless Chromium)
- **Database:** PostgreSQL
- **ORM:** Entity Framework Core
- **Notification:** System.Net.Mail (SMTP)

## Configuration & Setup

1. **Database Setup:** Ensure PostgreSQL is running locally or remotely.
2. **Configuration File:** Create or update `appsettings.json` in the project root:

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
*(Note: Use Google Account "App Passwords" for `SenderPassword`, not your standard account password.)*

3. **Apply Database Migrations:**
```bash
dotnet ef database update
```

4. **Run the Tracker:**
```bash
dotnet run
```

## Roadmap (Next Steps)

- [x] **Phase 1-4:** Playwright Scraper & DB Integration
- [x] **Phase 5:** Real-time Email Notification System
- [ ] **Phase 6:** Docker Containerization & Cloud/Server Deployment (24/7 Uptime)
- [ ] **Phase 7:** Web UI / Dashboard for Price Trend Visualization (Blazor/Grafana)

## Author
Scott Lee (Built for the ultimate Cancun family vacation)