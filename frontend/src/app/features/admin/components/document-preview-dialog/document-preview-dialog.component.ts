import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

export interface DocumentPreviewData {
  blob: Blob;
  tipoDocumento: string;
  fileName: string;
}

@Component({
  selector: 'app-document-preview-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule
  ],
  templateUrl: './document-preview-dialog.component.html',
  styleUrl: './document-preview-dialog.component.css'
})
export class DocumentPreviewDialogComponent implements OnInit {
  public safeUrl!: SafeResourceUrl;
  public isPdf = false;

  constructor(
    private sanitizer: DomSanitizer,
    public dialogRef: MatDialogRef<DocumentPreviewDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: DocumentPreviewData
  ) {}

  public ngOnInit(): void {
    const objectUrl = URL.createObjectURL(this.data.blob);
    this.safeUrl = this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl);
    
    // Identifica se é PDF pelo tipo MIME ou extensão
    this.isPdf = this.data.blob.type === 'application/pdf' || this.data.fileName.toLowerCase().endsWith('.pdf');
  }

  public close(): void {
    this.dialogRef.close();
  }
}
