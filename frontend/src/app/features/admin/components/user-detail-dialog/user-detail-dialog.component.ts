import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminService } from '../../../../core/services/admin.service';
import { UserDetailDto } from '../../../../core/models/user.models';
import { DocumentPreviewDialogComponent } from '../document-preview-dialog/document-preview-dialog.component';
import { AdminReportService } from '../../../../core/services/admin-report.service';
import { AdminReportPdfService } from '../../../../core/services/admin-report-pdf.service';

export interface UserDetailDialogData {
  userId: string;
}

@Component({
  selector: 'app-user-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatDividerModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './user-detail-dialog.component.html',
  styleUrl: './user-detail-dialog.component.css'
})
export class UserDetailDialogComponent implements OnInit {
  private adminService = inject(AdminService);
  private reportService = inject(AdminReportService);
  private reportPdfService = inject(AdminReportPdfService);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);

  public userDetail: UserDetailDto | null = null;
  public isLoading = false;
  public isProcessing = false;

  public novoScore: number = 0;
  public motivoAlteracao: string = '';

  public rentabilidade: number = 5.0; // Rentabilidade padrão sugerida (5.0% a.m.)
  public observacaoAdministrativa: string = '';

  constructor(
    public dialogRef: MatDialogRef<UserDetailDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: UserDetailDialogData
  ) {}

  public ngOnInit(): void {
    this.loadUserDetail();
  }

  public loadUserDetail(): void {
    this.isLoading = true;
    this.adminService.getUserById(this.data.userId).subscribe({
      next: (detail) => {
        this.isLoading = false;
        this.userDetail = detail;
        if (detail.profile) {
          this.novoScore = detail.profile.scoreAtual;
        }
        if (detail.rentabilidade !== undefined && detail.rentabilidade !== null && detail.rentabilidade > 0) {
          this.rentabilidade = detail.rentabilidade;
        } else {
          this.rentabilidade = 5.0;
        }
        if (detail.observacaoAdministrativa) {
          this.observacaoAdministrativa = detail.observacaoAdministrativa;
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar detalhes do usuário.', 'Fechar', { duration: 4000 });
        this.dialogRef.close();
      }
    });
  }

  public viewDocument(documentId: string, tipoDocumento: string): void {
    this.adminService.getDocumentFile(this.data.userId, documentId).subscribe({
      next: (blob) => {
        this.dialog.open(DocumentPreviewDialogComponent, {
          data: {
            blob,
            tipoDocumento,
            fileName: `${this.data.userId}_${tipoDocumento}`
          }
        });
      },
      error: () => {
        this.snackBar.open('Erro ao obter arquivo físico do documento.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public approve(): void {
    this.isProcessing = true;
    const obs = this.userDetail?.role === 'Investidor' ? this.observacaoAdministrativa : undefined;

    this.adminService.approveUser(this.data.userId, undefined, obs).subscribe({
      next: () => {
        this.isProcessing = false;
        const msg = this.userDetail?.status === 'Aprovado'
          ? 'Observação atualizada com sucesso!'
          : 'Usuário aprovado com sucesso!';
        this.snackBar.open(msg, 'Fechar', { duration: 3000 });
        this.dialogRef.close(true); // retorna true para indicar que houve alteração
      },
      error: (err) => {
        this.isProcessing = false;
        const msg = err.error?.message || (this.userDetail?.status === 'Aprovado' ? 'Erro ao atualizar dados.' : 'Erro ao aprovar usuário.');
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }

  public reprove(): void {
    this.isProcessing = true;
    this.adminService.reproveUser(this.data.userId).subscribe({
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Usuário reprovado com sucesso!', 'Fechar', { duration: 3000 });
        this.dialogRef.close(true); // retorna true para indicar que houve alteração
      },
      error: (err) => {
        this.isProcessing = false;
        const msg = err.error?.message || 'Erro ao reprovar usuário.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }

  public updateScore(): void {
    if (!this.userDetail || this.novoScore < 0 || this.novoScore > 1000 || !this.motivoAlteracao) {
      this.snackBar.open('Por favor, informe um score válido (0-1000) e o motivo da alteração.', 'Fechar', { duration: 4000 });
      return;
    }

    this.isProcessing = true;
    this.adminService.updateUserScore(this.data.userId, this.novoScore, this.motivoAlteracao).subscribe({
      next: (res) => {
        this.isProcessing = false;
        this.snackBar.open('Score atualizado com sucesso!', 'Fechar', { duration: 3000 });
        this.motivoAlteracao = '';
        this.loadUserDetail();
      },
      error: (err) => {
        this.isProcessing = false;
        const msg = err.error?.message || 'Erro ao atualizar score.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }

  public generateReport(): void {
    if (!this.userDetail) return;
    this.isProcessing = true;
    
    if (this.userDetail.role === 'Cliente') {
      this.reportService.getBorrowerIndividual(this.userDetail.id).subscribe({
        next: (res) => {
          this.reportPdfService.generateBorrowerIndividual(res);
          this.isProcessing = false;
        },
        error: () => {
          this.isProcessing = false;
          this.snackBar.open('Erro ao gerar relatório individual do tomador.', 'Fechar', { duration: 4000 });
        }
      });
    } else if (this.userDetail.role === 'Investidor') {
      this.reportService.getInvestorIndividual(this.userDetail.id).subscribe({
        next: (res) => {
          this.reportPdfService.generateInvestorIndividual(res);
          this.isProcessing = false;
        },
        error: () => {
          this.isProcessing = false;
          this.snackBar.open('Erro ao gerar relatório individual do investidor.', 'Fechar', { duration: 4000 });
        }
      });
    }
  }
}
