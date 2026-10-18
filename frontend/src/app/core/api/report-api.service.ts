import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { TopCustomer } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReportApiService {
  private readonly http = inject(HttpClient);

  topCustomers(days: number, top = 5): Observable<TopCustomer[]> {
    const params = new HttpParams().set('days', days).set('top', top);
    return this.http.get<TopCustomer[]>('/api/reports/top-customers', { params });
  }
}
