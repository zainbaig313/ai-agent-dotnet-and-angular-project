import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { Account } from '../models';

@Injectable({
  providedIn: 'root'
})
export class AccountService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getAccounts(customerId: number): Observable<Account | Account[]> {
    return this.http.get<Account | Account[]>(`${this.apiUrl}/api/accounts/${customerId}`);
  }
}
