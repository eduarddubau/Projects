import {
  Component,
  computed,
  inject,
  signal,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
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
import { MatMenuModule } from '@angular/material/menu';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { serverErrorKey } from '@core/i18n/server-error-keys';
import { ProjectService } from '@core/services/project.service';
import { WorkspaceContextService } from '@core/services/workspace-context.service';
import { LanguageService } from '@core/services/language.service';
import { Project } from '@core/models/project';
import { Workspace } from '@core/models/workspace';
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
    MatMenuModule,
    DatePipe,
    AuroraComponent,
    TranslocoDirective,
    TranslocoPipe,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectDetailComponent {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private projectService = inject(ProjectService);
  private workspaceContext = inject(WorkspaceContextService);
  private fb = inject(FormBuilder);
  private dialog = inject(MatDialog);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private languageService = inject(LanguageService);

  /** Locale for the date:'medium' pipes; 'ro' locale data is registered in provideI18n. */
  dateLocale = computed(() => (this.languageService.lang() === 'ro' ? 'ro' : 'en-US'));

  isEditing = signal(false);
  isSaving = signal(false);

  // Route params are read once here rather than watched: this route is only
  // reachable per-id, so the component is rebuilt when the id changes.
  private projectId = signal(this.route.snapshot.paramMap.get('id') ?? undefined);
  workspaceId = this.route.snapshot.paramMap.get('workspaceId');
  project = this.projectService.project(this.projectId);

  // Read off the project, not the URL: this route resolves a project by id alone,
  // so the workspace in the path is a display detail and can name a different one.
  isOwner = computed(() => {
    if (!this.project.hasValue()) return false;
    const holder = this.project.value().workspaceId;
    return this.workspaceContext.workspaces().find((w) => w.id === holder)?.myRole === 'Owner';
  });

  // Everywhere the caller belongs that isn't already holding it. Membership of the
  // target is all the API asks for; ownership is only required of the source.
  moveTargets = computed<Workspace[]>(() => {
    if (!this.project.hasValue()) return [];
    const holder = this.project.value().workspaceId;
    return this.workspaceContext.workspaces().filter((w) => w.id !== holder);
  });

  form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
  });

  startEdit(): void {
    if (!this.project.hasValue()) return;
    const project = this.project.value();

    this.form.reset({ name: project.name, description: project.description ?? '' });
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
  }

  confirmDelete(project: Project): void {
    this.dialog
      .open(ConfirmDialogComponent, {
        width: '400px',
        data: {
          title: this.transloco.translate('projects.confirmDelete.title'),
          message: this.transloco.translate('projects.confirmDelete.message', {
            name: project.name,
          }),
          confirmLabel: this.transloco.translate('common.actions.delete'),
          warn: true,
        },
      })
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) return;

        this.projectService.deleteProject(project.id).subscribe({
          next: () => {
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleted'),
              this.transloco.translate('common.actions.close'),
              { duration: 3000 },
            );
            this.router.navigate(['/w', this.workspaceId, 'projects']);
          },
          error: () =>
            this.snackBar.open(
              this.transloco.translate('projects.notifications.deleteFailed'),
              this.transloco.translate('common.actions.close'),
              { duration: 5000 },
            ),
        });
      });
  }

  moveTo(project: Project, target: Workspace): void {
    this.projectService
      .moveProject(project.id, target.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (moved) => {
          this.project.set(moved);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate('projects.notifications.moved', { name: moved.workspaceName }),
            this.transloco.translate('common.actions.close'),
            { duration: 3000 },
          );

          // Keep the URL honest: the path still names the workspace it came from.
          this.router.navigate(['/w', moved.workspaceId, 'projects', moved.id], {
            replaceUrl: true,
          });
        },
        error: (err) =>
          this.snackBar.open(
            this.transloco.translate(serverErrorKey(err, 'projects.notifications.moveFailed')),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          ),
      });
  }

  save(): void {
    if (!this.project.hasValue() || this.form.invalid) return;
    const project = this.project.value();

    this.isSaving.set(true);
    const { name, description } = this.form.getRawValue();

    this.projectService
      .updateProject(project.id, { name, description: description || undefined })
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
        error: (err) => {
          this.isSaving.set(false);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate(serverErrorKey(err, 'projects.notifications.updateFailed')),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          );
        },
      });
  }
}
