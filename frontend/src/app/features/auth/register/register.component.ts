import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSnackBarModule
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css'
})
export class RegisterComponent implements OnInit {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private snackBar = inject(MatSnackBar);

  public registerForm!: FormGroup;
  public isLoading = false;

  public ngOnInit(): void {
    this.registerForm = this.fb.group({
      nome: ['', Validators.required],
      cpf: ['', [Validators.required, Validators.pattern(/^\d{11}$/)]], // 11 dígitos numéricos
      telefone: ['', Validators.required],
      dataNascimento: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      senha: ['', [Validators.required, Validators.minLength(4)]],
      role: ['Cliente', Validators.required], // Cliente ou Investidor
      possuiEmpresa: [false],
      razaoSocial: [''],
      nomeFantasia: [''],
      cnpj: [''],
      tempoEmpresa: [null],
      faturamentoMensal: [null]
    });

    // Validadores condicionais baseados no toggle possuiEmpresa
    this.registerForm.get('possuiEmpresa')?.valueChanges.subscribe(possui => {
      const fields = ['razaoSocial', 'cnpj', 'tempoEmpresa', 'faturamentoMensal'];
      fields.forEach(field => {
        const control = this.registerForm.get(field);
        if (possui) {
          if (field === 'tempoEmpresa' || field === 'faturamentoMensal') {
            control?.setValidators([Validators.required, Validators.min(0)]);
          } else if (field === 'cnpj') {
            control?.setValidators([Validators.required, Validators.pattern(/^\d{14}$/)]); // 14 dígitos numéricos
          } else {
            control?.setValidators([Validators.required]);
          }
        } else {
          control?.clearValidators();
          control?.setValue(null);
        }
        control?.updateValueAndValidity();
      });
    });
  }

  public onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isLoading = true;
    const formValue = { ...this.registerForm.value };
    
    // Garantir que a data esteja formatada no padrão que o backend C# espera (ISO string)
    formValue.dataNascimento = new Date(formValue.dataNascimento).toISOString();

    this.authService.register(formValue).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.snackBar.open('Cadastro efetuado com sucesso!', 'Fechar', { duration: 3000 });
        
        if (response.role === 'Administrador') {
          this.router.navigate(['/admin']);
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        const msg = err.error?.message || 'Erro ao efetuar cadastro. Tente novamente.';
        this.snackBar.open(msg, 'Fechar', { duration: 5000 });
      }
    });
  }
}
