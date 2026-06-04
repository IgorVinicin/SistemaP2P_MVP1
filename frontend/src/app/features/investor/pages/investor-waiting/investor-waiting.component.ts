import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-investor-waiting',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatIconModule, MatButtonModule],
  template: `
    <div class="waiting-container" style="max-width: 600px; margin: 4rem auto; padding: 0 1.5rem; text-align: center;">
      <mat-card style="padding: 2.5rem; border-radius: 12px; border: 1px solid #e2e8f0;">
        <mat-card-content>
          <mat-icon style="font-size: 64px; width: 64px; height: 64px; color: #0284c7; margin-bottom: 1.5rem;">hourglass_empty</mat-icon>
          <h2 style="font-size: 1.75rem; font-weight: 700; color: #0f172a; margin: 0 0 1rem 0;">Aguardando Aprovação</h2>
          <p style="color: #475569; font-size: 1.05rem; line-height: 1.6; margin-bottom: 2rem;">
            Seu cadastro está em análise. Aguarde a aprovação da equipe para ter acesso completo ao dashboard de investimentos.
          </p>
          <button mat-flat-button color="primary" (click)="authService.logout()">Sair</button>
        </mat-card-content>
      </mat-card>
    </div>
  `
})
export class InvestorWaitingComponent {
  public authService = inject(AuthService);
}
