import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PageHeader } from '../../shared/page-header/page-header';

@Component({
  selector: 'app-claim-detail-page',
  imports: [PageHeader],
  template: `
    <app-page-header title="Claim detail" [breadcrumbs]="[{ label: 'Claims', link: '/claims' }, { label: id() }]" />
    <section class="app-card app-card-body app-muted">The claim detail screen is implemented in a later phase.</section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClaimDetailPage {
  readonly id = input.required<string>();
}
