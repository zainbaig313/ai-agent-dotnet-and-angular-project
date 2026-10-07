export interface Customer {
  id: number;
  name: string;
  email: string;
}

export interface Account {
  id: number;
  accountNumber: string;
  balance: number;
  customerId: number;
}

export interface Transaction {
  id: number;
  amount: number;
  category: string;
  description: string;
  date: string;
  transactionType: string;
}

export interface AgentRequest {
  customerId: number;
  message: string;
}

export interface AgentResponse {
  response: string;
}

export interface ChatMessage {
  sender: 'user' | 'ai';
  text: string;
}
