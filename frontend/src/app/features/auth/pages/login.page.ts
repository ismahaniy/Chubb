import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLE_HOME } from '../../../core/auth/role-home';
import { DEMO_PASSWORD, DEMO_USERS, DemoUser } from '../demo-users';
import { DemoUserList } from '../ui/demo-user-list';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, DemoUserList],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto grid max-w-4xl gap-8 md:grid-cols-2">
      <form
        class="space-y-4 rounded-lg border border-neutral-200 bg-white p-6"
        [formGroup]="form"
        (ngSubmit)="submit()"
      >
        <h2 class="text-lg font-semibold">Sign in</h2>
        <label class="block text-sm">
          <span class="text-neutral-700">Email</span>
          <input
            type="email"
            formControlName="email"
            autocomplete="username"
            class="mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none"
          />
        </label>
        <label class="block text-sm">
          <span class="text-neutral-700">Password</span>
          <input
            type="password"
            formControlName="password"
            autocomplete="current-password"
            class="mt-1 block w-full rounded-md border border-neutral-300 px-3 py-2 focus:border-neutral-500 focus:outline-none"
          />
        </label>
        <button
          type="submit"
          class="w-full rounded-md bg-neutral-900 px-4 py-2 text-sm font-medium text-white hover:bg-neutral-700 disabled:opacity-50"
          [disabled]="form.invalid || submitting()"
        >
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <div>
        <h2 class="mb-1 text-lg font-semibold">Demo users</h2>
        <p class="mb-4 text-sm text-neutral-500">
          Pick one to fill the form. Password for all: <code>{{ demoPassword }}</code>
        </p>
        <app-demo-user-list [users]="demoUsers" (pick)="useDemoUser($event)" />
      </div>
    </div>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly demoUsers = DEMO_USERS;
  protected readonly demoPassword = DEMO_PASSWORD;
  protected readonly submitting = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected useDemoUser(user: DemoUser): void {
    this.form.setValue({ email: user.email, password: DEMO_PASSWORD });
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) return;
    this.submitting.set(true);
    try {
      const user = await this.auth.login(this.form.getRawValue());
      await this.router.navigateByUrl(ROLE_HOME[user.role]);
    } catch {
      // The error interceptor already showed the toast.
    } finally {
      this.submitting.set(false);
    }
  }
}
