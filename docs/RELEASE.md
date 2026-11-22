# Phát hành Toolkit cho máy tính (Windows)

Tài liệu này dành cho chủ dự án. Nội dung: đóng gói bộ cài, đưa lên máy chính, cách các máy tự cập nhật, chuyển dữ liệu từ site IIS sang máy tính, và gắn HTTPS miễn phí cho các site.

## Tổng quan

```
Máy phát triển                            Máy chính (Toolkit License, IIS, HTTPS)          Máy người dùng
tools/release/publish-desktop.ps1  ──►    Phiên bản > Tải lên        ──►  /updates/...  ◄── Toolkit tự cập nhật
  dotnet publish + vpk pack               Phiên bản > Phát hành 1.x.y                         (heartbeat → "Cập nhật ngay")
  → artifacts/releases/                   /download (trang công khai) ──► ToolkitApp-win-Setup.exe (lần cài đầu)
```

- **Bộ cài:** dùng Velopack (MIT). App được cài vào tài khoản Windows của người dùng (`%LOCALAPPDATA%\ToolkitApp`), không cần quyền quản trị, có lối tắt "Toolkit" trên Desktop và Start Menu.
- **Không có cửa sổ dòng lệnh:** bản cài được build dạng ứng dụng Windows. Log ghi vào `%LOCALAPPDATA%\Toolkit\logs` để tra lỗi khi cần. Lối tắt và thanh tác vụ mang icon Toolkit, gắn vào file `.exe` từ `docs/brand/toolkit.ico`.
- **Dữ liệu** nằm riêng ở `%LOCALAPPDATA%\Toolkit`, nên **gỡ cài đặt hay cập nhật không xóa dữ liệu**. Muốn xóa dữ liệu thì dùng Tài khoản > Dữ liệu cá nhân > "Xóa dữ liệu trên máy".
- **Cửa sổ app:** Toolkit mở trong Microsoft Edge (hoặc Chrome) ở chế độ app: cửa sổ riêng, không có thanh địa chỉ.
  - Đóng cửa sổ thì Toolkit vẫn chạy nền; bấm lối tắt để mở lại.
  - Thoát hẳn: menu tài khoản > "Thoát Toolkit".
  - Muốn mở bằng tab trình duyệt thường thì đặt `Desktop:Window = "Browser"` trong `appsettings.Desktop.json`.
  - Vị trí và kích thước cửa sổ: lần mở đầu tiên cửa sổ rộng 1320×880; sau đó Edge/Chrome nhớ lại vị trí và kích thước bạn để.
- **Mở tệp .md bằng Toolkit:** khi cài (và mỗi lần cập nhật), Toolkit thêm mình vào danh sách "Open with" của tệp `.md`, `.markdown` cho tài khoản Windows hiện tại (`HKCU\Software\Classes`), không tự đặt làm chương trình mặc định. Muốn nhấp đúp là mở bằng Toolkit: chuột phải tệp `.md` > Open with > Choose another app > Toolkit > Always. Gỡ cài đặt thì mục này cũng được gỡ.

## Chuẩn bị một lần

1. Cài .NET SDK 9 trên máy phát triển (Windows).
2. Cài công cụ đóng gói: `dotnet tool install -g vpk --version 1.2.161`. Phiên bản này phải khớp gói `Velopack` trong `ToolboxWeb.Web.csproj`.
3. Lấy **khóa công khai một dòng** ở trang **Khóa ký** của máy chính, lưu vào một file, ví dụ `D:\toolkit\public-key.txt`.
4. Giữ nguyên thư mục `artifacts/releases` giữa các lần phát hành. Velopack dựa vào bản trước trong thư mục này để tạo **gói chênh lệch (delta)**, chỉ vài trăm KB thay vì cả gói khoảng 90 MB.

## Mỗi lần phát hành

1. Tăng `<Version>` trong `src/ToolboxWeb.Web/ToolboxWeb.Web.csproj`, ví dụ `1.1.0`.
2. Chạy script ở thư mục gốc repo:

   ```powershell
   ./tools/release/publish-desktop.ps1 -Version 1.1.0 `
       -ServerUrl https://license.<miền> `
       -PublicKeyFile D:\toolkit\public-key.txt
   ```

   Script làm các việc sau:
   - publish bản Windows x64 có kèm .NET (máy người dùng không cần cài .NET);
   - ghi địa chỉ máy chính và khóa công khai vào `appsettings.Desktop.json` của bản publish (không sửa file trong repo);
   - thêm file `toolkit.desktop` để app chạy ở chế độ Desktop;
   - chạy `vpk pack`.

   Kết quả nằm trong `artifacts/releases`:
   - `ToolkitApp-win-Setup.exe`: bộ cài lần đầu;
   - `ToolkitApp-1.1.0-full.nupkg` và `-delta.nupkg`: gói cập nhật;
   - `releases.win.json`: danh mục mà app đọc để biết có bản mới;
   - `assets.win.json`, `RELEASES`, `ToolkitApp-win-Portable.zip`.
3. Trên máy chính: **Phiên bản > Bộ cài và gói cập nhật > Tải lên**, chọn toàn bộ file trong `artifacts/releases`. Nếu máy phát triển chép được thẳng vào thư mục dữ liệu của máy chính thì dùng `-CopyTo \\may-chinh\ToolkitData\License\updates`.
4. Vẫn trang đó, **Phát hành phiên bản**: ô phiên bản đã điền sẵn số mới, đường tải về là trang `/download`. Ghi chú thay đổi, tick "Bắt buộc" nếu cần, rồi bấm Phát hành.
5. Trong vòng vài giờ (hoặc ngay khi người dùng bấm "Kiểm tra ngay" trong Cài đặt), các máy thấy thanh "Đã có Toolkit 1.1.0".
   - Họ bấm **Cập nhật ngay**: app tải bản mới, tự đóng, cài, rồi mở lại; cửa sổ đang mở tự tải lại.
   - Trước khi nâng cấp database, app tự sao lưu vào `%LOCALAPPDATA%\Toolkit\backups`.
   - Bản **bắt buộc**: máy cài bằng bộ cài sẽ chỉ còn trang Cập nhật cho tới khi cập nhật xong.

**Đóng gói từ Linux hoặc macOS:** dùng `tools/release/publish-desktop.sh`. Cần cài `zstd` để tạo gói delta; cách này không ký số được.

## Ký số bộ cài

Hiện **không ký** (chủ dự án quyết định ngày 16/11/2026). Hệ quả:

- Lần đầu mở `ToolkitApp-win-Setup.exe`, Windows hiện "Windows protected your PC". Bấm **More info** rồi **Run anyway**; mỗi máy chỉ gặp một lần, lúc cài. Trang `/download` đã có hướng dẫn này.
- Các bản cập nhật sau đó do app tự tải nên thường không hỏi lại.
- Thỉnh thoảng phần mềm diệt virus báo nhầm file chưa ký; khi đó thêm Toolkit vào danh sách cho phép.

Khi muốn ký, script đã có sẵn tham số, không phải sửa mã:

- **Chứng chỉ mua** (file `.pfx` hoặc USB token), chạy trên Windows có `signtool`:

  ```powershell
  ./tools/release/publish-desktop.ps1 ... -SignParams '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f D:\certs\toolkit.pfx /p <mật khẩu>'
  ```

- **Chứng chỉ tự tạo** (chỉ dùng trên máy của bạn): tạo bằng `New-SelfSignedCertificate -Type CodeSigningCert`, cài vào "Trusted Root Certification Authorities" và "Trusted Publishers" trên từng máy, rồi ký như trên. SmartScreen vẫn có thể cảnh báo, vì nó dựa vào uy tín do Microsoft tính chứ không dựa vào chứng chỉ bạn tự cài.

## Chuyển dữ liệu từ site IIS sang máy tính

1. Trên site IIS (chế độ Server), mở **Tài khoản > Dữ liệu cá nhân > Xuất dữ liệu Toolkit**.
   - File `toolkit-data-<ngày>.json` gồm: ghi chú (kể cả ghi chú con), việc cần làm, phiên tập trung, liên kết nhanh, prompt, ảnh đại diện.
   - Không gồm mật khẩu máy chủ lưu trong ghi chú (chúng được mã hóa bằng khóa riêng của site); bạn nhập lại trên máy tính.
2. Trên Toolkit máy tính, mở **Tài khoản > Dữ liệu cá nhân > Nhập dữ liệu**: chọn file, bấm **Xem trước** để xem số mục sẽ thêm, rồi **Nhập dữ liệu** và xác nhận.
3. Mục đã có (cùng tiêu đề và thời điểm tạo, cùng địa chỉ liên kết, cùng tên prompt) được bỏ qua. Nhập lại cùng một file không tạo bản trùng.

Cách này cũng dùng được khi chuyển sang máy tính mới.

## HTTPS miễn phí cho các site trên máy chính (Let's Encrypt)

> Chỉ dùng trong mạng LAN, không có tên miền công khai? Dùng mkcert thay cho Let's Encrypt, xem `docs/LAN_SETUP.md`.

Site Toolkit License (`license.<miền>`) và site Web/điện thoại (`toolkit.<miền>`) đều phải chạy HTTPS:

- cookie đăng nhập quản trị chỉ gửi qua HTTPS;
- điện thoại chỉ cho cài app (PWA) khi site có HTTPS hợp lệ.

Chứng chỉ Let's Encrypt **miễn phí** và tự gia hạn. Ký số bộ cài là việc khác, xem mục trên.

**Cần có:**

- Một tên miền, ví dụ mua khoảng 10 USD/năm, hoặc tên miền con miễn phí của dịch vụ DNS động. Tạo bản ghi A cho `license.<miền>` và `toolkit.<miền>` trỏ về IP công khai của máy chính.
- Cổng 80 và 443 của máy chính mở ra Internet (trên router hoặc tường lửa). Let's Encrypt kiểm tra tên miền qua cổng 80.

**Các bước:**

1. Trong IIS Manager, mỗi site có binding `http` cổng 80 với Host name đúng tên miền (`license.<miền>`, `toolkit.<miền>`).
2. Tải **win-acme** (miễn phí, mã nguồn mở) từ win-acme.com, giải nén vào `C:\win-acme`, chạy `wacs.exe` bằng quyền quản trị.
3. Chọn **N** (Create certificate, default settings), chọn site IIS cần cấp và đồng ý điều khoản Let's Encrypt.
   - win-acme tự xác minh tên miền, cài chứng chỉ, thêm binding `https` cổng 443 cho site.
   - Nó cũng tạo **tác vụ hằng ngày** để tự gia hạn trước khi hết hạn (chứng chỉ có hạn 90 ngày).
4. Lặp lại cho site còn lại. Kiểm tra bằng cách mở `https://license.<miền>/download` trên điện thoại: phải thấy ổ khóa, không có cảnh báo.
5. Trong `publish-desktop.ps1`, dùng địa chỉ `https://license.<miền>` cho `-ServerUrl`.

## Giới hạn và việc chưa kiểm tra được

- Cài, cập nhật và gỡ cài đặt thật trên Windows: đã đóng gói thử trên Linux, nhưng chưa chạy bộ cài trên Windows.
- Cửa sổ Edge `--app` chỉ có trên Windows; trên máy khác Toolkit mở tab trình duyệt.
- "Open with" cho tệp `.md` và việc chuyển tệp sang cửa sổ Toolkit đang chạy: chưa thử trên Windows.
- Bản Toolkit không cài bằng bộ cài (chạy từ thư mục publish, hoặc `dotnet run`) không tự cập nhật được. Thanh thông báo khi đó dẫn tới trang tải về.
