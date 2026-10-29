using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ROVVR
{
    // Receive-only MAVLink stream. Named values arrive together in a burst, so
    // polling the single REST NAMED_VALUE_FLOAT cache loses all but the last one.
    public sealed class RovPilotTelemetryStream : IDisposable
    {
        private const int MaximumQueuedFrames = 64, MaximumMessageBytes = 8192;
        private readonly object gate = new object();
        private readonly Queue<Frame> frames = new Queue<Frame>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private ClientWebSocket socket;
        private bool started, disposed;
        private volatile string status = "Stopped";
        public string Status => status;

        public void Start(string baseUrl)
        {
            if (started || disposed) return;
            started = true;
            if (!TryStreamUri(baseUrl, out Uri uri)) { status = "Invalid BlueOS URL"; return; }
            _ = ReceiveAsync(uri, lifetime.Token);
        }

        public static bool TryStreamUri(string baseUrl, out Uri uri)
        {
            uri = null;
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri source)
                || (source.Scheme != "http" && source.Scheme != "https")) return false;
            var builder = new UriBuilder(source)
            {
                Scheme = source.Scheme == "https" ? "wss" : "ws",
                Path = source.AbsolutePath.TrimEnd('/') + "/v1/ws/mavlink",
                Query = "filter=^NAMED_VALUE_FLOAT$",
                Fragment = ""
            };
            uri = builder.Uri;
            return true;
        }

        // Called only on Unity's main thread. Timestamp conversion preserves
        // receive age if the rendering thread stalls; draining cannot freshen it.
        public void Drain(RovHudTelemetry telemetry, float now)
        {
            for (int count = 0; count < MaximumQueuedFrames; count++)
            {
                Frame frame;
                lock (gate)
                {
                    if (frames.Count == 0) break;
                    frame = frames.Dequeue();
                }
                if (frame.Json == null) telemetry.ResetPilotValues();
                else
                {
                    float age = (float)((Stopwatch.GetTimestamp() - frame.ReceivedTicks) / (double)Stopwatch.Frequency);
                    telemetry.ObservePilotStream(frame.Json, now - age);
                }
            }
        }

        private async Task ReceiveAsync(Uri uri, CancellationToken cancellation)
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    using (var connection = new ClientWebSocket())
                    {
                        lock (gate)
                        {
                            if (disposed) return;
                            socket = connection;
                        }
                        Enqueue(null);
                        try
                        {
                            status = "Connecting";
                            using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                            {
                                connectTimeout.CancelAfter(5000);
                                await connection.ConnectAsync(uri, connectTimeout.Token).ConfigureAwait(false);
                            }
                            status = "Connected";
                            var buffer = new byte[MaximumMessageBytes];
                            while (connection.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
                            {
                                int used = 0;
                                WebSocketReceiveResult result;
                                do
                                {
                                    result = await connection.ReceiveAsync(new ArraySegment<byte>(buffer, used, buffer.Length - used), cancellation).ConfigureAwait(false);
                                    if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Telemetry stream closed");
                                    if (result.MessageType != WebSocketMessageType.Text) throw new WebSocketException("Unexpected telemetry frame type");
                                    used += result.Count;
                                    if (!result.EndOfMessage && used == buffer.Length) throw new WebSocketException("Telemetry frame exceeds size limit");
                                } while (!result.EndOfMessage);
                                Enqueue(Encoding.UTF8.GetString(buffer, 0, used));
                            }
                        }
                        catch (Exception error) when (!cancellation.IsCancellationRequested)
                        {
                            status = "Unavailable: " + error.Message;
                        }
                        finally
                        {
                            Enqueue(null);
                            lock (gate) { if (ReferenceEquals(socket, connection)) socket = null; }
                        }
                    }
                    await Task.Delay(1000, cancellation).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            finally { status = "Stopped"; }
        }

        private void Enqueue(string json)
        {
            lock (gate)
            {
                if (disposed) return;
                if (frames.Count >= MaximumQueuedFrames - 1)
                {
                    frames.Clear();
                    frames.Enqueue(new Frame(null));
                }
                frames.Enqueue(new Frame(json));
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                lifetime.Cancel();
                socket?.Abort();
                socket?.Dispose();
                socket = null;
                frames.Clear();
            }
            lifetime.Dispose();
        }

        private readonly struct Frame
        {
            public readonly string Json;
            public readonly long ReceivedTicks;
            public Frame(string json) { Json = json; ReceivedTicks = Stopwatch.GetTimestamp(); }
        }
    }
}
