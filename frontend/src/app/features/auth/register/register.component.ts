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
      cpf: ['', [Validators.required, Validators.pattern(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/)]], // Máscara de CPF
      telefone: ['', [Validators.required, Validators.pattern(/^\(\d{2}\) \d{4,5}-\d{4}$/)]], // Máscara de Telefone
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
            control?.setValidators([Validators.required, Validators.pattern(/^\d{2}\.\d{3}\.\d{3}\/\d{4}-\d{2}$/)]); // Máscara de CNPJ
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
