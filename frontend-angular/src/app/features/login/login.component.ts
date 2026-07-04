import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '@core/services/auth.service';
import { LoginCredentials } from '@core/models/login-credentials';
import { APP_NAME } from '@core/tokens/app.tokens';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
    RouterModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    TranslocoDirective
  ],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LoginComponent {
  appName = APP_NAME;
  showPassword = false;

  private authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private transloco = inject(TranslocoService);

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
          const returnUrl = this.route.snapshot.queryParams['returnUrl'] ?? '/projects';
          this.router.navigateByUrl(returnUrl);
        },
        error: (err) => {
          console.error('Login failed', err);
          this.snackBar.open(
            this.transloco.translate('auth.login.invalidCredentials'),
            this.transloco.translate('common.actions.close'),
            { duration: 3000 },
          );
        }
      });
    } else {
      this.loginForm.markAllAsTouched();
    }
  }
}