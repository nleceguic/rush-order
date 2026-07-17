// Best-effort SignalR round-trip measurement for the "evento llega en
// <500ms P95" SLO. Opt-in via MEASURE_SIGNALR=true (see run-load-tests.sh)
// because it adds a WebSocket connection per iteration and has NOT been
// verified end-to-end against a running deployment — treat it as
// informational until it's been validated against real staging.
//
// Speaks the ASP.NET Core JSON Hub Protocol directly over k6/ws using the
// "skip negotiation" flow: RestaurantHub is exposed at /ws/restaurant and
// the API's JWT bearer handler explicitly reads ?access_token= for any
// /ws path (see RushOrder.Infrastructure/DependencyInjection.cs), which is
// exactly the mechanism non-browser SignalR clients use to connect
// straight to the WS endpoint without a prior HTTP /negotiate round trip.
import ws from 'k6/ws';
import { Trend } from 'k6/metrics';

export const signalrEventLatency = new Trend('signalr_event_latency', true);

const RECORD_SEPARATOR = '\x1e';

// wsBaseUrl: e.g. wss://api-staging.rushorder.es
// token: waiter's JWT access token
// restaurantId: tenant whose "restaurant:{id}" group to join
// orderId: the order whose OrderStatusUpdated event we're waiting for
// sendStatusUpdate: callback that fires the PATCH /orders/{id}/status —
//   invoked right after the group join so the elapsed time only covers
//   PATCH-sent -> event-received, not connection setup.
export function measureOrderStatusLatency(wsBaseUrl, token, restaurantId, orderId, sendStatusUpdate) {
  let latencyMs = null;

  ws.connect(`${wsBaseUrl}/ws/restaurant?access_token=${token}`, {}, function (socket) {
    let joined = false;
    let patchSentAt = null;

    socket.on('open', () => {
      socket.send(JSON.stringify({ protocol: 'json', version: 1 }) + RECORD_SEPARATOR);
    });

    socket.on('message', (data) => {
      const frames = data.split(RECORD_SEPARATOR).filter((m) => m.length > 0);

      for (const raw of frames) {
        let msg;
        try {
          msg = JSON.parse(raw);
        } catch {
          continue;
        }

        if (!joined && Object.keys(msg).length === 0) {
          // Empty object = successful handshake ack.
          joined = true;
          socket.send(
            JSON.stringify({ type: 1, target: 'JoinRestaurantAsync', arguments: [restaurantId] }) +
              RECORD_SEPARATOR,
          );
          patchSentAt = Date.now();
          sendStatusUpdate();
          continue;
        }

        if (msg.type === 1 && msg.target === 'OrderStatusUpdated' && msg.arguments?.[0] === orderId) {
          latencyMs = Date.now() - patchSentAt;
          socket.close();
        }
      }
    });

    socket.setTimeout(() => socket.close(), 3000); // never hold a VU past 3s waiting for the event
  });

  if (latencyMs !== null) signalrEventLatency.add(latencyMs);
  return latencyMs;
}
