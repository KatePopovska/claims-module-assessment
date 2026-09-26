import { Routes } from '@angular/router';
import { Shell } from './layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'claims' },
      { path: 'claims/new', loadChildren: () => import('./features/fnol/fnol.routes').then((m) => m.FNOL_ROUTES) },
      { path: 'claims/:id', loadChildren: () => import('./features/claim-detail/claim-detail.routes').then((m) => m.CLAIM_DETAIL_ROUTES) },
      { path: 'claims', loadChildren: () => import('./features/claims-list/claims-list.routes').then((m) => m.CLAIMS_LIST_ROUTES) },
    ],
  },
  { path: '**', redirectTo: 'claims' },
];
