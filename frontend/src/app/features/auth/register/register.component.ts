import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { BrazilianValidators } from '../../../core/validators/brazilian-validators';

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
  private route = inject(ActivatedRoute);

  public registerForm!: FormGroup;
  public isLoading = false;
  public isRolePredefined = false;

  public ngOnInit(): void {
    this.registerForm = this.fb.group({
      nome: ['', Validators.required],
      cpf: ['', [Validators.required, BrazilianValidators.cpf()]],
      telefone: ['', [Validators.required, Validators.pattern(/^\(\d{2}\) \d{4,5}-\d{4}$/)]],
      dataNascimento: ['', [Validators.required, BrazilianValidators.maioridade()]],
      email: ['', [Validators.required, Validators.email]],
      senha: ['', [Validators.required, Validators.minLength(4)]],
      role: ['Cliente', Validators.required], // Cliente ou Investidor
      valorInicialInvestimento: [null],
      possuiEmpresa: [false],
      razaoSocial: [''],
      nomeFantasia: [''],
      cnpj: [''],
      tempoEmpresa: [null],
      faturamentoMensal: [null]
    });

    // Validadores condicionais baseados na role (Investidor usa CPF e não exige e-mail ou dados de empresa)
    this.registerForm.get('role')?.valueChanges.subscribe(role => {
      const emailControl = this.registerForm.get('email');
      const empresaControl = this.registerForm.get('possuiEmpresa');
      if (role === 'Investidor') {
        emailControl?.clearValidators();
        emailControl?.setValue('');
        empresaControl?.setValue(false);
      } else {
        emailControl?.setValidators([Validators.required, Validators.email]);
      }
      emailControl?.updateValueAndValidity();
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
            control?.setValidators([Validators.required, BrazilianValidators.cnpj()]);
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

    const routeRole = this.route.snapshot.data['role'];
    if (routeRole) {
      this.isRolePredefined = true;
      this.registerForm.patchValue({ role: routeRole });
      if (routeRole === 'Investidor') {
        this.registerForm.get('email')?.clearValidators();
        this.registerForm.get('email')?.updateValueAndValidity();
        this.registerForm.get('possuiEmpresa')?.setValue(false);
      }
    }
  }

  public onSubmit(): void {
    if (this.registerForm.invalid) return;

    this.isLoading = true;
    const formValue = { ...this.registerForm.value };
    
    // Limpar máscaras antes de enviar para o backend
    formValue.cpf = formValue.cpf.replace(/\D/g, '');
    formValue.telefone = formValue.telefone.replace(/\D/g, '');
    if (formValue.cnpj) {
      formValue.cnpj = formValue.cnpj.replace(/\D/g, '');
    }

    if (formValue.role === 'Investidor' && !formValue.email) {
      formValue.email = formValue.cpf;
    }

    // Se investidor informou valor inicial, validar formato
    if (formValue.role === 'Investidor' && formValue.valorInicialInvestimento !== null && formValue.valorInicialInvestimento !== undefined && formValue.valorInicialInvestimento !== '') {
      const valor = Number(formValue.valorInicialInvestimento);
      if (isNaN(valor) || valor <= 0) {
        this.snackBar.open('O valor a investir deve ser um número positivo e maior que zero.', 'Fechar', { duration: 4000 });
        this.isLoading = false;
        return;
      }
      formValue.valorInicialInvestimento = Math.round(valor * 100) / 100;
    }

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

  public onCpfInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');
    if (value.length > 11) value = value.substring(0, 11);
    
    let formatted = value;
    if (value.length > 9) {
      formatted = `${value.substring(0, 3)}.${value.substring(3, 6)}.${value.substring(6, 9)}-${value.substring(9)}`;
    } else if (value.length > 6) {
      formatted = `${value.substring(0, 3)}.${value.substring(3, 6)}.${value.substring(6)}`;
    } else if (value.length > 3) {
      formatted = `${value.substring(0, 3)}.${value.substring(3)}`;
    }
    
    this.registerForm.get('cpf')?.setValue(formatted, { emitEvent: false });
  }

  public onTelefoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');
    if (value.length > 11) value = value.substring(0, 11);
    
    let formatted = value;
    if (value.length > 10) {
      formatted = `(${value.substring(0, 2)}) ${value.substring(2, 7)}-${value.substring(7)}`;
    } else if (value.length > 6) {
      formatted = `(${value.substring(0, 2)}) ${value.substring(2, 6)}-${value.substring(6)}`;
    } else if (value.length > 2) {
      formatted = `(${value.substring(0, 2)}) ${value.substring(2)}`;
    } else if (value.length > 0) {
      formatted = `(${value}`;
    }
    
    this.registerForm.get('telefone')?.setValue(formatted, { emitEvent: false });
  }

  public onCnpjInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    let value = input.value.replace(/\D/g, '');
    if (value.length > 14) value = value.substring(0, 14);
    
    let formatted = value;
    if (value.length > 12) {
      formatted = `${value.substring(0, 2)}.${value.substring(2, 5)}.${value.substring(5, 8)}/${value.substring(8, 12)}-${value.substring(12)}`;
    } else if (value.length > 8) {
      formatted = `${value.substring(0, 2)}.${value.substring(2, 5)}.${value.substring(5, 8)}/${value.substring(8)}`;
    } else if (value.length > 5) {
      formatted = `${value.substring(0, 2)}.${value.substring(2, 5)}.${value.substring(5)}`;
    } else if (value.length > 2) {
      formatted = `${value.substring(0, 2)}.${value.substring(2)}`;
    }
    
    this.registerForm.get('cnpj')?.setValue(formatted, { emitEvent: false });
  }
}
