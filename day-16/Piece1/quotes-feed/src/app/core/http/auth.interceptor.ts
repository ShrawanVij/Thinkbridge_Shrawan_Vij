import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../../auth/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const authHeader = authService.authHeader();

  if (!authHeader['Authorization']) {
    return next(req);
  }

  return next(req.clone({ setHeaders: authHeader }));
};