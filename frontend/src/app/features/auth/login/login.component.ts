import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule,
    MatIconModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css'
})
export class LoginComponent {
  public currentYear = new Date().getFullYear();
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  public loginForm: FormGroup = this.fb.group({
    email: ['', [Validators.required]],
    senha: ['', [Validators.required, Validators.minLength(4)]]
  });

  public isLoading = false;

  public onSubmit(): void {
    if (this.loginForm.invalid) return;

    this.isLoading = true;
    this.authService.login(this.loginForm.value).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.snackBar.open('Login realizado com sucesso!', 'Fechar', { duration: 3000 });
        
        if (response.role === 'Administrador') {
          this.router.navigate(['/admin']);
        } else if (response.role === 'Investidor') {
          if (response.status === 'Aprovado') {
            this.router.navigate(['/investor/dashboard']);
          } else if (response.status === 'EmAnalise') {
            this.router.navigate(['/investor/aguardando-aprovacao']);
          } else if (response.status === 'Reprovado') {
            this.router.navigate(['/investor/reprovado']);
          } else {
            // Pendente
            this.router.navigate(['/dashboard']);
          }
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        const msg = err.error?.message || 'Erro ao realizar login. Tente novamente.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }
}
