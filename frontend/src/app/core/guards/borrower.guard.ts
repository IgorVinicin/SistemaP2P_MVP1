import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const borrowerGuard: CanActivateFn = route => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.getMyStatus().pipe(
    map(me => me.role === 'Cliente' && (!route.data['requiresApproval'] || me.status === 'Aprovado')
      ? true : router.createUrlTree(['/dashboard'])),
    catchError(() => of(router.createUrlTree(['/dashboard'])))
  );
};
