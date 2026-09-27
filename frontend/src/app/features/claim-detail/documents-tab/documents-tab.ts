import { DatePipe } from '@angular/common';
import { HttpEventType } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, ElementRef, inject, input, OnInit, output, signal, viewChild } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { DocumentsApi } from '../../../api/documents-api.service';
import { ClaimDetail, ClaimDocument } from '../../../api/models';
import { userDisplayName } from '../../../core/auth/mock-users';
import { NotificationService } from '../../../core/notifications/notification.service';
import { fileSize } from '../../../shared/format';

export const MAX_UPLOAD_BYTES = 50 * 1024 * 1024;
export const ALLOWED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png', '.docx', '.xlsx', '.txt', '.csv'];
export const DOCUMENT_TYPES = ['Police Report', 'Photo', 'Estimate', 'Invoice', 'Medical Report', 'Correspondence', 'Other'];

export function uploadValidationError(file: File): string | null {
  const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
  if (!ALLOWED_EXTENSIONS.includes(extension)) {
    return `${file.name}: only PDF, JPEG, PNG, DOCX, XLSX, TXT and CSV files can be uploaded.`;
  }

  return file.size > MAX_UPLOAD_BYTES ? `${file.name} is larger than the 50 MB limit.` : null;
}

function documentIcon(document: ClaimDocument): string {
  if (document.contentType.startsWith('image/')) {
    return 'image';
  }

  return document.contentType === 'application/pdf' ? 'picture_as_pdf' : 'description';
}

@Component({
  selector: 'app-documents-tab',
  imports: [DatePipe, ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatProgressBarModule, MatSelectModule, MatTooltipModule],
  templateUrl: './documents-tab.html',
  styleUrl: './documents-tab.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DocumentsTab implements OnInit {
  private readonly documentsApi = inject(DocumentsApi);
  private readonly notifications = inject(NotificationService);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  readonly claim = input.required<ClaimDetail>();
  readonly changed = output<void>();

  protected readonly documents = signal<ClaimDocument[]>([]);
  protected readonly loading = signal(true);
  protected readonly uploading = signal<{ name: string; percent: number } | null>(null);
  protected readonly documentType = new FormControl('Other', { nonNullable: true });
  protected readonly documentTypes = DOCUMENT_TYPES;
  protected readonly accept = ALLOWED_EXTENSIONS.join(',');
  protected readonly fileSize = fileSize;
  protected readonly icon = documentIcon;
  protected readonly userName = userDisplayName;

  ngOnInit(): void {
    this.load();
  }

  protected chooseFile(): void {
    this.fileInput().nativeElement.click();
  }

  protected onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    const error = uploadValidationError(file);
    if (error) {
      this.notifications.error(error);
      return;
    }

    this.uploading.set({ name: file.name, percent: 0 });
    this.documentsApi.upload(this.claim().id, file, this.documentType.value, null).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress) {
          this.uploading.set({ name: file.name, percent: event.total ? Math.round((event.loaded / event.total) * 100) : 0 });
        } else if (event.type === HttpEventType.Response) {
          this.uploading.set(null);
          this.notifications.success(`${file.name} uploaded.`);
          this.load();
          this.changed.emit();
        }
      },
      error: () => this.uploading.set(null),
    });
  }

  protected open(document: ClaimDocument): void {
    const expiresAt = document.downloadUrlExpiresAt ? new Date(document.downloadUrlExpiresAt).getTime() : null;
    if (expiresAt === null || expiresAt - Date.now() > 60_000) {
      window.open(document.downloadUrl, '_blank', 'noopener');
      return;
    }

    const tab = window.open('', '_blank');
    this.documentsApi.list(this.claim().id).subscribe({
      next: (documents) => {
        this.documents.set(documents);
        const fresh = documents.find((d) => d.id === document.id);
        if (tab && fresh) {
          tab.opener = null;
          tab.location.href = fresh.downloadUrl;
        } else {
          tab?.close();
        }
      },
      error: () => tab?.close(),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.documentsApi.list(this.claim().id).subscribe({
      next: (documents) => {
        this.documents.set(documents);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
