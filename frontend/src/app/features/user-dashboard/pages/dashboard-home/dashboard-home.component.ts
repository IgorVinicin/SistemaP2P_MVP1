import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DocumentUploadCardComponent } from '../../components/document-upload-card/document-upload-card.component';
import { AuthService } from '../../../../core/services/auth.service';
import { DocumentService } from '../../../../core/services/document.service';
import { DocumentStatusDto } from '../../../../core/models/document.models';
import { LoanService } from '../../../../core/services/loan.service';
import { LoanEligibility } from '../../../../core/models/loan.models';
import { ScoreFormComponent } from '../score-form/score-form.component';

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    DocumentUploadCardComponent,
    ScoreFormComponent
  ],
  templateUrl: './dashboard-home.component.html',
  styleUrl: './dashboard-home.component.css'
})
export class DashboardHomeComponent implements OnInit {
  public authService = inject(AuthService);
  private documentService = inject(DocumentService);
  private loanService = inject(LoanService);
  private router = inject(Router);

  public documents: DocumentStatusDto[] = [];
  public isLoading = false;
  public eligibility: LoanEligibility | null = null;

  public ngOnInit(): void {
    const role = this.authService.userRole();
    const status = this.authService.userStatus();

    if (role === 'Administrador') {
      this.router.navigate(['/admin']);
      return;
    }

    if (role === 'Investidor') {
      if (status === 'Aprovado') {
        this.router.navigate(['/investor/dashboard']);
        return;
      } else if (status === 'EmAnalise') {
        this.router.navigate(['/investor/aguardando-aprovacao']);
        return;
      } else if (status === 'Reprovado') {
        this.router.navigate(['/investor/reprovado']);
        return;
      }
    }

    if (role === 'Cliente') {
      if (status === 'Aprovado' || status === 'EmAnalise') {
        this.router.navigate(['/loans/my']);
        return;
      }
    }

    this.loadDocumentsStatus();
    if (status === 'Aprovado' && role === 'Cliente') {
      this.loadEligibility();
    }
  }

  public loadDocumentsStatus(): void {
    this.isLoading = true;
    this.documentService.getDocumentsStatus().subscribe({
      next: (docs) => {
        this.isLoading = false;
        this.documents = docs;

        // Auto-detectar se possui empresa se houver documento do tipo FachadaEmpresa na lista
        const hasFachada = docs.some(d => d.tipoDocumento === 'FachadaEmpresa');
        if (hasFachada && !this.authService.possuiEmpresa()) {
          this.authService.setPossuiEmpresa(true);
        }
      },
      error: () => {
        this.isLoading = false;
      }
    });
  }

  public getDocStatus(tipo: 'Frente' | 'Verso' | 'Selfie' | 'FachadaEmpresa'): string | null {
    const doc = this.documents.find(d => d.tipoDocumento === tipo);
    return doc ? doc.status : null;
  }

  public onUploadSuccess(newStatus: string): void {
    // Atualiza status do usuário no AuthService
    this.authService.updateStatus(newStatus);
    // Recarrega lista de documentos
    this.loadDocumentsStatus();

    if (this.authService.userRole() === 'Cliente' && (newStatus === 'Aprovado' || newStatus === 'EmAnalise')) {
      this.router.navigate(['/loans/my']);
    }
  }

  public loadEligibility(): void {
    this.loanService.getEligibility().subscribe({
      next: (data) => {
        this.eligibility = data;
      },
      error: () => {}
    });
  }
}
