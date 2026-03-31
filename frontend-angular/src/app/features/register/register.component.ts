import { Component, inject, signal, ChangeDetectionStrategy, DestroyRef } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors, FormGroup } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '@core/services/auth.service';

@Component({
  selector: 'app-register',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatIcon
  ],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  hidePassword = signal(true);
  hideConfirmPassword = signal(true);
  isLoading = signal(false);

  registerForm = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName:  ['', Validators.required],
    email:     ['', [Validators.required, Validators.email]],
    password:  ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[A-Z])(?=.*[!@#$%^&*()_+{}:"<>?]).+$/)
    ]],
    confirmPassword: ['', Validators.required]
  }, { validators: passwordMatchValidator });

  constructor() {
    this.clearServerErrorOnChange('email');
    this.clearServerErrorOnChange('password');
  }

  togglePassword(event: MouseEvent) {
    this.hidePassword.update(v => !v);
    event.stopPropagation();
  }

  toggleConfirmPassword(event: MouseEvent) {
    this.hideConfirmPassword.update(v => !v);
    event.stopPropagation();
  }

  onSubmit() {
    if (this.registerForm.invalid) return;

    this.isLoading.set(true);
    const { confirmPassword, ...credentials } = this.registerForm.getRawValue();

    this.authService.register(credentials).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.snackBar.open('Registration successful! Welcome.', 'Close', { duration: 3000 });
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.isLoading.set(false);
        this.handleBackendErrors(err);
      }
    });
  }

  private handleBackendErrors(err: any): void {
    const errors: Record<string, string[]> = err.error ?? {};

    // Identity error codes that map to the email field
    const emailCodes = ['DuplicateUserName', 'DuplicateEmail', 'InvalidEmail'];
    // Identity error codes that map to the password field
    const passwordCodes = ['PasswordTooShort', 'PasswordRequiresUpper', 
                          'PasswordRequiresDigit', 'PasswordRequiresNonAlphanumeric',
                          'PasswordRequiresUniqueChars'];

    const firstMatch = (codes: string[]) =>
      codes.flatMap(code => errors[code] ?? []).at(0);

    const emailError    = firstMatch(emailCodes);
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
    const fallback = Object.values(errors).flat().at(0) ?? 'An unexpected error occurred.';
    this.snackBar.open(fallback, 'Close', { duration: 5000 });
  }

  private clearServerErrorOnChange(controlName: string): void {
    this.registerForm.get(controlName)?.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const ctrl = this.registerForm.get(controlName);
        if (!ctrl?.hasError('serverError')) return;

        const { serverError: _, ...remaining } = ctrl.errors ?? {};
        ctrl.setErrors(Object.keys(remaining).length ? remaining : null);
      });
  }
}

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password        = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password && confirmPassword && password !== confirmPassword
    ? { passwordMismatch: true }
    : null;
}