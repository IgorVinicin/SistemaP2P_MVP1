import { Routes } from '@angular/router';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { LayoutComponent } from './shared/components/layout/layout.component';
import { DashboardHomeComponent } from './features/user-dashboard/pages/dashboard-home/dashboard-home.component';
import { AdminHomeComponent } from './features/admin/pages/admin-home/admin-home.component';
import { ScoreConfigComponent } from './features/admin/pages/score-config/score-config.component';
import { ScoreFormComponent } from './features/user-dashboard/pages/score-form/score-form.component';
import { CreditConfigComponent } from './features/admin/pages/credit-config/credit-config.component';
import { LoanRequestComponent } from './features/user-dashboard/pages/loan-request/loan-request.component';
import { MyLoansComponent } from './features/user-dashboard/pages/my-loans/my-loans.component';
import { LoanHistoryComponent } from './features/user-dashboard/pages/loan-history/loan-history.component';
import { AdminLoansComponent } from './features/admin/pages/admin-loans/admin-loans.component';
import { authGuard } from './core/guards/auth.guard';
import { adminGuard } from './core/guards/admin.guard';

import { InvestorDashboardComponent } from './features/investor/pages/investor-dashboard/investor-dashboard.component';
import { InvestorWaitingComponent } from './features/investor/pages/investor-waiting/investor-waiting.component';
import { InvestorReprovedComponent } from './features/investor/pages/investor-reproved/investor-reproved.component';
import { investorGuard } from './core/guards/investor.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'register', component: RegisterComponent },
  {
    path: '',
    component: LayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardHomeComponent },
      { path: 'investor/dashboard', component: InvestorDashboardComponent, canActivate: [investorGuard] },
      { path: 'investor/aguardando-aprovacao', component: InvestorWaitingComponent, canActivate: [investorGuard] },
      { path: 'investor/reprovado', component: InvestorReprovedComponent, canActivate: [investorGuard] },
      { path: 'admin', component: AdminHomeComponent, canActivate: [adminGuard] },
      { path: 'admin/questions', component: ScoreConfigComponent, canActivate: [adminGuard] },
      { path: 'admin/credit', component: CreditConfigComponent, canActivate: [adminGuard] },
      { path: 'admin/loans', component: AdminLoansComponent, canActivate: [adminGuard] },
      { path: 'score', component: ScoreFormComponent },
      { path: 'loans/request', component: LoanRequestComponent },
      { path: 'loans/my', component: MyLoansComponent },
      { path: 'loans/history', component: LoanHistoryComponent },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: 'login' }
];
