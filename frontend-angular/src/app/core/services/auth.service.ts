import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { BehaviorSubject } from 'rxjs';

@Injectable({ 
    providedIn: 'root'
})
export class AuthService {
  private router = inject(Router);
  
  // Use a BehaviorSubject to track if the user is logged in
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
  isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  login() {
    // 1. Logic to verify credentials goes here
    this.isAuthenticatedSubject.next(true);

    // 2. Redirect to the entity list
    this.router.navigate(['/entities']);
  }

  logout() {
    this.isAuthenticatedSubject.next(false);
    this.router.navigate(['/login']);
  }
}