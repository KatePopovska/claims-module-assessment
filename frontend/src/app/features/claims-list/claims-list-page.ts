import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { PageHeader } from '../../shared/page-header/page-header';

@Component({
  selector: 'app-claims-list-page',
  imports: [PageHeader, MatButtonModule, MatIconModule, RouterLink],
  template: `
    <app-page-header title="Claims" subtitle="Review, filter and manage First Notice of Loss claims.">
      <a headerActions mat-flat-button routerLink="/claims/new"><mat-icon>add</mat-icon>Log New Claim</a>
    </app-page-header>
    <section class="app-card app-card-body app-muted">The claims dashboard is implemented in the next phase.</section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimsListPage {}
