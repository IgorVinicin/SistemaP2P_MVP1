import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ProfileService, UserProfileDto } from '../../../../core/services/profile.service';
import { AuthService } from '../../../../core/services/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css'
})
export class ProfileComponent implements OnInit {
  private fb = inject(FormBuilder);
  private profileService = inject(ProfileService);
  public authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);

  public userProfile: UserProfileDto | null = null;
  public profileForm!: FormGroup;
  public isLoading = false;
  public isSaving = false;

  ngOnInit(): void {
    this.initForm();
    this.loadProfile();
  }

  private initForm(): void {
    this.profileForm = this.fb.group({
      telefone: ['', [Validators.required, Validators.minLength(8)]]
    });
  }

  public loadProfile(): void {
    this.isLoading = true;
    this.profileService.getMyProfile().subscribe({
      next: (data) => {
        this.userProfile = data;
        this.isLoading = false;

        if (data.profile) {
          this.profileForm.patchValue({
            telefone: data.profile.telefone || ''
          });
        }
      },
      error: () => {
        this.isLoading = false;
        this.snackBar.open('Erro ao carregar dados do perfil.', 'Fechar', { duration: 4000 });
      }
    });
  }

  public saveProfile(): void {
    if (this.profileForm.invalid) {
      this.snackBar.open('Por favor, informe um telefone válido.', 'Fechar', { duration: 3000 });
      return;
    }

    this.isSaving = true;
    this.profileService.updateMyProfile(this.profileForm.value).subscribe({
      next: (res) => {
        this.isSaving = false;
        this.snackBar.open(res.message || 'Telefone atualizado com sucesso!', 'Fechar', { duration: 3500 });
        this.loadProfile();
      },
      error: (err) => {
        this.isSaving = false;
        const msg = err.error?.message || 'Erro ao salvar alterações do perfil.';
        this.snackBar.open(msg, 'Fechar', { duration: 4000 });
      }
    });
  }
}
