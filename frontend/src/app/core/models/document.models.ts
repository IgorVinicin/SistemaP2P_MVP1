export interface DocumentStatusDto {
  id: string;
  tipoDocumento: 'Frente' | 'Verso' | 'Selfie' | 'FachadaEmpresa';
  status: 'Pendente' | 'Aprovado' | 'Reprovado';
  createdAt: string;
}

export interface UploadResponseDto {
  message: string;
  tipoDocumento: string;
  caminhoLocal: string;
  userStatus: string;
}
