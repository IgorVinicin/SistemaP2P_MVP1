import { Component, inject, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { RouterOutlet, RouterLink, Router, RouterLinkActive, NavigationEnd } from '@angular/router';
import { CommonModule } from '@angular/common';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { BreakpointObserver } from '@angular/cdk/layout';
import { AuthService } from '../../../core/services/auth.service';
import { Subject, takeUntil, filter } from 'rxjs';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    CommonModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatSidenavModule,
    MatListModule,
    MatDividerModule
  ],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.css'
})
export class LayoutComponent implements OnInit, OnDestroy {
  public authService = inject(AuthService);
  private router = inject(Router);
  private breakpointObserver = inject(BreakpointObserver);
  private destroy$ = new Subject<void>();

  @ViewChild('sidenav') sidenav!: MatSidenav;

  public isMobile = false;
  public sidenavOpened = false;

  public get isInvestidor(): boolean {
    return this.authService.userRole() === 'Investidor';
  }

  ngOnInit(): void {
    this.breakpointObserver
      .observe(['(max-width: 768px)'])
      .pipe(takeUntil(this.destroy$))
      .subscribe(result => {
        this.isMobile = result.matches;
        if (this.isMobile || this.isInvestidor) {
          this.sidenavOpened = false;
        } else {
          this.sidenavOpened = this.authService.isAuthenticated();
        }
      });

    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        if (this.isMobile && this.sidenav) {
          this.sidenav.close();
        }
        if (this.authService.isAuthenticated()) {
          this.authService.getMyStatus().subscribe({ error: () => {} });
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  public onNavItemClick(): void {
    if (this.isMobile && this.sidenav) {
      this.sidenav.close();
    }
  }

  public logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
