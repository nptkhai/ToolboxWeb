# Toolkit License: app quản trị bản quyền trên máy chính

Toolkit License (`src/ToolkitLicense.Web`) là app riêng chạy trên máy chính (IIS). Chỉ chủ dự án dùng nó để:

- duyệt yêu cầu kích hoạt từ ứng dụng Toolkit trên máy tính;
- cấp, gia hạn, cấp lại và thu hồi bản quyền;
- quản lý người dùng và thiết bị;
- phát hành phiên bản mới và xem từng máy đang chạy phiên bản nào.

Máy chính không nhận dữ liệu công việc, Jira hay ghi chú của ai. Phía thiết bị (bản Desktop của Toolkit) mô tả ở mục "Phía máy tính" bên dưới (giai đoạn M4).

## Thành phần

| Phần | Ở đâu |
|---|---|
| Dữ liệu | SQLite `license.db` trong thư mục dữ liệu (`Storage:DataRoot`) |
| Đăng nhập quản trị | ASP.NET Identity: mật khẩu, rồi **bắt buộc** mã 6 số từ ứng dụng xác thực |
| Khóa ký bản quyền | `signing-key.pem` (ECDSA P-256) trong thư mục dữ liệu, tự tạo lần chạy đầu |
| Giao diện | Dùng chung `tokens.css`, `components.css`, `vendor-sync.css`, Bootstrap, font Inter, icon Lucide và logo của `ToolboxWeb.Web` (liên kết trong csproj, không chép). Bộ dịch JSON và `<tbx-icon>` cũng dùng chung mã nguồn. |
| Chữ | `Localization/vi-VN.json`, `Localization/en-US.json` của app này |

Thư mục dữ liệu chứa:

| File / thư mục | Nội dung |
|---|---|
| `license.db` | Người dùng, thiết bị, yêu cầu, bản quyền, phiên bản, nhật ký thao tác, tài khoản quản trị |
| `backups/` | Bản sao DB trước mỗi lần nâng cấp schema (giữ 5 bản mới nhất) |
| `keys/` | Khóa Data Protection (cookie đăng nhập, khóa xác thực hai lớp) |
| `signing-key.pem` | Khóa riêng ký bản quyền: **phải sao lưu** |
| `setup-code.txt` | Mã cài đặt lần đầu; tự xóa khi đã tạo tài khoản quản trị |
| `updates/` | Bộ cài và gói cập nhật bản Desktop (kết quả của `tools/release`), phục vụ công khai, chỉ đọc, tại `/updates`; xem `docs/RELEASE.md` |

## Lần chạy đầu

1. Mở site. Chưa có tài khoản quản trị nên site chuyển tới `/setup`.
2. Mở `setup-code.txt` trong thư mục dữ liệu trên máy chủ, nhập mã đó cùng tên đăng nhập và mật khẩu: ít nhất 10 ký tự, có chữ hoa, chữ thường và chữ số. Sau bước này không ai tạo thêm tài khoản quản trị được nữa.
3. App bắt bật xác thực hai lớp ngay: mọi trang khác chuyển về `/account/two-factor` cho tới khi xong.
   - Nhập khóa hiện trên trang vào ứng dụng xác thực (Microsoft Authenticator, Google Authenticator, 2FAS…).
   - Nhập mã 6 số mà ứng dụng hiện ra.
4. Lưu 10 mã khôi phục. Mỗi mã dùng được một lần khi mất điện thoại; có thể tạo bộ mới ở trang Tài khoản quản trị.
5. Vào trang **Khóa ký**:
   - sao chép **khóa công khai một dòng** và dán vào `License:PublicKey` trong `appsettings.Desktop.json` của bản Toolkit trên máy tính (xem "Phía máy tính");
   - sao lưu `signing-key.pem`.

Đăng nhập sai 5 lần thì tài khoản khóa 15 phút. Trang đăng nhập và API giới hạn số lần gọi theo địa chỉ IP: đăng nhập 10 lần/phút, API 30 lần/phút.

## Các trang

| Trang | Việc làm được |
|---|---|
| Tổng quan | Số yêu cầu đang chờ, thiết bị có bản quyền, thiết bị hoạt động trong 7 ngày, phiên bản mới nhất và số máy chưa cập nhật, thao tác gần đây |
| Yêu cầu kích hoạt | Danh sách chờ duyệt và đã xử lý; **kích hoạt thủ công**: dán mã `TKT1.…` người dùng gửi qua chat hoặc thư |
| Chi tiết yêu cầu | Xem máy, người dùng Windows, dấu khóa thiết bị. Duyệt: chọn hoặc thêm người dùng, chọn thời hạn (mặc định không hết hạn; hoặc 6 tháng, 1 năm, đến ngày). Từ chối. Sau khi duyệt hiện bản quyền và nút sao chép, dùng cho đường thủ công |
| Thiết bị | Tìm theo tên máy, người dùng, mã thiết bị. Mỗi máy hiện tình trạng bản quyền, hạn, phiên bản (kèm "Cần cập nhật") và lần cuối hoạt động |
| Chi tiết thiết bị | Gắn người dùng, ghi chú, **Cấp lại / gia hạn** (giữ hạn hiện tại hoặc chọn hạn mới), **Thu hồi** (có lý do), lịch sử bản quyền (sao chép lại được) và các yêu cầu kích hoạt |
| Người dùng | Thêm, sửa, xóa. Xóa người dùng thì thiết bị và bản quyền của họ vẫn giữ, chỉ không còn gắn với ai |
| Phiên bản | Phát hành phiên bản (số, ghi chú, đường tải về, bắt buộc hay không), xóa, xem số máy theo từng phiên bản đang chạy. **Bộ cài và gói cập nhật**: tải lên kết quả của script phát hành (tối đa 1 GB mỗi lần); số phiên bản mới trong `releases.win.json` được điền sẵn vào form phát hành |
| Tải Toolkit (`/download`) | Trang **công khai**, không cần đăng nhập: phiên bản mới nhất, nút tải `ToolkitApp-win-Setup.exe`, các bước cài, vượt cảnh báo SmartScreen và kích hoạt |
| Khóa ký | Khóa công khai PEM, dấu khóa SHA-256, đường dẫn thư mục dữ liệu |
| Nhật ký thao tác | Mọi thao tác của quản trị, 50 dòng mỗi trang |
| Tài khoản quản trị | Đổi mật khẩu, tạo mã khôi phục mới |

Mọi thao tác xóa, từ chối, thu hồi, duyệt và cấp lại đều hỏi xác nhận trong hộp thoại có ghi tên thiết bị hoặc người dùng. Bản quyền không bao giờ bị xóa: thu hồi hay thay bằng bản mới đều được ghi lại trên chính bản đó.

## Định dạng trao đổi với thiết bị

Cả ba loại có dạng `TIỀN_TỐ.payload.chữ_ký`:

- `payload` là JSON UTF-8 (tên trường camelCase), mã hóa base64url.
- `chữ_ký` là ECDSA P-256 / SHA-256 trên đúng các byte của payload, dạng r‖s 64 byte (IEEE P1363), mã hóa base64url.
  - .NET: `DSASignatureFormat.IeeeP1363FixedFieldConcatenation`.
  - Node: `dsaEncoding: 'ieee-p1363'`.

| Tiền tố | Ai ký | Payload |
|---|---|---|
| `TKT1` mã kích hoạt | Thiết bị, bằng khóa riêng của nó | `{ v: 1, deviceId, machineName, windowsUser, appVersion, publicKey, createdAt }`. `publicKey` là SubjectPublicKeyInfo base64 của khóa P-256 thiết bị; `deviceId` gồm 8–64 ký tự chữ, số và `-` (GUID) |
| `TKL1` bản quyền | Máy chính, bằng `signing-key.pem` | `{ v: 1, licenseId, deviceId, deviceKey, customer, issuedAt, expiresAt }`. `deviceKey` là base64url(SHA-256(khóa công khai thiết bị)); `expiresAt` là `null` khi không hết hạn |
| `TKH1` heartbeat | Thiết bị | `{ v: 1, deviceId, appVersion, licenseId, sentAt }` |

Thiết bị nhận bản quyền khi:

- chữ ký đúng với khóa công khai của máy chính gắn trong app;
- `deviceId` và `deviceKey` khớp với chính nó;
- `expiresAt` chưa qua.

Việc kiểm tra này không cần mạng.

Mã thiết bị gắn với khóa đầu tiên gửi lên. Một mã kích hoạt cùng `deviceId` nhưng khóa khác bị từ chối (`keyMismatch`), ví dụ khi thư mục dữ liệu bị chép sang máy khác.

## API cho thiết bị

Không cần đăng nhập. Mọi yêu cầu đều được ký bằng khóa của thiết bị. Câu trả lời không chứa bí mật: bản quyền chỉ dùng được trên máy giữ khóa riêng tương ứng.

| Gọi | Gửi | Trả |
|---|---|---|
| `POST /api/activations` | `{ "token": "TKT1.…" }` | `202 { requestId, status, license, expiresAt }`. Máy đã có yêu cầu đang chờ thì nhận lại yêu cầu đó; máy đã có bản quyền còn hiệu lực thì nhận ngay `approved` kèm bản quyền |
| `GET /api/activations/{requestId}` | — | `{ requestId, status: pending \| approved \| rejected \| revoked, license, expiresAt }`. Nếu bản quyền đã được gia hạn thì trả bản mới |
| `POST /api/devices/heartbeat` | `{ "heartbeat": "TKH1.…" }` | `{ license: { status: active \| revoked \| expired \| none, licenseId, expiresAt, text }, latestRelease, updateAvailable, updateRequired, serverTime }`. `updateRequired`: có bản **bắt buộc** mới hơn phiên bản máy đang chạy. `text` chỉ có khi máy chưa giữ bản quyền hiện hành (vừa cấp lại hoặc gia hạn). Ghi lại phiên bản đang chạy và lần cuối hoạt động |
| `GET /api/releases/latest` | — | `{ version, notes, mandatory, downloadUrl, publishedAt }` hoặc `204` khi chưa có phiên bản |

Khi bị từ chối, API trả `400`, `404` hoặc `409` với `{ "error": "…" }`. Các mã lỗi:

- `malformed`: không đọc được;
- `badSignature`: chữ ký sai;
- `badDevice`: mã thiết bị không hợp lệ;
- `keyMismatch`: mã thiết bị đã gắn với khóa khác;
- `unknownDevice`: heartbeat từ máy chưa đăng ký;
- `stale`: giờ của heartbeat lệch quá 15 phút;
- `notFound`: không có yêu cầu này.

Gọi quá giới hạn thì nhận `429`.

## Phía máy tính (bản Desktop của Toolkit)

Mã nằm trong `src/ToolboxWeb.Web`, chỉ chạy ở chế độ Desktop. `WireFormat.cs` của app này được liên kết vào đó, nên hai bên dùng chung một định dạng.

**Cấu hình** trong `appsettings.Desktop.json`, mục `License`:

| Khóa | Ý nghĩa |
|---|---|
| `ServerUrl` | Địa chỉ máy chính, ví dụ `https://license.<miền>`. Để trống thì chỉ kích hoạt thủ công được |
| `PublicKey` | Khóa công khai một dòng (hoặc PEM) lấy từ trang **Khóa ký**. Trang Cài đặt của máy tính hiện dấu khóa SHA-256 của khóa này, để so với trang Khóa ký |
| `CheckHours` | Số giờ giữa hai lần báo về máy chính (mặc định 6) |

Chạy `dotnet run -- --desktop` ở môi trường Development mà không có `PublicKey` thì không kiểm tra bản quyền. Ngoài Development, thiếu khóa thì app chỉ mở màn hình Kích hoạt kèm báo lỗi cấu hình.

**Danh tính máy:**

- Lần đầu cần tới, app tạo mã thiết bị (GUID) và cặp khóa ECDSA P-256, lưu trong `device.json` của thư mục dữ liệu.
- Khóa riêng được mã hóa bằng Data Protection.
- Khi chép `device.json` sang máy khác, hoặc tên máy đổi, máy đó nhận danh tính mới và phải kích hoạt lại.

**Trạng thái và cách app xử lý:**

| Trạng thái | App làm gì |
|---|---|
| Chưa kích hoạt (hoặc bản quyền sai chữ ký, của máy khác) | Mọi trang chuyển về `/activate.html` (không có sidebar); yêu cầu ghi trả 403 |
| Còn hiệu lực | Bình thường; còn từ 14 ngày trở xuống thì có thanh nhắc gia hạn |
| Hết hạn / bị thu hồi | **Chỉ đọc**: xem được mọi trang, có thanh báo. Mọi POST bị từ chối, trừ những thao tác gắn `[AllowWhenUnlicensed]`: kích hoạt, Cài đặt, đổi ngôn ngữ, thoát app, tìm Jira, So sánh DB, tải dữ liệu cá nhân. Hẹn giờ không lưu phiên |

**Màn hình Kích hoạt (`/activate.html`):**

1. **Gửi yêu cầu qua mạng:** app gửi `TKT1` lên `POST /api/activations`, rồi hỏi lại mỗi 10 giây (khi trang đang mở) hoặc mỗi 2 phút (chạy nền). Được duyệt thì app tự mở.
2. **Thủ công:** sao chép mã kích hoạt gửi người quản trị; họ dán vào trang Yêu cầu kích hoạt, duyệt, rồi gửi lại bản quyền `TKL1`. Người dùng dán bản quyền và bấm "Áp dụng bản quyền".

**Báo về máy chính (heartbeat):**

- **Lịch gửi:** 15 giây sau khi mở app, sau đó mỗi `CheckHours` giờ; hoặc khi bấm "Kiểm tra ngay" trong Cài đặt.
- **Gửi đi:** phiên bản đang chạy.
- **Nhận về và xử lý:**
  - bản gia hạn hoặc cấp lại: app tự lưu;
  - tin thu hồi: app chuyển sang chỉ đọc;
  - phiên bản mới nhất: hiện thanh "Đã có Toolkit x.y.z", có nút "Tải về" (nếu có đường tải) và "Để sau". Bản bắt buộc thì không ẩn được. Cài cập nhật thật làm ở M5.
- **Không có mạng:** app vẫn dùng tới ngày hết hạn ghi trong bản quyền (chủ dự án chọn không bắt buộc liên lạc định kỳ). Thu hồi chỉ có hiệu lực khi máy kết nối lại.

## Dựng trên IIS (máy chính)

1. Cài **ASP.NET Core Hosting Bundle 9** trên máy chủ.
2. Publish:

   ```
   dotnet publish src/ToolkitLicense.Web -c Release -o <thư mục publish>
   ```

   Thư mục publish đã có `Content/` (gồm cả các file giao diện dùng chung) và `web.config`.
3. Tạo site IIS riêng, ví dụ `license.<miền>`:
   - **bắt buộc HTTPS** với chứng chỉ hợp lệ, vì cookie đăng nhập chỉ gửi qua HTTPS khi không chạy ở môi trường Development;
   - app pool "No Managed Code".
4. Đặt thư mục dữ liệu **ngoài thư mục site**, ví dụ `D:\ToolkitData\License`. Chọn một trong hai cách:
   - `appsettings.Production.json`: `{ "Storage": { "DataRoot": "D:\\ToolkitData\\License" } }`;
   - biến môi trường `Storage__DataRoot` trong `web.config`.

   Cấp quyền ghi thư mục này cho tài khoản app pool (`IIS AppPool\<tên pool>`).
5. Mở site, làm các bước "Lần chạy đầu" ở trên.
6. **Sao lưu định kỳ** thư mục dữ liệu, ít nhất `license.db`, `signing-key.pem` và `keys/`:
   - mất `signing-key.pem` thì không cấp được bản quyền mà app tin nữa;
   - lộ `signing-key.pem` thì người khác tự tạo được bản quyền;
   - mất `keys/` thì phải bật lại xác thực hai lớp.
7. Có thể giới hạn site chỉ nhận truy cập từ IP của bạn (IP Address and Domain Restrictions), **trừ** đường `/api/`, vì các máy cài Toolkit cần gọi được API.

Cập nhật app: publish đè lên thư mục site. Thư mục dữ liệu ở ngoài nên không bị đụng tới. Khi có migration mới, app tự sao lưu DB vào `backups/` trước khi nâng cấp.

## Chạy thử trên máy phát triển

```
dotnet run --project src/ToolkitLicense.Web
```

Mở `http://localhost:5190`. Dữ liệu nằm trong `src/ToolkitLicense.Web/App_Data/` (đã có trong `.gitignore`). Mã cài đặt nằm trong `App_Data/setup-code.txt`.

## Giới hạn đã biết

- Ứng dụng .NET có thể bị dịch ngược để bỏ kiểm tra bản quyền. Với công cụ nội bộ thì chấp nhận được.
- Máy không bao giờ kết nối lại thì không biết mình bị thu hồi (theo lựa chọn không bắt buộc liên lạc định kỳ).
- Chỉ có một tài khoản quản trị (chủ dự án). Muốn thêm người duyệt thì cần làm thêm phân quyền.
- Đổi khóa ký (khi lộ khóa) cần phát hành bản desktop mới chứa khóa công khai mới và cấp lại mọi bản quyền. Hiện chưa có trang làm việc này.
