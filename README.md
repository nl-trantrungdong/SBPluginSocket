# WebSocket MessagePack Plugin for SilverBullet

## Mô tả
Plugin này cho phép SilverBullet kết nối WebSocket với server sử dụng MessagePack serialization để gửi và nhận dữ liệu binary.

## Tính năng Resource Management

### 1. Tự động Cleanup Resources
Plugin đã được thiết kế để đảm bảo cleanup resources đúng cách ngay cả khi SilverBullet crash hoặc tắt đột ngột:

- **IDisposable Implementation**: Plugin implement IDisposable để cleanup managed resources
- **Finalizer**: Có finalizer để đảm bảo cleanup ngay cả khi Dispose() không được gọi
- **Static Connection Tracking**: Theo dõi tất cả WebSocket connections đang hoạt động
- **Automatic Cleanup Timer**: Tự động đóng các connections cũ hơn 5 phút
- **Process Exit Handling**: Tự động cleanup khi process exit hoặc unhandled exception

### 2. Safe WebSocket Management
- **SafeCloseWebSocket()**: Method an toàn để đóng WebSocket với error handling
- **Timeout Handling**: Xử lý timeout cho WebSocket operations
- **Exception Safety**: Tất cả WebSocket operations được wrap trong try-catch
- **Resource Disposal**: ManualResetEvent và CancellationTokenSource được dispose đúng cách

### 3. Static Cleanup Methods
```csharp
// Force cleanup tất cả connections
WebSocketMessagePackPlugin.ForceCleanup();

// Kiểm tra số lượng connections đang hoạt động
int activeCount = WebSocketMessagePackPlugin.GetActiveConnectionCount();
```

## Cách sử dụng

### Basic Usage
```loli
#WebSocketTest
WebSocketMessagePackPlugin "username" "password" "ip" "token" "userId" "timestamp" "signature" "30000" -> VAR "result"
```

### Parameters
- `Username`: Tên đăng nhập
- `Password`: Mật khẩu
- `IpAddress`: IP address của client
- `WsToken`: WebSocket token
- `UserId`: User ID
- `Timestamp`: Timestamp (long)
- `Signature`: Signature string
- `Timeout`: Timeout trong milliseconds (mặc định: 30000)

### Output
Kết quả MessagePack unpacked sẽ được lưu vào biến được chỉ định.

## Troubleshooting

### Vấn đề "Không thể xóa thư mục SilverBullet"
Nếu gặp vấn đề này, plugin đã được cải thiện để:

1. **Tự động cleanup**: Tất cả WebSocket connections sẽ được đóng tự động
2. **Process exit handling**: Cleanup được trigger khi SilverBullet tắt
3. **Stale connection cleanup**: Connections cũ sẽ được đóng sau 5 phút
4. **Force cleanup method**: Có thể gọi `WebSocketMessagePackPlugin.ForceCleanup()` để force cleanup

### Manual Cleanup
Nếu vẫn gặp vấn đề, có thể:

1. **Restart SilverBullet**: Plugin sẽ tự động cleanup khi khởi động lại
2. **Check Task Manager**: Kiểm tra xem có process nào của SilverBullet còn chạy không
3. **Use Force Cleanup**: Gọi method cleanup từ code khác nếu cần

### Logging
Plugin log chi tiết các hoạt động:
- Connection status
- Sent/received data
- Error messages
- Cleanup operations

## Technical Details

### Resource Management Flow
1. **Connection Creation**: WebSocket được tạo và track trong static dictionary
2. **Operation Execution**: WebSocket operations với timeout và error handling
3. **Cleanup**: Tự động cleanup trong finally block
4. **Static Tracking**: Connection được remove khỏi tracking sau khi đóng
5. **Timer Cleanup**: Stale connections được cleanup mỗi 30 giây
6. **Process Exit**: Force cleanup tất cả connections khi process exit

### Error Handling
- **WebSocket Errors**: Được log và handle gracefully
- **Timeout Errors**: Throw TimeoutException với message rõ ràng
- **Disposal Errors**: Log nhưng không throw để tránh crash
- **Unhandled Exceptions**: Trigger cleanup khi có unhandled exception

### Performance Considerations
- **Connection Pooling**: Không sử dụng connection pooling để tránh memory leaks
- **Timeout Management**: Mỗi connection có timeout riêng
- **Memory Management**: Resources được dispose ngay sau khi sử dụng
- **Thread Safety**: Sử dụng ConcurrentDictionary cho thread safety

## Dependencies
- MessagePack 1.9.11
- WebSocketSharp (custom fork với CustomHeaders support)
- Newtonsoft.Json
- SilverBullet Framework

## Version History
- **v1.0**: Basic WebSocket MessagePack functionality
- **v1.1**: Added resource management và cleanup features
- **v1.2**: Improved error handling và logging 