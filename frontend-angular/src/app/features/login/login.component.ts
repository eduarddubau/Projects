import { Component, ChangeDetectionStrategy, inject } from '@angular/core';
import { ReactiveFormsModule, FormGroup, FormControl, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '@services/auth.service';
import { LoginCredentials } from '@models/login-credentials';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [
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
  // Toggle for the password visibility
  showPassword = false;

  private authService = inject(AuthService);
  private snackBar = inject(MatSnackBar);

  // The form model with validation rules
  loginForm = new FormGroup({
    email: new FormControl('', { 
      nonNullable: true, 
      validators: [Validators.required, Validators.email] 
    }),
    password: new FormControl('', { 
      nonNullable: true, 
      validators: [Validators.required, Validators.minLength(6)] 
    })
  });

  onSubmit(): void {
    if (this.loginForm.valid) {
    const credentials: LoginCredentials = this.loginForm.getRawValue();

    // Call the service and subscribe to trigger the HTTP request
    this.authService.login(credentials).subscribe({
      next: () => { console.log('Login successful!');},
      error: (err) => {
        console.error('Login failed', err);
        this.snackBar.open('Login failed', 'Close', { duration: 3000 });
      }
    });
    } else {
      // Mark all fields as touched to show validation errors
      this.loginForm.markAllAsTouched();
    }
  }
}