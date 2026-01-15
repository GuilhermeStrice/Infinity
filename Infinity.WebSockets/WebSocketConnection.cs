using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infinity.Core;
using Infinity.Core.Exceptions;
using Infinity.WebSockets.Enums;

namespace Infinity.WebSockets
{
    public abstract class WebSocketConnection : NetworkConnection
    {
        internal ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

        internal Timer? pingTimer;
        internal ILogger? logger;
        internal long lastPingTicks;
        internal volatile bool shuttingDown;
        internal volatile bool closeSent;
        internal volatile bool closeReceived;

        protected abstract NetworkStream Stream { get; }
        protected abstract bool MaskOutgoingFrames { get; }
        public abstract int MaxPayloadSize { get; set; }

        protected abstract bool ValidateIncomingMask(bool masked);

        public override async Task<SendErrors> Send(MessageWriter writer)
        {
            if (state != ConnectionState.Connected || Stream == null || closeSent)
                return SendErrors.Disconnected;

            await InvokeBeforeSend(writer).ConfigureAwait(false);

            var frame = WebSocketFrame.CreateFrame(
                this,
                writer.Buffer,
                writer.Length,
                WebSocketOpcode.Binary,
                true,
                MaskOutgoingFrames);

            try
            {
                var manager = frame.AsManager();

                await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);

                manager.Dispose();
            }
            catch (Exception ex)
            {
                logger?.WriteError("WebSocket send failed: " + ex.ToString());
                await ShutdownInternalAsync(
                    InfinityInternalErrors.ConnectionDisconnected,
                    "Send failed").ConfigureAwait(false);
                return SendErrors.Disconnected;
            }

            return SendErrors.None;
        }

        private async Task ShutdownInternalAsync(InfinityInternalErrors error, string reason)
        {
            shuttingDown = true;

            try
            {
                pingTimer?.Dispose();
            }
            catch (Exception ex)
            {
                logger?.WriteError("Failed to dispose ping timer: " + ex);
            }

            OnInternalDisconnect?.Invoke(error);

            State = ConnectionState.NotConnected;

            await InvokeDisconnected(reason, Helpers.BuildEmptyReader(this)).ConfigureAwait(false);

            Dispose();
        }

        protected override async Task DisconnectInternal(InfinityInternalErrors error, string reason)
        {
            await ShutdownInternalAsync(error, reason).ConfigureAwait(false);
        }

        protected override async Task DisconnectRemote(string reason, MessageReader reader)
        {
            MessageWriter frame;

            try
            {
                if (Stream != null)
                {
                    frame = WebSocketFrame.CreateFrame(
                        this,
                        ReadOnlySpan<byte>.Empty,
                        0,
                        WebSocketOpcode.Close,
                        true,
                        MaskOutgoingFrames);

                    try
                    {
                        var manager = frame.AsManager();

                        await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);
                        closeSent = true;

                        manager.Dispose();
                    }
                    catch (Exception ex)
                    {
                        logger?.WriteError("Failed to send close frame to remote: " + ex);
                    }
                }
            }
            finally
            {
                await InvokeDisconnected(reason, reader).ConfigureAwait(false);

                Dispose();
            }
        }

        protected override bool SendDisconnect(MessageWriter writer)
        {
            if (Stream == null || closeSent || state != ConnectionState.Connected)
                return false;

            MessageWriter frame;

            frame = WebSocketFrame.CreateFrame(
                this,
                ReadOnlySpan<byte>.Empty,
                0,
                WebSocketOpcode.Close,
                true,
                MaskOutgoingFrames);

            try
            {
                Stream.Write(frame.Buffer.ToArray(), 0, frame.Length);
                try
                {
                    Stream.Flush();
                }
                catch (Exception ex)
                {
                    logger?.WriteError("Flush failed in SendDisconnect: " + ex);
                }

                closeSent = true;
                return true;
            }
            catch (Exception ex)
            {
                logger?.WriteError("Failed to send close frame: " + ex);
                return false;
            }
        }

        protected override void SetState(ConnectionState newState) => state = newState;

        protected void StartPingTimer(int interval = 5000)
        {
            pingTimer = new Timer(async _ => await SendPing(), null, interval, Timeout.Infinite);
        }

        private async Task SendPing()
        {
            if (state != ConnectionState.Connected || Stream == null)
                return;

            MessageWriter frame;

            try
            {
                lastPingTicks = DateTime.UtcNow.Ticks;

                frame = WebSocketFrame.CreateFrame(
                    this,
                    ReadOnlySpan<byte>.Empty,
                    0,
                    WebSocketOpcode.Ping,
                    true,
                    MaskOutgoingFrames);

                var manager = frame.AsManager();

                await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);

                manager.Dispose();
            }
            catch (Exception ex)
            {
                logger?.WriteError("Failed to send ping frame: " + ex);
            }
            finally
            {
                try
                {
                    pingTimer?.Change(5000, Timeout.Infinite);
                }
                catch (Exception ex)
                {
                    logger?.WriteError("Failed to reschedule ping timer: " + ex);
                }
            }
        }

        protected async Task ReceiveLoop()
        {
            if (Stream == null) return;

            List<byte>? frag = null;
            WebSocketOpcode fragOpcode = WebSocketOpcode.Binary;
            int totalPayloadLen = 0;

            try
            {
                while (state == ConnectionState.Connected)
                {
                    if (!WebSocketFrame.TryReadFrame(Stream, out var opcode, out var fin, out var masked, out var payload))
                    {
                        await ShutdownInternalAsync(InfinityInternalErrors.ConnectionDisconnected, "Failed to read frame").ConfigureAwait(false);
                        return;
                    }

                    // After close received, ignore non-control frames
                    if (closeReceived && opcode != WebSocketOpcode.Close && opcode != WebSocketOpcode.Ping && opcode != WebSocketOpcode.Pong)
                        continue;

                    // Control frame rules
                    bool isControl = opcode == WebSocketOpcode.Close || opcode == WebSocketOpcode.Ping || opcode == WebSocketOpcode.Pong;
                    if (isControl)
                    {
                        if (!fin || payload.Length > 125)
                        {
                            await CloseWithCode(1002, "Invalid control frame").ConfigureAwait(false);
                            return;
                        }
                    }

                    

                    // Validate masking
                    if (!ValidateIncomingMask(masked))
                    {
                        var cw = new MessageWriter(allocator);
                        cw.Write((byte)(1002 >> 8));
                        cw.Write((byte)(1002 & 0xFF));
                        var frameClose = WebSocketFrame.CreateFrame(
                            this,
                            cw.Buffer,
                            cw.Length,
                            WebSocketOpcode.Close,
                            true,
                            MaskOutgoingFrames);

                        try
                        {
                            var manager = frameClose.AsManager();

                            await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);

                            manager.Dispose();
                        }
                        catch { }
                        
                        closeSent = true;
                        await ShutdownInternalAsync(InfinityInternalErrors.ConnectionDisconnected, "Invalid masking").ConfigureAwait(false);
                        return;
                    }

                    // Fragmentation handling
                    if (!fin)
                    {
                        if (opcode == WebSocketOpcode.Binary || opcode == WebSocketOpcode.Text)
                        {
                            if (frag != null)
                            {
                                await CloseWithCode(1002, "Protocol error: nested fragmented message").ConfigureAwait(false);
                                return;
                            }
                            frag ??= new List<byte>(payload.Length * 2);
                            frag.Clear();
                            frag.AddRange(payload);
                            fragOpcode = opcode;
                            totalPayloadLen = payload.Length;
                        }
                        else if (opcode == WebSocketOpcode.Continuation)
                        {
                            if (frag == null)
                            {
                                await CloseWithCode(1002, "Protocol error: unexpected continuation").ConfigureAwait(false);
                                return;
                            }
                            frag.AddRange(payload);
                            totalPayloadLen += payload.Length;
                        }

                        if (totalPayloadLen > MaxPayloadSize)
                        {
                            await CloseWithCode(1009, "Message too big").ConfigureAwait(false);
                            return;
                        }
                        continue;
                    }

                    if (opcode == WebSocketOpcode.Continuation && frag != null)
                    {
                        frag.AddRange(payload);
                        totalPayloadLen += payload.Length;

                        if (totalPayloadLen > MaxPayloadSize)
                        {
                            await CloseWithCode(1009, "Message too big").ConfigureAwait(false);
                            return;
                        }

                        payload = frag.ToArray();
                        opcode = fragOpcode;
                        frag = null;
                    }

                    if (payload.Length > MaxPayloadSize)
                    {
                        await CloseWithCode(1009, "Message too big").ConfigureAwait(false);
                        return;
                    }

                    switch (opcode)
                    {
                        case WebSocketOpcode.Binary:
                            {
                                var reader = new MessageReader(allocator, payload, 0, payload.Length);
                                await InvokeBeforeReceive(reader).ConfigureAwait(false);
                                await InvokeDataReceived(reader).ConfigureAwait(false);
                                break;
                            }
                        case WebSocketOpcode.Text:
                            {
                                try { _ = new UTF8Encoding(false, true).GetString(payload); }
                                catch (DecoderFallbackException)
                                {
                                    await CloseWithCode(1007, "Invalid UTF-8").ConfigureAwait(false);
                                    return;
                                }
                                var reader = new MessageReader(allocator, payload, 0, payload.Length);
                                await InvokeBeforeReceive(reader).ConfigureAwait(false);
                                await InvokeDataReceived(reader).ConfigureAwait(false);
                                break;
                            }
                        case WebSocketOpcode.Ping:
                            {
                                var pong = WebSocketFrame.CreateFrame(this, payload.AsSpan(), payload.Length, WebSocketOpcode.Pong, true, MaskOutgoingFrames);
                                try
                                {
                                    var manager = pong.AsManager();

                                    await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);

                                    manager.Dispose();
                                }
                                catch { }
                                break;
                            }
                        case WebSocketOpcode.Pong:
                            {
                                if (lastPingTicks != 0)
                                {
                                    var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastPingTicks).TotalMilliseconds;
                                    AveragePingMs = (float)elapsed;
                                    lastPingTicks = 0;
                                }
                                break;
                            }
                        case WebSocketOpcode.Close:
                            {
                                if (payload.Length == 1)
                                {
                                    await CloseWithCode(1002, "Invalid close payload length").ConfigureAwait(false);
                                    return;
                                }

                                ushort code = 1000;
                                string reason = string.Empty;
                                if (payload.Length >= 2)
                                {
                                    code = (ushort)((payload[0] << 8) | payload[1]);
                                    if (payload.Length > 2)
                                    {
                                        try { reason = Encoding.UTF8.GetString(payload, 2, payload.Length - 2); } catch { }
                                    }
                                }

                                if (!closeSent)
                                {
                                    var cw = new MessageWriter(allocator);
                                    cw.Write((byte)(code >> 8));
                                    cw.Write((byte)(code & 0xFF));
                                    if (!string.IsNullOrEmpty(reason))
                                    {
                                        var rb = Encoding.UTF8.GetBytes(reason);
                                        cw.Write(rb, 0, rb.Length);
                                    }
                                    var frame = WebSocketFrame.CreateFrame(this, cw.Buffer, cw.Length, WebSocketOpcode.Close, true, MaskOutgoingFrames);
                                    try
                                    {
                                        var manager = frame.AsManager();

                                        await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);

                                        manager.Dispose();
                                    }
                                    catch { }
                                    closeSent = true;
                                }

                                closeReceived = true;
                                await ShutdownInternalAsync(InfinityInternalErrors.ConnectionDisconnected,
                                    string.IsNullOrEmpty(reason) ? "Remote closed" : reason).ConfigureAwait(false);
                                return;
                            }
                        default:
                            break;
                    }
                }
            }
            catch (IOException) { return; }
            catch (SocketException) { return; }
            catch (Exception ex)
            {
                if (!shuttingDown && state == ConnectionState.Connected)
                {
                    logger?.WriteError("WebSocket receive loop failed: " + ex.Message);
                    await ShutdownInternalAsync(InfinityInternalErrors.ConnectionDisconnected, "Receive failed").ConfigureAwait(false);
                }
            }
        }

        private async Task CloseWithCode(ushort code, string reason)
        {
            // Prepare close payload
            var cw = new MessageWriter(allocator);
            cw.Write((byte)(code >> 8));
            cw.Write((byte)(code & 0xFF));

            if (!string.IsNullOrEmpty(reason))
            {
                var rb = Encoding.UTF8.GetBytes(reason);
                cw.Write(rb, 0, rb.Length);
            }

            // Create WebSocket close frame
            var frame = WebSocketFrame.CreateFrame(
                this,
                cw.Buffer,
                cw.Length,
                WebSocketOpcode.Close,
                true,
                MaskOutgoingFrames);

            try
            {
                var manager = frame.AsManager();

                await Stream.WriteAsync(manager.Memory).ConfigureAwait(false);
                closeSent = true;

                manager.Dispose();
            }
            catch (Exception ex)
            {
                logger?.WriteError("Failed to send close frame: " + ex);
            }

            // Shutdown connection regardless of write success
            try
            {
                await ShutdownInternalAsync(InfinityInternalErrors.ConnectionDisconnected, reason).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.WriteError("Error during ShutdownInternalAsync in CloseWithCode: " + ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            shuttingDown = true;

            try
            {
                pingTimer?.Dispose();
            }
            catch
            {

            }
        }
    }
}
