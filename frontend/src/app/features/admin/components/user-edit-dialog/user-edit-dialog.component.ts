import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { AdminService } from '../../../../core/services/admin.service';
import { UserDetailDto } from '../../../../core/models/user.models';

export interface UserEditDialogData {
  userId: string;
}

@Component({
  selector: 'app-user-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatDividerModule,
    MatSnackBarModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './user-edit-dialog.component.html',
  styleUrl: './user-edit-dialog.component.css'
})
export class UserEditDialogComponent implements OnInit {
  private adminService = inject(AdminService);
  private snackBar = inject(MatSnackBar);

  public userDetail: UserDetailDto | null = null;
  public isLoading = false;
  public isProcessing = false;

  // Form Fields
  public editForm = {
    nome: '',
    cpf: '',
    telefone: '',
    dataNascimento: '',
    status: ''
  };

  // Score Fields
  public novoScore: number = 0;
  public motivoAlteracao: string = '';

  constructor(
    public dialogRef: MatDialogRef<UserEditDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: UserEditDialogData
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
          this.editForm.nome = detail.profile.nome;
          this.editForm.cpf = detail.profile.cpf;
          this.editForm.telefone = detail.profile.telefone;
          this.editForm.dataNascimento = detail.profile.dataNascimento ? detail.profile.dataNascimento.substring(0, 10) : '';
        }
        this.editForm.status = detail.status;
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar detalhes do usuário.', 'Fechar', { duration: 4000 });
        this.dialogRef.close();
      }
    });
  }

  public saveProfile(): void {
    if (!this.userDetail) return;
    this.isProcessing = true;

    this.adminService.updateUserProfile(this.data.userId, this.editForm).subscribe({
      next: () => {
        this.adminService.updateUserStatus(this.data.userId, this.editForm.status).subscribe({
          next: () => {
            this.isProcessing = false;
            this.snackBar.open('Perfil atualizado com sucesso!', 'Fechar', { duration: 3000 });
            this.loadUserDetail();
          },
          error: (err) => {
            this.isProcessing = false;
            this.snackBar.open(err.error?.message || 'Erro ao atualizar status do usuário.', 'Fechar', { duration: 4000 });
          }
        });
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao atualizar dados cadastrais.', 'Fechar', { duration: 4000 });
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
      next: () => {
        this.isProcessing = false;
        this.snackBar.open('Score atualizado com sucesso!', 'Fechar', { duration: 3000 });
        this.motivoAlteracao = '';
        this.loadUserDetail();
      },
      error: (err) => {
        this.isProcessing = false;
        this.snackBar.open(err.error?.message || 'Erro ao atualizar score.', 'Fechar', { duration: 4500 });
      }
    });
  }
}
