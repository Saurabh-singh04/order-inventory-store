import { HttpClient, HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateOrderRequest, Order, OrderStatus, PagedResult } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class OrderApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/orders';

  /** Full response so the caller can tell a new order (201) from an idempotent replay (200). */
  place(request: CreateOrderRequest, idempotencyKey: string): Observable<HttpResponse<Order>> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<Order>(this.baseUrl, request, { headers, observe: 'response' });
  }

  myOrders(page: number, pageSize: number): Observable<PagedResult<Order>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/my`, { params });
  }

  all(page: number, pageSize: number, status?: OrderStatus): Observable<PagedResult<Order>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<PagedResult<Order>>(this.baseUrl, { params });
  }

  get(id: number): Observable<Order> {
    return this.http.get<Order>(`${this.baseUrl}/${id}`);
  }

  changeStatus(id: number, status: OrderStatus): Observable<Order> {
    return this.http.patch<Order>(`${this.baseUrl}/${id}/status`, { status });
  }
}
