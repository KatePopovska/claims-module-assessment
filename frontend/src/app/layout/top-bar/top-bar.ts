import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { initials } from '../../core/auth/mock-users';

@Component({
  selector: 'app-top-bar',
  imports: [ReactiveFormsModule, MatButtonModule, MatIconModule, MatMenuModule],
  templateUrl: './top-bar.html',
  styleUrl: './top-bar.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TopBar {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly initials = initials;

  protected submitSearch(): void {
    const term = this.search.value.trim();
    this.router.navigate(['/claims'], { queryParams: { search: term || null, page: null }, queryParamsHandling: 'merge' });
    this.search.setValue('');
  }

  protected switchUser(userId: string): void {
    this.auth.switchUser(userId);
  }
}
