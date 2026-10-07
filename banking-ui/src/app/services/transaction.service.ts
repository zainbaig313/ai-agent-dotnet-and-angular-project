import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Transaction } from '../models';

@Injectable({
  providedIn: 'root'
})
export class TransactionService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getTransactions(customerId: number): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`${this.apiUrl}/api/transactions/${customerId}`);
  }
}
