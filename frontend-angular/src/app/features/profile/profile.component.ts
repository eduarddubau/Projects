import {
  Component,
  computed,
  inject,
  signal,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  DestroyRef,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSnackBar } from '@angular/material/snack-bar';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ProfileService } from '@core/services/profile.service';
import { AuthService } from '@core/services/auth.service';
import { LANGUAGES, Lang, LanguageService } from '@core/services/language.service';
import { ThemeService, Theme } from '@core/services/theme.service';
import { fullNameOf, initialsOf } from '@core/utils/person';
import {
  PaletteService,
  PALETTES,
  PALETTE_PREVIEWS,
  Palette,
} from '@core/services/palette.service';
import { AuroraComponent } from '@shared/aurora/aurora.component';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    DatePipe,
    AuroraComponent,
    TranslocoDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProfileComponent {
  private profileService = inject(ProfileService);
  private authService = inject(AuthService);
  private fb = inject(FormBuilder);
  private snackBar = inject(MatSnackBar);
  private cdr = inject(ChangeDetectorRef);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private languageService = inject(LanguageService);
  private themeService = inject(ThemeService);
  private paletteService = inject(PaletteService);

  /** Locale for the date pipes; 'ro' locale data is registered in provideI18n. */
  dateLocale = computed(() => (this.languageService.lang() === 'ro' ? 'ro' : 'en-US'));

  // Appearance settings mirror the header controls, gathered in one place.
  theme = this.themeService.theme;
  lang = this.languageService.lang;
  languages = LANGUAGES;
  palette = this.paletteService.palette;
  schemes = PALETTES.map((id) => ({ id, preview: PALETTE_PREVIEWS[id] }));

  isEditing = signal(false);
  isSaving = signal(false);
  profile = this.profileService.myProfile();

  isAdmin = this.authService.isAdmin;

  // hasValue(), not value(): a resource in an error state throws when read.
  fullName = computed(() => (this.profile.hasValue() ? fullNameOf(this.profile.value()) : ''));

  initials = computed(() => (this.profile.hasValue() ? initialsOf(this.profile.value()) : ''));

  form = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(50)]],
    lastName: ['', [Validators.required, Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    nickname: ['', [Validators.maxLength(30)]],
  });

  setPalette(palette: Palette): void {
    this.paletteService.set(palette);
  }

  setTheme(theme: Theme): void {
    this.themeService.set(theme);
  }

  setLanguage(lang: Lang): void {
    void this.languageService.set(lang);
  }

  startEdit(): void {
    if (!this.profile.hasValue()) return;
    const profile = this.profile.value();

    this.form.reset({
      firstName: profile.firstName,
      lastName: profile.lastName,
      email: profile.email,
      nickname: profile.nickname ?? '',
    });
    this.isEditing.set(true);
  }

  cancelEdit(): void {
    this.isEditing.set(false);
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving.set(true);
    const { firstName, lastName, email, nickname } = this.form.getRawValue();

    this.profileService
      .updateProfile({ firstName, lastName, email, nickname: nickname.trim() || null })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.profile.set(updated);
          this.isEditing.set(false);
          this.isSaving.set(false);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate('profile.notifications.updated'),
            this.transloco.translate('common.actions.close'),
            { duration: 3000 },
          );
          // The header reads the name from the JWT claims; a refresh re-issues
          // the token so the new name survives reloads too.
          this.authService.refresh().subscribe({
            error: () => {
              /* The save already succeeded; a stale token corrects on next refresh. */
            },
          });
        },
        error: () => {
          this.isSaving.set(false);
          this.cdr.markForCheck();
          this.snackBar.open(
            this.transloco.translate('profile.notifications.updateFailed'),
            this.transloco.translate('common.actions.close'),
            { duration: 5000 },
          );
        },
      });
  }
}
