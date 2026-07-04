import {
  Component, computed, inject, signal, OnInit,
  ChangeDetectionStrategy, ChangeDetectorRef, DestroyRef
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ProjectService } from '@core/services/project.service';
import { LanguageService } from '@core/services/language.service';
import { Project } from '@core/models/project';
import { ConfirmDialogComponent } from '@shared/confirm-dialog/confirm-dialog.component';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-project-detail',
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    DatePipe,
    AuroraComponent,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProjectDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private fb = inject(FormBuilder);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private languageService = inject(LanguageService);

  /** Locale for the date:'medium' pipes; 'ro' locale data is registered in provideI18n. */
  dateLocale = computed(() => this.languageService.lang() === 'ro' ? 'ro' : 'en-US');

  isLoading = signal(true);
  hasError = signal(false);
  isEditing = signal(false);
  isSaving = signal(false);
  project = signal<Project | null>(null);

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)]
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.hasError.set(true);
      this.isLoading.set(false);
      return;
    }

    this.projectService.getMyProjectById(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => {
          this.project.set(project);
          this.isLoading.set(false);
          this.cdr.markForCheck();
        },
        error: () => {
          this.hasError.set(true);
          this.isLoading.set(false);
          this.cdr.markForCheck();
        }
      });
  }

  startEdit(): void {
    const project = this.project();
    if (!project) return;

    this.form.reset({ name: project.name, description: project.description ?? '' });
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
  }

  confirmDelete(project: Project): void {
    this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: this.transloco.translate('projects.confirmDelete.title'),
        message: this.transloco.translate('projects.confirmDelete.message', { name: project.name }),
        confirmLabel: this.transloco.translate('common.actions.delete'),
        warn: true
      }
    })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteMyProject(project.id).subscribe({
          next: () => {
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleted'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
            this.router.navigate(['/projects']);
          },
          error: () => this.snackBar.open(
            this.transloco.translate('projects.notifications.deleteFailed'),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          )
        });
      });
  }

  save(): void {
    const project = this.project();
    if (!project || this.form.invalid) return;

    this.isSaving.set(true);
    const { name, description } = this.form.getRawValue();

    this.projectService.updateProject(project.id, { name, description: description || undefined })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.project.set(updated);
          this.isEditing.set(false);
          this.isSaving.set(false);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate('projects.notifications.updated'),
            this.transloco.translate('common.actions.close'),
            { duration: 3000 },
          );
        },
        error: () => {
          this.isSaving.set(false);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate('projects.notifications.updateFailed'),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          );
        }
      });
  }
}
