# PCF Auditing Suite

Petty Cash Fund auditing and control system for teams that need traceable receipt uploads, branch verification, manager approval, and live petty-cash balance monitoring.

The application is built with ASP.NET Core MVC and MySQL. Buyers submit receipt images, OCR extracts the expense details, branch staff confirms delivered items, and managers or owners approve, reject, fund, surrender, and report on petty-cash activity from role-safe dashboards.

---

## What the system does

- Upload and review receipt images before submitting an audit.
- Extract receipt details with Google Gemini OCR.
- Route submitted audits through branch verification and manager or owner approval.
- Track petty-cash balances, funding updates, deductions, and surrender requests.
- Keep receipt files protected behind authorization-checked application routes.
- Provide dashboards and reports for pending work, audit history, cash movement, and accountability.
- Seed default roles for local testing and demonstration.

---

## User roles

| Role | Main responsibilities |
| --- | --- |
| Owner | Full system oversight, user management, approval, reporting, and cash control. |
| Manager | Reviews assigned buyer activity, approves audits, manages staff workflow, and monitors balances. |
| Buyer | Uploads receipts, reviews extracted details, submits audits, and requests cash surrender. |
| Branch Staff | Confirms that submitted receipt items were physically received at the branch. |
| Admin | Supports account and system administration tasks. |

---

## Tech stack

- ASP.NET Core MVC on .NET 9
- Entity Framework Core 9
- Pomelo MySQL provider
- MySQL / MariaDB through XAMPP or another local server
- Google Gemini OCR service
- Azure Document Intelligence service implementation included
- BCrypt password hashing
- xUnit test project with SQLite and InMemory EF Core contexts
- Tailwind-style utility classes in Razor views

---

## Requirements

Install these before running the project:

1. [.NET 9 SDK](https://dotnet.microsoft.com/download)
2. [XAMPP](https://www.apachefriends.org/) or any MySQL-compatible server
3. Git
4. A Google Gemini API key for real OCR extraction

---

## Local setup

Clone the repository:

```bash
git clone https://github.com/Yanniii15/AUDIT_SYSTEM.git
cd AUDIT_SYSTEM
```

Restore packages:

```bash
dotnet restore
```

Create the MySQL database:

```sql
CREATE DATABASE audit_ckr_dayo;
```

Update `AuditCkDayo/appsettings.json` if your local MySQL credentials are different:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=audit_ckr_dayo;user=root;password="
  },
  "GoogleGemini": {
    "ApiKey": ""
  }
}
```

For safer local configuration, store the Gemini key in user secrets:

```bash
cd AuditCkDayo
dotnet user-secrets set "GoogleGemini:ApiKey" "YOUR_GEMINI_API_KEY"
cd ..
```

You can also use an environment variable:

```bash
GoogleGemini__ApiKey=YOUR_GEMINI_API_KEY
```

---

## Database migration and seed data

The application applies Entity Framework migrations and seeds default demo accounts during startup.

Default local accounts:

| Role | Email | Password |
| --- | --- | --- |
| Owner | `alice@test.com` | `Password123!` |
| Manager | `bob@test.com` | `Password123!` |
| Buyer | `charlie@test.com` | `Password123!` |
| Branch Staff | `staff@test.com` | `Password123!` |

Start MySQL before launching the application.

---

## Run the application

From the repository root:

```bash
dotnet run --project AuditCkDayo/AuditCkDayo.csproj
```

Open the URL printed by the terminal. Common local URLs are:

- `https://localhost:5001`
- `http://localhost:5000`

If the browser shows a certificate warning on HTTPS, trust the local .NET development certificate:

```bash
dotnet dev-certs https --trust
```

---

## Run tests

```bash
dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --configuration Release
```

The test project validates controller flows, model behavior, view rendering expectations, OCR integration behavior, and ledger/session logic using isolated test databases.

---

## Project structure

```text
AuditCkDayo/
  Controllers/        MVC controllers for authentication, audits, users, reports, and establishments
  Data/               Entity Framework DbContext, migrations, and seed data
  Models/             Domain models for users, audits, receipts, ledger entries, and notifications
  Services/           OCR service contracts and implementations
  ViewModels/         Razor form and page models
  Views/              Razor pages for dashboards, workflows, account screens, and reports
  wwwroot/            Static assets

AuditCkDayo.Tests/    xUnit test project
```

---

## Important notes

- Do not commit real API keys, database passwords, user secrets, or uploaded receipt files.
- Receipt uploads are financial documents and should remain protected by application authorization.
- Presentation files (`*.pptx`) and rendered presentation screenshots are intentionally ignored by Git.
- Run the test suite before pushing changes that affect workflow, data, authentication, or reporting behavior.

---

## License

This project is provided for PCF auditing and management use. Add a license file if the project will be distributed publicly.
