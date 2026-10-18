export type Role = 'Admin' | 'Customer';

export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Cancelled';

export interface User {
  id: number;
  fullName: string;
  email: string;
  role: Role;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  fullName: string;
  email: string;
  password: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
  createdAt: string;
  rowVersion: string;
}

export type ProductSort = 'name' | 'price' | 'stock' | 'newest';

export interface ProductQuery {
  search?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  sortBy?: ProductSort;
  sortDir?: 'asc' | 'desc';
  page: number;
  pageSize: number;
  includeInactive?: boolean;
}

export interface SaveProductRequest {
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
  rowVersion?: string;
}

export interface OrderItem {
  productId: number;
  sku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface Order {
  id: number;
  customerId: number;
  customerName: string;
  status: OrderStatus;
  subTotal: number;
  discount: number;
  total: number;
  createdAt: string;
  updatedAt: string | null;
  items: OrderItem[];
}

export interface CreateOrderRequest {
  items: { productId: number; quantity: number }[];
}

export interface TopCustomer {
  customerId: number;
  fullName: string;
  email: string;
  orderCount: number;
  totalSpend: number;
}

/** RFC 7807 body returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  retryAfterSeconds?: number;
  traceId?: string;
}

export interface OrderStatusChangedEvent {
  orderId: number;
  customerId: number;
  oldStatus: OrderStatus;
  newStatus: OrderStatus;
  changedAt: string;
}

export interface OrderPlacedEvent {
  orderId: number;
  customerId: number;
  total: number;
  createdAt: string;
}
