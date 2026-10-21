# Kiểm tra giao diện (UI check)

Bộ kiểm tra Playwright cho các màn hình chính của ToolboxWeb. Đây là một phần của "Definition Of Done" trong `AGENTS.md`.

Với mỗi trang, ở 3 kiểu màn hình (desktop 1440px sáng, desktop 1440px tối, mobile 390px), bộ kiểm tra ghi nhận:

- lỗi trong console trình duyệt;
- tràn ngang (thanh cuộn ngang của trang);
- ô nhập không có label;
- chữ nhỏ hơn 12px;
- ảnh chụp toàn trang, so với ảnh mốc (sai khác tối đa 2%).

## Cơ chế "chỉ được giảm"

- `npm run baseline` ghi lại ảnh mốc **và số lỗi hiện có** của từng trang.
- `npm run check` chỉ báo lỗi khi số lỗi của một trang **tăng lên** so với mốc, hoặc khi ảnh chụp khác mốc.
- Khi sửa xong một lỗi cũ, chạy lại `npm run baseline` để hạ mốc xuống.

Nhờ vậy, những lỗi đã biết không che mất lỗi mới. Nếu chưa có mốc, mọi lỗi đều bị báo.

Mọi thứ bộ kiểm tra ghi ra (ảnh, mốc, báo cáo, trạng thái đăng nhập) nằm trong `TBX_UI_OUT`, mặc định là thư mục `toolboxweb-ui` trong thư mục tạm của hệ điều hành. **Không có gì được ghi vào repo.**

## Chuẩn bị (một lần)

```powershell
cd tests\ui
npm install
npx playwright install chromium
```

## Chạy

1. Chạy app ở chế độ Development, dùng **database tạm** để kết quả không phụ thuộc dữ liệu thật. Bật thêm dữ liệu Jira giả nếu muốn kiểm tra các trang Jira:

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:ASPNETCORE_URLS = "http://localhost:5080"
   $env:Storage__DatabasePath = "$env:TEMP\toolboxweb-ui-db\app.db"
   $env:Storage__UploadsPath = "$env:TEMP\toolboxweb-ui-db\uploads"
   $env:Jira__UseFakeData = "true"
   dotnet run --project src\ToolboxWeb.Web\ToolboxWeb.Web.csproj --no-launch-profile
   ```

   `Jira__UseFakeData` chỉ có tác dụng ở Development. Ở môi trường khác, app bỏ qua cờ này và ghi cảnh báo vào log.

2. Ở một cửa sổ khác:

   ```powershell
   cd tests\ui
   $env:TBX_UI_JIRA = "1"        # bỏ dòng này nếu app không bật Jira__UseFakeData
   npm run baseline              # lần đầu, hoặc sau khi đã duyệt thay đổi giao diện
   npm run check                 # các lần sau
   npm run summary               # bảng tổng hợp số lỗi theo trang (Markdown)
   ```

   Trên bash, thay `$env:TÊN = "giá trị"` bằng `export TÊN=giá trị`.

3. Báo cáo HTML nằm trong `%TEMP%\toolboxweb-ui\report`; mở bằng lệnh `npx playwright show-report <đường dẫn>`. Ảnh mốc nằm trong `%TEMP%\toolboxweb-ui\baseline\<kiểu màn hình>\`.

## Biến môi trường

| Biến | Mặc định | Ý nghĩa |
|---|---|---|
| `TBX_BASE_URL` | `http://localhost:5080` | Địa chỉ app đang chạy |
| `TBX_UI_OUT` | `<thư mục tạm>/toolboxweb-ui` | Nơi ghi ảnh, mốc, báo cáo |
| `TBX_UI_EMAIL`, `TBX_UI_PASSWORD` | `ui.check@example.local`, `UiCheck@12345` | Tài khoản thử; được đăng ký tự động ở lần chạy đầu |
| `TBX_UI_JIRA` | (trống) | Đặt `1` để kiểm tra cả các trang Jira (cần `Jira__UseFakeData=true`) |
| `TBX_UI_TIMEZONE` | `Asia/Ho_Chi_Minh` | Múi giờ của trình duyệt khi kiểm tra |

## Ghi chú

- Giờ của trình duyệt được cố định lúc 10:00 của ngày chạy, để hình nền thay đổi theo giờ không làm lệch ảnh chụp. Đồng hồ và thời tiết trên topbar được che khi chụp.
- Dữ liệu Jira giả và dữ liệu mẫu tính theo ngày hiện tại, nên ảnh mốc tạo hôm trước có thể lệch phần ngày tháng khi chạy hôm sau. Nếu chỉ lệch ngày, chạy lại `npm run baseline`.
- Trang `access-denied` là trang Jira mở bằng tài khoản thường, dùng để kiểm tra trang báo không có quyền.
- Bộ kiểm tra chỉ đo được những quy tắc đo được. Các quy tắc còn lại trong UI Standard (một màu nút chính, xác nhận khi xóa…) vẫn cần người xem lại.
