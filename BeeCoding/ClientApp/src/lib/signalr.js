import * as signalR from '@microsoft/signalr';

// Some views (Solve, Live code) join the board group for a subset of its events, so the
// hub also broadcasts ones they don't handle (presence, wallChanged, …). SignalR logs a
// "No client method with the name 'x' found." warning for each — harmless noise. Pass
// everything else through untouched.
const logger = {
  log(level, message) {
    if (level < signalR.LogLevel.Warning) return;
    if (typeof message === 'string' && message.includes('No client method with the name')) return;
    const fn = level >= signalR.LogLevel.Error ? console.error
      : level >= signalR.LogLevel.Warning ? console.warn
      : console.log;
    fn(`[signalr] ${message}`);
  },
};

/**
 * Creates (but does not start) a board hub connection.
 * The auth cookie rides along automatically on same-origin / proxied requests.
 */
export function createBoardConnection() {
  return new signalR.HubConnectionBuilder()
    .withUrl('/hubs/board')
    .withAutomaticReconnect([0, 1000, 3000, 5000, 10000])
    .configureLogging(logger)
    .build();
}
