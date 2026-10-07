import { Component, Input, Output, EventEmitter, inject, OnDestroy, OnChanges, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
export class DocumentUploadCardComponent implements OnDestroy, OnChanges {
  private destroyRef = inject(DestroyRef);
  private documentService = inject(DocumentService);
  private snackBar = inject(MatSnackBar);

  @Input() public documentId: string | null = null;
  public savedPreviewUrl: string | null = null;
  public savedFileUrl: string | null = null;
  public previewError = '';

  ngOnChanges(): void {
    this.releaseSavedPreview();
    this.previewError = '';
    const id = this.documentId;
    if (!id) return;
    this.documentService.getDocumentFile(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: blob => {
        if (this.documentId !== id) return;
        this.releaseSavedPreview();
        this.savedFileUrl = URL.createObjectURL(blob);
        if (blob.type.startsWith('image/')) this.savedPreviewUrl = this.savedFileUrl;
      },
      error: () => this.previewError = 'Prévia indisponível. O status do envio permanece registrado.'
    });
  }

  private releaseSavedPreview(): void {
    if (this.savedFileUrl) URL.revokeObjectURL(this.savedFileUrl);
    this.savedFileUrl = null;
    this.savedPreviewUrl = null;
  }

  @Input() public tipoDocumento!: string;
  @Input() public label!: string;
  @Input() public currentStatus: string | null = null; // 'Pendente' | 'Aprovado' | 'Reprovado' | 'Nenhum' (null)

  @Output() public uploadSuccess = new EventEmitter<string>(); // emite o novo userStatus

  public selectedFile: File | null = null;
  public previewUrl: string | null = null;
  public isUploading = false;

  public async onFileSelected(event: any): Promise<void> {
    const file = event.target.files?.[0];
    if (!file) return;

    // Resetar o input para permitir selecionar novamente o mesmo arquivo se desejar
    event.target.value = '';

    this.clearSelection();
    if (file.size > 10 * 1024 * 1024) {
      this.snackBar.open('O arquivo deve ter no máximo 10 MB.', 'Fechar', { duration: 4000 });
      return;
    }
    const fileName = file.name.toLowerCase();
    const fileType = (file.type || '').toLowerCase();

    const isImage = fileType.startsWith('image/') || /\.(jpe?g|png|webp|heic|heif|bmp)$/i.test(fileName);
    const isPdf = fileType === 'application/pdf' || /\.pdf$/i.test(fileName);

    if (!isImage && !isPdf) {
      this.snackBar.open('Formato não suportado. Por favor, envie uma foto (JPG, PNG) ou documento PDF.', 'Fechar', { duration: 4000 });
      this.selectedFile = null;
      this.previewUrl = null;
      return;
    }

    if (isImage) {
      try {
        // Comprimir imagem para evitar estouro de memória no navegador móvel
        const compressed = await this.compressImage(file);
        this.selectedFile = compressed;
        this.previewUrl = URL.createObjectURL(compressed);
      } catch (e) {
        this.selectedFile = file;
        this.previewUrl = URL.createObjectURL(file);
      }
    } else {
      this.selectedFile = file;
      this.previewUrl = null;
    }
  }

  public clearSelection(): void {
    if (this.previewUrl) {
      URL.revokeObjectURL(this.previewUrl);
    }
    this.selectedFile = null;
    this.previewUrl = null;
  }

  ngOnDestroy(): void { this.clearSelection(); this.releaseSavedPreview(); }

  public formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  private compressImage(file: File): Promise<File> {
    return new Promise((resolve) => {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        const img = new Image();
        img.onload = () => {
          const canvas = document.createElement('canvas');
          let width = img.width;
          let height = img.height;

          const maxDimension = 1920;
          if (width > maxDimension || height > maxDimension) {
            if (width > height) {
              height = Math.round((height * maxDimension) / width);
              width = maxDimension;
            } else {
              width = Math.round((width * maxDimension) / height);
              height = maxDimension;
            }
          }

          canvas.width = width;
          canvas.height = height;

          const ctx = canvas.getContext('2d');
          if (!ctx) {
            resolve(file);
            return;
          }

          ctx.drawImage(img, 0, 0, width, height);

          canvas.toBlob(
            (blob) => {
              if (!blob) {
                resolve(file);
                return;
              }
              const compressedFile = new File([blob], file.name.replace(/\.[^/.]+$/, '.jpg'), {
                type: 'image/jpeg',
                lastModified: Date.now()
              });
              resolve(compressedFile);
            },
            'image/jpeg',
            0.85
          );
        };
        img.onerror = () => resolve(file);
        img.src = e.target.result;
      };
      reader.onerror = () => resolve(file);
      reader.readAsDataURL(file);
    });
  }

  public upload(): void {
    if (this.isUploading || !this.selectedFile) return;

    this.isUploading = true;
    this.documentService.uploadDocument(this.selectedFile, this.tipoDocumento).subscribe({
      next: (res) => {
        this.isUploading = false;
        this.clearSelection();
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
