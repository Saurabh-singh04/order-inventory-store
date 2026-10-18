import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { toApiError } from '../../core/errors/api-error';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <section class="auth-card card">
      <h1>Create account</h1>

      @if (error()) {
        <div class="alert alert-error" role="alert">{{ error() }}</div>
      }

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <label class="field">
          <span>Full name</span>
          <input type="text" formControlName="fullName" autocomplete="name" />
          @if (form.controls.fullName.touched && form.controls.fullName.invalid) {
            <small class="field-error">Name must be 2–100 characters.</small>
          }
        </label>

        <label class="field">
          <span>Email</span>
          <input type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <small class="field-error">Enter a valid email address.</small>
          }
        </label>

        <label class="field">
          <span>Password</span>
          <input type="password" formControlName="password" autocomplete="new-password" />
          @if (form.controls.password.touched && form.controls.password.invalid) {
            <small class="field-error">At least 8 characters.</small>
          }
        </label>

        <button type="submit" class="btn btn-primary btn-block" [disabled]="submitting()">
          {{ submitting() ? 'Creating account…' : 'Create account' }}
        </button>
      </form>

      <p class="muted center">Already registered? <a routerLink="/login">Sign in</a></p>
    </section>
  `,
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    this.auth.register(this.form.getRawValue()).subscribe({
      next: (user) => {
        this.toast.success(`Welcome, ${user.fullName}!`);
        this.router.navigate(['/products']);
      },
      error: (err) => {
        this.error.set(toApiError(err).message);
        this.submitting.set(false);
      },
    });
  }
}
