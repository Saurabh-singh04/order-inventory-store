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
  "traceId": "0HNPD2788O5QR:0000000C"
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
  "email": "tester.1792325885365@store.local",
  "password": "Tester@123"
}
```

**Responses**

#### 201 Created – Register new customer

Sent: `POST /api/auth/register`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-10-18T12:33:06.2993484Z",
  "refreshToken": "dQHT0jSKpKKutoiKq_hZ1thVEXoHhrmKxS0p...",
  "refreshTokenExpiresAt": "2026-10-25T12:18:06.2989065Z",
  "user": {
    "id": 1005,
    "fullName": "Postman Tester",
    "email": "tester.1792325885365@store.local",
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
  "traceId": "0HNPD2788O5QR:00000004"
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
  "traceId": "0HNPD2788O5QR:00000003"
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
  "accessTokenExpiresAt": "2026-10-18T12:33:07.0459525Z",
  "refreshToken": "utjvtPIwo2CmTJ45PKv_piC96f0iljZo67p9...",
  "refreshTokenExpiresAt": "2026-10-25T12:18:07.0459083Z",
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
  "traceId": "0HNPD2788O5QR:00000007"
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
  "traceId": "0HNPD2788O5QR:00000041",
  "retryAfterSeconds": 60
}
```

### POST /api/auth/refresh

Swaps a refresh token for a new pair. The old refresh token stops working (rotation). Using an already-rotated token again revokes all of that user's sessions.

**Access:** Anonymous (refresh token in the body)

**Request body**

```json
{
  "refreshToken": "dQHT0jSKpKKutoiKq_hZ1thVEXoHhrmKxS0p..."
}
```

**Responses**

#### 200 OK – Refresh token (rotates)

Sent: `POST /api/auth/refresh`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "accessTokenExpiresAt": "2026-10-18T12:33:07.7506785Z",
  "refreshToken": "EGwo2yvQdnw0HdeMPMTgG1FBB_KwdHt6DTqo...",
  "refreshTokenExpiresAt": "2026-10-25T12:18:07.7506777Z",
  "user": {
    "id": 1005,
    "fullName": "Postman Tester",
    "email": "tester.1792325885365@store.local",
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
  "traceId": "0HNPD2788O5QR:00000009"
}
```

### POST /api/auth/logout

Revokes the refresh token. Always 204, even if the token was already revoked.

**Access:** Anonymous (refresh token in the body)

**Request body**

```json
{
  "refreshToken": "EGwo2yvQdnw0HdeMPMTgG1FBB_KwdHt6DTqo..."
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

Header **`Idempotency-Key: <any unique string, e.g. a GUID>`** is required. Same key + same body again → the original order (200). Same key + different body → 422.

**Request body**

```json
{
  "items": [
    {
      "productId": 1008,
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
  "id": 10,
  "customerId": 1006,
  "customerName": "Race a",
  "status": "Pending",
  "subTotal": 999.0,
  "discount": 0.0,
  "total": 999.0,
  "createdAt": "2026-10-18T12:18:54.7959545Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1009,
      "sku": "RACE-3222825983",
      "productName": "Race Demo Item",
      "quantity": 1,
      "unitPrice": 999.0,
      "lineTotal": 999.0
    }
  ]
}
```

Loser → `409 Conflict` (RowVersion check failed, nothing was reserved; stock ends at 0)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Stock for one or more products changed while your order was being placed and is no longer available.",
  "instance": "/api/orders",
  "code": "out_of_stock",
  "traceId": "0HNPD2788O5R1:00000001"
}
```

**Responses**

#### 200 OK – Replay same Idempotency-Key -> same order (200)

Sent: `POST /api/orders` with `Idempotency-Key: e707f984-18c7-4713-ac4a-4b60162da606`

Response header `Idempotent-Replayed: true`

```json
{
  "id": 8,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 1800.0,
  "total": 10200.0,
  "createdAt": "2026-10-18T12:18:10.7998867Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1008,
      "sku": "ORD-1792325885365",
      "productName": "Order Test Speaker 1792325885365 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 201 Created – Place order (Customer)

Sent: `POST /api/orders` with `Idempotency-Key: e707f984-18c7-4713-ac4a-4b60162da606`

Response header `Location: http://localhost:5080/api/orders/8`

```json
{
  "id": 8,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 1800.0,
  "total": 10200.0,
  "createdAt": "2026-10-18T12:18:10.7998867Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1008,
      "sku": "ORD-1792325885365",
      "productName": "Order Test Speaker 1792325885365 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 400 Bad Request – Place order without Idempotency-Key -> 400

Sent: `POST /api/orders`

```json
{
  "items": [
    {
      "productId": 1008,
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
  "detail": "The Idempotency-Key header is required when placing an order.",
  "instance": "/api/orders",
  "traceId": "0HNPD2788O5QR:0000001C"
}
```

#### 403 Forbidden – Admin cannot place orders -> 403

Sent: `POST /api/orders` with `Idempotency-Key: 41ce8d0e-11a3-4186-9d5c-211f3f87e4c5`

```json
{
  "items": [
    {
      "productId": 1008,
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
  "traceId": "0HNPD2788O5QR:00000025"
}
```

#### 409 Conflict – Not enough stock on one line -> 409, nothing reserved

Sent: `POST /api/orders` with `Idempotency-Key: e8e99f3b-4f43-40f1-afb3-c3f796052cd1`

```json
{
  "items": [
    {
      "productId": 1008,
      "quantity": 1
    },
    {
      "productId": 1007,
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
  "detail": "Not enough stock for: Last Unit Gadget 1792325885365 (requested 2, available 1).",
  "instance": "/api/orders",
  "code": "out_of_stock",
  "traceId": "0HNPD2788O5QR:00000021"
}
```

#### 422 Unprocessable Entity – Same key, different body -> 422

Sent: `POST /api/orders` with `Idempotency-Key: e707f984-18c7-4713-ac4a-4b60162da606`

```json
{
  "items": [
    {
      "productId": 1008,
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
  "traceId": "0HNPD2788O5QR:0000001F"
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
      "id": 8,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 1800.0,
      "total": 10200.0,
      "createdAt": "2026-10-18T12:18:10.7998867Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1008,
          "sku": "ORD-1792325885365",
          "productName": "Order Test Speaker 1792325885365 (v2)",
          "quantity": 2,
          "unitPrice": 6000.0,
          "lineTotal": 12000.0
        }
      ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
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
  "traceId": "0HNPD2788O5QR:0000002C"
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
      "id": 8,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 1800.0,
      "total": 10200.0,
      "createdAt": "2026-10-18T12:18:10.7998867Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1008,
          "sku": "ORD-1792325885365",
          "productName": "Order Test Speaker 1792325885365 (v2)",
          "quantity": 2,
          "unitPrice": 6000.0,
          "lineTotal": 12000.0
        }
      ]
    },
    {
      "id": 7,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Cancelled",
      "subTotal": 18000.0,
      "discount": 2700.0,
      "total": 15300.0,
      "createdAt": "2026-10-18T12:06:46.8649183Z",
      "updatedAt": "2026-10-18T12:06:47.1735453Z",
      "items": [
        {
          "productId": 1006,
          "sku": "ORD-1792325194792",
          "productName": "Order Test Speaker 1792325194792 (v2)",
          "quantity": 3,
          "unitPrice": 6000.0,
          "lineTotal": 18000.0
        }
      ]
    },
    "... 6 more item(s) on this page"
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 8,
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
  "traceId": "0HNPD2788O5QR:0000000C"
}
```

### GET /api/orders/{id}

One order with its lines.

**Access:** Owner of the order or Admin

**Responses**

#### 200 OK – Get order as owner

Sent: `GET /api/orders/8`

```json
{
  "id": 8,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 1800.0,
  "total": 10200.0,
  "createdAt": "2026-10-18T12:18:10.7998867Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1008,
      "sku": "ORD-1792325885365",
      "productName": "Order Test Speaker 1792325885365 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 403 Forbidden – Get order as another customer -> 403

Sent: `GET /api/orders/8`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You can only view your own orders.",
  "instance": "/api/orders/8",
  "traceId": "0HNPD2788O5QR:00000028"
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
  "traceId": "0HNPD2788O5QR:0000002A"
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

Sent: `PATCH /api/orders/8/status`

```json
{
  "id": 8,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Confirmed",
  "subTotal": 12000.0,
  "discount": 1800.0,
  "total": 10200.0,
  "createdAt": "2026-10-18T12:18:10.7998867Z",
  "updatedAt": "2026-10-18T12:18:13.5438837Z",
  "items": [
    {
      "productId": 1008,
      "sku": "ORD-1792325885365",
      "productName": "Order Test Speaker 1792325885365 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

#### 400 Bad Request – Pending -> Shipped (skip) -> 400

Sent: `PATCH /api/orders/8/status`

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
  "instance": "/api/orders/8/status",
  "traceId": "0HNPD2788O5QR:0000002D"
}
```

#### 403 Forbidden – Customer tries to confirm -> 403

Sent: `PATCH /api/orders/8/status`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Customers can only cancel orders.",
  "instance": "/api/orders/8/status",
  "traceId": "0HNPD2788O5QR:0000002E"
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
  "traceId": "0HNPD2788O5QR:0000003A"
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
  "totalCount": 21,
  "totalPages": 2
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
  "traceId": "0HNPD2788O5QR:00000011"
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
  "traceId": "0HNPD2788O5QR:00000013"
}
```

### POST /api/products

Creates a product. SKU is stored upper-case and must be unique.

**Access:** Admin

**Request body**

```json
{
  "sku": "LAST-1792325885365",
  "name": "Last Unit Gadget 1792325885365",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true
}
```

**Responses**

#### 201 Created – Create product (Admin) - stock 1 for the out-of-stock test

Sent: `POST /api/products`

Response header `Location: http://localhost:5080/api/products/1007`

```json
{
  "id": 1007,
  "sku": "LAST-1792325885365",
  "name": "Last Unit Gadget 1792325885365",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true,
  "createdAt": "2026-10-18T12:18:09.546633Z",
  "rowVersion": "AAAAAAAAJzw="
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
  "traceId": "0HNPD2788O5QR:00000017"
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
  "traceId": "0HNPD2788O5QR:00000019"
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
  "traceId": "0HNPD2788O5QR:00000018"
}
```

#### 409 Conflict – Create duplicate SKU -> 409

Sent: `POST /api/products`

```json
{
  "sku": "ORD-1792325885365",
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
  "detail": "SKU 'ORD-1792325885365' already exists.",
  "instance": "/api/products",
  "code": "sku_taken",
  "traceId": "0HNPD2788O5QR:00000016"
}
```

### PUT /api/products/{id}

Updates a product. Send the `rowVersion` you got from GET – if the product changed since (another admin or an order changing stock) you get 409 and must reload.

**Access:** Admin

**Request body**

```json
{
  "sku": "ORD-1792325885365",
  "name": "Order Test Speaker 1792325885365 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "rowVersion": "AAAAAAAAJz0="
}
```

**Responses**

#### 200 OK – Update product (Admin, with rowVersion)

Sent: `PUT /api/products/1008`

```json
{
  "id": 1008,
  "sku": "ORD-1792325885365",
  "name": "Order Test Speaker 1792325885365 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "createdAt": "2026-10-18T12:18:09.7350741Z",
  "rowVersion": "AAAAAAAAJz4="
}
```

#### 409 Conflict – Update with stale rowVersion -> 409

Sent: `PUT /api/products/1008`

```json
{
  "sku": "ORD-1792325885365",
  "name": "Lost update",
  "price": 1,
  "stockQuantity": 999,
  "isActive": true,
  "rowVersion": "AAAAAAAAJz0="
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "This product was changed by someone else (or by a new order). Reload it and try again.",
  "instance": "/api/products/1008",
  "code": "concurrency",
  "traceId": "0HNPD2788O5QR:0000001B"
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
    "orderCount": 5,
    "totalSpend": 103297.15
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
  "traceId": "0HNPD2788O5QR:0000003C"
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
  "traceId": "0HNPD2788O5QR:0000003E"
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
  "traceId": "0HNPD2788O5QR:0000003D"
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
