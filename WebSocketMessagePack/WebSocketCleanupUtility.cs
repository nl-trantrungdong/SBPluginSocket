using System;
using System.Reflection;

namespace WebSocketMessagePack
{
    /// <summary>
    /// Utility class để cleanup WebSocket connections từ bên ngoài
    /// </summary>
    public static class WebSocketCleanupUtility
    {
        /// <summary>
        /// Force cleanup tất cả WebSocket connections
        /// </summary>
        public static void ForceCleanupAllConnections()
        {
            try
            {
                // Gọi static method từ WebSocketMessagePackPlugin
                var pluginType = typeof(WebSocketMessagePackPlugin);
                var forceCleanupMethod = pluginType.GetMethod("ForceCleanup", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (forceCleanupMethod != null)
                {
                    forceCleanupMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in WebSocketCleanupUtility: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy số lượng WebSocket connections đang hoạt động
        /// </summary>
        /// <returns>Số lượng connections</returns>
        public static int GetActiveConnectionCount()
        {
            try
            {
                var pluginType = typeof(WebSocketMessagePackPlugin);
                var getCountMethod = pluginType.GetMethod("GetActiveConnectionCount", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (getCountMethod != null)
                {
                    return (int)getCountMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting connection count: {ex.Message}");
            }
            
            return -1; // Return -1 if error
        }

        /// <summary>
        /// Kiểm tra xem có WebSocket connections nào đang hoạt động không
        /// </summary>
        /// <returns>True nếu có connections đang hoạt động</returns>
        public static bool HasActiveConnections()
        {
            return GetActiveConnectionCount() > 0;
        }

        /// <summary>
        /// Log thông tin về WebSocket connections
        /// </summary>
        public static void LogConnectionInfo()
        {
            var count = GetActiveConnectionCount();
            System.Diagnostics.Debug.WriteLine($"Active WebSocket connections: {count}");
        }
    }
} 