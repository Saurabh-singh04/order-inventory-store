# API examples

Real requests and responses captured from a Newman run of `Store.postman_collection.json`
against the local API. Long lists are cut to the first two items. Tokens are shortened.

## 0. Health

### Health (API + database)

`GET /health` → **200 OK**

Response:

```text
Healthy
```

## 1. Auth

### Register new customer

`POST /api/auth/register` → **201 Created**

Request:

```json
{
  "fullName": "Postman Tester",
  "email": "tester.1792325194792@store.local",
  "password": "Tester@123"
}
```

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpX...",
  "accessTokenExpiresAt": "2026-10-18T12:21:36.474966Z",
  "refreshToken": "cNq9_kdrm1tD3swV2XhwxowsINm4WSUo...",
  "refreshTokenExpiresAt": "2026-10-25T12:06:36.4733421Z",
  "user": {
    "id": 1004,
    "fullName": "Postman Tester",
    "email": "tester.1792325194792@store.local",
    "role": "Customer"
  }
}
```

### Register duplicate email -> 409

`POST /api/auth/register` → **409 Conflict**

Request:

```json
{
  "fullName": "Postman Tester",
  "email": "tester.1792325194792@store.local",
  "password": "Tester@123"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "An account with this email already exists.",
  "instance": "/api/auth/register",
  "code": "email_taken",
  "traceId": "0HNPD210LSUND:00000003"
}
```

### Register invalid body -> 400

`POST /api/auth/register` → **400 Bad Request**

Request:

```json
{
  "fullName": "X",
  "email": "not-an-email",
  "password": "123"
}
```

Response:

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
  "traceId": "0HNPD210LSUND:00000004"
}
```

### Login admin

`POST /api/auth/login` → **200 OK**

Request:

```json
{
  "email": "admin@store.local",
  "password": "Admin@123"
}
```

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpX...",
  "accessTokenExpiresAt": "2026-10-18T12:21:37.7517508Z",
  "refreshToken": "-LSGPuQoHHPYa3gS2tTo1k_ZsLZ91R2Y...",
  "refreshTokenExpiresAt": "2026-10-25T12:06:37.7516702Z",
  "user": {
    "id": 1,
    "fullName": "Store Admin",
    "email": "admin@store.local",
    "role": "Admin"
  }
}
```

### Login customer

`POST /api/auth/login` → **200 OK**

Request:

```json
{
  "email": "aarav@store.local",
  "password": "Customer@123"
}
```

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpX...",
  "accessTokenExpiresAt": "2026-10-18T12:21:38.2285598Z",
  "refreshToken": "YlNy26oKU-opinlbFaO-XIU5mt2svCm9...",
  "refreshTokenExpiresAt": "2026-10-25T12:06:38.2285578Z",
  "user": {
    "id": 2,
    "fullName": "Aarav Sharma",
    "email": "aarav@store.local",
    "role": "Customer"
  }
}
```

### Login wrong password -> 401

`POST /api/auth/login` → **401 Unauthorized**

Request:

```json
{
  "email": "aarav@store.local",
  "password": "wrong-password"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid email or password.",
  "instance": "/api/auth/login",
  "traceId": "0HNPD210LSUND:00000007"
}
```

### Refresh token (rotates)

`POST /api/auth/refresh` → **200 OK**

Request:

```json
{
  "refreshToken": "cNq9_kdrm1tD3swV2XhwxowsINm4WSUof9p0iFi6-omNZTeSt3qhk4AUo27GrQrQBHODe2BeECmfnhiBnZYTUw"
}
```

Response:

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpX...",
  "accessTokenExpiresAt": "2026-10-18T12:21:39.0306089Z",
  "refreshToken": "5RuqIPPTdeCPVfyyDj04N7VIxbi1oYD0...",
  "refreshTokenExpiresAt": "2026-10-25T12:06:39.0306079Z",
  "user": {
    "id": 1004,
    "fullName": "Postman Tester",
    "email": "tester.1792325194792@store.local",
    "role": "Customer"
  }
}
```

### Refresh with already-used token -> 401 (reuse detection)

`POST /api/auth/refresh` → **401 Unauthorized**

Request:

```json
{
  "refreshToken": "cNq9_kdrm1tD3swV2XhwxowsINm4WSUof9p0iFi6-omNZTeSt3qhk4AUo27GrQrQBHODe2BeECmfnhiBnZYTUw"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Refresh token is invalid or expired.",
  "instance": "/api/auth/refresh",
  "traceId": "0HNPD210LSUND:00000009"
}
```

### Refresh with the newer token -> 401 (whole session revoked)

`POST /api/auth/refresh` → **401 Unauthorized**

Request:

```json
{
  "refreshToken": "5RuqIPPTdeCPVfyyDj04N7VIxbi1oYD00eFecyvV0VP3WtNB5WwZJiTrJwksgoxUMtCjWKtIixvsDhUz2ZzMOA"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Refresh token is invalid or expired.",
  "instance": "/api/auth/refresh",
  "traceId": "0HNPD210LSUND:0000000A"
}
```

### Logout (revoke refresh token) -> 204

`POST /api/auth/logout` → **204 No Content**

Request:

```json
{
  "refreshToken": "5RuqIPPTdeCPVfyyDj04N7VIxbi1oYD00eFecyvV0VP3WtNB5WwZJiTrJwksgoxUMtCjWKtIixvsDhUz2ZzMOA"
}
```

### No token -> 401

`GET /api/orders/my` → **401 Unauthorized**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/orders/my",
  "traceId": "0HNPD210LSUND:0000000C"
}
```

## 2. Products

### List products (default)

`GET /api/products` → **200 OK**

Response:

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
    "... 10 more"
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 19,
  "totalPages": 2
}
```

### Filter + sort (search, price range, in stock, price desc)

`GET /api/products?search=laptop&minPrice=10000&maxPrice=70000&inStock=true&sortBy=price&sortDir=desc&page=1&pageSize=10` → **200 OK**

Response:

```json
{
  "items": [
    {
      "id": 2,
      "sku": "LAP-1002",
      "name": "HP Pavilion 14 Laptop",
      "price": 64990.0,
      "stockQuantity": 8,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z",
      "rowVersion": "AAAAAAAAB9I="
    },
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
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 2,
  "totalPages": 1
}
```

### Page 2, page size 5

`GET /api/products?page=2&pageSize=5` → **200 OK**

Response:

```json
{
  "items": [
    {
      "id": 7,
      "sku": "AUD-3003",
      "name": "JBL Flip 6 Bluetooth Speaker",
      "price": 9999.0,
      "stockQuantity": 15,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z",
      "rowVersion": "AAAAAAAAB9c="
    },
    {
      "id": 1001,
      "sku": "LAST-1792324676694",
      "name": "Last Unit Gadget 1792324676694",
      "price": 1500.0,
      "stockQuantity": 1,
      "isActive": true,
      "createdAt": "2026-10-18T11:58:00.2114175Z",
      "rowVersion": "AAAAAAAAJxk="
    },
    "... 3 more"
  ],
  "page": 2,
  "pageSize": 5,
  "totalCount": 19,
  "totalPages": 4
}
```

### Out of stock only

`GET /api/products?inStock=false` → **200 OK**

Response:

```json
{
  "items": [
    {
      "id": 15,
      "sku": "CAM-8001",
      "name": "Webcam Full HD 1080p",
      "price": 2199.0,
      "stockQuantity": 0,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z",
      "rowVersion": "AAAAAAAAB98="
    }
  ],
  "page": 1,
  "pageSize": 12,
  "totalCount": 1,
  "totalPages": 1
}
```

### minPrice > maxPrice -> 400

`GET /api/products?minPrice=500&maxPrice=100` → **400 Bad Request**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "minPrice cannot be greater than maxPrice.",
  "instance": "/api/products",
  "traceId": "0HNPD210LSUND:00000011"
}
```

### Get product by id

`GET /api/products/1` → **200 OK**

Response:

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

### Get unknown product -> 404

`GET /api/products/999999` → **404 Not Found**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Product '999999' was not found.",
  "instance": "/api/products/999999",
  "traceId": "0HNPD210LSUND:00000013"
}
```

### Create product (Admin) - stock 1 for the out-of-stock test

`POST /api/products` → **201 Created**

Request:

```json
{
  "sku": "LAST-1792325194792",
  "name": "Last Unit Gadget 1792325194792",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true
}
```

Response header `Location: http://localhost:5080/api/products/1005`

Response:

```json
{
  "id": 1005,
  "sku": "LAST-1792325194792",
  "name": "Last Unit Gadget 1792325194792",
  "price": 1500,
  "stockQuantity": 1,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.3713313Z",
  "rowVersion": "AAAAAAAAJy8="
}
```

### Create product (Admin) - used for orders

`POST /api/products` → **201 Created**

Request:

```json
{
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true
}
```

Response header `Location: http://localhost:5080/api/products/1006`

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzA="
}
```

### Create duplicate SKU -> 409

`POST /api/products` → **409 Conflict**

Request:

```json
{
  "sku": "ORD-1792325194792",
  "name": "Duplicate",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "SKU 'ORD-1792325194792' already exists.",
  "instance": "/api/products",
  "code": "sku_taken",
  "traceId": "0HNPD210LSUND:00000016"
}
```

### Create invalid product -> 400

`POST /api/products` → **400 Bad Request**

Request:

```json
{
  "sku": "x",
  "name": "",
  "price": 0,
  "stockQuantity": -1,
  "isActive": true
}
```

Response:

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
  "traceId": "0HNPD210LSUND:00000017"
}
```

### Create product as Customer -> 403

`POST /api/products` → **403 Forbidden**

Request:

```json
{
  "sku": "NOPE-1",
  "name": "Nope",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/products",
  "traceId": "0HNPD210LSUND:00000018"
}
```

### Create product anonymous -> 401

`POST /api/products` → **401 Unauthorized**

Request:

```json
{
  "sku": "NOPE-2",
  "name": "Nope",
  "price": 10,
  "stockQuantity": 1,
  "isActive": true
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/products",
  "traceId": "0HNPD210LSUND:00000019"
}
```

### Update product (Admin, with rowVersion)

`PUT /api/products/1006` → **200 OK**

Request:

```json
{
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "rowVersion": "AAAAAAAAJzA="
}
```

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000,
  "stockQuantity": 10,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzE="
}
```

### Update with stale rowVersion -> 409

`PUT /api/products/1006` → **409 Conflict**

Request:

```json
{
  "sku": "ORD-1792325194792",
  "name": "Lost update",
  "price": 1,
  "stockQuantity": 999,
  "isActive": true,
  "rowVersion": "AAAAAAAAJzA="
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "This product was changed by someone else (or by a new order). Reload it and try again.",
  "instance": "/api/products/1006",
  "code": "concurrency",
  "traceId": "0HNPD210LSUND:0000001B"
}
```

## 3. Orders (rules 1, 3)

### Place order without Idempotency-Key -> 400

`POST /api/orders` → **400 Bad Request**

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 1
    }
  ]
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "The Idempotency-Key header is required when placing an order.",
  "instance": "/api/orders",
  "traceId": "0HNPD210LSUND:0000001C"
}
```

### Place order (Customer)

`POST /api/orders` → **201 Created**

Header `Idempotency-Key: 88543103-dbc0-4fcd-b937-7189175984a5`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 2
    }
  ]
}
```

Response header `Location: http://localhost:5080/api/orders/6`

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Replay same Idempotency-Key -> same order (200)

`POST /api/orders` → **200 OK**

Header `Idempotency-Key: 88543103-dbc0-4fcd-b937-7189175984a5`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 2
    }
  ]
}
```

Response header `Idempotent-Replayed: true`

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Same key, different body -> 422

`POST /api/orders` → **422 Unprocessable Entity**

Header `Idempotency-Key: 88543103-dbc0-4fcd-b937-7189175984a5`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 5
    }
  ]
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc4918#section-11.2",
  "title": "Idempotency key reused",
  "status": 422,
  "detail": "This Idempotency-Key was already used for a different order request.",
  "instance": "/api/orders",
  "traceId": "0HNPD210LSUND:0000001F"
}
```

### Stock reduced only once (10 -> 8)

`GET /api/products/1006` → **200 OK**

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000.0,
  "stockQuantity": 8,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzM="
}
```

### Not enough stock on one line -> 409, nothing reserved

`POST /api/orders` → **409 Conflict**

Header `Idempotency-Key: 95e96370-b52e-44ca-bf08-bacfd670ef05`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 1
    },
    {
      "productId": 1005,
      "quantity": 2
    }
  ]
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "Not enough stock for: Last Unit Gadget 1792325194792 (requested 2, available 1).",
  "instance": "/api/orders",
  "code": "out_of_stock",
  "traceId": "0HNPD210LSUND:00000021"
}
```

### First product untouched after failed order (still 8)

`GET /api/products/1006` → **200 OK**

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000.0,
  "stockQuantity": 8,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzM="
}
```

### Unknown product -> 400

`POST /api/orders` → **400 Bad Request**

Header `Idempotency-Key: c3d893cd-d0e1-4538-8fe8-73451531078c`

Request:

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

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "These products do not exist or are not available: 999999.",
  "instance": "/api/orders",
  "traceId": "0HNPD210LSUND:00000023"
}
```

### Empty items -> 400

`POST /api/orders` → **400 Bad Request**

Header `Idempotency-Key: a44cc5b9-edb2-4077-b41c-df02710aa60b`

Request:

```json
{
  "items": []
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Items": [
      "An order needs at least one item."
    ]
  },
  "traceId": "0HNPD210LSUND:00000024"
}
```

### Admin cannot place orders -> 403

`POST /api/orders` → **403 Forbidden**

Header `Idempotency-Key: 0d01749d-71b2-4ad5-be53-8966b628fda2`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 1
    }
  ]
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/orders",
  "traceId": "0HNPD210LSUND:00000025"
}
```

### My orders (paged)

`GET /api/orders/my?page=1&pageSize=10` → **200 OK**

Response:

```json
{
  "items": [
    {
      "id": 6,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 600.0,
      "total": 11400.0,
      "createdAt": "2026-10-18T12:06:42.9855747Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1006,
          "sku": "ORD-1792325194792",
          "productName": "Order Test Speaker 1792325194792 (v2)",
          "quantity": 2,
          "unitPrice": 6000.0,
          "lineTotal": 12000.0
        }
      ]
    },
    {
      "id": 5,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Cancelled",
      "subTotal": 18000.0,
      "discount": 900.0,
      "total": 17100.0,
      "createdAt": "2026-10-18T12:00:21.4308672Z",
      "updatedAt": "2026-10-18T12:00:21.6323529Z",
      "items": [
        {
          "productId": 1004,
          "sku": "ORD-1792324815161",
          "productName": "Order Test Speaker 1792324815161 (v2)",
          "quantity": 3,
          "unitPrice": 6000.0,
          "lineTotal": 18000.0
        }
      ]
    },
    "... 4 more"
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 6,
  "totalPages": 1
}
```

### Get order as owner

`GET /api/orders/6` → **200 OK**

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Get order as another customer -> 403

`GET /api/orders/6` → **403 Forbidden**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You can only view your own orders.",
  "instance": "/api/orders/6",
  "traceId": "0HNPD210LSUND:00000028"
}
```

### Get order as Admin

`GET /api/orders/6` → **200 OK**

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": null,
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Get unknown order -> 404

`GET /api/orders/999999` → **404 Not Found**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Order '999999' was not found.",
  "instance": "/api/orders/999999",
  "traceId": "0HNPD210LSUND:0000002A"
}
```

### All orders filtered by status (Admin)

`GET /api/orders?status=Pending&page=1&pageSize=20` → **200 OK**

Response:

```json
{
  "items": [
    {
      "id": 6,
      "customerId": 2,
      "customerName": "Aarav Sharma",
      "status": "Pending",
      "subTotal": 12000.0,
      "discount": 600.0,
      "total": 11400.0,
      "createdAt": "2026-10-18T12:06:42.9855747Z",
      "updatedAt": null,
      "items": [
        {
          "productId": 1006,
          "sku": "ORD-1792325194792",
          "productName": "Order Test Speaker 1792325194792 (v2)",
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

### All orders as Customer -> 403

`GET /api/orders` → **403 Forbidden**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/orders",
  "traceId": "0HNPD210LSUND:0000002C"
}
```

## 4. Status transitions (rule 4)

### Pending -> Shipped (skip) -> 400

`PATCH /api/orders/6/status` → **400 Bad Request**

Request:

```json
{
  "status": "Shipped"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Business rule violated",
  "status": 400,
  "detail": "Cannot change order status from Pending to Shipped. Allowed: Pending -> Confirmed -> Shipped, or Pending/Confirmed -> Cancelled.",
  "instance": "/api/orders/6/status",
  "traceId": "0HNPD210LSUND:0000002D"
}
```

### Customer tries to confirm -> 403

`PATCH /api/orders/6/status` → **403 Forbidden**

Request:

```json
{
  "status": "Confirmed"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "Customers can only cancel orders.",
  "instance": "/api/orders/6/status",
  "traceId": "0HNPD210LSUND:0000002E"
}
```

### Undefined status value -> 400

`PATCH /api/orders/6/status` → **400 Bad Request**

Request:

```json
{
  "status": 99
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Status": [
      "The field Status is invalid."
    ]
  },
  "traceId": "0HNPD210LSUND:0000002F"
}
```

### Admin: Pending -> Confirmed

`PATCH /api/orders/6/status` → **200 OK**

Request:

```json
{
  "status": "Confirmed"
}
```

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Confirmed",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": "2026-10-18T12:06:46.3208888Z",
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Customer cancels a Confirmed order -> 400

`PATCH /api/orders/6/status` → **400 Bad Request**

Request:

```json
{
  "status": "Cancelled"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Business rule violated",
  "status": 400,
  "detail": "Only Pending orders can be cancelled by the customer. This order is Confirmed.",
  "instance": "/api/orders/6/status",
  "traceId": "0HNPD210LSUND:00000031"
}
```

### Admin: Confirmed -> Shipped

`PATCH /api/orders/6/status` → **200 OK**

Request:

```json
{
  "status": "Shipped"
}
```

Response:

```json
{
  "id": 6,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Shipped",
  "subTotal": 12000.0,
  "discount": 600.0,
  "total": 11400.0,
  "createdAt": "2026-10-18T12:06:42.9855747Z",
  "updatedAt": "2026-10-18T12:06:46.5546423Z",
  "items": [
    {
      "productId": 1006,
      "sku": "ORD-1792325194792",
      "productName": "Order Test Speaker 1792325194792 (v2)",
      "quantity": 2,
      "unitPrice": 6000.0,
      "lineTotal": 12000.0
    }
  ]
}
```

### Shipped -> Cancelled (final state) -> 400

`PATCH /api/orders/6/status` → **400 Bad Request**

Request:

```json
{
  "status": "Cancelled"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Business rule violated",
  "status": 400,
  "detail": "Order is Shipped and can no longer be changed.",
  "instance": "/api/orders/6/status",
  "traceId": "0HNPD210LSUND:00000033"
}
```

### Place a second order (3 units)

`POST /api/orders` → **201 Created**

Header `Idempotency-Key: 7f04956e-e3c0-47b8-a4f8-143f04792b02`

Request:

```json
{
  "items": [
    {
      "productId": 1006,
      "quantity": 3
    }
  ]
}
```

Response header `Location: http://localhost:5080/api/orders/7`

Response:

```json
{
  "id": 7,
  "customerId": 2,
  "customerName": "Aarav Sharma",
  "status": "Pending",
  "subTotal": 18000.0,
  "discount": 2700.0,
  "total": 15300.0,
  "createdAt": "2026-10-18T12:06:46.8649183Z",
  "updatedAt": null,
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
}
```

### Stock after second order (8 -> 5)

`GET /api/products/1006` → **200 OK**

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000.0,
  "stockQuantity": 5,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzc="
}
```

### Customer cancels own Pending order

`PATCH /api/orders/7/status` → **200 OK**

Request:

```json
{
  "status": "Cancelled"
}
```

Response:

```json
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
}
```

### Cancelling restored the stock (back to 8)

`GET /api/products/1006` → **200 OK**

Response:

```json
{
  "id": 1006,
  "sku": "ORD-1792325194792",
  "name": "Order Test Speaker 1792325194792 (v2)",
  "price": 6000.0,
  "stockQuantity": 8,
  "isActive": true,
  "createdAt": "2026-10-18T12:06:41.6643937Z",
  "rowVersion": "AAAAAAAAJzk="
}
```

### Cancel again -> 400 (stock not restored twice)

`PATCH /api/orders/7/status` → **400 Bad Request**

Request:

```json
{
  "status": "Cancelled"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Business rule violated",
  "status": 400,
  "detail": "Order is already Cancelled.",
  "instance": "/api/orders/7/status",
  "traceId": "0HNPD210LSUND:00000038"
}
```

### Other customer cancels it -> 403

`PATCH /api/orders/7/status` → **403 Forbidden**

Request:

```json
{
  "status": "Cancelled"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You can only change your own orders.",
  "instance": "/api/orders/7/status",
  "traceId": "0HNPD210LSUND:00000039"
}
```

### Unknown order -> 404

`PATCH /api/orders/999999/status` → **404 Not Found**

Request:

```json
{
  "status": "Confirmed"
}
```

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not found",
  "status": 404,
  "detail": "Order '999999' was not found.",
  "instance": "/api/orders/999999/status",
  "traceId": "0HNPD210LSUND:0000003A"
}
```

## 5. Reports (Part B)

### Top customers, last 30 days (Admin)

`GET /api/reports/top-customers?days=30` → **200 OK**

Response:

```json
[
  {
    "customerId": 2,
    "fullName": "Aarav Sharma",
    "email": "aarav@store.local",
    "orderCount": 4,
    "totalSpend": 93097.15
  }
]
```

### days=0 -> 400

`GET /api/reports/top-customers?days=0` → **400 Bad Request**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Validation failed",
  "status": 400,
  "detail": "days must be between 1 and 365.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPD210LSUND:0000003C"
}
```

### As Customer -> 403

`GET /api/reports/top-customers?days=30` → **403 Forbidden**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have permission to access this resource.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPD210LSUND:0000003D"
}
```

### Anonymous -> 401

`GET /api/reports/top-customers?days=30` → **401 Unauthorized**

Response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Unauthorized",
  "status": 401,
  "detail": "A valid access token is required.",
  "instance": "/api/reports/top-customers",
  "traceId": "0HNPD210LSUND:0000003E"
}
```

## 6. Rate limit (run last)

### Login attempts until 429

`POST /api/auth/login` → **429 Too Many Requests**

Request:

```json
{
  "email": "nobody@store.local",
  "password": "wrong"
}
```

Response header `Retry-After: 60`

Response:

```json
{
  "title": "Too many requests",
  "status": 429,
  "detail": "Too many login attempts. Please try again in 60 seconds.",
  "instance": "/api/auth/login",
  "traceId": "0HNPD210LSUND:00000041",
  "retryAfterSeconds": 60
}
```

