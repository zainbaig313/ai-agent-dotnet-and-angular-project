import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AgentRequest, AgentResponse } from '../models';

@Injectable({
  providedIn: 'root'
})
export class AgentService {
  private apiUrl = environment.apiUrl;

  constructor(private http: HttpClient) {}

  askAgent(customerId: number, message: string): Observable<AgentResponse> {
    const body: AgentRequest = {
      customerId: customerId,
      message: message
    };
    return this.http.post<AgentResponse>(`${this.apiUrl}/api/agent`, body);
  }
}
