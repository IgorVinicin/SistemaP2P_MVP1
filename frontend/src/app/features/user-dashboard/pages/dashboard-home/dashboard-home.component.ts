import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { DocumentUploadCardComponent } from '../../components/document-upload-card/document-upload-card.component';
import { AuthService } from '../../../../core/services/auth.service';
import { DocumentService } from '../../../../core/services/document.service';
import { DocumentStatusDto } from '../../../../core/models/document.models';

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    DocumentUploadCardComponent
  ],
  templateUrl: './dashboard-home.component.html',
  styleUrl: './dashboard-home.component.css'
})
export class DashboardHomeComponent implements OnInit {
  public authService = inject(AuthService);
  private documentService = inject(DocumentService);

  public documents: DocumentStatusDto[] = [];
  public isLoading = false;

  public ngOnInit(): void {
    this.loadDocumentsStatus();
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
  }
}
