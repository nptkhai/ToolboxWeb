# Mô hình triển khai: một ứng dụng, ba cách dùng (web · desktop · mobile) + máy chính cấp license

Kế hoạch đã được chủ dự án duyệt ngày 14/11/2026. Chưa bắt đầu làm (trừ phần tên và logo). Mỗi giai đoạn bắt đầu bằng danh sách việc để chủ dự án chọn.

## Bối cảnh

Chủ dự án muốn chuyển ToolboxWeb từ một website dùng chung DB (IIS + SQLite trên server) sang mô hình:
web không DB + desktop có DB riêng trên từng máy + mobile, cộng một **máy chính** chỉ quản lý người dùng/thiết bị/license và phát hành phiên bản.

Đã chốt (23/10 → 14/11/2026):
- **Mobile giai đoạn đầu dùng bản web** (cài dạng PWA). App mobile có DB riêng để sau.
- **Desktop chỉ Windows.**
- **Bản web không cần license**, chỉ đăng nhập Jira.
- **Kích hoạt online, chủ dự án duyệt**, kèm cách chép/dán thủ công khi máy không có mạng.

## Đánh giá: code hiện tại đáp ứng tới đâu

| Yêu cầu | Đáp ứng? | Ghi chú |
|---|---|---|
| 1. Dùng trên web | Có | Đang chạy như vậy. Cần thêm "chế độ web" không DB (xem mục 6). |
| 1. Desktop | Có, sửa vừa | Chạy chính app ASP.NET Core này trên máy (`localhost`), mở trong cửa sổ riêng, đóng gói bằng Velopack (MIT). Không phải viết lại giao diện. |
| 1. Mobile | Một phần | ASP.NET Core không chạy làm server trên iOS/Android. Giai đoạn đầu: cài bản web dạng PWA. Mobile có DB riêng = viết lại giao diện sang Blazor + .NET MAUI (rất lớn, để sau). |
| 2. DB trên từng thiết bị | Có | SQLite đã có; `RuntimeStoragePaths` cho chọn đường dẫn. Chuyển mặc định ra `%LOCALAPPDATA%\Toolkit`. Mọi truy vấn đã lọc `UserId`. |
| 2. Cấu hình gắn với máy, không gắn tài khoản | Sửa vừa | Desktop bỏ màn hình đăng nhập: một "chủ máy" ngầm định (giữ nguyên cột `UserId`, gán một user cố định), tùy chọn khóa bằng PIN. Các cài đặt đang ở `localStorage` có thể giữ hoặc chuyển vào DB máy. |
| 2. Jira cấu hình trên thiết bị | Sửa vừa | Hiện đăng nhập Jira mỗi phiên (`JiraAuthService` tạo user "bóng" trong DB, phiên Jira nằm trong RAM, gọi `/rest/auth/1/session`). Desktop: trang **Cài đặt → Jira** lưu URL, tài khoản, mật khẩu/PAT mã hóa bằng Data Protection (như mật khẩu ghi chú Máy chủ, `INoteSecretProtector`), tự đăng nhập lại khi hết phiên. |
| 2. Không thấy dữ liệu người khác | Có | Mỗi máy (mỗi tài khoản Windows) một DB riêng; máy chính không giữ dữ liệu công việc. |
| 3. Máy chính quản lý user/thiết bị/license | Viết mới | App nhỏ riêng (ASP.NET Core + SQLite/SQL Server), chỉ chủ dự án đăng nhập (Identity + 2FA, giới hạn IP nếu được). |
| 4. Cấp license từ token | Viết mới | Ký số ECDSA P-256 (có sẵn trong .NET, không cần thư viện), app desktop chứa khóa công khai để tự kiểm tra, không cần mạng sau khi kích hoạt. |
| 5. Báo phiên bản, chọn cập nhật | Viết mới | Velopack kiểm tra bản mới từ máy chính; thanh "Có bản x.y.z · Cập nhật ngay / Để sau"; thiết bị gửi phiên bản hiện tại lên máy chính. |
| 6. Bản web gọi Jira, không DB | Sửa vừa | Đăng nhập Jira bằng cookie (không tạo user trong DB); ẩn các module cần DB; bỏ ghi nhật ký. So sánh DB / Bắt SQL **không nên** mở trên web (server web phải với tới SQL Server nội bộ và nhận mật khẩu SQL), chỉ có trên desktop. |

**Kết luận:** đáp ứng được web + desktop + máy chính mà **không viết lại** ứng dụng; mobile giai đoạn đầu dùng bản web. Phần lớn công việc là: thêm chế độ chạy (Web/Desktop), bỏ đăng nhập trên desktop, trang Cài đặt Jira, và viết mới máy chính + đóng gói/cập nhật.

## Tên sản phẩm và logo

- Chủ dự án chốt (14/11/2026): chỉ đổi **tên người dùng nhìn thấy** sang **Toolkit**; solution, project, namespace, tiền tố `tbx-`, `window.Toolbox`, khóa `localStorage`, cookie, `app.db` giữ nguyên.
- Đã làm: chữ trong `vi-VN.json`/`en-US.json`, prompt mẫu (3 nơi), tiêu đề SQL script sinh ra, tiêu đề thông báo của hẹn giờ, logo và favicon (xem `docs/brand/README.md`).
- Các tên mới trong mô hình này dùng "Toolkit": app máy chính `Toolkit License`, bộ cài `Toolkit Setup.exe`, thư mục dữ liệu `%LOCALAPPDATA%\Toolkit`.
- Nếu sau này đổi tên thư mục project: trước đó phải thêm `.SetApplicationName("ToolboxWeb")` cho Data Protection trong `Program.cs`. Hiện chưa đặt, nên khóa mã hóa phụ thuộc đường dẫn; đổi thư mục sẽ làm mật khẩu đã lưu trong ghi chú Máy chủ không giải mã được và đăng xuất mọi người.

## Kiến trúc đề xuất

```
                ┌──────────── Máy chính (bạn quản lý, HTTPS) ────────────┐
                │ Toolkit License (app mới): người dùng, thiết bị, license │
                │ duyệt kích hoạt, phiên bản phát hành, báo phiên bản     │
                │ Thư mục cập nhật Velopack (file tĩnh)                    │
                │ Toolkit chế độ Web (site riêng, không DB)            │
                └──────▲──────────────────▲──────────────────▲───────────┘
   kích hoạt/báo phiên bản│      tải bản cập nhật│          trình duyệt/PWA│
        ┌─────────────────┴──────────────────┴──┐          ┌───────────┴──────────┐
        │ Desktop Windows: Toolkit chế độ    │          │ Web / Mobile (PWA):   │
        │ Desktop, Kestrel localhost + SQLite   │──Jira──▶ │ chỉ Jira + công cụ    │
        │ trong %LOCALAPPDATA%, cửa sổ riêng    │          │ không cần DB          │
        └───────────────────────────────────────┘          └───────────────────────┘
```

Một mã nguồn (repo ToolboxWeb), chọn chế độ bằng cấu hình `App:Mode` = `Web` | `Desktop` (mặc định `Desktop` khi đóng gói, `Web` trên máy chính).

## Các thay đổi chính

### A. Chế độ chạy (app chính)
- `App:Mode` trong `Program.cs`; một `IAppModeAccessor` (Infrastructure) để controller/view hỏi chế độ.
- **Web:** không đăng ký `ApplicationDbContext`/Identity store; đăng nhập Jira bằng cookie auth thuần (claims từ Jira, phiên Jira trong `JiraSessionStore` như hiện tại); `AppNavigation` ẩn nhóm CÁ NHÂN và CÔNG CỤ DB; `IActivityLogService` thay bằng bản no-op; `/Search/Mine` và các controller cần DB trả 404; Jira Dashboard/Công việc Jira/ghi hạn Jira chạy bình thường. Hẹn giờ có thể chạy chỉ trên trình duyệt (không lưu lịch sử).
- **Desktop:** bỏ trang đăng nhập/đăng ký; khi khởi động tạo (nếu chưa có) một user "chủ máy" và tự đăng nhập; ẩn các trang Identity không còn nghĩa (email, mật khẩu, xóa tài khoản → thay bằng "Xóa dữ liệu trên máy").
- Đường dẫn dữ liệu mặc định ở Desktop: `%LOCALAPPDATA%\Toolkit\{db,uploads,keys,logs}` (sửa `Infrastructure/Storage/RuntimeStoragePaths.cs`); sao lưu `app.db` trước `MigrateAsync` khi đổi phiên bản.
- Chỉ nghe `127.0.0.1` với cổng tự chọn; chống truy cập từ máy khác.

### B. Cài đặt trên thiết bị
- Trang **Cài đặt** (menu avatar): Jira (URL, tài khoản, mật khẩu hoặc Personal Access Token, nút Kiểm tra), giờ làm mỗi ngày, ngày lễ (gắn với APP-P06 trong `docs/FEATURE_INVENTORY.md`), license của máy, phiên bản/cập nhật.
- Bảng mới `DeviceSettings` (key/value) + migration; bí mật mã hóa bằng Data Protection như `INoteSecretProtector`.
- `JiraSessionStore`/`JiraClientFactory`: ở Desktop tạo phiên từ cấu hình đã lưu và tự đăng nhập lại khi Jira trả 401.

### C. License trên thiết bị
- Lần chạy đầu: tạo `DeviceId` (GUID) + cặp khóa ECDSA của thiết bị, lưu mã hóa; **token kích hoạt** = Base64 của `{deviceId, tên máy, tài khoản Windows, phiên bản app, khóa công khai thiết bị, thời điểm}`.
- Màn hình **Kích hoạt** (chặn mọi trang khi chưa có license hợp lệ): "Gửi yêu cầu" (POST lên máy chính, rồi chờ duyệt, tự kiểm tra lại) hoặc "Sao chép token" + ô "Dán license".
- **License** = JSON `{licenseId, deviceId, người dùng, ngày cấp, hết hạn, tính năng}` + chữ ký ECDSA của máy chính. App chứa khóa công khai của máy chính, kiểm tra chữ ký và `deviceId` khi khởi động; không cần mạng.
- Giới hạn nói rõ với chủ dự án: app .NET có thể bị dịch ngược để bỏ kiểm tra license. Với công cụ nội bộ thì chấp nhận được; nếu cần chặt hơn thì thêm làm rối mã (để sau).

### D. Máy chính: app mới `src/ToolkitLicense.Web`
- ASP.NET Core MVC, cùng bộ token/giao diện (dùng lại `tokens.css`, `components.css`), DB riêng.
- Bảng: `Users` (người dùng của bạn, không phải tài khoản đăng nhập), `Devices`, `ActivationRequests`, `Licenses` (có thể thu hồi), `Releases` (phiên bản, ghi chú, bắt buộc hay không), `DeviceHeartbeats` (phiên bản đang chạy, lần cuối thấy).
- API cho thiết bị: `POST /api/activations` (nhận token), `GET /api/activations/{id}` (trạng thái + license khi đã duyệt), `POST /api/devices/{id}/heartbeat` (phiên bản), `GET /api/releases/latest`. Thiết bị ký request bằng khóa của thiết bị.
- Trang quản trị (chỉ bạn): duyệt/từ chối kích hoạt, gán người dùng, cấp lại/thu hồi license, xem thiết bị và phiên bản đang chạy, phát hành bản mới.
- Máy chính **không** nhận dữ liệu công việc, Jira hay ghi chú.

### E. Đóng gói và cập nhật desktop
- `dotnet publish -r win-x64 --self-contained` rồi `vpk pack` (Velopack, MIT) ra bộ cài `Setup.exe` và gói cập nhật (có delta).
- Cửa sổ: mở app ở chế độ PWA (Edge/Chrome `--app=`) hoặc khung WebView2 (giấy phép Microsoft, cần bạn duyệt theo chính sách). Đề xuất bắt đầu bằng cửa sổ PWA, không thêm thư viện.
- Khi chạy: kiểm tra `releases/latest` lúc mở và mỗi vài giờ, gửi heartbeat; có bản mới thì hiện thanh "Cập nhật ngay / Để sau"; "Cập nhật ngay" = Velopack tải, khởi động lại, chạy migration (đã sao lưu DB).
- Ký số `Setup.exe` (chứng chỉ code signing, có phí hằng năm) để tránh cảnh báo SmartScreen; không ký vẫn chạy được nhưng có cảnh báo.
- Ghi `THIRD-PARTY-NOTICES.md` cho Velopack.

### F. Bản web và mobile (PWA)
- Site riêng trên máy chính chạy `App:Mode=Web`; máy chính phải với tới được Jira.
- Thêm `manifest.webmanifest`, icon, service worker chỉ lưu đệm CSS/JS (không lưu dữ liệu); thanh "Có phiên bản mới, tải lại" khi bạn deploy.
- Mobile: mở site trên điện thoại, "Thêm vào màn hình chính".

## Triển khai (deploy)

1. **Máy chính** (Windows Server/VPS có tên miền + HTTPS): IIS với 3 site: `license.<miền>` (Toolkit License, chỉ bạn), `toolkit.<miền>` (Toolkit chế độ Web), `updates.<miền>` (thư mục file Velopack, chỉ đọc). Khóa riêng ký license lưu ngoài thư mục web, có sao lưu.
2. **Phát hành bản mới:** tăng `Version` trong csproj → build web + desktop → `vpk pack` → chép gói vào thư mục cập nhật → tạo bản ghi `Releases` trên trang quản trị → các máy thấy thông báo. Có thể tự động hóa bằng GitHub Actions khi gắn tag (để giai đoạn sau, vì CI đang hoãn).
3. **Người dùng mới:** tải `Setup.exe` từ máy chính → cài → màn hình Kích hoạt → bạn duyệt → dùng.
4. **Dữ liệu hiện có trên server IIS:** không có đường chuyển tự động sang từng máy (mỗi người một DB). Đề xuất làm thêm "Xuất dữ liệu của tôi" (APP-P01) trên bản cũ trước khi tắt, rồi "Nhập" trên desktop.

## Chia giai đoạn

| GĐ | Việc | Công sức |
|---|---|---|
| R0 | Đổi tên hiển thị sang Toolkit, logo mới — **đã làm 14/11/2026** | S |
| M0 | Chế độ `App:Mode`, đường dẫn dữ liệu Desktop, sao lưu trước migration, tự đăng nhập "chủ máy" | M |
| M1 | Trang Cài đặt + Jira lưu trên máy (tự đăng nhập lại), bỏ đăng nhập Jira kiểu cũ ở Desktop | M |
| M2 | Chế độ Web: cookie auth Jira không DB, ẩn module cần DB, no-op nhật ký, PWA + thanh phiên bản mới | M |
| M3 | Máy chính Toolkit License: quản trị, API kích hoạt, ký license, phát hành phiên bản, heartbeat | L |
| M4 | License trên thiết bị: token, màn hình Kích hoạt (online + thủ công), kiểm tra chữ ký | M |
| M5 | Đóng gói Velopack, cửa sổ app, kiểm tra/áp dụng cập nhật, xuất/nhập dữ liệu chuyển từ bản IIS | M–L |
| Sau | Mobile có DB riêng (Blazor + MAUI) nếu thật sự cần | L+ |

Mỗi giai đoạn: trước khi làm, liệt kê việc cụ thể trong hộp chọn để chủ dự án đánh dấu (quy trình đã thống nhất).

## File chính sẽ đụng tới

- `src/ToolboxWeb.Web/Program.cs`, `Extensions/ServiceCollectionExtensions.cs` (đăng ký theo chế độ)
- `Infrastructure/Storage/RuntimeStoragePaths.cs` (đường dẫn Desktop)
- `Services/JiraAuthService.cs`, `Services/JiraSessionStore.cs`, `Infrastructure/Jira/JiraClientFactory.cs` (Jira theo thiết bị / cookie auth web)
- `ViewModels/AppNavigation.cs` (ẩn nhóm theo chế độ), `Views/Shared/_Layout.cshtml` (thanh cập nhật, menu Cài đặt)
- `Areas/Identity/Pages/Account/*` (bỏ ở Desktop/Web)
- Mới: `Infrastructure/Licensing/*`, `Infrastructure/Updates/*`, `Controllers/SettingsController.cs`, `Controllers/ActivationController.cs`, migration `AddDeviceSettings`
- Mới: `src/ToolkitLicense.Web/*` (app máy chính), `tools/release/*` (script đóng gói)
- `THIRD-PARTY-NOTICES.md`, `AGENTS.md`, `README.md`, `docs/ARCHITECTURE.md` (mô hình mới)

## Kiểm tra

- `dotnet build` hai project không cảnh báo.
- Desktop: chạy với `App:Mode=Desktop`, xác nhận DB tạo trong `%LOCALAPPDATA%` (thử trên Linux bằng đường dẫn tạm), không có trang đăng nhập, Jira tự đăng nhập từ cài đặt (dữ liệu Jira giả), các trang cá nhân hoạt động.
- Web: chạy với `App:Mode=Web`, không có file DB được tạo, đăng nhập Jira (giả) dùng được Jira Dashboard/Công việc Jira/ghi hạn, các trang cần DB trả 404 và không có trong menu/Ctrl+K.
- License: kích hoạt online (duyệt trên máy chính chạy cùng máy thử) và thủ công; license của máy A dán vào máy B bị từ chối; sửa một ký tự license thì bị từ chối; thu hồi license có hiệu lực ở lần kiểm tra sau.
- Cập nhật: đóng gói 2 phiên bản, cài bản 1, phát hành bản 2 → thanh thông báo, "Để sau" giữ bản 1, "Cập nhật ngay" lên bản 2 và giữ nguyên dữ liệu (có file sao lưu). Phần này chỉ chạy được trên Windows thật: cần máy của chủ dự án.
- `tests/ui` cho cả hai chế độ ở 1440/390, sáng/tối.

## Rủi ro và giới hạn

- License phía client có thể bị bẻ khóa (app .NET dịch ngược được).
- Bỏ đăng nhập trên desktop nghĩa là ai dùng tài khoản Windows đó đều thấy dữ liệu; có tùy chọn khóa bằng PIN.
- Máy chính là điểm duy nhất cho kích hoạt và cập nhật; máy đã kích hoạt vẫn chạy khi máy chính tắt.
- Mỗi máy một DB: không đồng bộ giữa desktop và điện thoại (đúng yêu cầu, nhưng cần nói rõ với người dùng).
- iOS chỉ hỗ trợ PWA hạn chế; app iOS thật cần tài khoản Apple Developer.
