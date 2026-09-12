import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../Core/services/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ CommonModule, FormsModule ],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginComponent implements OnInit {
  credentials = { userNumber: '', password: '' };
  errorMessage: string = '';

  constructor(
    private authService: AuthService, 
    private router: Router
  ) {}

  ngOnInit(): void {
    document.body.classList.add('login-page');
  }

  onLogin(): void {
    this.errorMessage = '';
    this.authService.login(this.credentials.userNumber, this.credentials.password)
      .subscribe({
        next: (response) => {
          // Navigate to dynamic dashboard based on RBAC configuration
          if (response.dashboard) {
            this.router.navigate([response.dashboard]);
          } else {
            this.errorMessage = 'No default dashboard assigned. Contact admin.';
          }
        },
        error: (err) => { 
          this.errorMessage = 'Invalid credentials or access denied.';
          console.error('Login failed', err);
        }
      });
  }
}