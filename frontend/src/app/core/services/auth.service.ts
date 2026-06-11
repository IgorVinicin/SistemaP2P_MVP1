import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { LoginDto, RegisterDto, AuthResponseDto } from '../models/auth.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private apiUrl = `${environment.apiUrl}/auth`;

  // Signals para estado do usuário
  private _currentUser = signal<AuthResponseDto | null>(null);
  private _possuiEmpresa = signal<boolean>(false);

  public currentUser = computed(() => this._currentUser());
  public isAuthenticated = computed(() => !!this._currentUser()?.token);
  public userRole = computed(() => this._currentUser()?.role || '');
  public userStatus = computed(() => this._currentUser()?.status || 'Pendente');
  public possuiEmpresa = computed(() => this._possuiEmpresa());

  constructor(private http: HttpClient) {
    this.loadSession();
  }

  private loadSession(): void {
    const token = localStorage.getItem('token');
    const role = localStorage.getItem('role');
    const status = localStorage.getItem('status');
    const email = localStorage.getItem('email');
    const userId = localStorage.getItem('userId');
    const possuiEmpresa = localStorage.getItem('possuiEmpresa') === 'true';

    if (token && role && status && email && userId) {
      this._currentUser.set({
        token,
        role,
        status,
        email,
        userId
      });
      this._possuiEmpresa.set(possuiEmpresa);
    }
  }

  public login(dto: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.apiUrl}/login`, dto).pipe(
      tap(response => {
        this.saveSession(response);
      })
    );
  }

  public register(dto: RegisterDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.apiUrl}/register`, dto).pipe(
      tap(response => {
        this.saveSession(response);
        this.setPossuiEmpresa(dto.possuiEmpresa);
      })
    );
  }

  public logout(): void {
    localStorage.removeItem('token');
    localStorage.removeItem('role');
    localStorage.removeItem('status');
    localStorage.removeItem('email');
    localStorage.removeItem('userId');
    localStorage.removeItem('possuiEmpresa');
    this._currentUser.set(null);
    this._possuiEmpresa.set(false);
  }

  public setPossuiEmpresa(value: boolean): void {
    localStorage.setItem('possuiEmpresa', value ? 'true' : 'false');
    this._possuiEmpresa.set(value);
  }

  private saveSession(response: AuthResponseDto): void {
    localStorage.setItem('token', response.token);
    localStorage.setItem('role', response.role);
    localStorage.setItem('status', response.status);
    localStorage.setItem('email', response.email);
    localStorage.setItem('userId', response.userId);
    this._currentUser.set(response);
  }

  // Permite atualizar o status em cache quando o upload de documentos atualiza o status do user
  public updateStatus(newStatus: string): void {
    const current = this._currentUser();
    if (current) {
      const updated = { ...current, status: newStatus };
      this.saveSession(updated);
    }
  }
}
