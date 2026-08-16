import { Component, inject, signal, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators,
  AbstractControl,
  ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatIcon } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { isRateLimited, throttleMessage } from '@core/utils/rate-limit';

/** Server-side field error: translation key when the Identity code is known, raw text otherwise. */
interface ServerFieldError {
  key: string | null;
  text: string;
}

// Known ASP.NET Identity error codes mapped to translated messages; the raw
// server text is kept as a fallback so unknown codes still surface something.
const IDENTITY_ERROR_KEYS: Record<string, string> = {
  DuplicateUserName: 'auth.register.serverErrors.duplicateEmail',
  DuplicateEmail: 'auth.register.serverErrors.duplicateEmail',
  InvalidEmail: 'auth.register.serverErrors.invalidEmail',
  PasswordTooShort: 'auth.register.serverErrors.passwordTooShort',
  PasswordRequiresUpper: 'auth.register.serverErrors.passwordRequiresUpper',
  PasswordRequiresLower: 'auth.register.serverErrors.passwordRequiresLower',
  PasswordRequiresDigit: 'auth.register.serverErrors.passwordRequiresDigit',
  PasswordRequiresNonAlphanumeric: 'auth.register.serverErrors.passwordRequiresNonAlphanumeric',
  PasswordRequiresUniqueChars: 'auth.register.serverErrors.passwordRequiresUniqueChars',
};

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatIcon,
    TranslocoDirective,
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);

  hidePassword = signal(true);
  hideConfirmPassword = signal(true);
  isLoading = signal(false);

  registerForm = this.fb.nonNullable.group(
    {
      firstName: ['', Validators.required],
      lastName: ['', Validators.required],
      email: ['', [Validators.required, Validators.email]],
      password: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/),
        ],
      ],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordMatchValidator },
  );

  constructor() {
    this.clearServerErrorOnChange('email');
    this.clearServerErrorOnChange('password');
  }

  serverError(controlName: string): ServerFieldError | null {
    return this.registerForm.get(controlName)?.getError('serverError') ?? null;
  }

  togglePassword(event: MouseEvent) {
    this.hidePassword.update((v) => !v);
    event.stopPropagation();
  }

  toggleConfirmPassword(event: MouseEvent) {
    this.hideConfirmPassword.update((v) => !v);
    event.stopPropagation();
  }

  onSubmit() {
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    const { confirmPassword, ...credentials } = this.registerForm.getRawValue();

    this.authService.register(credentials).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.snackBar.open(
          this.transloco.translate('auth.register.success'),
          this.transloco.translate('common.actions.close'),
          { duration: 3000 },
        );
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.handleBackendErrors(err);
      },
    });
  }

  private handleBackendErrors(err: HttpErrorResponse): void {
    // A throttled response carries an ErrorResponse, not the field-keyed Identity errors
    // the rest of this method unpacks, so it has to be handled before them.
    if (isRateLimited(err)) {
      const message = throttleMessage(err, this.transloco, 'auth.register');

      this.snackBar.open(message, this.transloco.translate('common.actions.close'), {
        duration: 5000,
      });
      return;
    }

    const errors: Record<string, string[]> = err.error ?? {};

    // Identity error codes that map to the email field
    const emailCodes = ['DuplicateUserName', 'DuplicateEmail', 'InvalidEmail'];
    // Identity error codes that map to the password field
    const passwordCodes = [
      'PasswordTooShort',
      'PasswordRequiresUpper',
      'PasswordRequiresLower',
      'PasswordRequiresDigit',
      'PasswordRequiresNonAlphanumeric',
      'PasswordRequiresUniqueChars',
    ];

    const firstMatch = (codes: string[]): ServerFieldError | undefined => {
      for (const code of codes) {
        const text = errors[code]?.[0];
        if (text !== undefined) {
          return { key: IDENTITY_ERROR_KEYS[code] ?? null, text };
        }
      }
      return undefined;
    };

    const emailError = firstMatch(emailCodes);
    const passwordError = firstMatch(passwordCodes);

    if (emailError) {
      this.registerForm.get('email')?.setErrors({ serverError: emailError });
      return;
    }

    if (passwordError) {
      this.registerForm.get('password')?.setErrors({ serverError: passwordError });
      return;
    }

    // Fallback: show the first error message from any key
    const unexpected = this.transloco.translate('common.errors.unexpected');
    const fallback = Object.values(errors).flat().at(0) ?? unexpected;
    this.snackBar.open(fallback, this.transloco.translate('common.actions.close'), {
      duration: 5000,
    });
  }

  private clearServerErrorOnChange(controlName: string): void {
    this.registerForm
      .get(controlName)
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const ctrl = this.registerForm.get(controlName);
        if (!ctrl?.hasError('serverError')) return;

        const { serverError: _, ...remaining } = ctrl.errors ?? {};
        ctrl.setErrors(Object.keys(remaining).length ? remaining : null);
      });
  }
}

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}
