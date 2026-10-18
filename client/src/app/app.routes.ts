import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },

  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account',
    loadComponent: () => import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },

  // Catalogue and cart are public; checkout itself requires a customer login.
  {
    path: 'products',
    title: 'Products',
    loadComponent: () => import('./features/catalog/catalog.component').then((m) => m.CatalogComponent),
  },
  {
    path: 'cart',
    title: 'Cart',
    loadComponent: () => import('./features/cart/cart.component').then((m) => m.CartComponent),
  },
  {
    path: 'orders',
    canActivate: [authGuard, roleGuard('Customer')],
    title: 'My orders',
    loadComponent: () => import('./features/orders/my-orders.component').then((m) => m.MyOrdersComponent),
  },

  {
    path: 'admin',
    canActivate: [roleGuard('Admin')],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'orders' },
      {
        path: 'orders',
        title: 'Orders',
        loadComponent: () => import('./features/admin/admin-orders.component').then((m) => m.AdminOrdersComponent),
      },
      {
        path: 'products',
        title: 'Manage products',
        loadComponent: () => import('./features/admin/admin-products.component').then((m) => m.AdminProductsComponent),
      },
      {
        path: 'products/:id',
        title: 'Edit product',
        loadComponent: () => import('./features/admin/product-form.component').then((m) => m.ProductFormComponent),
      },
      {
        path: 'reports',
        title: 'Top customers',
        loadComponent: () => import('./features/admin/top-customers.component').then((m) => m.TopCustomersComponent),
      },
    ],
  },

  {
    path: 'forbidden',
    title: 'Access denied',
    loadComponent: () => import('./features/misc/simple-pages.component').then((m) => m.ForbiddenComponent),
  },
  {
    path: '**',
    title: 'Not found',
    loadComponent: () => import('./features/misc/simple-pages.component').then((m) => m.NotFoundComponent),
  },
];
