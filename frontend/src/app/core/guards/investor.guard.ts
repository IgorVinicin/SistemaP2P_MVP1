import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const investorGuard: CanActivateFn = (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  const role = authService.userRole();
  const status = authService.userStatus();

  if (role !== 'Investidor') {
    router.navigate(['/dashboard']);
    return false;
  }

  // Regras de acesso baseado no status para o Investidor
  if (status === 'Aprovado') {
    // Permite acessar a rota /investor/dashboard
    if (state.url === '/investor/dashboard') {
      return true;
    }
    router.navigate(['/investor/dashboard']);
    return false;
  }

  if (status === 'Pendente') {
    if (state.url === '/dashboard') {
      return true;
    }
    router.navigate(['/dashboard']);
    return false;
  }

  if (status === 'EmAnalise') {
    if (state.url === '/investor/aguardando-aprovacao') {
      return true;
    }
    router.navigate(['/investor/aguardando-aprovacao']);
    return false;
  }

  if (status === 'Reprovado') {
    if (state.url === '/investor/reprovado') {
      return true;
    }
    router.navigate(['/investor/reprovado']);
    return false;
  }

  return false;
};
