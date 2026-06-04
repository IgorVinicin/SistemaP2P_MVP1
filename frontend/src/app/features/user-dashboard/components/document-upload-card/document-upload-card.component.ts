import { Component, Input, Output, EventEmitter, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { DocumentService } from '../../../../core/services/document.service';

@Component({
  selector: 'app-document-upload-card',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './document-upload-card.component.html',
  styleUrl: './document-upload-card.component.css'
})
export class DocumentUploadCardComponent {
  private documentService = inject(DocumentService);
  private snackBar = inject(MatSnackBar);

  @Input() public tipoDocumento!: string;
  @Input() public label!: string;
  @Input() public currentStatus: string | null = null; // 'Pendente' | 'Aprovado' | 'Reprovado' | 'Nenhum' (null)

  @Output() public uploadSuccess = new EventEmitter<string>(); // emite o novo userStatus

  public selectedFile: File | null = null;
  public isUploading = false;

  public onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      // Validar tipo simples: apenas imagens ou PDF
      const allowedTypes = ['image/jpeg', 'image/png', 'application/pdf'];
      if (!allowedTypes.includes(file.type)) {
        this.snackBar.open('Formato inválido. Apenas JPG, PNG ou PDF.', 'Fechar', { duration: 4000 });
        this.selectedFile = null;
        return;
      }
      this.selectedFile = file;
    }
  }

  public upload(): void {
    if (!this.selectedFile) return;

    this.isUploading = true;
    this.documentService.uploadDocument(this.selectedFile, this.tipoDocumento).subscribe({
      next: (res) => {
        this.isUploading = false;
        this.selectedFile = null;
        this.currentStatus = 'Pendente'; // backend reseta para Pendente após re-upload
        this.snackBar.open(`Documento "${this.label}" enviado com sucesso!`, 'Fechar', { duration: 3000 });
        this.uploadSuccess.emit(res.userStatus);
      },
      error: (err) => {
        this.isUploading = false;
        const msg = err.error?.message || 'Erro ao enviar documento. Tente novamente.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }
}
