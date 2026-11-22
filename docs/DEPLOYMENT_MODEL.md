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
| M0 | Chế độ `App:Mode`, đường dẫn dữ liệu Desktop, sao lưu trước migration, tự đăng nhập "chủ máy" — **đã làm 14/11/2026** (xem "Trạng thái M0") | M |
| M1 | Trang Cài đặt + Jira lưu trên máy (tự đăng nhập lại), bỏ đăng nhập Jira kiểu cũ ở Desktop — **đã làm 14/11/2026** (xem "Trạng thái M1") | M |
| M2 | Chế độ Web: cookie auth Jira không DB, ẩn module cần DB, no-op nhật ký, PWA + thanh phiên bản mới — **đã làm 15/11/2026** (xem "Trạng thái M2") | M |
| M3 | Máy chính Toolkit License: quản trị, API kích hoạt, ký license, phát hành phiên bản, heartbeat — **đã làm 15/11/2026** (xem "Trạng thái M3" và `docs/LICENSE_SERVER.md`) | L |
| M4 | License trên thiết bị: token, màn hình Kích hoạt (online + thủ công), kiểm tra chữ ký — **đã làm 15/11/2026** (xem "Trạng thái M4") | M |
| M5 | Đóng gói Velopack, cửa sổ app, kiểm tra/áp dụng cập nhật, xuất/nhập dữ liệu chuyển từ bản IIS — **đã làm 16/11/2026** (xem "Trạng thái M5" và `docs/RELEASE.md`) | M–L |
| Sau | Mobile có DB riêng (Blazor + MAUI) nếu thật sự cần | L+ |

Mỗi giai đoạn: trước khi làm, liệt kê việc cụ thể trong hộp chọn để chủ dự án đánh dấu (quy trình đã thống nhất).

## Trạng thái M0 (14/11/2026)

Chủ dự án chốt: **bản Desktop chỉ phát hành cho người dùng ở M5** (`Setup.exe` tải từ máy chính, kích hoạt license, tự cập nhật). Trước đó chế độ Desktop chỉ để phát triển và chạy thử. Mobile: mở site trên điện thoại → "Thêm vào màn hình chính"; từ M2 là PWA.

- **`App:Mode`** (`Infrastructure/Hosting/AppMode.cs`):
  - `Server` (mặc định) giữ nguyên site IIS hiện tại;
  - `Desktop` bật bằng `--desktop`, `TOOLKIT_DESKTOP=1` hoặc `App__Mode=Desktop`, và nạp thêm `appsettings.Desktop.json`.
- **Dữ liệu Desktop** nằm trong `%LOCALAPPDATA%\Toolkit` (đổi được bằng `Desktop:DataRoot`): `app.db`, `uploads/`, `keys/`, `logs/`, `backups/`, `device.json` (id chủ máy), `instance.json` (bản đang chạy).
- **Sao lưu DB** trước khi chạy migration mới, ở mọi chế độ (`Infrastructure/Storage/DatabaseBackup.cs`):
  - ghi vào `backups/app-<thời gian>-before-<migration>.db`, giữ 5 bản;
  - ở Server, thư mục `backups` nằm cạnh file DB, hoặc theo `Storage:BackupsPath`;
  - sao lưu lỗi thì app không khởi động.
- **Chủ máy** (`Services/DeviceOwnerService.cs`): chọn theo thứ tự
  1. tài khoản ghi trong `device.json`;
  2. tài khoản không phải Jira có sẵn trong DB (lấy tài khoản cũ nhất nếu có nhiều);
  3. tạo tài khoản mới mang tên người dùng Windows, không mật khẩu.
  
  `DesktopOwnerSignInMiddleware` tự đăng nhập chủ máy cho mọi request.
- **Ở Desktop:**
  - trang đăng nhập, đăng ký, đăng xuất chuyển về Tổng quan;
  - Email, Mật khẩu, Xác thực hai lớp trả 404 và không có trong menu;
  - "Xóa tài khoản" thành "Xóa dữ liệu trên máy";
  - menu tài khoản và Ctrl+K có "Thoát Toolkit" thay cho "Đăng xuất".
- **Chỉ máy đó mở được:**
  - nghe `127.0.0.1:5180`, hoặc cổng trống khác khi 5180 bận;
  - từ chối kết nối không phải loopback, chỉ nhận Host `localhost`/`127.0.0.1`, không chuyển HTTPS;
  - mở lần hai thì mở trình duyệt vào bản đang chạy rồi thoát (`/desktop/ping`, `instance.json`).
- **Jira ở Desktop:** xem "Trạng thái M1".
- **Chạy thử:** `dotnet run --project src\ToolboxWeb.Web\ToolboxWeb.Web.csproj -- --desktop`.


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

## Trạng thái M1 (14/11/2026)

Chủ dự án chọn cả 4 việc, đăng nhập Jira bằng mật khẩu hoặc token.

- **Trang Cài đặt** (`/settings.html`, `SettingsController`, chỉ ở Desktop):
  - có trong menu tài khoản và Ctrl+K ("Cài đặt Jira");
  - kết nối Jira gồm địa chỉ, cách đăng nhập (mật khẩu, hoặc token truy cập cá nhân của Jira Server/Data Center 8.14+), tên đăng nhập, mật khẩu hoặc token;
  - nút "Kiểm tra kết nối" thử mà không lưu; "Lưu và kết nối" thử rồi lưu; "Gỡ kết nối" hỏi lại kèm địa chỉ. Lưu và gỡ đều ghi nhật ký hoạt động.
- **Lưu trên máy:**
  - kết nối nằm trong `device.json` (`DeviceFileStore`, mục `jira`), mật khẩu hoặc token được mã hóa bằng Data Protection và không hiện lại;
  - "Xóa dữ liệu trên máy" xóa luôn kết nối này.
- **Tự kết nối** (`Services/DesktopJiraService.cs`):
  - cả app dùng một phiên Jira, mở nền ngay khi app chạy, và mở lại từ thông tin đã lưu khi phiên hết hạn (`Jira:SessionTimeoutHours`);
  - với mật khẩu, nếu Jira trả 401 giữa chừng thì client đăng nhập lại một lần rồi gửi lại (`JiraClient`). Cơ chế này cũng áp dụng ở Server;
  - lỗi được báo rõ: không tới được Jira, sai thông tin, địa chỉ không phải Jira, không đọc được mật khẩu đã lưu.
- **Menu và trang Jira:**
  - đã có kết nối thì nhóm JIRA hiện cùng dữ liệu cá nhân trong một tài khoản; logo dẫn tới Jira Dashboard;
  - chưa kết nối thì trang Jira chuyển sang Cài đặt kèm lời nhắc (thông báo trung tính), lưu xong quay lại đúng trang;
  - kết nối lỗi thì báo lý do.
- **Tổng quan:** ô "Giờ log Jira hôm nay" hiện khi đã có kết nối.
- **Server:** đăng nhập Jira như cũ; trang Cài đặt không có.

## Trạng thái M2 (15/11/2026)

Chủ dự án chọn cả 4 việc. So sánh DB và Bắt SQL **chỉ có ở Desktop**. Site IIS hiện tại **giữ chế độ Server**; bản Web là một site IIS thứ hai.

- **Chế độ Web** (`App:Mode=Web`):
  - **Đăng nhập:** chỉ bằng Jira; không có đăng ký, không có trang tài khoản.
  - **Menu:** chỉ còn Jira Dashboard, Công việc Jira và Hẹn giờ tập trung. Các trang cần DB mang `[NotInWebMode]` (`Infrastructure/Hosting/WebModeRules.cs`) và trả 404: Tổng quan, Hoạt động, Ghi chú, Việc cần làm, Liên kết, Prompt, So sánh DB, Bắt SQL, Cài đặt, Tabulator Demo.
  - **Không ghi DB nào ra đĩa:**
    - Identity dùng SQLite trong bộ nhớ, chỉ để giữ tài khoản bóng của người đăng nhập Jira; mất khi khởi động lại, cùng lúc với phiên Jira;
    - nhật ký hoạt động không ghi (`NoOpActivityLogService`), nên ghi hạn lên Jira vẫn chạy;
    - trên đĩa chỉ có khóa Data Protection (để cookie còn hiệu lực) và log.
  - **Hẹn giờ:** chỉ đếm giờ trên trình duyệt; không lưu phiên, không có số liệu và lịch sử.
- **Cài như ứng dụng (PWA, cả ba chế độ):**
  - `manifest.webmanifest` dùng icon TK 192/512;
  - service worker `Content/sw.js` chỉ lưu đệm file tĩnh có phiên bản, font, icon, thư viện và trang `/offline.html`. Trang và dữ liệu Jira luôn lấy từ mạng; không bao giờ lưu.
- **Báo phiên bản mới (cả ba chế độ):**
  - mỗi trang ghi số build;
  - `site.js` hỏi `/app/version` mỗi 10 phút và khi quay lại tab; khác số build thì hiện thanh "Đã có phiên bản mới của Toolkit · Tải lại";
  - menu tài khoản hiện "Toolkit 1.0.0" (`<Version>` trong csproj). **Tăng `Version` mỗi lần phát hành.**

### Bật "Tệp & Thư mục" trên site IIS (chế độ Server)
Màn hình Tệp & Thư mục đọc và ghi tệp **trên ổ đĩa của máy chủ**, nên mặc định trên site IIS không ai dùng được. Bản máy tính luôn có màn hình này (chủ máy mở thư mục nào cũng được); bản Web không có.
1. Ghi vào `appsettings.Production.json` của site (hoặc biến môi trường `Files__AllowedUsers__0`, `Files__AllowedFolders__0`…):
   ```json
   "Files": {
     "AllowedUsers": [ "ten.dang.nhap@congty.vn" ],
     "AllowedFolders": [ "D:\\TaiLieuChung" ]
   }
   ```
   - `AllowedUsers`: email của **tài khoản local** được dùng. Tài khoản đăng nhập bằng Jira không bao giờ có quyền.
   - `AllowedFolders`: các thư mục nền; thư mục gốc chỉ được chọn bên trong các thư mục này. Link/junction trỏ ra ngoài bị chặn.
   - Để trống một trong hai danh sách là tắt màn hình. Người không có tên trong danh sách không thấy menu và mở link trực tiếp gặp 404.
2. Cấp quyền NTFS cho tài khoản app pool (`IIS AppPool\<tên pool>`) trên từng thư mục trong `AllowedFolders`: Read nếu chỉ xem, Modify nếu cần sửa và quản lý tệp.
3. Trên IIS không có hộp thoại Windows: người dùng chọn thư mục bằng hộp chọn vẽ trong trang (chỉ duyệt bên trong `AllowedFolders`), và "Lưu bản sao" thành "Tải xuống".

### Dựng site Web trên IIS (máy chính)
1. Tạo site IIS thứ hai (ví dụ `toolkit.<miền>`), publish cùng bản build với site Server.
2. Đặt chế độ Web theo một trong hai cách:
   - trong `web.config` của site, phần `<aspNetCore>`, thêm `<environmentVariables><environmentVariable name="App__Mode" value="Web" /></environmentVariables>`;
   - hoặc ghi `"App": { "Mode": "Web" }` trong `appsettings.Production.json` của site đó.
3. `Storage:DataProtectionKeysPath` trỏ ra ngoài thư mục site (giống site Server, nhưng dùng thư mục riêng).
4. Gắn **HTTPS có chứng chỉ hợp lệ**: điện thoại chỉ cho cài app khi site chạy HTTPS.
5. Máy chủ phải vào được Jira (`Jira:DefaultBaseUrl`).
6. **Cài trên điện thoại:**
   - Android Chrome: menu ⋮ → "Cài đặt ứng dụng";
   - iPhone Safari: Chia sẻ → "Thêm vào MH chính".

## Trạng thái M3 (15/11/2026)

Chủ dự án chọn cả 4 việc. Các quyết định:

- **Thời hạn license:** chọn khi duyệt; mặc định không hết hạn; thu hồi được bất cứ lúc nào.
- **Đăng nhập quản trị:** mật khẩu cộng xác thực hai lớp bắt buộc.
- **DB:** SQLite.

Chi tiết cách dùng và cách dựng trên IIS ở `docs/LICENSE_SERVER.md`.

- **App quản trị riêng:** `src/ToolkitLicense.Web`, đã thêm vào `ToolboxWeb.sln`.
  - Giao diện dùng chung token, thành phần, font, icon và logo với Toolkit, liên kết từ `ToolboxWeb.Web` chứ không chép.
  - Có tiếng Việt và tiếng Anh, giao diện sáng và tối, dùng được trên màn hình 390px.
- **Đăng nhập:**
  - Tài khoản quản trị đầu tiên tạo bằng mã cài đặt một lần, ghi trong thư mục dữ liệu trên máy chủ.
  - Xác thực hai lớp bằng ứng dụng xác thực là bắt buộc, kèm 10 mã khôi phục.
  - Đăng nhập sai 5 lần thì khóa 15 phút; trang đăng nhập và API giới hạn số lần gọi.
- **Kích hoạt và cấp license:**
  - API `POST /api/activations` và `GET /api/activations/{id}`;
  - kích hoạt thủ công bằng cách dán mã `TKT1.…`;
  - duyệt (gắn người dùng, chọn thời hạn) hoặc từ chối;
  - license ký bằng ECDSA P-256. Khóa ký sinh lần chạy đầu, khóa công khai và dấu khóa xem ở trang Khóa ký, để gắn vào bản desktop ở M4.
- **Quản lý người dùng, thiết bị, license:**
  - cấp lại hoặc gia hạn, thu hồi có lý do; mọi thao tác đều hỏi xác nhận có ghi tên;
  - nhật ký thao tác ghi mọi việc của quản trị.
- **Phát hành phiên bản và báo phiên bản:**
  - trang Phiên bản (số, ghi chú, đường tải, bắt buộc), kèm bảng số máy theo từng phiên bản đang chạy;
  - `GET /api/releases/latest`;
  - `POST /api/devices/heartbeat` do thiết bị ký: ghi phiên bản và lần cuối hoạt động, trả tình trạng license, bản license mới khi vừa gia hạn, và phiên bản mới nhất.
- **Khác với phác thảo ban đầu:**
  - heartbeat là `POST /api/devices/heartbeat`; mã thiết bị nằm trong nội dung đã ký thay vì trên đường dẫn;
  - không có bảng `DeviceHeartbeats` riêng: phiên bản và lần cuối hoạt động lưu ngay trên thiết bị.
- **Chưa làm (M4):** phía thiết bị, gồm tạo khóa và mã kích hoạt, màn hình Kích hoạt, kiểm tra license khi mở app, gửi heartbeat, thanh "Có phiên bản mới".

## Trạng thái M4 (15/11/2026)

Chủ dự án chọn cả 4 việc:

- **Hết hạn hoặc bị thu hồi:** chỉ đọc.
- **Liên lạc định kỳ với máy chính:** không bắt buộc.
- **Địa chỉ máy chính và khóa công khai:** ghi trong `appsettings.Desktop.json` (`License:ServerUrl`, `License:PublicKey`).

Chi tiết ở mục "Phía máy tính" của `docs/LICENSE_SERVER.md`.

- **Khóa và mã kích hoạt** (`DeviceIdentityService`):
  - mã thiết bị và khóa ECDSA P-256 lưu trong `device.json`; khóa riêng mã hóa bằng Data Protection;
  - chép sang máy khác thì thành máy mới;
  - mã `TKT1` dùng đúng định dạng máy chính đọc (`WireFormat.cs` dùng chung).
- **Màn hình Kích hoạt** (`/activate.html`):
  - chưa có bản quyền thì mọi trang dẫn về đây;
  - gửi yêu cầu qua mạng rồi tự mở app khi được duyệt, hoặc làm thủ công (sao chép mã, dán bản quyền).
- **Kiểm tra bản quyền** (`DeviceLicenseService`, `DesktopLicenseMiddleware`):
  - kiểm chữ ký, đúng máy, còn hạn; không cần mạng;
  - hết hạn hoặc thu hồi thì chỉ đọc; thao tác không đổi dữ liệu app vẫn chạy (`[AllowWhenUnlicensed]`);
  - trang Cài đặt có khung Bản quyền, kèm nút "Kiểm tra ngay".
- **Báo phiên bản và thu hồi** (`LicenseHeartbeatService`):
  - báo phiên bản đang chạy, tự nhận bản gia hạn, biết khi bị thu hồi;
  - thanh "Đã có Toolkit x.y.z" có "Tải về" và "Để sau"; bản bắt buộc thì không ẩn được.
- **Máy chính:** trang Khóa ký có thêm khóa công khai một dòng để dán vào cấu hình.
- **Trước khi phát hành bản Desktop (M5):** điền `License:ServerUrl` và `License:PublicKey` thật vào `appsettings.Desktop.json`.

## Trạng thái M5 (16/11/2026)

Chủ dự án chọn cả 4 việc. Các quyết định:

- **Cửa sổ app:** Edge/Chrome `--app`.
- **Nơi đặt bộ cài và gói cập nhật:** trong app Toolkit License.
- **Ký số Setup.exe:** chưa ký; script có sẵn chỗ để ký sau.
- **HTTPS:** Let's Encrypt miễn phí qua win-acme, có hướng dẫn.

Cách phát hành từng bước ở `docs/RELEASE.md`. Dựng toàn bộ trên một máy Windows trong mạng LAN (HTTPS bằng mkcert) ở `docs/LAN_SETUP.md`.

- **Đóng gói bộ cài:**
  - `tools/release/publish-desktop.ps1` (Windows) và `publish-desktop.sh` (Linux/macOS): publish bản Windows x64 kèm .NET, ghi `License:ServerUrl`/`License:PublicKey` vào bản publish, thêm `toolkit.desktop`, rồi `vpk pack` ra `ToolkitApp-win-Setup.exe`, gói full và delta, `releases.win.json`.
  - App cài vào `%LOCALAPPDATA%\ToolkitApp`, tách khỏi dữ liệu `%LOCALAPPDATA%\Toolkit`, nên gỡ cài đặt không mất dữ liệu.
  - Đã đóng gói thử 1.0.0 và 1.1.0 trên Linux: gói delta khoảng 450 KB.
- **Cập nhật trong app:**
  - `Velopack.VelopackApp.Build().Run()` chạy đầu tiên trong `Program.cs`.
  - `DesktopUpdateService` tải gói từ `{License:ServerUrl}/updates` rồi khởi động lại; database được sao lưu trước khi nâng cấp.
  - Thanh "Đã có Toolkit x.y.z" có nút "Cập nhật ngay" (mở trang `/update.html`).
  - Bản bắt buộc (`updateRequired` trong heartbeat) giữ máy đã cài ở trang Cập nhật cho tới khi cập nhật xong.
  - Bản không cài bằng Setup chỉ thấy link tải về.
- **Cửa sổ app:** `AppWindow` mở Edge hoặc Chrome `--app` với hồ sơ riêng trong thư mục dữ liệu; không có hai trình duyệt đó thì mở trình duyệt mặc định.
- **Trang tải về trên máy chính:**
  - `/download` công khai;
  - `/updates` phục vụ gói, chỉ đọc, không liệt kê thư mục;
  - trang Phiên bản có mục tải gói lên và tự điền số phiên bản mới.
- **Xuất/nhập dữ liệu** (`DataTransferService`, Tài khoản > Dữ liệu cá nhân):
  - xuất ghi chú, việc cần làm, phiên tập trung, liên kết, prompt, ảnh đại diện (không gồm mật khẩu máy chủ);
  - nhập có xem trước và xác nhận, bỏ qua mục đã có.
- **Chưa kiểm tra được:** cài, cập nhật, gỡ trên Windows thật; cửa sổ Edge `--app`; HTTPS thật trên IIS.

