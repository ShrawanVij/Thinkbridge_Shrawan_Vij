import { Component, effect, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { LoginFormComponent } from './login-form.component';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [LoginFormComponent],
  templateUrl: './login-page.component.html',
  styleUrl: './login-page.component.css',
})
export class LoginPageComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  constructor() {
    effect(() => {
      if (this.authService.isAuthenticated()) {
        const redirectTo = this.route.snapshot.queryParamMap.get('redirectTo') ?? '/';
        this.router.navigateByUrl(redirectTo);
      }
    });
  }
}