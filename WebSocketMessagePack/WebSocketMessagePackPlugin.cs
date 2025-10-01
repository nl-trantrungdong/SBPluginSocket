using Extreme.Net;
using Newtonsoft.Json;
using PluginFramework;
using PluginFramework.Attributes;
using RuriLib;
using RuriLib.LS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Windows.Media;
using WebSocketSharp;

namespace WebSocketMessagePack
{
    public class WebSocketMessagePackPlugin : BlockBase, IBlockPlugin, IDisposable
    {
        private string variableName = "";
        private string username = "";
        private string password = "";
        private string info = "";
         private string signature = "";
        private string timeout = "30000";
    
        private bool disposed = false;

        // Static tracking for cleanup
        private static readonly ConcurrentDictionary<WebSocket, DateTime> ActiveConnections = new ConcurrentDictionary<WebSocket, DateTime>();
        private static readonly Timer CleanupTimer;
        private static bool isShuttingDown = false;

        static WebSocketMessagePackPlugin()
        {
            // Setup cleanup timer to check for stale connections every 30 seconds
            CleanupTimer = new Timer(CleanupStaleConnections, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Register for process exit to cleanup
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                isShuttingDown = true;
                ForceCleanupAllConnections();
                CleanupTimer?.Dispose();
            };

            // Register for unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                isShuttingDown = true;
                ForceCleanupAllConnections();
                CleanupTimer?.Dispose();
            };
        }

        /// <summary>
        /// Public method to force cleanup all WebSocket connections
        /// Call this method when SilverBullet is shutting down or when you need to ensure cleanup
        /// </summary>
        public static void ForceCleanup()
        {
            isShuttingDown = true;
            ForceCleanupAllConnections();
            CleanupTimer?.Dispose();
        }

        /// <summary>
        /// Get the number of active WebSocket connections
        /// </summary>
        public static int GetActiveConnectionCount()
        {
            return ActiveConnections.Count;
        }

        [Text("VariableName:", "Output variable")]
        public string VariableName
        {
            get { return this.variableName; }
            set { this.variableName = value; this.OnPropertyChanged("VariableName"); }
        }

        [Text("Username:", "Username for auth")]
        public string Username
        {
            get { return this.username; }
            set { this.username = value; this.OnPropertyChanged("Username"); }
        }
        [Text("Password:", "Password for auth")]
        public string Password
        {
            get { return this.password; }
            set { this.password = value; this.OnPropertyChanged("Password"); }
        }
        [Text("Info:", "Info JSON string")]
        public string Info
        {
            get { return this.info; }
            set { this.info = value; this.OnPropertyChanged("Info"); }
        }
        [Text("Signature:", "Signature string")]
        public string Signature
        {
            get { return this.signature; }
            set { this.signature = value; this.OnPropertyChanged("Signature"); }
        }
        [Text("Timeout (ms):", "Timeout in milliseconds")]
        public string Timeout
        {
            get { return this.timeout; }
            set { this.timeout = value; this.OnPropertyChanged("Timeout"); }
        }


        public WebSocketMessagePackPlugin()
        {
            base.Label = "SocketMessagePack";
        }
        public string Name
        {
            get
            {
                return "SocketMessagePack";
            }
        }

        public LinearGradientBrush LinearGradientBrush => new LinearGradientBrush(new GradientStopCollection { new GradientStop(Colors.MediumPurple, 0.0) });
        public bool LightForeground => false;

        public override void Process(BotData data)
        {
            base.Process(data);

            // Check if we're shutting down
            if (isShuttingDown)
            {
                data.Log(new LogEntry("Plugin is shutting down, skipping WebSocket operation", Colors.Yellow));
                return;
            }

            string username = BlockBase.ReplaceValues(this.Username, data);
            string password = BlockBase.ReplaceValues(this.Password, data);
            string signature = BlockBase.ReplaceValues(this.Signature, data);
            string infoJsonString = BlockBase.ReplaceValues(this.Info, data);
            string timeoutStr = BlockBase.ReplaceValues(this.Timeout, data);
            int timeout = 30000;
            int.TryParse(timeoutStr, out timeout);

            // Không parse info thành object nữa, chỉ dùng chuỗi JSON
            string wsToken = "";
            try
            {
                var infoObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(infoJsonString);
                if (infoObj != null && infoObj.ContainsKey("wsToken"))
                    wsToken = infoObj["wsToken"]?.ToString() ?? "";
            }
            catch { }

            string wsUrl = $"wss://websocket.azhkthg1.net/wsbinary?token={wsToken}";

            var resultBuilder = new System.Text.StringBuilder();
            var messageCount = 0;
            Exception wsException = null;
            WebSocket ws = null;
            ManualResetEvent doneEvent = null;
            CancellationTokenSource cts = null;
            bool variableSet = false;
            bool onCloseCalled = false;

            try
            {
                doneEvent = new ManualResetEvent(false);
                cts = new CancellationTokenSource(timeout + 10000); // Timeout + 10s buffer

                ws = new WebSocket(wsUrl);

                // Track this connection
                ActiveConnections.TryAdd(ws, DateTime.Now);

                ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                ws.WaitTime = TimeSpan.FromMilliseconds(timeout);
                ws.CustomHeaders = new[] {
                    new KeyValuePair<string, string>("Host", "websocket.azhkthg1.net"),
                    new KeyValuePair<string, string>("Connection", "Upgrade"),
                    new KeyValuePair<string, string>("Pragma", "no-cache"),
                    new KeyValuePair<string, string>("Cache-Control", "no-cache"),
                    new KeyValuePair<string, string>("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36"),
                    new KeyValuePair<string, string>("Upgrade", "websocket"),
                    new KeyValuePair<string, string>("Origin", "https://web.sunwin.sx"),
                    new KeyValuePair<string, string>("Sec-WebSocket-Version", "13"),
                    new KeyValuePair<string, string>("Accept-Encoding", "gzip, deflate, br, zstd"),
                    new KeyValuePair<string, string>("Accept-Language", "vi-VN,vi;q=0.9,fr-FR;q=0.8,fr;q=0.7,en-US;q=0.6,en;q=0.5"),
                    new KeyValuePair<string, string>("Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits")
                };

                if (data.UseProxies && data.Proxy != null)
                {
                    if (!data.Proxy.Type.ToString().Equals("Http", StringComparison.OrdinalIgnoreCase))
                        throw new NotSupportedException("Only HTTP proxy is supported for WebSocket debug!");
                    ws.SetProxy("http://" + data.Proxy.Host + ":" + data.Proxy.Port, data.Proxy.Username, data.Proxy.Password);
                }

                data.Log(new LogEntry($"The Web Socket client connected to {wsUrl}", Colors.Cyan));

                ws.OnOpen += (s, e) =>
                {
                    try
                    {
                        string logMsg = JsonConvert.SerializeObject(new object[] { 1, "MiniGame", username, password, new Dictionary<string, object> { { "info", infoJsonString }, { "signature", signature } } });
                        data.Log(new LogEntry($"Sent {logMsg} to the server", Colors.Yellow));
                        // Tạo payload đúng chuẩn
                        var map = new Dictionary<string, object>
                        {
                            { "info", infoJsonString },
                            { "signature", signature }
                        };
                        object[] payload = new object[]
                        {
                            1,
                            "MiniGame",
                            username,
                            password,
                            map
                        };
                        byte[] binaryPayload = MessagePack.MessagePackSerializer.Serialize(payload);
                        ws.Send(binaryPayload);
                    }
                    catch (Exception ex)
                    {
                        string err = ex.Message;
                        data.Log(new LogEntry($"Error sending data: {ex.Message}", Colors.Red));
                        wsException = ex;
                        resultBuilder.AppendLine(" | ERROR: " + ex.Message);
                        SafeCloseWebSocket(ws);
                        doneEvent.Set();
                    }
                };

                ws.OnMessage += (s, e) =>
                {
                    try
                    {
                        string json = null;
                        if (e.IsText)
                        {
                            string logMsg = e.Data;
                            data.Log(new LogEntry("WebSocket TEXT: " + e.Data, Colors.Cyan));
                            resultBuilder.AppendLine(logMsg);
                            json = e.Data;
                        }
                        else if (e.IsBinary)
                        {
                            var unpacked = MessagePack.MessagePackSerializer.Deserialize<object[]>(e.RawData);
                            json = JsonConvert.SerializeObject(unpacked, Formatting.None);
                            string logMsg = json;
                            data.Log(new LogEntry($"Unpacked JSON:  {json}", Colors.Cyan));
                            resultBuilder.AppendLine(logMsg);
                        }

                        if (!string.IsNullOrEmpty(json) && (
                            json.Contains("1,false,100") ||
                            json.Contains("1,false,105") ||
                            json.Contains("Đã có lỗi xảy ra") ||
                            json.Contains("Token đã hết hạn")
                        ))
                        {
                            resultBuilder.AppendLine("Critical error detected, closing WebSocket and ending process.");
                            data.Log(new LogEntry("Critical error detected, closing WebSocket and ending process.", Colors.Red));
                            SafeCloseWebSocket(ws);
                            doneEvent.Set();
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        string err = "Exception: " + ex.Message;
                        data.Log(new LogEntry(err, Colors.Red));
                        wsException = ex;
                        resultBuilder.AppendLine(" | ERROR: " + ex.Message);
                        SafeCloseWebSocket(ws);
                        doneEvent.Set();
                        return;
                    }

                    messageCount++;
                    if (messageCount >= 2)
                    {
                        SafeCloseWebSocket(ws);
                        doneEvent.Set();
                    }
                };

                ws.OnError += (s, e) =>
                {
                    string err = $"WebSocket Error: {e.Message}";
                    data.Log(new LogEntry(err, Colors.Red));
                    wsException = e.Exception ?? new Exception(e.Message);
                    resultBuilder.AppendLine(" | ERROR: " + e.Message);
                    SafeCloseWebSocket(ws);
                    doneEvent.Set();
                };

                ws.OnClose += (s, e) =>
                {
                    onCloseCalled = true;
                    string logMsg = $"WebSocket closed with code: {e.Code}";
                    data.Log(new LogEntry(logMsg, Colors.Yellow));
                    resultBuilder.AppendLine(" | closedcode: " + e.Code);
                    doneEvent.Set();
                };

                ws.Connect();

                // Wait for completion with timeout
                if (!doneEvent.WaitOne(timeout + 5000))
                {
                    data.Log(new LogEntry("WebSocket operation timed out", Colors.Red));
                    throw new TimeoutException("WebSocket operation timed out");
                }
            }
            catch (Exception ex)
            {
                string err = $"WebSocket process error: {ex.Message}";
                data.Log(new LogEntry(err, Colors.Red));
                wsException = ex;
                resultBuilder.AppendLine(" | ERROR: " + ex.Message);
            }
            finally
            {
                // Ensure cleanup
                SafeCloseWebSocket(ws);

                if (doneEvent != null)
                {
                    try
                    {
                        doneEvent.Dispose();
                    }
                    catch (Exception ex)
                    {
                        data.Log(new LogEntry($"Error disposing ManualResetEvent: {ex.Message}", Colors.Red));
                    }
                }

                if (cts != null)
                {
                    try
                    {
                        cts.Dispose();
                    }
                    catch (Exception ex)
                    {
                        data.Log(new LogEntry($"Error disposing CancellationTokenSource: {ex.Message}", Colors.Red));
                    }
                }
            }

            if (wsException != null)
            {
                throw wsException;
            }

            // Luôn luôn InsertVariable với giá trị cuối cùng của resultBuilder
            BlockBase.InsertVariable(data, false, resultBuilder.ToString().Trim(), this.VariableName, "", "", false, true);
        }

        /// <summary>
        /// Safely closes WebSocket connection with proper error handling
        /// </summary>
        private void SafeCloseWebSocket(WebSocket ws)
        {
            if (ws != null)
            {
                try
                {
                    // Remove from tracking
                    ActiveConnections.TryRemove(ws, out _);

                    if (ws.ReadyState == WebSocketState.Open || ws.ReadyState == WebSocketState.Connecting)
                    {
                        ws.Close(CloseStatusCode.Normal);
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't throw - we're in cleanup
                    System.Diagnostics.Debug.WriteLine($"Error closing WebSocket: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Cleanup stale connections that have been open too long
        /// </summary>
        private static void CleanupStaleConnections(object state)
        {
            if (isShuttingDown) return;

            var staleConnections = new List<WebSocket>();
            var cutoffTime = DateTime.Now.AddMinutes(-5); // Close connections older than 5 minutes

            foreach (var kvp in ActiveConnections)
            {
                if (kvp.Value < cutoffTime)
                {
                    staleConnections.Add(kvp.Key);
                }
            }

            foreach (var ws in staleConnections)
            {
                try
                {
                    if (ws.ReadyState == WebSocketState.Open || ws.ReadyState == WebSocketState.Connecting)
                    {
                        ws.Close(CloseStatusCode.Normal);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cleaning up stale WebSocket: {ex.Message}");
                }
                finally
                {
                    ActiveConnections.TryRemove(ws, out _);
                }
            }
        }

        /// <summary>
        /// Force cleanup all active connections (called on shutdown)
        /// </summary>
        private static void ForceCleanupAllConnections()
        {
            foreach (var kvp in ActiveConnections)
            {
                try
                {
                    var ws = kvp.Key;
                    if (ws.ReadyState == WebSocketState.Open || ws.ReadyState == WebSocketState.Connecting)
                    {
                        ws.Close(CloseStatusCode.Normal);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error force cleaning up WebSocket: {ex.Message}");
                }
            }
            ActiveConnections.Clear();
        }

        /// <summary>
        /// Cleanup resources when plugin is disposed
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed && disposing)
            {
                // Cleanup managed resources here if needed
                disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup
        /// </summary>
        ~WebSocketMessagePackPlugin()
        {
            Dispose(false);
        }

        public override string ToLS(bool indent = true)
        {
            BlockWriter writer = new BlockWriter(base.GetType(), indent, base.Disabled);
            writer.Label(base.Label).Token("SocketMessagePack", "")
                .Literal(this.Username, "")
                .Literal(this.Password, "")
                .Literal(this.Info, "")
                .Literal(this.Signature, "")
                .Literal(this.Timeout, "");
            if (!writer.CheckDefault(this.VariableName, "VariableName"))
            {
                writer.Arrow().Token("VAR", "").Literal(this.VariableName, "").Indent(1);
            }
            return writer.ToString();
        }

        public override BlockBase FromLS(string line)
        {
            string input = line.Trim();
            if (input.StartsWith("#"))
            {
                base.Label = LineParser.ParseLabel(ref input);
            }
            this.Username = LineParser.ParseLiteral(ref input, "Username", false, null);
            this.Password = LineParser.ParseLiteral(ref input, "Password", false, null);
            this.Info = LineParser.ParseLiteral(ref input, "Info", false, null);
            this.Signature = LineParser.ParseLiteral(ref input, "Signature", false, null);
            this.Timeout = LineParser.ParseLiteral(ref input, "Timeout", false, null);
            if (LineParser.ParseToken(ref input, TokenType.Arrow, false, true) == "")
            {
                return this;
            }
            try
            {
                this.VariableName = LineParser.ParseToken(ref input, TokenType.Literal, true, true);
            }
            catch
            {
                throw new ArgumentException("Variable name not specified");
            }
            return this;
        }
    }
}