import { Injectable, signal } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { GameResponse } from '../interfaces/api.interface';

@Injectable({
  providedIn: 'root'
})
export class SignalrService {
  private hubConnection: signalR.HubConnection | undefined;

  // Signal reativo para a UI
  public liveGamesUpdates = signal<GameResponse[]>([]);
  public isConnected = signal<boolean>(false);

  constructor() { }

  public startConnection(): void {
    // environment.apiUrl geralmente é "https://localhost:7039/api/v1" ou similar.
    // Vamos garantir a URL base corretamente (e.g. root/hubs/games).
    // Supondo que o startup.cs configurou CORS e URL: "/hubs/games"
    const baseUrl = environment.apiUrl.replace(/\/api\/v\d+$/, '') || 'https://localhost:7039';
    const hubUrl = `${baseUrl}/hubs/games`;

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.hubConnection
      .start()
      .then(() => {
        console.log('SignalR Connection Started');
        this.isConnected.set(true);
        this.addReceivers();
      })
      .catch((err: any) => {
        console.error('Error while starting SignalR connection: ' + err);
        this.isConnected.set(false);
      });

    this.hubConnection.onreconnecting(() => {
        this.isConnected.set(false);
    });

    this.hubConnection.onreconnected(() => {
        this.isConnected.set(true);
    });

    this.hubConnection.onclose(() => {
        this.isConnected.set(false);
    });
  }

  public stopConnection(): void {
    if (this.hubConnection) {
        this.hubConnection.stop();
        this.isConnected.set(false);
    }
  }

  private addReceivers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.on('ReceiveGameUpdates', (data: GameResponse[]) => {
      console.log('SignalR Recebeu atualizações de jogos:', data);
      this.liveGamesUpdates.set(data);
    });
  }
}
