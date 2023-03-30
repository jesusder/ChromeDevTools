using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MasterDevs.ChromeDevTools
{
    public class SimpleWebSocket : IDisposable
    {
        private const int RECEIVE_CHUNK_SIZE = 64;

        private ClientWebSocket _clientWebSocket;

        public Action<string> OnMessageReceived;

        public SimpleWebSocket()
        {
            _clientWebSocket = new ClientWebSocket();
        }

        public async Task Connect(string endpoint)
        {
            await _clientWebSocket.ConnectAsync(new Uri(endpoint), CancellationToken.None);
            _ = Receive(_clientWebSocket);
        }

        public Task SendMessage(string message)
        {
            if (!IsOpen())
            {
                return Task.CompletedTask;
            }

            return _clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public bool IsOpen()
        {
            return _clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open;
        }

        public void Close()
        {
            _clientWebSocket?.Abort();
        }

        public void Dispose()
        {
            if (_clientWebSocket == null)
            {
                return;
            }

            _clientWebSocket.Dispose();
            _clientWebSocket = null;
        }

        private async Task Receive(ClientWebSocket webSocket)
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[RECEIVE_CHUNK_SIZE]);
            while (webSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        memoryStream.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        break;
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(memoryStream, Encoding.UTF8))
                    {
                        string message = await reader.ReadToEndAsync();
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
        }
    }
}
