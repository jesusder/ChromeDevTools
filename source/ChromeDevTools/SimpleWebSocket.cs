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

        private ClientWebSocket clientWebSocket;

        public Action<string> OnMessageReceived;

        public SimpleWebSocket()
        {
            clientWebSocket = new ClientWebSocket();
        }

        public Task ConnectAsync(string endpoint)
        {
            return clientWebSocket.ConnectAsync(new Uri(endpoint), CancellationToken.None);
        }

        public Task SendMessageAsync(string message)
        {
            if (!IsOpen())
            {
                return Task.CompletedTask;
            }

            return clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public bool IsOpen()
        {
            return clientWebSocket != null && clientWebSocket.State == WebSocketState.Open;
        }

        public void Close()
        {
            clientWebSocket?.Abort();
        }

        public void Dispose()
        {
            if (clientWebSocket == null)
            {
                return;
            }

            clientWebSocket.Dispose();
            clientWebSocket = null;
        }

        public async Task ReceiveAsync()
        {
            if (!IsOpen())
            {
                return;
            }

            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[RECEIVE_CHUNK_SIZE]);
            while (clientWebSocket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    do
                    {
                        result = await clientWebSocket.ReceiveAsync(buffer, CancellationToken.None);
                        memoryStream.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
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
