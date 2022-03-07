﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleKeypad
{
    //Copyright 2018 Sam Wood
    public class TelnetClient : IDisposable
    {
        private readonly int _port;
        private readonly string _host;
        private readonly TimeSpan _sendRate;
        private readonly SemaphoreSlim _sendRateLimit;
        private readonly CancellationTokenSource _internalCancellation;

        private TcpClient _tcpClient;
        private StreamReader _tcpReader;
        private StreamWriter _tcpWriter;

        public EventHandler<string> MessageReceived;
        public EventHandler ConnectionClosed;

        /// <summary>
        /// Simple telnet client
        /// </summary>
        /// <param name="host">Destination Hostname or IP</param>
        /// <param name="port">Destination TCP port number</param>
        /// <param name="sendRate">Minimum time span between sends. This is a throttle to prevent flooding the server.</param>
        /// <param name="token"></param>
        public TelnetClient(string host, int port, TimeSpan sendRate, CancellationToken token)
        {
            _host = host;
            _port = port;
            _sendRate = sendRate;
            _sendRateLimit = new SemaphoreSlim(1);
            _internalCancellation = new CancellationTokenSource();

            token.Register(() => _internalCancellation.Cancel());
        }

        /// <summary>
        /// Connect and wait for incoming messages. 
        /// When this task completes you are connected. 
        /// You cannot call this method twice; if you need to reconnect, dispose of this instance and create a new one.
        /// </summary>
        /// <returns></returns>
        public async Task Connect()
        {
            if (_tcpClient != null)
            {
                throw new NotSupportedException($"{nameof(Connect)} aborted: Reconnecting is not supported. You must dispose of this instance and instantiate a new TelnetClient");
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_host, _port);

            _tcpReader = new StreamReader(_tcpClient.GetStream());
            _tcpWriter = new StreamWriter(_tcpClient.GetStream()) { AutoFlush = true };

            // Fire-and-forget looping task that waits for messages to arrive
            _ = WaitForMessage();
        }

        /// <summary>
        /// Gets a value indicating whether the underlying socket is connected to a remote host.
        /// </summary>
        /// <returns>
        /// true if the underlying socket is connected to a remote resource; otherwise, false.
        /// </returns>
        public bool IsConnected
        {
            get { return _tcpClient != null && _tcpClient.Connected; }
        }

        public async Task Send(string message)
        {
            try
            {
                // Wait for any previous send commands to finish and release the semaphore
                // This throttles our commands
                await _sendRateLimit.WaitAsync(_internalCancellation.Token);

                // Send command + params
                await _tcpWriter.WriteLineAsync(message);

                // Block other commands until our timeout to prevent flooding
                await Task.Delay(_sendRate, _internalCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // We're waiting to release our semaphore, and someone cancelled the task on us (I'm looking at you, WaitForMessages...)
                // This happens if we've just sent something and then disconnect immediately
                Trace.TraceInformation($"{nameof(Send)} aborted: {nameof(_internalCancellation.IsCancellationRequested)} == true");
            }
            catch (ObjectDisposedException)
            {
                // This happens during ReadLineAsync() when we call Disconnect() and close the underlying stream
                // This is an expected exception during disconnection if we're in the middle of a send
                Trace.TraceInformation($"{nameof(Send)} failed: {nameof(_tcpWriter)} or {nameof(_tcpWriter.BaseStream)} disposed");
            }
            catch (IOException)
            {
                // This happens when we start WriteLineAsync() if the socket is disconnected unexpectedly
                Trace.TraceError($"{nameof(Send)} failed: Socket disconnected unexpectedly");
                throw;
            }
            catch (Exception error)
            {
                Trace.TraceError($"{nameof(Send)} failed: {error}");
                throw;
            }
            finally
            {
                // Exit our semaphore
                _sendRateLimit.Release();
            }
        }

        private async Task WaitForMessage()
        {
            try
            {
                while (true)
                {
                    if (_internalCancellation.IsCancellationRequested)
                    {
                        Trace.TraceInformation($"{nameof(WaitForMessage)} aborted: {nameof(_internalCancellation.IsCancellationRequested)} == true");
                        break;
                    }

                    string message;

                    try
                    {
                        if (!_tcpClient.Connected)
                        {
                            Trace.TraceInformation($"{nameof(WaitForMessage)} aborted: {nameof(_tcpClient)} is not connected");
                            break;
                        }

                        // Due to CR/LF platform differences, we sometimes get empty messages if the server sends us over-eager EOL markers
                        // Because ReadLine*() strips out the EOL characters, the message can end up empty (but not null!)
                        // I *think* this is a server implementation problem rather than our problem to solve
                        // So just handle empty messages in your consumer library
                        message = await _tcpReader.ReadLineAsync();

                        if (message == null)
                        {
                            Trace.TraceInformation($"{nameof(WaitForMessage)} aborted: {nameof(_tcpReader)} reached end of stream");
                            break;
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // This happens during ReadLineAsync() when we call Disconnect() and close the underlying stream
                        // This is an expected exception during disconnection
                        Trace.TraceInformation($"{nameof(WaitForMessage)} aborted: {nameof(_tcpReader)} or {nameof(_tcpReader.BaseStream)} disposed. This is expected after calling Disconnect()");
                        break;
                    }
                    catch (IOException)
                    {
                        // This happens when we start ReadLineAsync() if the socket is disconnected unexpectedly
                        Trace.TraceError($"{nameof(WaitForMessage)} aborted: Socket disconnected unexpectedly");
                        break;
                    }
                    catch (Exception error)
                    {
                        Trace.TraceError($"{nameof(WaitForMessage)} aborted: {error}");
                        break;
                    }

                    Trace.TraceInformation($"{nameof(WaitForMessage)} received: {message} [{message.Length}]");

                    OnMessageReceived(message);
                }
            }
            finally
            {
                Trace.TraceInformation($"{nameof(WaitForMessage)} completed: Calling {nameof(Disconnect)}");
                Disconnect();
            }
        }

        /// <summary>
        /// Disconnecting will leave TelnetClient in an unusable state.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                // Blow up any outstanding tasks
                _internalCancellation.Cancel();

                // Both reader and writer use the TcpClient.GetStream(), and closing them will close the underlying stream
                // So closing the stream for TcpClient is redundant
                // But it means we're triple sure!
                _tcpReader?.Close();
                _tcpWriter?.Close();
                _tcpClient?.Close();
            }
            catch (Exception error)
            {
                Trace.TraceError($"{nameof(Disconnect)} error: {error}");
            }
            finally
            {
                OnConnectionClosed();
            }
        }

        private void OnMessageReceived(string message)
        {
            EventHandler<string> messageReceived = MessageReceived;

            if (messageReceived != null)
            {
                messageReceived(this, message);
            }
        }

        private void OnConnectionClosed()
        {
            EventHandler connectionClosed = ConnectionClosed;

            if (connectionClosed != null)
            {
                connectionClosed(this, new EventArgs());
            }
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Disconnect();
            }

            _disposed = true;
        }
    }
}
