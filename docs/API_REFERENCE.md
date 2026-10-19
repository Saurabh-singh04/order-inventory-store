# API reference

Every endpoint shown in Swagger, with the JSON to send and **every response it can return**.
All responses below are real – captured from a run of `postman/Store.postman_collection.json` against the
local API with the seeded data (tokens are shortened, long lists are cut to two items).

- Base URL: `http://localhost:5080` (Swagger: `/swagger`)
- Protected endpoints need `Authorization: Bearer <accessToken>` (from login/register/refresh).
- Every error is RFC 7807 **ProblemDetails** (`application/problem+json`) with `status`, `title`, `detail`,
  `traceId`, plus `code` on 409s (`out_of_stock`, `concurrency`, `email_taken`, `sku_taken`) and `errors` on
  validation failures.
- Seeded logins: `admin@store.local` / `Admin@123`, `aarav@store.local`, `priya@store.local`,
  `rohan@store.local` / `Customer@123`.

## Contents

- **Auth**
  - [POST /api/auth/register](#post-apiauthregister)
  - [POST /api/auth/login](#post-apiauthlogin)
  - [POST /api/auth/refresh](#post-apiauthrefresh)
  - [POST /api/auth/logout](#post-apiauthlogout)
- **Orders**
  - [POST /api/orders](#post-apiorders)
  - [GET /api/orders](#get-apiorders)
  - [GET /api/orders/my](#get-apiordersmy)
  - [GET /api/orders/{id}](#get-apiordersid)
  - [PATCH /api/orders/{id}/status](#patch-apiordersidstatus)
- **Products**
  - [GET /api/products](#get-apiproducts)
  - [GET /api/products/{id}](#get-apiproductsid)
  - [POST /api/products](#post-apiproducts)
  - [PUT /api/products/{id}](#put-apiproductsid)
- **Reports**
  - [GET /api/reports/top-customers](#get-apireportstop-customers)
- **Health**
  - [GET /health](#get-health)

## Common to every protected endpoint

Calling an endpoint marked Customer / Admin / Signed in without a valid token returns:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/orders/my",
  "traceId": "0HNPDL3AT80M6:0000000C"
}
```

An expired token returns the same body with `"detail": "Access token has expired."` and the
response header `X-Token-Expired: true` (the Angular interceptor uses it to refresh and retry once).
A valid token with the wrong role returns 403 with `"detail": "You do not have permission to access this resource."`.

---

## Auth

### POST /api/auth/register

Creates a **Customer** account and signs it in (returns tokens straight away).

**Access:** Anonymous

**Request body**

```json
{
  "fullName": "Postman Tester",
  "email": "tester.1792390833652@store.local",
  "password": "Tester@123"
}
```

**Responses**

#### 201 Created – Register new customer

Sent: `POST /api/auth/register`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-10-19T06:35:34.3876698Z",
  "refreshToken": "TVTIcvH8ztSj4BsfQoqxdMtcum2e_4PiwlDS...",
  "refreshTokenExpiresAt": "2026-10-26T06:20:34.3876668Z",
  "user": {
    "id": 1010,
    "fullName": "Postman Tester",
    "email": "tester.1792390833652@store.local",
    "role": "Customer"
  }
}
```

#### 400 Bad Request – Register invalid body -> 400

Sent: `POST /api/auth/register`

```json
{
  "fullName": "X",
  "email": "not-an-email",
  "password": "123"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": [
      "The Email field is not a valid e-mail address."
    ],
    "FullName": [
      "The field FullName must be a string with a minimum length of 2 and a maximum length of 100."
    ],
    "Password": [
      "The field Password must be a string with a minimum length of 8 and a maximum length of 100."
    ]
  },
  "traceId": "0HNPDL3AT80M6:00000004"
}
```

#### 409 Conflict – Register duplicate email -> 409

Sent: `POST /api/auth/register`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "An account with this email already exists.",
  "instance": "/api/auth/register",
  "code": "email_taken",
  "traceId": "0HNPDL3AT80M6:00000003"
}
```

### POST /api/auth/login

Returns a 15-minute JWT access token and a 7-day refresh token.

**Access:** Anonymous – max 5 requests / minute / IP

**Request body**

```json
{
  "email": "admin@store.local",
  "password": "Admin@123"
}
```

**Responses**

#### 200 OK – Login admin

Sent: `POST /api/auth/login`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-10-19T06:35:35.1860196Z",
  "refreshToken": "Mw4yoBkZ1I2e6G4oLtBx4eGax9QX9o3nISG4...",
  "refreshTokenExpiresAt": "2026-10-26T06:20:35.1860172Z",
  "user": {
    "id": 1,
    "fullName": "Store Admin",
    "email": "admin@store.local",
    "role": "Admin"
  }
}
```

#### 401 Unauthorized – Login wrong password -> 401

Sent: `POST /api/auth/login`

```json
{
  "email": "aarav@store.local",
  "password": "wrong-password"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password.",
  "instance": "/api/auth/login",
  "traceId": "0HNPDL3AT80M6:00000007"
}
```

#### 429 Too Many Requests – Login attempts until 429

Sent: `POST /api/auth/login`

```json
{
  "email": "nobody@store.local",
  "password": "wrong"
}
```

Response header `Retry-After: 60`

```json
{
  "title": "Too many requests",
  "status": 429,
  "detail": "Too many login attempts. Please try again in 60 seconds.",
  "instance": "/api/auth/login",
  "traceId": "0HNPDL3AT80M6:00000041",
  "retryAfterSeconds": 60
}
```

### POST /api/auth/refresh

Swaps a refresh token for a new pair. The old refresh token stops working (rotation). Using an already-rotated token again revokes all of that user's sessions.

**Access:** Anonymous (refresh token in the body)

**Request body**

```json
{
  "refreshToken": "TVTIcvH8ztSj4BsfQoqxdMtcum2e_4PiwlDS..."
}
```

**Responses**

#### 200 OK – Refresh token (rotates)

Sent: `POST /api/auth/refresh`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-10-19T06:35:36.3082076Z",
  "refreshToken": "0CfMBsQXJZ15pSkJh-v_LGvquwMwbhwUuLAh...",
  "refreshTokenExpiresAt": "2026-10-26T06:20:36.3082056Z",
  "user": {
    "id": 1010,
    "fullName": "Postman Tester",
    "email": "tester.1792390833652@store.local",
    "role": "Customer"
  }
}
```

#### 401 Unauthorized – Refresh with already-used token -> 401 (reuse detection)

Sent: `POST /api/auth/refresh`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Refresh token is invalid or expired.",
  "instance": "/api/auth/refresh",
  "traceId": "0HNPDL3AT80M6:00000009"
}
```

### POST /api/auth/logout

Revokes the refresh token. Always 204, even if the token was already revoked.

**Access:** Anonymous (refresh token in the body)

**Request body**

```json
{
  "refreshToken": "0CfMBsQXJZ15pSkJh-v_LGvquwMwbhwUuLAh..."
}
```

**Responses**

#### 204 No Content – Logout (revoke refresh token) -> 204

Sent: `POST /api/auth/logout`

_(empty body)_

---

## Orders

### POST /api/orders

Places an order. Stock for all items is reserved in one transaction (all or nothing). Prices and discounts are calculated on the server.

**Access:** Customer

Header **`Idempotency-Key: <any unique string, e.g. a GUID>`** (recommended; the Angular app always sends it). Same key + same body again → the original order (200). Same key + different body → 422. Without the header the order is placed normally, with no duplicate protection.

**Request body**

```json
{
  "items": [
    {
      "productId": 1012,
      "quantity": 2
    }
  ]
}
```

**Rule 2 – two customers buy the last unit at the same moment** (two parallel requests with
different keys, captured with curl; the same race is asserted by the integration test):

Winner → `201 Created`

```json
{
  "id": 12,
  "customerId": 1008,
  "customerName": "Race a",
  "status": "Pending",
  "subTotal": 999.0,
  "discount": 0.0,
  "total": 999.0,
  "createdAt": "2026-10-19T06:18:51.770185Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1010,
      "sku": "RACE-1036818279",
      "productName": "Race Demo Item",
      "quantity": 1,
      "unitPrice": 999.0,
      "lineTotal": 999.0
    }
  ]
}
```

Loser → `409 Conflict`. Its UPDATE failed the RowVersion check, so nothing was saved; it re-read the stock, found 0 and returned the normal out-of-stock answer. Stock ends at 0, one order exists

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Not enough stock for: Race Demo Item (requested 1, available 0).",
  "instance": "/api/orders",
  "code": "out_of_stock",
  "traceId": "0HNPDL3AT80M4:00000001"
}
```

**Responses**

#### 200 OK – Replay same Idempotency-Key -> same order (200)

Sent: `POST /api/orders` with `Idempotency-Key: d4608108-a5e1-4ba3-bd9d-ca05927d9faa`

Response header `Idempotent-Replayed: true`

```json
{
  "id": 14,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-19T06:20:39.4392562Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1012,
      "sku": "ORD-1792390833652",
      "productName": "Order Test Speaker 1792390833652 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 201 Created – Place order (Customer)

Sent: `POST /api/orders` with `Idempotency-Key: d4608108-a5e1-4ba3-bd9d-ca05927d9faa`

Response header `Location: http://localhost:5080/api/orders/14`

```json
{
  "id": 14,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-19T06:20:39.4392562Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1012,
      "sku": "ORD-1792390833652",
      "productName": "Order Test Speaker 1792390833652 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 400 Bad Request – Unknown product -> 400

Sent: `POST /api/orders` with `Idempotency-Key: db969504-c064-4673-9d69-f89e58842e0e`

```json
{
  "items": [
    {
      "productId": 999999,
      "quantity": 1
    }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "These products do not exist or are not available: 999999.",
  "instance": "/api/orders",
  "traceId": "0HNPDL3AT80M6:00000022"
}
```

#### 403 Forbidden – Admin cannot place orders -> 403

Sent: `POST /api/orders` with `Idempotency-Key: d3d1cfdc-3bd3-4b3d-8da6-7bfef5beb8fa`

```json
{
  "items": [
    {
      "productId": 1012,
      "quantity": 1
    }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/orders",
  "traceId": "0HNPDL3AT80M6:00000024"
}
```

#### 409 Conflict – Not enough stock on one line -> 409, nothing reserved

Sent: `POST /api/orders` with `Idempotency-Key: 5f20bfa6-a970-479d-b7b8-f64f80643e0f`

```json
{
  "items": [
    {
      "productId": 1012,
      "quantity": 1
    },
    {
      "productId": 1011,
      "quantity": 2
    }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Not enough stock for: Last Unit Gadget 1792390833652 (requested 2, available 1).",
  "instance": "/api/orders",
  "code": "out_of_stock",
  "traceId": "0HNPDL3AT80M6:00000020"
}
```

#### 422 Unprocessable Entity – Same key, different body -> 422

Sent: `POST /api/orders` with `Idempotency-Key: d4608108-a5e1-4ba3-bd9d-ca05927d9faa`

```json
{
  "items": [
    {
      "productId": 1012,
      "quantity": 5
    }
  ]
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Idempotency key reused",
  "status": 422,
  "detail": "This Idempotency-Key was already used for a different order request.",
  "instance": "/api/orders",
  "traceId": "0HNPDL3AT80M6:0000001E"
}
```

### GET /api/orders

All orders, newest first. Query: `status` (Pending | Confirmed | Shipped | Cancelled), `page`, `pageSize`.

**Access:** Admin

**Responses**

#### 200 OK – All orders filtered by status (Admin)

Sent: `GET /api/orders?status=Pending&page=1&pageSize=20`

```json
{
  "items": [
    {
      "id": 14,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 600.0,
      "total": 11400.0,
      "createdAt": "2026-10-19T06:20:39.4392562Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1012,
          "sku": "ORD-1792390833652",
          "productName": "Order Test Speaker 1792390833652 (v2)",
          "quantity": 2,
          "unitPrice": 6000.0,
          "lineTotal": 12000.0
        }
      ]
    },
    {
      "id": 12,
      "customerId": 1008,
      "customerName": "Race a",
      "status": "Pending",
      "subTotal": 999.0,
      "discount": 0.0,
      "total": 999.0,
      "createdAt": "2026-10-19T06:18:51.770185Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1010,
          "sku": "RACE-1036818279",
          "productName": "Race Demo Item",
          "quantity": 1,
          "unitPrice": 999.0,
          "lineTotal": 999.0
        }
      ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2,
  "totalPages": 1
}
```

#### 403 Forbidden – All orders as Customer -> 403

Sent: `GET /api/orders`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/orders",
  "traceId": "0HNPDL3AT80M6:0000002B"
}
```

### GET /api/orders/my

The signed-in customer's orders, newest first. Query: `page`, `pageSize` (max 100).

**Access:** Signed in

**Responses**

#### 200 OK – My orders (paged)

Sent: `GET /api/orders/my?page=1&pageSize=10`

```json
{
  "items": [
    {
      "id": 14,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 600.0,
      "total": 11400.0,
      "createdAt": "2026-10-19T06:20:39.4392562Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1012,
          "sku": "ORD-1792390833652",
          "productName": "Order Test Speaker 1792390833652 (v2)",
          "quantity": 2,
          "unitPrice": 6000.0,
          "lineTotal": 12000.0
        }
      ]
    },
    {
      "id": 9,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Cancelled",
      "subTotal": 18000.0,
      "discount": 900.0,
      "total": 17100.0,
      "createdAt": "2026-10-18T12:18:14.1546971Z",
      "updatedAt": "2026-10-18T12:18:14.4819688Z",
      "items": [
        {
          "productId": 1008,
          "sku": "ORD-1792325885365",
          "productName": "Order Test Speaker 1792325885365 (v2)",
          "quantity": 3,
          "unitPrice": 6000.0,
          "lineTotal": 18000.0
        }
      ]
    },
    "... 8 more item(s) on this page"
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 10,
  "totalPages": 1
}
```

#### 401 Unauthorized – No token -> 401

Sent: `GET /api/orders/my`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/orders/my",
  "traceId": "0HNPDL3AT80M6:0000000C"
}
```

### GET /api/orders/{id}

One order with its lines.

**Access:** Owner of the order or Admin

**Responses**

#### 200 OK – Get order as owner

Sent: `GET /api/orders/14`

```json
{
  "id": 14,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-19T06:20:39.4392562Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1012,
      "sku": "ORD-1792390833652",
      "productName": "Order Test Speaker 1792390833652 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 403 Forbidden – Get order as another customer -> 403

Sent: `GET /api/orders/14`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You can only view your own orders.",
  "instance": "/api/orders/14",
  "traceId": "0HNPDL3AT80M6:00000027"
}
```

#### 404 Not Found – Get unknown order -> 404

Sent: `GET /api/orders/999999`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Order '999999' was not found.",
  "instance": "/api/orders/999999",
  "traceId": "0HNPDL3AT80M6:00000029"
}
```

### PATCH /api/orders/{id}/status

Allowed: Pending → Confirmed → Shipped, and Pending/Confirmed → Cancelled. Shipped and Cancelled are final. Cancelling puts the stock back.

**Access:** Admin: any valid transition. Customer: only cancel their own Pending order

**Request body**

```json
{
  "status": "Confirmed"
}
```

**Responses**

#### 200 OK – Admin: Pending -> Confirmed

Sent: `PATCH /api/orders/14/status`

```json
{
  "id": 14,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Confirmed",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-19T06:20:39.4392562Z",
  "updatedAt": "2026-10-19T06:20:42.5587283Z",
  "items": [
    {
      "productId": 1012,
      "sku": "ORD-1792390833652",
      "productName": "Order Test Speaker 1792390833652 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 400 Bad Request – Pending -> Shipped (skip) -> 400

Sent: `PATCH /api/orders/14/status`

```json
{
  "status": "Shipped"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Business rule violated",
  "status": 400,
  "detail": "Cannot change order status from Pending to Shipped. Allowed: Pending -> Confirmed -> Shipped, or Pending/Confirmed -> Cancelled.",
  "instance": "/api/orders/14/status",
  "traceId": "0HNPDL3AT80M6:0000002C"
}
```

#### 403 Forbidden – Customer tries to confirm -> 403

Sent: `PATCH /api/orders/14/status`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Customers can only cancel orders.",
  "instance": "/api/orders/14/status",
  "traceId": "0HNPDL3AT80M6:0000002D"
}
```

#### 404 Not Found – Unknown order -> 404

Sent: `PATCH /api/orders/999999/status`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Order '999999' was not found.",
  "instance": "/api/orders/999999/status",
  "traceId": "0HNPDL3AT80M6:00000039"
}
```

---

## Products

### GET /api/products

Paged, filtered and sorted in SQL. Query: `search` (name or SKU), `minPrice`, `maxPrice`, `inStock` (true/false), `sortBy` (name | price | stock | newest), `sortDir` (asc | desc), `page`, `pageSize` (max 100). Cached; the cache is cleared when a product is created/updated or stock changes.

**Access:** Anonymous (Admin may add `includeInactive=true`)

**Responses**

#### 200 OK – List products (default)

Sent: `GET /api/products`

```json
{
  "items": [
    {
      "id": 11,
      "sku": "ACC-4004",
      "name": "1TB Portable SSD",
      "price": 7499.0,
      "stockQuantity": 18,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z",
      "rowVersion": "AAAAAAAAB9s="
    },
    {
      "id": 14,
      "sku": "TAB-7001",
      "name": "Apple iPad 10th Gen",
      "price": 34900.0,
      "stockQuantity": 1,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z",
      "rowVersion": "AAAAAAAAB94="
    },
    "... 10 more item(s) on this page"
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 25,
  "totalPages": 3
}
```

#### 400 Bad Request – minPrice > maxPrice -> 400

Sent: `GET /api/products?minPrice=500&maxPrice=100`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "minPrice cannot be greater than maxPrice.",
  "instance": "/api/products",
  "traceId": "0HNPDL3AT80M6:00000011"
}
```

### GET /api/products/{id}

One product. `rowVersion` is needed for the update call.

**Access:** Anonymous

**Responses**

#### 200 OK – Get product by id

Sent: `GET /api/products/1`

```json
{
  "id": 1,
  "sku": "LAP-1001",
  "name": "Lenovo IdeaPad Slim 5 Laptop",
  "price": 58999.0,
  "stockQuantity": 11,
  "isActive": true,
  "createdAt": "2026-01-01T00:00:00Z",
  "rowVersion": "AAAAAAAAB+E="
}
```

#### 404 Not Found – Get unknown product -> 404

Sent: `GET /api/products/999999`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Product '999999' was not found.",
  "instance": "/api/products/999999",
  "traceId": "0HNPDL3AT80M6:00000013"
}
```

### POST /api/products

Creates a product. SKU is stored upper-case and must be unique.

**Access:** Admin

**Request body**

```json
{
  "sku": "LAST-1792390833652",
  "name": "Last Unit Gadget 1792390833652",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true
}
```

**Responses**

#### 201 Created – Create product (Admin) - stock 1 for the out-of-stock test

Sent: `POST /api/products`

Response header `Location: http://localhost:5080/api/products/1011`

```json
{
  "id": 1011,
  "sku": "LAST-1792390833652",
  "name": "Last Unit Gadget 1792390833652",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true,
  "createdAt": "2026-10-19T06:20:38.2461422Z",
  "rowVersion": "AAAAAAAAJ1Q="
}
```

#### 400 Bad Request – Create invalid product -> 400

Sent: `POST /api/products`

```json
{
  "sku": "x",
  "name": "",
  "price": 0,
  "stockQuantity": -1,
  "isActive": true
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Sku": [
      "The field Sku must be a string with a minimum length of 3 and a maximum length of 32."
    ],
    "Name": [
      "The Name field is required.",
      "The field Name must be a string with a minimum length of 2 and a maximum length of 200."
    ],
    "Price": [
      "The field Price must be between 0.01 and 10000000."
    ],
    "StockQuantity": [
      "The field StockQuantity must be between 0 and 1000000."
    ]
  },
  "traceId": "0HNPDL3AT80M6:00000017"
}
```

#### 401 Unauthorized – Create product anonymous -> 401

Sent: `POST /api/products`

```json
{
  "sku": "NOPE-2",
  "name": "Nope",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/products",
  "traceId": "0HNPDL3AT80M6:00000019"
}
```

#### 403 Forbidden – Create product as Customer -> 403

Sent: `POST /api/products`

```json
{
  "sku": "NOPE-1",
  "name": "Nope",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/products",
  "traceId": "0HNPDL3AT80M6:00000018"
}
```

#### 409 Conflict – Create duplicate SKU -> 409

Sent: `POST /api/products`

```json
{
  "sku": "ORD-1792390833652",
  "name": "Duplicate",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "SKU 'ORD-1792390833652' already exists.",
  "instance": "/api/products",
  "code": "sku_taken",
  "traceId": "0HNPDL3AT80M6:00000016"
}
```

### PUT /api/products/{id}

Updates a product. Send the `rowVersion` you got from GET – if the product changed since (another admin or an order changing stock) you get 409 and must reload.

**Access:** Admin

**Request body**

```json
{
  "sku": "ORD-1792390833652",
  "name": "Order Test Speaker 1792390833652 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "rowVersion": "AAAAAAAAJ1U="
}
```

**Responses**

#### 200 OK – Update product (Admin, with rowVersion)

Sent: `PUT /api/products/1012`

```json
{
  "id": 1012,
  "sku": "ORD-1792390833652",
  "name": "Order Test Speaker 1792390833652 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "createdAt": "2026-10-19T06:20:38.3667997Z",
  "rowVersion": "AAAAAAAAJ1Y="
}
```

#### 409 Conflict – Update with stale rowVersion -> 409

Sent: `PUT /api/products/1012`

```json
{
  "sku": "ORD-1792390833652",
  "name": "Lost update",
  "price": 1,
  "stockQuantity": 999,
  "isActive": true,
  "rowVersion": "AAAAAAAAJ1U="
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "This product was changed by someone else (or by a new order). Reload it and try again.",
  "instance": "/api/products/1012",
  "code": "concurrency",
  "traceId": "0HNPDL3AT80M6:0000001B"
}
```

---

## Reports

### GET /api/reports/top-customers

Top customers by spend on Confirmed + Shipped orders. Query: `days` (1–365, default 30), `top` (default 5). Runs as one SQL statement.

**Access:** Admin

**Responses**

#### 200 OK – Top customers, last 30 days (Admin)

Sent: `GET /api/reports/top-customers?days=30`

```json
[
  {
    "customerId": 2,
    "fullName": "Aarav Sharma",
    "email": "aarav@store.local",
    "orderCount": 6,
    "totalSpend": 114697.15
  }
]
```

#### 400 Bad Request – days=0 -> 400

Sent: `GET /api/reports/top-customers?days=0`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "days must be between 1 and 365.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPDL3AT80M6:0000003C"
}
```

#### 401 Unauthorized – Anonymous -> 401

Sent: `GET /api/reports/top-customers?days=30`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPDL3AT80M6:0000003E"
}
```

#### 403 Forbidden – As Customer -> 403

Sent: `GET /api/reports/top-customers?days=30`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPDL3AT80M6:0000003D"
}
```

---

## Health

### GET /health

API + database health check. Plain text `Healthy` / `Unhealthy`.

**Access:** Anonymous

**Responses**

#### 200 OK – Health (API + database)

Sent: `GET /health`

```text
Healthy
```
