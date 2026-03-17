import { Component, signal, inject, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { API_URL } from '@core/tokens/app.tokens';

@Component({
  selector: 'app-home',
  imports: [DatePipe],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent implements OnInit {
  private http = inject(HttpClient);
  private apiUrl = inject<string>(API_URL);
  
  connectionStatus = signal<string>('Checking...');
  connectionColor = signal<string>('#666');
  apiData = signal<any>(null);

  ngOnInit() {
    this.checkConnection();
  }

  checkConnection() {
    this.connectionStatus.set('Connecting...');
    
    this.http.get(`${this.apiUrl}/api/health`, { responseType: 'text' }).subscribe({
      next: (data: any) => {
        this.apiData.set({ message: data, timestamp: new Date() });
        this.connectionStatus.set('Backend Reachable');
        this.connectionColor.set('#28a745');
      },
      error: (err) => {
        console.error('Bridge failed:', err);
        this.apiData.set(null);
        this.connectionStatus.set('Connection Failed');
        this.connectionColor.set('#dc3545');
      }
    });
  }
}
