import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { ApprovalRequestedEvent, WorkflowUpdatedEvent } from '../models/workflow.models';

export type SignalRConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface SignalRHandlers {
  onWorkflowUpdated: (event: WorkflowUpdatedEvent) => void;
  onApprovalRequested: (event: ApprovalRequestedEvent) => void;
  onReconnected: () => void;
}

@Injectable({ providedIn: 'root' })
export class WorkflowSignalRService {
  private connection: HubConnection | null = null;

  readonly connectionState = signal<SignalRConnectionState>('disconnected');

  async connect(handlers: SignalRHandlers): Promise<void> {
    if (this.connection) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/workflows')
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('WorkflowUpdated', (payload: WorkflowUpdatedEvent) => {
      handlers.onWorkflowUpdated(payload);
    });

    this.connection.on('ApprovalRequested', (payload: ApprovalRequestedEvent) => {
      handlers.onApprovalRequested(payload);
    });

    this.connection.onreconnecting(() => {
      this.connectionState.set('reconnecting');
    });

    this.connection.onreconnected(() => {
      this.connectionState.set('connected');
      handlers.onReconnected();
    });

    this.connection.onclose(() => {
      this.connectionState.set('disconnected');
    });

    this.connectionState.set('connecting');

    try {
      await this.connection.start();
      this.connectionState.set('connected');
    } catch {
      this.connectionState.set('disconnected');
    }
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return;
    }

    const conn = this.connection;
    this.connection = null;
    await conn.stop();
    this.connectionState.set('disconnected');
  }
}
