# Heka API

Multi-vendor e-commerce marketplace API built with .NET 8 Clean Architecture, serving the Egyptian market with bilingual (English/Arabic) support.

## Tech Stack

- **.NET 8** — ASP.NET Core Web API
- **SQL Server** + Entity Framework Core
- **JWT** authentication with refresh tokens
- **Google OAuth** & email OTP verification
- **Paymob** payment gateway integration
- **Firebase Cloud Messaging** push notifications
- **Serilog** structured logging
- **Swagger/OpenAPI** documentation
- **Prometheus** metrics

## Project Structure

```
Graduation.API/     Presentation layer — controllers, middleware, hosted services
Graduation.BLL/     Business logic layer — services, background jobs, Paymob integration
Graduation.DAL/     Data access layer — DbContext, entities, migrations
Shared/             Shared utilities — code generator, background task queue
```

## Prerequisites

- .NET 8 SDK
- SQL Server instance
- (Optional) Paymob account for payment integration
- (Optional) Firebase project for push notifications

## Setup

1. **Clone and configure connection string:**

```json
// appsettings.json or user-secrets
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=HekaDb;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "JWTSettings": {
    "securityKey": "your-256-bit-key-here-at-least-32-chars",
    "validIssuer": "HekaAPI",
    "validAudience": "HekaApp"
  }
}
```

2. **Run the application:**

```bash
dotnet run --project Graduation.API
```

Migrations apply automatically on startup. An admin user and default roles are seeded on first run:

| Role     | Email                     | Password   |
|----------|---------------------------|------------|
| Admin    | admin@graduationapp.com   | Admin@123  |

## API Overview

~140 endpoints organized across controllers:

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
| Product Variants  | `/api/products/{id}/variants` | Mixed | Variant management         |
| Reviews           | `/api/reviews`         | Mixed    | Product reviews & reporting    |
| Vendors           | `/api/vendors`         | Vendor   | Vendor profile & orders        |
| Wishlist          | `/api/wishlist`        | JWT      | Wishlist management            |
| Notifications     | `/api/notifications`   | JWT      | In-app & push notifications    |
| Health            | `/api/health`          | Public   | Health check                   |
| Metrics           | `/metrics`             | Public   | Prometheus metrics             |
| Brands            | `/api/brands`          | Public   | Vendor brands listing          |
| Images            | `/api/images`          | JWT      | Image upload/deletion          |

## Roles

- **Admin** — full system control, reports, vendor approval, user management
- **Vendor** — store profile, product CRUD, order fulfillment, GPS tracking
- **Customer** — browse, cart, wishlist, order placement, reviews

## Key Features

- Multi-language (English / Arabic) via `Accept-Language` header
- Rate limiting on auth and OTP endpoints
- Email OTP for registration and password reset
- Refresh token rotation with revocation
- Paymob payment gateway (credit card, cash on delivery)
- Real-time order GPS tracking
- Firebase push notifications
- Admin dashboard with 9 report types
- Review moderation system with reporting
- Product variants (size, color, etc.)
- Background job processing with retry & dead-letter
- Prometheus metrics at `/metrics`
