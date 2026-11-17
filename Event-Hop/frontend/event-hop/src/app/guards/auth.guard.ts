import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = async (ruta) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!await auth.verificarSesion()) return router.createUrlTree(['/login']);
  const roles: string[] = ruta.data['roles'] ?? [];
  if (roles.length && !roles.includes(auth.usuario!.rol)) {
    const inicio = auth.usuario!.rol === 'admin' ? '/admin'
      : auth.usuario!.rol === 'cafeteria' ? '/cafeteria' : '/';
    return router.createUrlTree([inicio]);
  }
  return true;
};
