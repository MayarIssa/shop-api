# Shop API

An e-commerce REST API built with **ASP.NET Core 8**, **Entity Framework Core** and **JWT authentication**. It covers the core of an online store: a product catalog, a shopping cart, checkout and order management, with separate customer and admin permissions.

![CI](https://github.com/MayarIssa/shop-api/actions/workflows/ci.yml/badge.svg)

## Features

- **Auth:** register and log in with email and password. Passwords are hashed with ASP.NET Core Identity's `PasswordHasher`, and every request after login carries a signed JWT.
- **Roles:** customers browse, shop and see only their own orders. Admins manage categories, products and order status.
- **Catalog:** categories and products with full CRUD, plus search, category and price filters, sorting and paging.
- **Cart:** add, update and remove items, with stock checked on every change.
- **Checkout:** turns the cart into an order inside a database transaction. Stock is reduced and the cart emptied together, or not at all.
- **Orders:** order history, cancellation that puts stock back, and admin status updates (Pending → Paid → Shipped → Delivered).
- **Data integrity:** each order line stores the price at checkout, so changing a product's price later doesn't rewrite past orders. A product that has been ordered is deactivated rather than deleted.
- **Errors:** consistent RFC 7807 problem responses and model validation on every request.
- **Docs:** Swagger UI with a JWT "Authorize" button.
- **Tests:** xUnit integration tests that start the real API against an in-memory SQLite database, run on every push with GitHub Actions.

## Tech stack

C# · ASP.NET Core 8 Web API · Entity Framework Core 8 · SQL Server / SQLite · JWT Bearer · Swagger (Swashbuckle) · xUnit · GitHub Actions

## Run it locally

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
cd src/ShopApi
dotnet run
```

Open **http://localhost:5210/swagger**. On first run the app creates a SQLite database (`shop.db`) with sample categories and products, plus an admin account:

| | |
|---|---|
| Admin email | `admin@shop.local` |
| Admin password | `Admin123!` |

These development credentials live in `appsettings.Development.json` and are only used locally.

### Using SQL Server instead

Set the provider and connection string, for example with environment variables:

```bash
Database__Provider=SqlServer
ConnectionStrings__Shop="Server=localhost;Database=Shop;Trusted_Connection=True;TrustServerCertificate=True"
```

### Production settings

`Jwt__Key` (at least 32 characters) is required and should come from a secret store, never from source control. Set `Seed__AdminEmail` and `Seed__AdminPassword` to create the first admin.

## Run the tests

```bash
dotnet test
```

## Endpoints

| Method | Path | Who |
|---|---|---|
| POST | `/api/auth/register` | Anyone |
| POST | `/api/auth/login` | Anyone |
| GET | `/api/auth/me` | Signed in |
| GET | `/api/categories`, `/api/categories/{id}` | Anyone |
| POST, PUT, DELETE | `/api/categories/{id}` | Admin |
| GET | `/api/products?search=&categoryId=&minPrice=&maxPrice=&sortBy=&page=&pageSize=` | Anyone |
| GET | `/api/products/{id}` | Anyone |
| POST, PUT, DELETE | `/api/products/{id}` | Admin |
| GET, DELETE | `/api/cart` | Signed in |
| POST | `/api/cart/items` | Signed in |
| PUT, DELETE | `/api/cart/items/{productId}` | Signed in |
| POST | `/api/orders/checkout` | Signed in |
| GET | `/api/orders`, `/api/orders/{id}` | Owner or admin |
| POST | `/api/orders/{id}/cancel` | Owner or admin |
| GET | `/api/orders/all?status=` | Admin |
| PATCH | `/api/orders/{id}/status` | Admin |

## Project structure

```
src/ShopApi
  Controllers/   HTTP endpoints, one per resource
  Data/          EF Core DbContext and first-run seed data
  Dtos/          Request and response shapes, with validation rules
  Models/        Database entities
  Services/      JWT token creation
tests/ShopApi.Tests
  ShopApiFactory.cs  Starts the API in memory with a private SQLite database
  ApiTests.cs        Auth, catalog, cart and checkout tests
```

## Author

**Mayar Mohamed Issa** · [GitHub](https://github.com/MayarIssa) · [LinkedIn](https://www.linkedin.com/in/mayar-issa/)
