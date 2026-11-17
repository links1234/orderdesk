import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface ServiceOffering {
  id: number;
  name: string;
  category: string;
  price: number;
}

export interface OrderLine {
  serviceId: number;
  name: string;
  quantity: number;
  unitPrice: number;
}

export interface Order {
  id: string;
  customerEmail: string;
  status: string;
  subtotal: number;
  discount: number;
  tax: number;
  total: number;
  paymentReference: string | null;
  lines: OrderLine[];
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private readonly http = inject(HttpClient);

  services(): Observable<ServiceOffering[]> {
    return this.http.get<ServiceOffering[]>('/api/services');
  }

  create(customerEmail: string, lines: { serviceId: number; quantity: number }[], promoCode: string): Observable<Order> {
    return this.http.post<Order>('/api/orders', { customerEmail, lines, promoCode: promoCode || null });
  }

  pay(orderId: string, cardToken: string, idempotencyKey: string): Observable<Order> {
    return this.http.post<Order>(
      `/api/orders/${orderId}/pay`,
      { cardToken },
      { headers: { 'Idempotency-Key': idempotencyKey } },
    );
  }
}
