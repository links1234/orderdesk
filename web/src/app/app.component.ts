import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Order, OrderService, ServiceOffering } from './order.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule, CurrencyPipe],
  template: `
    <main>
      <h1>OrderDesk</h1>
      <p class="lede">Pick services, place an order, and pay for it.</p>

      @if (!order()) {
        <section aria-labelledby="catalog-heading">
          <h2 id="catalog-heading">Services</h2>
          <table>
            <thead>
              <tr><th>Service</th><th>Price</th><th>Quantity</th></tr>
            </thead>
            <tbody>
              @for (service of services(); track service.id) {
                <tr>
                  <td>{{ service.name }} <small>{{ service.category }}</small></td>
                  <td>{{ service.price | currency: 'CAD' : 'symbol-narrow' }}</td>
                  <td>
                    <input
                      type="number" min="0" max="20"
                      [attr.aria-label]="'Quantity for ' + service.name"
                      [ngModel]="quantities()[service.id] || 0"
                      (ngModelChange)="setQuantity(service.id, $event)" />
                  </td>
                </tr>
              }
            </tbody>
          </table>

          <label>Email <input type="email" name="email" [(ngModel)]="email" placeholder="you@example.com" /></label>
          <label>Promo code <input type="text" name="promo" [(ngModel)]="promoCode" /></label>
          <p data-testid="estimate">Items selected: {{ unitCount() }}</p>
          <button type="button" (click)="placeOrder()" [disabled]="busy() || unitCount() === 0">Place order</button>
        </section>
      } @else {
        <section aria-labelledby="order-heading">
          <h2 id="order-heading">Your order</h2>
          <p>Status: <strong data-testid="status">{{ order()!.status }}</strong></p>
          <ul>
            @for (line of order()!.lines; track line.serviceId) {
              <li>{{ line.quantity }} × {{ line.name }}</li>
            }
          </ul>
          <dl>
            <dt>Subtotal</dt><dd data-testid="subtotal">{{ order()!.subtotal | currency: 'CAD' : 'symbol-narrow' }}</dd>
            <dt>Discount</dt><dd data-testid="discount">{{ order()!.discount | currency: 'CAD' : 'symbol-narrow' }}</dd>
            <dt>Tax</dt><dd data-testid="tax">{{ order()!.tax | currency: 'CAD' : 'symbol-narrow' }}</dd>
            <dt>Total</dt><dd data-testid="total">{{ order()!.total | currency: 'CAD' : 'symbol-narrow' }}</dd>
          </dl>

          @if (order()!.status === 'Pending') {
            <label>Card
              <select name="card" [(ngModel)]="cardToken">
                <option value="tok_visa">Test card (approved)</option>
                <option value="tok_declined">Test card (declined)</option>
              </select>
            </label>
            <button type="button" (click)="pay()" [disabled]="busy()">Pay now</button>
          } @else {
            <p data-testid="receipt">Payment reference: {{ order()!.paymentReference }}</p>
          }
          <button type="button" class="secondary" (click)="reset()">Start a new order</button>
        </section>
      }

      @if (error()) {
        <p role="alert">{{ error() }}</p>
      }
    </main>
  `,
})
export class AppComponent implements OnInit {
  private readonly orders = inject(OrderService);

  readonly services = signal<ServiceOffering[]>([]);
  readonly quantities = signal<Record<number, number>>({});
  readonly order = signal<Order | null>(null);
  readonly error = signal('');
  readonly busy = signal(false);
  readonly unitCount = computed(() => Object.values(this.quantities()).reduce((sum, value) => sum + value, 0));

  email = '';
  promoCode = '';
  cardToken = 'tok_visa';
  private paymentKey = crypto.randomUUID();

  ngOnInit(): void {
    this.orders.services().subscribe({
      next: (services) => this.services.set(services),
      error: () => this.error.set('The service list could not be loaded.'),
    });
  }

  setQuantity(serviceId: number, value: number | null): void {
    const quantity = Math.max(0, Math.min(20, Math.floor(Number(value) || 0)));
    this.quantities.update((current) => ({ ...current, [serviceId]: quantity }));
  }

  placeOrder(): void {
    const lines = Object.entries(this.quantities())
      .filter(([, quantity]) => quantity > 0)
      .map(([serviceId, quantity]) => ({ serviceId: Number(serviceId), quantity }));
    this.run(this.orders.create(this.email, lines, this.promoCode));
  }

  pay(): void {
    // The same key is reused if the customer clicks twice, so the card is charged once.
    this.run(this.orders.pay(this.order()!.id, this.cardToken, this.paymentKey));
  }

  reset(): void {
    this.order.set(null);
    this.quantities.set({});
    this.error.set('');
    this.paymentKey = crypto.randomUUID();
  }

  private run(request: ReturnType<OrderService['create']>): void {
    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: (order) => {
        this.order.set(order);
        this.busy.set(false);
      },
      error: (response: HttpErrorResponse) => {
        const problem = response.error ?? {};
        const fieldErrors = Object.values(problem.errors ?? {}).flat().join(' ');
        this.error.set(fieldErrors || problem.detail || 'Something went wrong. Please try again.');
        this.busy.set(false);
      },
    });
  }
}
