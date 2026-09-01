import * as signalR from '@microsoft/signalr';

/**
 * Creates (but does not start) a board hub connection.
 * The auth cookie rides along automatically on same-origin / proxied requests.
 */
export function createBoardConnection() {
  return new signalR.HubConnectionBuilder()
    .withUrl('/hubs/board')
    .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();
}
