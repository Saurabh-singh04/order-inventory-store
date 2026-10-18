import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AuthService } from './core/auth/auth.service';
import { CartService } from './core/cart/cart.service';
import { OrderHubService } from './core/realtime/order-hub.service';
import { ToastContainerComponent } from './shared/toast/toast-container.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastContainerComponent],
  template: `
    <header class="topbar">
      <div class="topbar-inner">
        <a routerLink="/products" class="brand">ShopLine</a>

        <button
          type="button"
          class="menu-toggle"
          [attr.aria-expanded]="menuOpen()"
          aria-controls="main-nav"
          (click)="menuOpen.set(!menuOpen())"
        >
          ☰ <span class="sr-only">Menu</span>
        </button>

        <nav id="main-nav" class="nav" [class.open]="menuOpen()">
          <a routerLink="/products" routerLinkActive="active">Products</a>

          @if (!auth.isAdmin()) {
            <a routerLink="/cart" routerLinkActive="active">
              Cart
              @if (cart.count() > 0) {
                <span class="pill">{{ cart.count() }}</span>
              }
            </a>
          }

          @if (auth.isCustomer()) {
            <a routerLink="/orders" routerLinkActive="active">My orders</a>
          }

          @if (auth.isAdmin()) {
            <a routerLink="/admin/orders" routerLinkActive="active">Orders</a>
            <a routerLink="/admin/products" routerLinkActive="active">Manage products</a>
            <a routerLink="/admin/reports" routerLinkActive="active">Reports</a>
          }

          <span class="nav-spacer"></span>

          @if (auth.user(); as user) {
            <span class="nav-user" [title]="user.email">{{ user.fullName }} <small>({{ user.role }})</small></span>
            <button type="button" class="btn btn-small" (click)="auth.logout()">Sign out</button>
          } @else {
            <a routerLink="/login" routerLinkActive="active">Sign in</a>
            <a routerLink="/register" class="btn btn-small btn-primary">Register</a>
          }
        </nav>
      </div>
    </header>

    <main class="container">
      <router-outlet />
    </main>

    <app-toast-container />
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
  protected readonly cart = inject(CartService);
  protected readonly menuOpen = signal(false);

  constructor() {
    // Creating the hub service starts/stops the SignalR connection with the session.
    inject(OrderHubService);

    inject(Router)
      .events.pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => this.menuOpen.set(false));
  }
}
