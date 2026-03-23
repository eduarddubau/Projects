import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuthService } from '@core/services/auth.service';
import { LoginCredentials } from '@core/models/login-credentials';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
    RouterModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent {
  showPassword = false;

  private authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  loginForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8)]
    })
  });

  onSubmit(): void {
    if (this.loginForm.valid) {
      const credentials: LoginCredentials = this.loginForm.getRawValue();

      this.authService.login(credentials).subscribe({
        next: () => {
          const returnUrl = this.route.snapshot.queryParams['returnUrl'] ?? '/dashboard';
          this.router.navigateByUrl(returnUrl);
        },
        error: (err) => {
          console.error('Login failed', err);
          this.snackBar.open('Invalid email or password.', 'Close', { duration: 3000 });
        }
      });
    } else {
      this.loginForm.markAllAsTouched();
    }
  }
}