import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CustomerService } from '../../services/customer.service';
import { AccountService } from '../../services/account.service';
import { TransactionService } from '../../services/transaction.service';
import { AgentService } from '../../services/agent.service';
import { Customer, Account, Transaction, ChatMessage } from '../../models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  customers: Customer[] = [];
  selectedCustomerId: number = 0;
  selectedCustomer: Customer | null = null;
  accounts: Account[] = [];
  transactions: Transaction[] = [];

  // Chat state
  messages: ChatMessage[] = [];
  userMessage: string = '';

  // Status & loading states
  loading: boolean = false;
  errorMessage: string = '';
  aiLoading: boolean = false;
  aiErrorMessage: string = '';

  constructor(
    private customerService: CustomerService,
    private accountService: AccountService,
    private transactionService: TransactionService,
    private agentService: AgentService
  ) {}

  ngOnInit(): void {
    this.loadCustomers();
  }

  loadCustomers(): void {
    this.loading = true;
    this.errorMessage = '';

    this.customerService.getCustomers().subscribe({
      next: (data) => {
        this.customers = data;
        if (this.customers && this.customers.length > 0) {
          this.selectedCustomerId = this.customers[0].id;
          this.onCustomerChange();
        } else {
          this.loading = false;
        }
      },
      error: () => {
        this.errorMessage = 'Unable to load customer information.';
        this.loading = false;
      }
    });
  }

  onCustomerChange(): void {
    if (!this.selectedCustomerId) return;

    this.loading = true;
    this.errorMessage = '';
    this.selectedCustomer = null;
    this.accounts = [];
    this.transactions = [];

    // Load customer details
    this.customerService.getCustomer(this.selectedCustomerId).subscribe({
      next: (customer) => {
        this.selectedCustomer = customer;
      },
      error: () => {
        this.errorMessage = 'Unable to load customer information.';
      }
    });

    // Load customer account
    this.accountService.getAccounts(this.selectedCustomerId).subscribe({
      next: (data: any) => {
        if (Array.isArray(data)) {
          this.accounts = data;
        } else if (data) {
          this.accounts = [data];
        } else {
          this.accounts = [];
        }
      },
      error: () => {
        this.errorMessage = 'Unable to load customer information.';
      }
    });

    // Load customer transactions
    this.transactionService.getTransactions(this.selectedCustomerId).subscribe({
      next: (transactions) => {
        this.transactions = transactions || [];
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Unable to load customer information.';
        this.loading = false;
      }
    });
  }

  askAI(): void {
    const trimmedMessage = this.userMessage.trim();
    if (!trimmedMessage || this.aiLoading) {
      return;
    }

    // Add user message to chat history
    this.messages.push({
      sender: 'user',
      text: trimmedMessage
    });

    this.userMessage = '';
    this.aiLoading = true;
    this.aiErrorMessage = '';

    this.agentService.askAgent(this.selectedCustomerId, trimmedMessage).subscribe({
      next: (res) => {
        this.messages.push({
          sender: 'ai',
          text: res.response
        });
        this.aiLoading = false;
      },
      error: () => {
        this.aiErrorMessage = 'Unable to get a response from the AI assistant.';
        this.aiLoading = false;
      }
    });
  }
}
