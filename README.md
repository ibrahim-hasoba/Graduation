# Heka API

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen)](.github/PULL_REQUEST_TEMPLATE.md)

Multi-vendor e-commerce marketplace API built with **.NET 8 Clean Architecture**, serving the Egyptian market with full bilingual (English/Arabic) support.

## Tech Stack

| Category         | Technologies |
|-----------------|--------------|
| **Runtime**     | .NET 8, ASP.NET Core |
| **Database**    | SQL Server + Entity Framework Core |
| **Auth**        | JWT + Refresh Tokens, Google OAuth, Email OTP |
| **Payments**    | Paymob gateway integration |
| **Messaging**   | Firebase Cloud Messaging (push notifications) |
| **Background**  | Hangfire (fire-and-forget, retries, dead-letter) |
| **Logging**     | Serilog (structured, file + console sinks) |
| **API Docs**    | Swagger / OpenAPI |
| **Monitoring**  | Prometheus metrics at `/metrics` |
| **DI / Mapping**| Scrutor (assembly scanning), AutoMapper |

## Project Structure

```
Graduation.API/     ASP.NET Core Web API — controllers, middleware, hosted services
Graduation.BLL/     Business logic — services, DTOs, background jobs, external integrations
Graduation.DAL/     Data access — EF Core DbContext, entities, migrations, repositories
```

Built with **Clean Architecture**: API depends on BLL, BLL depends on DAL. Inversion of control at the infrastructure boundaries.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server instance (local or remote)
- (Optional) [Paymob](https://paymob.com) account for payments
- (Optional) Firebase project for push notifications

## Setup

```bash
# Clone
git clone https://github.com/your-org/heka-api.git
cd heka-api

# Configure connection string & JWT secret in appsettings.Development.json
# (see below for required settings)

# Run
dotnet run --project Graduation.API
```

### Required Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=HekaDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "JWTSettings": {
    "securityKey": "your-256-bit-key-at-least-32-chars",
    "validIssuer": "HekaAPI",
    "validAudience": "HekaApp"
  }
}
```

## API Overview

~140 endpoints across 18 controllers:

| Controller        | Base Path              | Auth     | Description                    |
|-------------------|------------------------|----------|--------------------------------|
| Account           | `/api/account`         | Mixed    | Register, login, profile, OTP  |
| Address           | `/api/address`         | JWT      | User address management        |
| Admin             | `/api/admin`           | Admin    | Dashboard, users, reports      |
| Admin Vendors     | `/api/admin/vendors`   | Admin    | Vendor approval & management   |
| Cart              | `/api/cart`            | JWT      | Shopping cart                  |
| Categories        | `/api/categories`      | Public   | Product categories             |
| Orders            | `/api/orders`          | Mixed    | Order lifecycle & tracking     |
| Payments          | `/api/payments`        | Mixed    | Paymob integration & webhook   |
| Products          | `/api/products`        | Mixed    | Product catalog (vendor owned) |
| Product Variants  | `/api/products/{id}/variants` | Mixed | Variant management          |
| Reviews           | `/api/reviews`         | Mixed    | Product reviews & reporting    |
| Vendors           | `/api/vendors`         | Vendor   | Vendor profile & orders        |
| Wishlist          | `/api/wishlist`        | JWT      | Wishlist management            |
| Notifications     | `/api/notifications`   | JWT      | In-app & push notifications    |
| Health            | `/api/health`          | Public   | Health check                   |
| Metrics           | `/metrics`             | Public   | Prometheus metrics             |
| Brands            | `/api/brands`          | Public   | Vendor brands listing          |
| Images            | `/api/images`          | JWT      | Image upload/deletion          |

## Roles & Permissions

- **Admin** — full system control, reports, vendor approval, user management
- **Vendor** — store profile, product CRUD, order fulfillment, GPS tracking
- **Customer** — browse, cart, wishlist, order placement, reviews

## Key Features

- **Multi-language** — English / Arabic via `Accept-Language` header
- **Rate limiting** — on auth and OTP endpoints
- **Email OTP** — registration and password reset verification
- **Refresh token rotation** — with revocation on reuse
- **Paymob payments** — credit card + cash on delivery
- **Real-time GPS tracking** — order delivery location updates
- **Push notifications** — Firebase Cloud Messaging
- **Admin dashboard** — 9 report types with chart data
- **Review moderation** — reporting system for inappropriate content
- **Product variants** — sizes, colors, and other options
- **Background jobs** — Hangfire with retry and dead-letter queue
- **Prometheus** — app metrics at `/metrics`

## Contributing

PRs are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

Licensed under the [MIT License](LICENSE).
