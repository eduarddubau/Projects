import { Component, inject, signal, ChangeDetectionStrategy } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIcon } from '@angular/material/icon';
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

  hidePassword = signal(true);
  hideConfirmPassword = signal(true);
  isLoading = signal(false);

  togglePassword(event: MouseEvent) {
    this.hidePassword.update(val => !val);
    event.stopPropagation();
  }

  toggleConfirmPassword(event: MouseEvent) {
    this.hideConfirmPassword.update(val => !val);
    event.stopPropagation();
  }

  registerForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required]],
    lastName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [
      Validators.required,
      Validators.minLength(8),
      Validators.pattern(/^(?=.*[A-Z])(?=.*[!@#$%^&*()_+{}:"<>?]).+$/)
    ]],
    confirmPassword: ['', [Validators.required]]
  }, { validators: this.passwordMatchValidator });

  private passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');
    return password && confirmPassword && password.value !== confirmPassword.value
      ? { passwordMismatch: true }
      : null;
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

  private handleBackendErrors(err: any) {
    const message = err.error?.message ?? 'An unexpected error occurred.';
    this.snackBar.open(message, 'Close', { duration: 5000 });
  }
}