import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (solicitud, siguiente) => {
  const token = inject(AuthService).token;
  if (token && solicitud.url.startsWith('/api/')) {
    solicitud = solicitud.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
  return siguiente(solicitud);
};
