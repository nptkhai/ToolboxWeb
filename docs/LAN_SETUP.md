# Dựng Toolkit trên mạng LAN (trên máy của bạn)

Hướng dẫn từng bước để chạy toàn bộ Toolkit trên **một máy Windows của bạn** và dùng trong **mạng LAN** (cùng Wi-Fi hoặc cùng mạng công ty):

| Thành phần | Địa chỉ (ví dụ IP máy là `192.168.1.10`) | Dùng để |
|---|---|---|
| **Site chính**: Toolkit License | `https://192.168.1.10:8443` | Duyệt kích hoạt, quản lý bản quyền, phát hành phiên bản, trang tải bộ cài |
| **Site Web**: Toolkit chế độ Web | `https://192.168.1.10:9443` | Mở Toolkit bằng trình duyệt và **điện thoại** (Jira, hẹn giờ) |
| **Desktop** | cài trên từng máy Windows (`http://localhost:5180`, chỉ máy đó mở được) | Toolkit đầy đủ, dữ liệu nằm trên máy |
| **Mobile** | điện thoại mở site Web rồi "Thêm vào màn hình chính" | Như app trên điện thoại |

Thay `192.168.1.10` bằng IP thật của máy bạn trong mọi lệnh bên dưới.

> **Vì sao cần chứng chỉ riêng cho LAN?** Site chính chỉ cho đăng nhập quản trị qua HTTPS, và điện thoại chỉ cho cài app khi site có HTTPS hợp lệ. Let's Encrypt (miễn phí, xem `docs/RELEASE.md`) cần tên miền công khai nên không dùng được trong LAN. Vì vậy ta dùng **mkcert**: công cụ miễn phí (BSD-3-Clause) tạo một "nhà cấp chứng chỉ" riêng của bạn, rồi cài nó lên các thiết bị của bạn để chúng tin chứng chỉ đó.

---

## 0. Chuẩn bị (làm một lần)

**0.1. Giữ IP cố định cho máy**
1. Mở PowerShell, chạy `ipconfig` và ghi lại "IPv4 Address" của card Wi-Fi hoặc Ethernet, ví dụ `192.168.1.10`.
2. Vào trang quản trị router, tìm mục *DHCP Reservation* / *Address Reservation* và gán cố định IP đó cho máy.
   - Nếu IP đổi, phải làm lại chứng chỉ và đóng gói lại Desktop (mục 7).

**0.2. Mạng là "Private" và mở cổng**
1. Settings → Network & Internet → Wi-Fi (hoặc Ethernet) → mạng đang dùng → **Network profile: Private**.
2. PowerShell **chạy bằng quyền Administrator**:

   ```powershell
   New-NetFirewallRule -DisplayName "Toolkit LAN" -Direction Inbound -Protocol TCP -LocalPort 8443,9443 -Action Allow -Profile Private
   ```

**0.3. Cài IIS và .NET**
1. Bật IIS: `Win + R` → `optionalfeatures` → tick **Internet Information Services**, mở rộng và tick thêm *Web Management Tools → IIS Management Console*. Bấm OK.
2. Tải và cài **ASP.NET Core 9.0 Runtime – Windows Hosting Bundle** từ trang .NET 9 của Microsoft (dotnet.microsoft.com → .NET 9.0 → *Hosting Bundle*). Cài **sau** khi đã bật IIS.
3. Cài **.NET SDK 9** (cùng trang) để build.
4. Mở lại PowerShell, chạy `iisreset`.

**0.4. Lấy mã nguồn**

```powershell
cd D:\
git clone https://github.com/nptkhai/ToolboxWeb.git
cd ToolboxWeb
git checkout claude/relaxed-goodall-e9palp   # nhánh có M3–M5; sau khi gộp vào khainpt_dev thì dùng khainpt_dev
```

**0.5. Thư mục dữ liệu** (nằm ngoài thư mục web, cập nhật site không đụng tới):

```powershell
New-Item -ItemType Directory -Force C:\ToolkitData\License, C:\ToolkitData\Web | Out-Null
```

---

## 1. Chứng chỉ HTTPS cho LAN (mkcert)

1. Cài mkcert: `winget install FiloSottile.mkcert`. Hoặc tải `mkcert-…-windows-amd64.exe` từ trang GitHub của mkcert, đổi tên thành `mkcert.exe` và đặt vào một thư mục trong PATH.
2. Tạo "nhà cấp chứng chỉ" riêng và cài nó vào Windows (bấm **Yes** khi Windows hỏi):

   ```powershell
   mkcert -install
   ```
3. Tạo chứng chỉ cho IP và tên máy (`$env:COMPUTERNAME`):

   ```powershell
   cd C:\ToolkitData
   mkcert -pkcs12 -p12-file toolkit-lan.p12 192.168.1.10 localhost $env:COMPUTERNAME
   ```

   Ra file `C:\ToolkitData\toolkit-lan.p12`, mật khẩu mặc định là `changeit`.
4. Đưa chứng chỉ vào IIS:
   - mở **IIS Manager** (`inetmgr`), chọn tên máy ở cột trái;
   - mở **Server Certificates** → **Import…**;
   - chọn `toolkit-lan.p12`, mật khẩu `changeit`, store **Personal** → OK;
   - nếu cột *Name* trống: bấm chuột phải vào chứng chỉ → *Edit…* (hoặc *Rename*), đặt tên `toolkit-lan` để dễ chọn.
5. Lấy file CA để cài lên các thiết bị khác:

   ```powershell
   mkcert -CAROOT        # in ra thư mục, ví dụ C:\Users\<bạn>\AppData\Local\mkcert
   Copy-Item "$(mkcert -CAROOT)\rootCA.pem" C:\ToolkitData\toolkit-lan-ca.crt
   ```

   File `toolkit-lan-ca.crt` **được phép** chép sang máy và điện thoại khác. File `rootCA-key.pem` trong cùng thư mục là **bí mật**: không chép đi đâu, chỉ sao lưu kín.

---

## 2. Site chính: Toolkit License

**2.1. Publish**

```powershell
cd D:\ToolboxWeb
dotnet publish src/ToolkitLicense.Web -c Release -o C:\inetpub\toolkit-license
```

**2.2. Tạo site trong IIS Manager**
1. **Application Pools → Add Application Pool…**:
   - Name: `ToolkitLicense`;
   - .NET CLR version: **No Managed Code** → OK.
2. **Sites → Add Website…**:
   - Site name: `ToolkitLicense`;
   - Application pool: `ToolkitLicense`;
   - Physical path: `C:\inetpub\toolkit-license`;
   - Binding:
     - Type **https**;
     - IP **All Unassigned**;
     - Port **8443**;
     - SSL certificate **toolkit-lan**.

   Bấm OK.
3. Cho app pool quyền ghi vào thư mục dữ liệu, và đặt thư mục dữ liệu (PowerShell Administrator):

   ```powershell
   icacls C:\ToolkitData\License /grant "IIS AppPool\ToolkitLicense:(OI)(CI)M"
   & "$env:windir\system32\inetsrv\appcmd.exe" set config "ToolkitLicense" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Storage__DataRoot',value='C:\ToolkitData\License']" /commit:apphost
   iisreset
   ```

   Lệnh `appcmd … /commit:apphost` ghi cấu hình vào IIS chứ không vào `web.config`, nên publish lại không làm mất.

**2.3. Lần chạy đầu**
1. Trên máy này, mở Edge: `https://192.168.1.10:8443`. Phải thấy ổ khóa, không có cảnh báo. Trang chuyển tới **/setup**.
2. Mở file `C:\ToolkitData\License\setup-code.txt`, nhập mã trong đó, đặt tên đăng nhập và mật khẩu (ít nhất 10 ký tự, có chữ hoa, chữ thường, chữ số).
3. **Bật xác thực hai lớp** (bắt buộc):
   - cài Microsoft Authenticator hoặc Google Authenticator trên điện thoại;
   - chọn thêm tài khoản bằng cách nhập khóa, nhập khóa hiện trên trang, rồi nhập mã 6 số.
4. **Lưu 10 mã khôi phục** vào trình quản lý mật khẩu hoặc giấy.
5. Vào **Khóa ký**:
   - bấm "Sao chép khóa một dòng";
   - dán vào file `C:\ToolkitData\public-key.txt` (Notepad → Save);
   - sao lưu file `C:\ToolkitData\License\signing-key.pem` ra nơi an toàn.

Nếu site báo lỗi **500.30**: Hosting Bundle chưa cài, hoặc cài trước IIS (cài lại rồi `iisreset`); hoặc thiếu quyền ở bước 2.2.3. Cách xem lỗi chi tiết:
- mở `C:\inetpub\toolkit-license\web.config`, đặt `stdoutLogEnabled="true"` và `stdoutLogFile="C:\ToolkitData\License\stdout"`;
- tải lại trang và đọc file log;
- tắt `stdoutLogEnabled` khi xong.

---

## 3. Site Web (trình duyệt và điện thoại)

**3.1. Publish**

```powershell
cd D:\ToolboxWeb
dotnet publish src/ToolboxWeb.Web -c Release -o C:\inetpub\toolkit-web
```

**3.2. Tạo site**
1. Application pool `ToolkitWeb`, **No Managed Code**.
2. Website:
   - Name `ToolkitWeb`, path `C:\inetpub\toolkit-web`;
   - binding **https**, cổng **9443**, chứng chỉ **toolkit-lan**.
3. Đặt chế độ Web và thư mục dữ liệu (PowerShell Administrator):

   ```powershell
   icacls C:\ToolkitData\Web /grant "IIS AppPool\ToolkitWeb:(OI)(CI)M"
   $appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
   & $appcmd set config "ToolkitWeb" -section:system.webServer/aspNetCore /+"environmentVariables.[name='App__Mode',value='Web']" /commit:apphost
   & $appcmd set config "ToolkitWeb" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Storage__DataProtectionKeysPath',value='C:\ToolkitData\Web\keys']" /commit:apphost
   & $appcmd set config "ToolkitWeb" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Logging__File__Directory',value='C:\ToolkitData\Web\logs']" /commit:apphost
   iisreset
   ```

   Bản Web không có database. Nó chỉ lưu khóa của cookie đăng nhập (`keys`) và log.
4. Mở `https://192.168.1.10:9443` và đăng nhập bằng tài khoản Jira.
   - Máy này phải vào được Jira (`https://task.ascvn.com.vn`, khóa `Jira:DefaultBaseUrl`).
   - Jira ở địa chỉ khác thì thêm biến `Jira__DefaultBaseUrl` theo cách như trên.

---

## 4. Desktop: đóng gói, đưa lên site chính, cài trên từng máy

**4.1. Đóng gói** (trên máy này, làm một lần cho mỗi phiên bản)

```powershell
cd D:\ToolboxWeb
dotnet tool install -g vpk --version 1.2.161     # chỉ lần đầu
./tools/release/publish-desktop.ps1 -Version 1.0.0 `
    -ServerUrl https://192.168.1.10:8443 `
    -PublicKeyFile C:\ToolkitData\public-key.txt `
    -CopyTo C:\ToolkitData\License\updates
```

- `-Version` phải bằng `<Version>` trong `src/ToolboxWeb.Web/ToolboxWeb.Web.csproj` (hiện là `1.0.0`).
- `-CopyTo` chép bộ cài và gói cập nhật thẳng vào thư mục của site chính. Nếu đóng gói ở máy khác thì bỏ tham số này, rồi tải các file trong `artifacts\releases` lên trang **Phiên bản → Tải lên**.
- PowerShell báo không cho chạy script thì chạy một lần `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.

**4.2. Phát hành trên site chính**
1. `https://192.168.1.10:8443/releases`: ô phiên bản đã điền sẵn `1.0.0`, đường tải về là `/download`.
2. Ghi chú (tùy ý) → **Phát hành**.

**4.3. Cài trên mỗi máy Windows**
1. **Máy khác** máy chủ (máy này đã tin CA từ bước 1):
   - chép `C:\ToolkitData\toolkit-lan-ca.crt` sang máy đó;
   - PowerShell Administrator: `certutil -addstore -f Root toolkit-lan-ca.crt`.
2. Mở Edge: `https://192.168.1.10:8443/download` → **Tải bộ cài (Setup.exe)**.
3. Mở `ToolkitApp-win-Setup.exe`. Windows hiện *"Windows protected your PC"* → **More info** → **Run anyway** (bộ cài chưa ký số; chỉ hỏi một lần).
4. Toolkit tự mở trong cửa sổ riêng và hiện màn hình **Kích hoạt** → bấm **Gửi yêu cầu kích hoạt**.
5. Trên site chính: **Yêu cầu kích hoạt** → bấm vào máy vừa gửi → chọn người dùng và thời hạn → **Duyệt**. Trong khoảng 10 giây, Toolkit trên máy kia tự mở.
6. Trong Toolkit Desktop: **Cài đặt** → nhập kết nối Jira (địa chỉ, tài khoản hoặc token) → **Lưu**.
7. Nếu có dữ liệu trên site Toolkit cũ (chế độ Server):
   - ở site cũ: **Tài khoản → Dữ liệu cá nhân → Xuất dữ liệu Toolkit**;
   - ở Desktop: **Tài khoản → Dữ liệu cá nhân → Nhập dữ liệu** → Xem trước → Nhập.

Vị trí cài:
- app nằm ở `%LOCALAPPDATA%\ToolkitApp`, dữ liệu ở `%LOCALAPPDATA%\Toolkit`;
- gỡ cài đặt (Settings → Apps) **không** xóa dữ liệu;
- đóng cửa sổ thì Toolkit vẫn chạy nền; thoát hẳn bằng menu tài khoản → **Thoát Toolkit**.

---

## 5. Mobile (điện thoại cùng Wi-Fi)

**5.1. Cài CA lên điện thoại** (một lần)

Gửi file `toolkit-lan-ca.crt` sang điện thoại (Zalo, email, cáp USB…), rồi:

- **Android:**
  - Cài đặt → **Bảo mật** (hoặc *Bảo mật và quyền riêng tư*) → *Cài đặt bảo mật khác* → **Mã hóa và thông tin xác thực** → **Cài đặt chứng chỉ** → **Chứng chỉ CA**;
  - chọn file → xác nhận "Vẫn cài đặt".
  - Tên các mục khác nhau tùy hãng; có thể tìm "chứng chỉ CA" trong ô tìm kiếm của Cài đặt.
- **iPhone:**
  1. Mở file bằng Safari hoặc Tệp → "Đã tải hồ sơ".
  2. Cài đặt → **Đã tải về hồ sơ** → **Cài đặt**.
  3. Cài đặt → Cài đặt chung → Giới thiệu → **Cài đặt tin cậy chứng nhận** → bật cho chứng chỉ "mkcert …".

**5.2. Mở và cài app**
1. Điện thoại vào cùng Wi-Fi với máy chủ.
2. Mở `https://192.168.1.10:9443` (Chrome trên Android, Safari trên iPhone). Phải thấy ổ khóa.
3. Đăng nhập Jira.
4. Cài như app:
   - **Android Chrome:** menu ⋮ → **Cài đặt ứng dụng** (hoặc *Thêm vào màn hình chính*);
   - **iPhone Safari:** nút **Chia sẻ** → **Thêm vào MH chính**.

Biểu tượng Toolkit xuất hiện trên màn hình chính. Bản mobile có các màn hình Jira và hẹn giờ; ghi chú, việc cần làm và công cụ DB chỉ có trên Desktop.

---

## 6. Phát hành bản mới về sau

1. Tăng `<Version>` trong `src/ToolboxWeb.Web/ToolboxWeb.Web.csproj`, ví dụ `1.1.0`.
2. Chạy lại `publish-desktop.ps1` với `-Version 1.1.0`. Giữ nguyên thư mục `artifacts\releases` giữa các lần để có gói chênh lệch nhỏ.
3. Trang **Phiên bản** → **Phát hành** `1.1.0`. Tick "Bắt buộc" nếu muốn mọi máy phải cập nhật.
4. Các máy Desktop thấy thanh **"Đã có Toolkit 1.1.0" → Cập nhật ngay**, trong vòng 6 giờ hoặc ngay khi bấm *Cài đặt → Kiểm tra ngay*.
5. Cập nhật site chính và site Web: dừng app pool, publish lại, rồi chạy lại.

   ```powershell
   Import-Module WebAdministration
   Stop-WebAppPool ToolkitWeb
   dotnet publish src/ToolboxWeb.Web -c Release -o C:\inetpub\toolkit-web
   Start-WebAppPool ToolkitWeb
   ```

   Làm tương tự với `ToolkitLicense` và `src/ToolkitLicense.Web`. Cấu hình đặt bằng `appcmd … /commit:apphost` vẫn giữ nguyên.

---

## 7. Khi IP máy đổi

1. Tạo lại chứng chỉ (mục 1, bước 3–4) với IP mới, rồi trong IIS Manager → site → **Bindings…** → chọn chứng chỉ mới cho cả hai site.
   - CA không đổi, nên thiết bị và điện thoại **không** phải cài lại `toolkit-lan-ca.crt`.
2. Đóng gói lại Desktop với `-ServerUrl https://<IP mới>:8443` và một số phiên bản mới, rồi cài lại trên các máy.
   - Địa chỉ máy chính được ghi vào bản cài, nên bản cũ không tự tìm được địa chỉ mới.
   - Dữ liệu trên máy được giữ nguyên.

Vì vậy nên giữ IP cố định ngay từ đầu (bước 0.1).

---

## 8. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Trình duyệt báo "Kết nối không riêng tư" | Thiết bị chưa tin CA | Cài `toolkit-lan-ca.crt` (mục 4.3 hoặc 5.1); mở đúng IP có trong chứng chỉ |
| Điện thoại không mở được trang | Khác Wi-Fi, mạng máy chủ đang là Public, chưa mở cổng | Mục 0.2; thử `ping 192.168.1.10` từ máy khác |
| Không có nút "Cài đặt ứng dụng" trên điện thoại | Trang chưa có HTTPS hợp lệ (không có ổ khóa) | Cài CA lên điện thoại; mở bằng `https://` |
| Desktop báo "Không kết nối được máy chủ bản quyền" | Máy đó chưa tin CA, sai IP/cổng, site chính đang dừng | `certutil -addstore -f Root toolkit-lan-ca.crt`; mở thử `https://192.168.1.10:8443/download` trên máy đó |
| Không đăng nhập được trang quản trị (quay lại trang đăng nhập) | Mở bằng `http://` | Luôn dùng `https://…:8443` |
| Site báo 500.30 | Chưa cài Hosting Bundle (hoặc cài trước IIS), thiếu quyền thư mục dữ liệu | Mục 0.3 và lệnh `icacls`; bật `stdoutLogEnabled` để xem lỗi |
| "Cập nhật ngay" báo chưa có gói cập nhật | Chưa chép hoặc tải gói lên, hoặc chưa bấm Phát hành | Mục 6 |

---

## Cần sao lưu

- `C:\ToolkitData\License`: database bản quyền, **`signing-key.pem`** (mất thì không cấp được bản quyền mà app tin), `keys\`, `updates\`.
- `%LOCALAPPDATA%\Toolkit` trên từng máy Desktop: dữ liệu của bạn. Có thể dùng thêm "Xuất dữ liệu Toolkit".
- Thư mục `mkcert -CAROOT`, gồm **`rootCA-key.pem`**: giữ kín. Mất file này thì phải tạo CA mới và cài lại lên mọi thiết bị.
- Mã khôi phục xác thực hai lớp của tài khoản quản trị.
