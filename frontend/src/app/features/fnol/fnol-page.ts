import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PageHeader } from '../../shared/page-header/page-header';

@Component({
  selector: 'app-fnol-page',
  imports: [PageHeader],
  template: `
    <app-page-header
      title="Log New Claim"
      subtitle="First Notice of Loss intake in three steps."
      [breadcrumbs]="[{ label: 'Claims', link: '/claims' }, { label: 'Log New Claim' }]"
    />
    <section class="app-card app-card-body app-muted">The FNOL intake form is implemented in a later phase.</section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FnolPage {}
