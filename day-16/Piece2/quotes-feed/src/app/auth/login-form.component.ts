import { Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { form, required, email as emailValidator, submit, FormField } from '@angular/forms/signals';
import { AuthService } from './auth.service';
import { LoginRequest } from './auth.model';

@Component({
  selector: 'app-login-form',
  imports: [FormField],
  templateUrl: './login-form.component.html',
  styleUrl: './login-form.component.css',
})
export class LoginFormComponent {
  private readonly authService = inject(AuthService);

  readonly serverError = signal<string | null>(null);

  private readonly model = signal<LoginRequest>({ email: '', password: '' });

  readonly loginForm = form(this.model, (f) => {
    required(f.email, { message: 'Email is required.' });
    emailValidator(f.email, { message: 'Enter a valid email address.' });
    required(f.password, { message: 'Password is required.' });
  });

  async onSubmit(): Promise<void> {
    this.serverError.set(null);

    await submit(this.loginForm, {
      action: async () => {
        try {
          await firstValueFrom(this.authService.login(this.model()));
        } catch {
          this.serverError.set('Invalid email or password.');
        }
        return undefined;
      },
      onInvalid: () => {
        if (this.loginForm.email().invalid()) {
          this.loginForm.email().focusBoundControl();
        } else if (this.loginForm.password().invalid()) {
          this.loginForm.password().focusBoundControl();
        }
      },
    });
  }
}