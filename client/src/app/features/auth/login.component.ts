import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/errors/api-error';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card card">
      <h1>Sign in</h1>

      @if (sessionExpired) {
        <div class="alert alert-info">Your session expired. Please sign in again.</div>
      }
      @if (error()) {
        <div class="alert" [class.alert-warning]="rateLimited()" [class.alert-error]="!rateLimited()" role="alert">
          {{ error() }}
        </div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label class="field">
          <span>Email</span>
          <input type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <small class="field-error">Enter a valid email address.</small>
          }
        </label>

        <label class="field">
          <span>Password</span>
          <input type="password" formControlName="password" autocomplete="current-password" />
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <small class="field-error">Password is required.</small>
          }
        </label>

        <button type="submit" class="btn btn-primary btn-block" [disabled]="submitting()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <p class="muted center">No account yet? <a routerLink="/register">Create one</a></p>
    </section>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly rateLimited = signal(false);
  protected readonly sessionExpired = this.route.snapshot.queryParamMap.has('expired');

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        const fallback = user.role === 'Admin' ? '/admin/orders' : '/products';
        this.router.navigateByUrl(returnUrl && returnUrl.startsWith('/') ? returnUrl : fallback);
      },
      error: (err) => {
        const apiError = toApiError(err, 'login');
        this.rateLimited.set(apiError.status === 429);
        this.error.set(apiError.message);
        this.submitting.set(false);
      },
    });
  }
}
