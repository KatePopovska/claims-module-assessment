import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export interface PromptDialogData {
  title: string;
  message?: string;
  label: string;
  confirmLabel: string;
  maxLength: number;
}

@Component({
  selector: 'app-prompt-dialog',
  imports: [ReactiveFormsModule, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      @if (data.message) {
        <p>{{ data.message }}</p>
      }
      <mat-form-field class="field">
        <mat-label>{{ data.label }}</mat-label>
        <textarea matInput [formControl]="text" rows="3" cdkFocusInitial></textarea>
        <mat-hint align="end">{{ text.value.length }} / {{ data.maxLength }}</mat-hint>
        @if (text.hasError('required')) {
          <mat-error>{{ data.label }} is required.</mat-error>
        } @else if (text.hasError('maxlength')) {
          <mat-error>Limited to {{ data.maxLength }} characters.</mat-error>
        }
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" (click)="confirm()">{{ data.confirmLabel }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    .field {
      width: 100%;
      min-width: 420px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PromptDialog {
  private readonly dialogRef = inject(MatDialogRef<PromptDialog, string>);
  protected readonly data = inject<PromptDialogData>(MAT_DIALOG_DATA);
  protected readonly text = new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(this.data.maxLength)] });

  protected confirm(): void {
    const value = this.text.value.trim();
    if (this.text.invalid || value.length === 0) {
      this.text.setValue(value);
      this.text.markAsTouched();
      return;
    }

    this.dialogRef.close(value);
  }
}
