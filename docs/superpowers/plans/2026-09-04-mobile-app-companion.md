# AuditCkDayo Mobile Companion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a native React Native + Expo mobile application (`mobile/`) for Auditor, Manager, and Branch Staff roles that integrates with the live .NET 9 backend on Railway via secure JWT Bearer REST APIs, strictly implementing the `Audit Emerald Clean` design tokens.

**Architecture:** A lightweight REST API layer (`AuditCkDayo.Controllers.Api`) is added to the existing ASP.NET Core 9.0 backend with JWT Bearer authentication, preserving all existing Razor routes. The mobile application in `mobile/` is built on Expo SDK 52 with NativeWind/Tailwind, React Navigation, and Axios, persisting tokens in `expo-secure-store`.

**Tech Stack:**
* **Backend**: ASP.NET Core 9.0, Entity Framework Core (MySQL), Microsoft.AspNetCore.Authentication.JwtBearer, BCrypt.Net-Next, xUnit.
* **Mobile Client**: React Native 0.76+, Expo SDK 52, TypeScript, NativeWind, React Navigation (Bottom Tabs + Native Stack), Axios, Expo SecureStore, Expo ImagePicker.

---

## File Structure Map

### Backend Layer (`AuditCkDayo/`)
* Create: `AuditCkDayo/Services/JwtTokenService.cs` — Generates and validates JWT tokens for mobile logins.
* Create: `AuditCkDayo/Controllers/Api/AuthApiController.cs` — `/api/auth/login` and `/api/auth/me`.
* Create: `AuditCkDayo/Controllers/Api/AuditsApiController.cs` — `/api/audits/by-date`, `/api/audits/{id}`, and `/api/audits/{id}` (PUT).
* Create: `AuditCkDayo/Controllers/Api/ManagerApiController.cs` — `/api/manager/dashboard`, `/api/manager/audits/{id}/approve`, `/api/manager/audits/{id}/reject`, `/api/manager/surrenders/{id}/confirm`.
* Create: `AuditCkDayo/Controllers/Api/BranchApiController.cs` — `/api/branch/deliveries` and `/api/branch/deliveries/{detailId}/verify`.
* Modify: `AuditCkDayo/Program.cs` — Registers JWT Bearer authentication alongside cookies and enables CORS.
* Create: `AuditCkDayo.Tests/ApiControllersTests.cs` — Tests JWT auth and API controller endpoints.

### Mobile Client Layer (`mobile/`)
* Create: `mobile/package.json` — Expo dependencies and scripts.
* Create: `mobile/app.json` — Expo app configuration.
* Create: `mobile/tailwind.config.js` — `Audit Emerald Clean` design tokens.
* Create: `mobile/src/types/index.ts` — TypeScript definitions mirroring backend domain models.
* Create: `mobile/src/services/api.ts` — Axios client with Bearer token interceptor and base URL.
* Create: `mobile/src/context/AuthContext.tsx` — Global auth provider managing user profile and SecureStore tokens.
* Create: `mobile/src/navigation/RootNavigator.tsx` — Role-based router directing to Auditor, Manager, or Branch Staff flows.
* Create: `mobile/src/screens/auth/LoginScreen.tsx` — Mobile login view with role badges.
* Create: `mobile/src/screens/auditor/AuditorEditScreen.tsx` — Date stepper, active date chips, multi-photo gallery, and line-item editor.
* Create: `mobile/src/screens/manager/ManagerDashboardScreen.tsx` — PCF float hero card, cash in/out breakdown, and operational queues.
* Create: `mobile/src/screens/manager/AuditApprovalQueueScreen.tsx` — One-tap Approve / Reject queue.
* Create: `mobile/src/screens/branch/DeliveryVerificationScreen.tsx` — Dock receiving delivery verification list.
* Create: `mobile/src/screens/branch/DailySalesUploadScreen.tsx` — Opening/Closing sales logbook camera capture.

---

## Tasks

### Task 1: Backend JWT Authentication Service & Program.cs Setup

**Files:**
* Create: `AuditCkDayo/Services/JwtTokenService.cs`
* Modify: `AuditCkDayo/Program.cs`
* Test: `AuditCkDayo.Tests/ApiControllersTests.cs`

- [ ] **Step 1: Write failing test for JwtTokenService**

```csharp
// AuditCkDayo.Tests/ApiControllersTests.cs
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using AuditCkDayo.Models;
using AuditCkDayo.Services;

namespace AuditCkDayo.Tests
{
    public class JwtTokenServiceTests
    {
        [Fact]
        public void GenerateToken_ReturnsValidJwtStringWithClaims()
        {
            var key = "AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026";
            var issuer = "AuditCkDayo";
            var service = new JwtTokenService(key, issuer);

            var user = new User
            {
                Id = 11,
                Name = "John Auditor",
                Email = "auditor@ckr.com",
                Role = UserRole.Auditor,
                EstablishmentId = 1
            };

            var token = service.GenerateToken(user);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            Assert.Equal("11", jwt.Subject);
            Assert.Equal("Auditor", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter FullyQualifiedName~JwtTokenServiceTests`
Expected: FAIL with "The type or namespace name 'JwtTokenService' could not be found"

- [ ] **Step 3: Implement JwtTokenService**

```csharp
// AuditCkDayo/Services/JwtTokenService.cs
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuditCkDayo.Models;

namespace AuditCkDayo.Services
{
    public class JwtTokenService
    {
        private readonly string _secretKey;
        private readonly string _issuer;

        public JwtTokenService(string secretKey, string issuer = "AuditCkDayo")
        {
            _secretKey = secretKey;
            _issuer = issuer;
        }

        public string GenerateToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secretKey);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            if (user.EstablishmentId.HasValue)
            {
                claims.Add(new Claim("EstablishmentId", user.EstablishmentId.Value.ToString()));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(30),
                Issuer = _issuer,
                Audience = _issuer,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
```

- [ ] **Step 4: Register JWT in Program.cs**

In `AuditCkDayo/Program.cs`, add JwtBearer authentication and dependency injection:

```csharp
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? "AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "AuditCkDayo";

builder.Services.AddSingleton(new AuditCkDayo.Services.JwtTokenService(jwtKey, jwtIssuer));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    })
    .AddJwtBearer(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowMobile", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
```

And before `app.UseAuthentication()`, add `app.UseCors("AllowMobile");`.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter FullyQualifiedName~JwtTokenServiceTests`
Expected: PASS

- [ ] **Step 6: Commit backend JWT configuration**

```bash
git add AuditCkDayo/Services/JwtTokenService.cs AuditCkDayo/Program.cs AuditCkDayo.Tests/ApiControllersTests.cs
git commit -m "feat(api): add JwtTokenService and configure JWT bearer authentication with CORS"
```

---

### Task 2: Backend REST API Controllers (`AuthApiController`, `AuditsApiController`, `ManagerApiController`, `BranchApiController`)

**Files:**
* Create: `AuditCkDayo/Controllers/Api/AuthApiController.cs`
* Create: `AuditCkDayo/Controllers/Api/AuditsApiController.cs`
* Create: `AuditCkDayo/Controllers/Api/ManagerApiController.cs`
* Create: `AuditCkDayo/Controllers/Api/BranchApiController.cs`
* Test: `AuditCkDayo.Tests/ApiControllersTests.cs`

- [ ] **Step 1: Write failing integration test for API login and audits endpoint**

```csharp
// Append to AuditCkDayo.Tests/ApiControllersTests.cs
[Fact]
public async Task AuthApi_LoginWithValidCredentials_ReturnsJwtToken()
{
    using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
    connection.Open();
    var options = new DbContextOptionsBuilder<AuditDbContext>().UseSqlite(connection).Options;

    using (var db = new AuditDbContext(options))
    {
        db.Database.EnsureCreated();
        db.Users.Add(new User
        {
            Id = 1,
            Name = "Auditor One",
            Email = "auditor@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            Role = UserRole.Auditor
        });
        db.SaveChanges();

        var jwtService = new JwtTokenService("AuditCkDayo_SuperSecret_Jwt_Security_Key_For_Mobile_2026");
        var controller = new AuthApiController(db, jwtService);
        var loginResult = await controller.Login(new LoginRequest { Email = "auditor@test.com", Password = "Password123!" });

        var okResult = Assert.IsType<OkObjectResult>(loginResult);
        dynamic val = okResult.Value!;
        Assert.NotNull(val);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj --filter FullyQualifiedName~AuthApi_LoginWithValidCredentials_ReturnsJwtToken`
Expected: FAIL with "The type or namespace name 'AuthApiController' could not be found"

- [ ] **Step 3: Implement AuthApiController**

```csharp
// AuditCkDayo/Controllers/Api/AuthApiController.cs
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuditCkDayo.Data;
using AuditCkDayo.Models;
using AuditCkDayo.Services;

namespace AuditCkDayo.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly AuditDbContext _context;
        private readonly JwtTokenService _jwt;

        public AuthApiController(AuditDbContext context, JwtTokenService jwt)
        {
            _context = context;
            _jwt = jwt;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Email and password are required." });
            }

            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.Establishment)
                .FirstOrDefaultAsync(u => u.Email == request.Email.Trim() && !u.IsDeleted);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var token = _jwt.GenerateToken(user);
            return Ok(new
            {
                token,
                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    Role = user.Role.ToString(),
                    user.EstablishmentId,
                    BranchName = user.Establishment?.Name
                }
            });
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4: Implement AuditsApiController, ManagerApiController, and BranchApiController**

Create:
* `AuditCkDayo/Controllers/Api/AuditsApiController.cs` exposing `GET /api/audits/by-date`, `GET /api/audits/{id}`, and `PUT /api/audits/{id}` guarded by `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.
* `AuditCkDayo/Controllers/Api/ManagerApiController.cs` exposing `GET /api/manager/dashboard`, `POST /api/manager/audits/{id}/approve`, and `POST /api/manager/audits/{id}/reject`.
* `AuditCkDayo/Controllers/Api/BranchApiController.cs` exposing `GET /api/branch/deliveries` and `POST /api/branch/deliveries/{detailId}/verify`.

- [ ] **Step 5: Run integration tests to verify pass**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: All tests pass.

- [ ] **Step 6: Commit backend API endpoints**

```bash
git add AuditCkDayo/Controllers/Api/ AuditCkDayo.Tests/ApiControllersTests.cs
git commit -m "feat(api): add REST endpoints for mobile Auth, Audits, Manager, and Branch Staff"
```

---

### Task 3: Mobile Project Scaffold & Tailwind Design Tokens Setup

**Files:**
* Create: `mobile/package.json`
* Create: `mobile/app.json`
* Create: `mobile/tsconfig.json`
* Create: `mobile/tailwind.config.js`
* Create: `mobile/global.d.ts`

- [ ] **Step 1: Scaffold package.json with Expo dependencies**

```json
{
  "name": "auditckdayo-mobile",
  "version": "1.0.0",
  "scripts": {
    "start": "expo start",
    "android": "expo start --android",
    "ios": "expo start --ios",
    "web": "expo start --web"
  },
  "dependencies": {
    "@react-navigation/bottom-tabs": "^7.2.0",
    "@react-navigation/native": "^7.0.14",
    "@react-navigation/native-stack": "^7.2.0",
    "axios": "^1.7.9",
    "expo": "~52.0.30",
    "expo-image-picker": "~16.0.5",
    "expo-secure-store": "~14.0.1",
    "expo-status-bar": "~2.0.1",
    "lucide-react-native": "^0.475.0",
    "nativewind": "^4.1.23",
    "react": "18.3.1",
    "react-native": "0.76.7",
    "react-native-safe-area-context": "4.12.0",
    "react-native-screens": "~4.4.0",
    "react-native-svg": "15.8.0",
    "tailwindcss": "^3.4.17"
  },
  "devDependencies": {
    "@babel/core": "^7.25.2",
    "@types/react": "~18.3.12",
    "typescript": "^5.3.3"
  },
  "private": true
}
```

- [ ] **Step 2: Configure tailwind.config.js with Audit Emerald Clean tokens**

```javascript
// mobile/tailwind.config.js
module.exports = {
  content: ["./App.{js,jsx,ts,tsx}", "./src/**/*.{js,jsx,ts,tsx}"],
  presets: [require("nativewind/preset")],
  theme: {
    extend: {
      colors: {
        primary: "#005f37",
        "primary-container": "#0f7a4a",
        "on-primary-container": "#a6ffc6",
        background: "#f1fcf2",
        surface: "#f1fcf2",
        "surface-card": "#ffffff",
        "surface-base": "#F6F8F7",
        "surface-container-low": "#ebf7ed",
        "surface-container": "#e5f1e7",
        "surface-container-high": "#e0ebe1",
        "on-surface": "#141e18",
        "on-surface-variant": "#3f4941",
        "border-hairline": "#DCE5DF",
        "text-secondary": "#61706A",
        "status-success": "#15803D",
        "status-success-bg": "#DCFCE7",
        "status-warning": "#B7791F",
        "status-warning-bg": "#FEF3C7",
        "status-danger": "#DC2626",
        "status-danger-bg": "#FEE2E2",
        "status-info": "#2563EB",
        "status-info-bg": "#DBEAFE"
      }
    }
  },
  plugins: []
};
```

- [ ] **Step 3: Verify TypeScript configuration and module declarations**

Create `mobile/tsconfig.json` extending `expo/tsconfig.base` and `mobile/global.d.ts` declaring `/// <reference types="nativewind/types" />`.

- [ ] **Step 4: Commit mobile project setup**

```bash
git add mobile/package.json mobile/app.json mobile/tailwind.config.js mobile/tsconfig.json mobile/global.d.ts
git commit -m "feat(mobile): initialize Expo React Native app with Audit Emerald Clean design tokens"
```

---

### Task 4: API Client, Secure Storage & Auth Context

**Files:**
* Create: `mobile/src/types/index.ts`
* Create: `mobile/src/services/api.ts`
* Create: `mobile/src/context/AuthContext.tsx`
* Create: `mobile/src/screens/auth/LoginScreen.tsx`

- [ ] **Step 1: Define TypeScript contracts mirroring backend**

```typescript
// mobile/src/types/index.ts
export type UserRole = "Auditor" | "Manager" | "BranchStaff" | "Owner" | "Buyer" | "Admin";

export interface UserProfile {
  id: number;
  name: string;
  email: string;
  role: UserRole;
  establishmentId?: number;
  branchName?: string;
}

export interface AuditItemSummary {
  id: number;
  buyerName: string;
  establishmentName: string;
  amount: number;
  description: string;
  entryDate: string;
  status: string;
  imageUrls: string[];
  detailsCount: number;
}
```

- [ ] **Step 2: Implement Axios API client with SecureStore token injection**

```typescript
// mobile/src/services/api.ts
import axios from "axios";
import * as SecureStore from "expo-secure-store";

export const API_BASE_URL = "https://makbiecompanies.dev";

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
  headers: { "Content-Type": "application/json" }
});

api.interceptors.request.use(async (config) => {
  const token = await SecureStore.getItemAsync("auth_token");
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

- [ ] **Step 3: Implement AuthContext & LoginScreen**

Create:
* `mobile/src/context/AuthContext.tsx` managing `login(email, password)`, `logout()`, `user`, and `isLoading`.
* `mobile/src/screens/auth/LoginScreen.tsx` styled in `Audit Emerald Clean` palette with emerald primary button, email/password inputs, and quick-login chip buttons for Auditor, Manager, and Branch Staff.

- [ ] **Step 4: Commit mobile authentication layer**

```bash
git add mobile/src/types/ mobile/src/services/ mobile/src/context/ mobile/src/screens/auth/
git commit -m "feat(mobile): implement SecureStore token management, Axios API client, and LoginScreen"
```

---

### Task 5: Role-Based Navigation & Dynamic Tab Routing

**Files:**
* Create: `mobile/src/navigation/RootNavigator.tsx`
* Create: `mobile/App.tsx`

- [ ] **Step 1: Implement Dynamic Tab Navigator by Role**

In `mobile/src/navigation/RootNavigator.tsx`:
* When `!user`, render `LoginScreen`.
* When `user.role === 'Auditor'`, render bottom tabs:
  1. `AuditorEditScreen` ("Edit Audits")
  2. `AuditorReportsScreen` ("Reports")
* When `user.role === 'Manager'`, render bottom tabs:
  1. `ManagerDashboardScreen` ("Overview")
  2. `AuditApprovalQueueScreen` ("Approvals")
  3. `SurrenderQueueScreen` ("Surrenders")
* When `user.role === 'BranchStaff'`, render bottom tabs:
  1. `DeliveryVerificationScreen` ("Deliveries")
  2. `DailySalesUploadScreen` ("Daily Sales")

- [ ] **Step 2: Hook RootNavigator into App.tsx**

Wrap `App.tsx` in `SafeAreaProvider` and `AuthProvider`.

- [ ] **Step 3: Commit navigation architecture**

```bash
git add mobile/src/navigation/ mobile/App.tsx
git commit -m "feat(mobile): implement role-based dynamic bottom tab navigation"
```

---

### Task 6: Auditor Flow: Date Stepper, Active Date Chips & Multi-Receipt Editor

**Files:**
* Create: `mobile/src/screens/auditor/AuditorEditScreen.tsx`
* Create: `mobile/src/components/ReceiptGalleryModal.tsx`

- [ ] **Step 1: Build AuditorEditScreen mirroring `/Audits/AuditorEdit`**

Features:
* Header: `Audit Corrections & Receipt Management` with `Auditor Portal` pill badge.
* Date Stepper: `<` and `>` arrow buttons to step through dates seamlessly.
* Active Date Chips: Horizonally scrolling chips (`Aug 27 (2)`, `Aug 28 (4)`, `Sep 02 (3)`) with instant 1-tap switching.
* Metric Bento Cards: Total Audited Amount (`₱...`), Submissions Count, Approved Count, and Pending Count.
* Receipts Cards:
  * ID badge (`#AUD-307`), status pill badge, submitter name, and total amount.
  * Multi-photo thumbnail strip (`Photo 1 of 7`, `Photo 2 of 7`, etc.) with tap to view full resolution.
  * Itemized line-items list displaying Item Name, Source/Vendor, Price, Quantity, Line Total, Allocation (Branch/Cost Center), and `✓ Has Receipt` / `NO RECEIPT` badge.

- [ ] **Step 2: Commit Auditor mobile screen**

```bash
git add mobile/src/screens/auditor/ mobile/src/components/
git commit -m "feat(mobile): build AuditorEditScreen with multi-receipt gallery and date chips"
```

---

### Task 7: Manager Flow: Executive Float Dashboard & One-Tap Approval Queue

**Files:**
* Create: `mobile/src/screens/manager/ManagerDashboardScreen.tsx`
* Create: `mobile/src/screens/manager/AuditApprovalQueueScreen.tsx`

- [ ] **Step 1: Implement ManagerDashboardScreen (matching `pcf_suite_executive_dashboard`)**

Features:
* Current PCF Float Hero Card (`₱42,500.00`) with `Safe Float` badge.
* Movement metrics: Cash In Today (`₱...`) and Cash Out Today (`₱...`).
* Operational Queue Summary:
  * Pending Audit Approvals card with total amount and count.
  * Pending Cash Surrenders card with count.

- [ ] **Step 2: Implement AuditApprovalQueueScreen (matching `pcf_suite_owner_approval_queue`)**

Features:
* Filter chips: All, High Value, Branch-specific.
* Audit inspection cards with receipt image preview, buyer name, branch location, and item count.
* One-tap **Approve** and **Reject** buttons with confirmation sheet.

- [ ] **Step 3: Commit Manager mobile screens**

```bash
git add mobile/src/screens/manager/
git commit -m "feat(mobile): build ManagerDashboardScreen and AuditApprovalQueueScreen"
```

---

### Task 8: Branch Staff Flow: Receiving Dock Verification & Daily Sales Intake

**Files:**
* Create: `mobile/src/screens/branch/DeliveryVerificationScreen.tsx`
* Create: `mobile/src/screens/branch/DailySalesUploadScreen.tsx`

- [ ] **Step 1: Implement DeliveryVerificationScreen (matching `/Audits/BranchVerifyList`)**

Features:
* Filtered automatically to the logged-in staff's assigned `EstablishmentId`.
* Card list of delivered items with quantity, vendor source, item name, and line total.
* Big tap target **Verify Delivery** button to clear items on the receiving dock.

- [ ] **Step 2: Implement DailySalesUploadScreen (matching `/SalesReports/Upload`)**

Features:
* Selection: Opening vs Closing Daily Sales Log Book.
* Business Date and Handover Date pickers.
* Native Camera Capture via `expo-image-picker`:
  * Snap up to 5 photos of the paper cashier logbook.
  * Preview thumbnails with delete/re-take buttons.
* Tap **Upload and Review** to send directly to the manager verification queue.

- [ ] **Step 3: Commit Branch Staff mobile screens**

```bash
git add mobile/src/screens/branch/
git commit -m "feat(mobile): build DeliveryVerificationScreen and DailySalesUploadScreen"
```

---

### Task 9: End-to-End Verification & Production Deployment

**Files:**
* Modify: `README.md` or mobile setup docs.

- [ ] **Step 1: Run full backend test suite**

Run: `dotnet test AuditCkDayo.Tests/AuditCkDayo.Tests.csproj`
Expected: All tests pass.

- [ ] **Step 2: Deploy backend updates to Railway**

Run: `railway up --service AuditCkDayo --environment production --ci -m "feat: deploy mobile REST API endpoints for AuditCkDayo companion app"`
Expected: Deployment SUCCESS and container listening on port 8080.

- [ ] **Step 3: Test mobile web preview locally**

Run: `cd mobile && npm run web`
Verify:
* Login as Auditor (`auditor@test.com`) $\rightarrow$ lands on `AuditorEditScreen`, steps through dates, switches receipt photos.
* Login as Manager (`manager1@test.com`) $\rightarrow$ lands on Executive Dashboard, reviews approval queue.
* Login as Branch Staff (`branch@test.com`) $\rightarrow$ lands on Delivery Verification.

- [ ] **Step 4: Commit and finalize**

```bash
git add .
git commit -m "feat(mobile): complete AuditCkDayo mobile companion application implementation"
```
