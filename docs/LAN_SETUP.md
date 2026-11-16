# Dựng Toolkit trên máy của bạn và dùng trong mạng Wi-Fi (LAN)

Tài liệu này viết để **Claude chạy trên máy bạn** (Claude Desktop / Claude Code) đọc và làm theo, từng bước có lệnh cụ thể. Bạn cũng có thể tự làm theo.

Sau khi xong, bạn có:

| Thành phần | Mở ở đâu | Dùng để |
|---|---|---|
| **Site chính**: Toolkit License | `https://localhost:8443` trên máy này | Quản trị bản quyền, duyệt kích hoạt, phát hành phiên bản |
| **Trang tải app máy tính** | `https://localhost:8443/download` trên máy này, hoặc `https://<địa-chỉ-máy>:8443/download` từ máy khác cùng Wi-Fi. Site Web cũng có nút **"Tải Toolkit cho máy tính"** (trang đăng nhập và menu tài khoản) dẫn tới trang này | Tải `ToolkitApp-win-Setup.exe` |
| **Site Web**: Toolkit chế độ Web | `https://localhost:9443` trên máy này, hoặc `https://<địa-chỉ-máy>:9443` từ iPhone cùng Wi-Fi | Jira và hẹn giờ trên trình duyệt; cài lên iPhone như một app |
| **App máy tính (Desktop)** | cài trên máy này | Toolkit đầy đủ, dữ liệu nằm trên máy |

**Không đổi IP máy.** Toàn bộ cách làm dưới đây không đụng tới IP, router, DNS hay card mạng.
- Trên máy này luôn dùng `localhost`.
- Từ iPhone dùng tên máy (`<TÊN-MÁY>.local`) hoặc IP hiện tại của máy. Khi IP đổi (router cấp lại), chỉ cần làm mới chứng chỉ bằng một lệnh (mục 7); iPhone và app máy tính không phải cài lại.

---

## Câu lệnh giao việc cho Claude

Mở Claude Desktop (hoặc terminal **Run as administrator** → `cd D:\Source_Toolbox\ToolboxWeb` → `claude`) và gửi:

```text
Đọc file D:\Source_Toolbox\ToolboxWeb\docs\LAN_SETUP.md và làm theo phần "Dành cho trợ lý" từ mục 0 đến mục 6, đúng thứ tự.
Tuân thủ mọi quy tắc trong mục "Quy tắc cho trợ lý". Trả lời tôi bằng tiếng Việt.
Trước khi làm, tóm tắt kế hoạch và cho tôi biết những việc cần quyền Administrator hoặc cần cài thêm phần mềm.
```

---

# Dành cho trợ lý

## Quy tắc cho trợ lý (bắt buộc)

1. **Không đổi IP, router, DNS, card mạng; không đặt IP tĩnh.** Chỉ được:
   - đặt cấu hình mạng Wi-Fi là *Private* (nếu đang Public; hỏi trước);
   - thêm đúng một luật tường lửa mở cổng 8443 và 9443 cho mạng *Private* (hỏi trước).
2. **Không sửa mã nguồn** trong `D:\Source_Toolbox\ToolboxWeb`; không commit, push, đổi nhánh, xóa hay ghi đè file đang có (kể cả `Content`, `DataProtectionKeys`, `logs`, `tmp` ở thư mục gốc). Chỉ được `git fetch` và `git pull --ff-only` trên nhánh `khainpt_dev` sau khi hỏi.
3. **Không đụng site và app pool IIS đang có.** Trước khi tạo gì, liệt kê chúng ra. **Không dùng `iisreset`**; chỉ dừng hoặc khởi động app pool `ToolkitLicense` và `ToolkitWeb`.
4. **Hỏi trước khi:** cài phần mềm (Hosting Bundle, .NET SDK, mkcert, vpk), bật tính năng Windows, hoặc làm việc không hoàn tác được.
5. **Dữ liệu** để ở `D:\ToolkitData`, ngoài repo và ngoài thư mục site. Thư mục site là `C:\inetpub\toolkit-license` và `C:\inetpub\toolkit-web`.
6. **Không làm thay chủ máy:**
   - tạo tài khoản quản trị và bật xác thực hai lớp;
   - sao chép khóa ở trang Khóa ký;
   - cài app trên máy, cài chứng chỉ lên iPhone.

   Đến những bước đó thì dừng, hướng dẫn chủ máy, chờ xác nhận rồi làm tiếp.
7. Mỗi bước: chạy, **kiểm tra kết quả**, báo một hai dòng. Lỗi thì đọc log, tìm nguyên nhân, sửa; không đoán. Các lệnh dưới đây là PowerShell chạy bằng quyền Administrator.

## Biến dùng chung

Chạy khối này ở đầu mỗi phiên PowerShell. Các bước sau dùng những biến này.

```powershell
$Repo        = 'D:\Source_Toolbox\ToolboxWeb'
$Data        = 'D:\ToolkitData'
$LicenseDir  = 'C:\inetpub\toolkit-license'
$WebDir      = 'C:\inetpub\toolkit-web'
$LicensePort = 8443
$WebPort     = 9443
$HostName    = $env:COMPUTERNAME
# IP hiện tại của máy (chỉ đọc, không thay đổi gì):
$Ip = (Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } | Select-Object -First 1).IPv4Address |
      Select-Object -First 1 -ExpandProperty IPAddress
$AppCmd = "$env:windir\system32\inetsrv\appcmd.exe"
"Tên máy: $HostName  |  IP hiện tại: $Ip"
```

## 0. Kiểm tra máy và mã nguồn

**0.1. Phần mềm cần có.** Báo cái nào có, cái nào thiếu:

```powershell
Get-Service W3SVC -ErrorAction SilentlyContinue                      # IIS
Test-Path "$env:ProgramFiles\IIS\Asp.Net Core Module\V2"              # ASP.NET Core Module (Hosting Bundle)
dotnet --list-runtimes | Select-String 'Microsoft.AspNetCore.App 9'  # runtime 9
dotnet --list-sdks | Select-String '^9\.'                            # SDK 9
git --version
```

Nếu thiếu (hỏi trước khi cài):
- **IIS:** `Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-ManagementConsole, IIS-ManagementScriptingTools -All`.
- **Hosting Bundle 9** (cài SAU IIS): `winget install Microsoft.DotNet.HostingBundle.9`. Nếu winget không có gói này, hướng dẫn chủ máy tải "ASP.NET Core 9.0 Runtime – Windows Hosting Bundle" trên trang .NET của Microsoft.
- **.NET SDK 9:** `winget install Microsoft.DotNet.SDK.9`.
- Cài xong Hosting Bundle thì khởi động lại dịch vụ IIS bằng `net stop was /y; net start w3svc`. Lệnh này dừng mọi site trong vài giây, nên báo chủ máy trước.

**0.2. Mã nguồn đúng nhánh `khainpt_dev`** (đã có site chính, bản quyền, bộ cài):

```powershell
cd $Repo
git fetch origin
git status --short
git branch --show-current
git rev-list --count HEAD..origin/khainpt_dev   # > 0 nghĩa là đang thiếu commit
Test-Path "$Repo\src\ToolkitLicense.Web"; Test-Path "$Repo\docs\LICENSE_SERVER.md"
```

- Không ở `khainpt_dev`: hỏi trước khi `git checkout khainpt_dev`. Có thay đổi chưa commit thì **không** chuyển nhánh, báo chủ máy.
- Thiếu commit: hỏi rồi `git pull --ff-only`. Nếu lệnh này không chạy được thì dừng, báo chủ máy.

**0.3. IIS hiện có và cổng trống:**

```powershell
Import-Module WebAdministration
Get-Website | Format-Table Name, State, PhysicalPath, @{n='Bindings';e={$_.bindings.Collection.bindingInformation -join ', '}}
Get-NetTCPConnection -State Listen -LocalPort $LicensePort, $WebPort -ErrorAction SilentlyContinue
```

Nếu 8443 hoặc 9443 đã bận: chọn cổng khác, báo chủ máy, và dùng cổng mới ở mọi bước sau.

**0.4. Thư mục dữ liệu:**

```powershell
New-Item -ItemType Directory -Force "$Data\License", "$Data\Web", "$Data\certs" | Out-Null
```

## 1. Chứng chỉ HTTPS dùng chung cho cả hai site

Site chính chỉ cho đăng nhập quản trị qua HTTPS. iPhone chỉ cài web app khi trang có HTTPS mà nó tin. Trong mạng nhà không có tên miền công khai, nên dùng **mkcert** (miễn phí, BSD-3-Clause): nó tạo một "nhà cấp chứng chỉ" (CA) riêng của chủ máy.

```powershell
# Cài mkcert (hỏi trước):  winget install FiloSottile.mkcert   (mở lại PowerShell sau khi cài)
mkcert -install                     # cài CA vào Windows; bấm Yes khi Windows hỏi
cd "$Data\certs"
mkcert -pkcs12 -p12-file toolkit.p12 localhost 127.0.0.1 $HostName "$HostName.local" $Ip
$pfx = Import-PfxCertificate -FilePath "$Data\certs\toolkit.p12" -CertStoreLocation Cert:\LocalMachine\My `
       -Password (ConvertTo-SecureString 'changeit' -AsPlainText -Force)
$pfx.FriendlyName = 'Toolkit LAN'
$Thumb = $pfx.Thumbprint
Copy-Item "$(mkcert -CAROOT)\rootCA.pem" "$Data\certs\toolkit-ca.crt" -Force
"Thumbprint: $Thumb"
```

- Chứng chỉ có các tên `localhost`, tên máy, `tên-máy.local` và IP hiện tại.
- File `toolkit-ca.crt` dùng để cài lên iPhone (mục C3).
- **Không** chép `rootCA-key.pem` (trong thư mục `mkcert -CAROOT`) đi đâu; file này chỉ được sao lưu kín.

## 2. Site chính (Toolkit License)

```powershell
cd $Repo
dotnet publish src\ToolkitLicense.Web -c Release -o $LicenseDir
Import-Module WebAdministration
if (-not (Test-Path IIS:\AppPools\ToolkitLicense)) {
    New-WebAppPool -Name ToolkitLicense | Out-Null
    Set-ItemProperty IIS:\AppPools\ToolkitLicense -Name managedRuntimeVersion -Value ''
}
if (-not (Get-Website | Where-Object Name -eq 'ToolkitLicense')) {
    New-Website -Name ToolkitLicense -PhysicalPath $LicenseDir -ApplicationPool ToolkitLicense -Port $LicensePort -IPAddress '*' -Ssl | Out-Null
    (Get-WebBinding -Name ToolkitLicense -Protocol https).AddSslCertificate($Thumb, 'My')
}
icacls "$Data\License" /grant "IIS AppPool\ToolkitLicense:(OI)(CI)M" | Out-Null
& $AppCmd set config ToolkitLicense -section:system.webServer/aspNetCore /+"environmentVariables.[name='Storage__DataRoot',value='$Data\License']" /commit:apphost
Restart-WebAppPool ToolkitLicense
```

- Binding không gắn tên miền (host trống), nên site mở được bằng `localhost`, tên máy lẫn IP.
- Cấu hình `/commit:apphost` nằm trong IIS chứ không trong `web.config`, nên publish lại không mất.
- Nếu `appcmd` báo biến đã tồn tại: bỏ qua, hoặc xóa bằng `/-"environmentVariables.[name='Storage__DataRoot']"` rồi thêm lại.

**Kiểm tra:**

```powershell
(Invoke-WebRequest "https://localhost:$LicensePort/download" -UseBasicParsing).StatusCode   # 200
Get-ChildItem "$Data\License"   # license.db, signing-key.pem, setup-code.txt, keys, updates
```

Lỗi 500.30 hoặc 502.5:
- tạm đặt `stdoutLogEnabled="true"` và `stdoutLogFile="D:\ToolkitData\License\stdout"` trong `$LicenseDir\web.config`;
- tải lại trang, đọc log, sửa lỗi, rồi tắt lại.

Nguyên nhân thường là thiếu Hosting Bundle hoặc thiếu quyền ghi thư mục dữ liệu.

**➜ Dừng và giao cho chủ máy mục C1** (tạo tài khoản quản trị, xác thực hai lớp, lấy khóa). Chờ chủ máy báo đã lưu khóa vào `D:\ToolkitData\public-key.txt`.

## 3. Site Web (trình duyệt, iPhone)

```powershell
cd $Repo
dotnet publish src\ToolboxWeb.Web -c Release -o $WebDir
if (-not (Test-Path IIS:\AppPools\ToolkitWeb)) {
    New-WebAppPool -Name ToolkitWeb | Out-Null
    Set-ItemProperty IIS:\AppPools\ToolkitWeb -Name managedRuntimeVersion -Value ''
}
if (-not (Get-Website | Where-Object Name -eq 'ToolkitWeb')) {
    New-Website -Name ToolkitWeb -PhysicalPath $WebDir -ApplicationPool ToolkitWeb -Port $WebPort -IPAddress '*' -Ssl | Out-Null
    (Get-WebBinding -Name ToolkitWeb -Protocol https).AddSslCertificate($Thumb, 'My')
}
icacls "$Data\Web" /grant "IIS AppPool\ToolkitWeb:(OI)(CI)M" | Out-Null
& $AppCmd set config ToolkitWeb -section:system.webServer/aspNetCore /+"environmentVariables.[name='App__Mode',value='Web']" /commit:apphost
& $AppCmd set config ToolkitWeb -section:system.webServer/aspNetCore /+"environmentVariables.[name='Storage__DataProtectionKeysPath',value='$Data\Web\keys']" /commit:apphost
& $AppCmd set config ToolkitWeb -section:system.webServer/aspNetCore /+"environmentVariables.[name='Logging__File__Directory',value='$Data\Web\logs']" /commit:apphost
# Nút "Tải Toolkit cho máy tính": {host} được thay bằng địa chỉ người xem đang dùng (localhost, tên máy, IP)
& $AppCmd set config ToolkitWeb -section:system.webServer/aspNetCore /+"environmentVariables.[name='App__DesktopDownloadUrl',value='https://{host}:$LicensePort/download']" /commit:apphost
Restart-WebAppPool ToolkitWeb
(Invoke-WebRequest "https://localhost:$WebPort/" -UseBasicParsing).StatusCode   # 200 (trang đăng nhập Jira)
```

- Bản Web không có database; nó chỉ lưu khóa của cookie đăng nhập và log.
- Trang đăng nhập và menu tài khoản có nút **"Tải Toolkit cho máy tính"**, mở trang tải về của site chính. Nút ẩn trên điện thoại, vì bộ cài chỉ chạy trên Windows.
- Máy này phải vào được Jira (`https://task.ascvn.com.vn`, khóa `Jira:DefaultBaseUrl`). Jira ở địa chỉ khác thì thêm biến `Jira__DefaultBaseUrl` theo cùng cách.

## 4. Cho iPhone và máy khác cùng Wi-Fi vào được (không đổi IP)

Hỏi chủ máy trước, rồi:

```powershell
# Mạng Wi-Fi đang dùng phải là Private (Public thì tường lửa chặn):
Get-NetConnectionProfile | Format-Table Name, InterfaceAlias, NetworkCategory
# Nếu Public, sau khi chủ máy đồng ý (đây là mạng nhà / mạng tin cậy):
#   Set-NetConnectionProfile -InterfaceAlias '<tên card>' -NetworkCategory Private
New-NetFirewallRule -DisplayName 'Toolkit (8443, 9443)' -Direction Inbound -Protocol TCP -LocalPort $LicensePort, $WebPort -Action Allow -Profile Private
```

Báo cho chủ máy hai địa chỉ để mở trên iPhone:
- `https://$HostName.local:9443`: theo tên máy, không đổi khi IP đổi; một số mạng không hỗ trợ;
- `https://${Ip}:9443`: theo IP hiện tại, luôn chạy được.

## 5. Bộ cài app máy tính và trang tải về

```powershell
cd $Repo
if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    dotnet tool install -g vpk --version 1.2.161  # chỉ lần đầu (hỏi trước)
    $env:PATH += ";$env:USERPROFILE\.dotnet\tools"  # để phiên này thấy vpk ngay
}
Set-ExecutionPolicy -Scope Process Bypass -Force
.\tools\release\publish-desktop.ps1 -Version 1.0.0 `
    -ServerUrl "https://localhost:$LicensePort" `
    -PublicKeyFile "$Data\public-key.txt" `
    -CopyTo "$Data\License\updates"
Get-ChildItem "$Data\License\updates"           # ToolkitApp-win-Setup.exe, *.nupkg, releases.win.json
```

- `-Version` phải bằng `<Version>` trong `src\ToolboxWeb.Web\ToolboxWeb.Web.csproj` (hiện `1.0.0`).
- `-ServerUrl https://localhost:8443`: app cài trên **máy này** nói chuyện với site chính qua `localhost`, nên IP đổi cũng không ảnh hưởng.
- Kết quả trung gian nằm ở `$Repo\artifacts\` (đã có trong `.gitignore`). Giữ thư mục `artifacts\releases` cho các lần sau.

**➜ Giao cho chủ máy mục C2:** phát hành bản 1.0.0 trên trang Phiên bản, rồi tải và cài app.

## 6. Kiểm tra cuối và bàn giao

```powershell
Get-Website | Where-Object Name -in 'ToolkitLicense', 'ToolkitWeb' | Format-Table Name, State
(Invoke-WebRequest "https://localhost:$LicensePort/download" -UseBasicParsing).StatusCode
(Invoke-WebRequest "https://localhost:$WebPort/" -UseBasicParsing).StatusCode
(Invoke-WebRequest "https://localhost:$LicensePort/updates/releases.win.json" -UseBasicParsing).StatusCode
```

Báo cho chủ máy:
- địa chỉ site chính, site Web, trang tải về;
- địa chỉ dùng trên iPhone (mục 4);
- vị trí dữ liệu, file `toolkit-ca.crt`;
- việc còn lại cho chủ máy (C1–C3);
- cách cập nhật về sau (mục 8).

---

# Việc của chủ máy

## C1. Lần đầu mở site chính

1. Mở Edge: `https://localhost:8443`. Phải có ổ khóa, không cảnh báo. Trang chuyển tới **Tạo tài khoản quản trị**.
2. Mở file `D:\ToolkitData\License\setup-code.txt`, nhập mã trong đó, đặt tên đăng nhập và mật khẩu (ít nhất 10 ký tự, có chữ hoa, chữ thường, chữ số).
3. **Bật xác thực hai lớp:**
   - cài Microsoft Authenticator hoặc Google Authenticator trên iPhone;
   - chọn thêm tài khoản → nhập khóa thủ công → gõ khóa hiện trên trang;
   - nhập mã 6 số mà ứng dụng hiện ra.
4. **Lưu 10 mã khôi phục** vào trình quản lý mật khẩu hoặc giấy.
5. Vào **Khóa ký**:
   - bấm **Sao chép khóa một dòng**;
   - mở Notepad, dán, lưu thành `D:\ToolkitData\public-key.txt`.
6. Sao lưu `D:\ToolkitData\License\signing-key.pem` ra nơi an toàn (USB, ổ cá nhân).
7. Báo Claude làm tiếp.

## C2. Phát hành và cài app máy tính

1. `https://localhost:8443/releases` → mục **Phát hành phiên bản**:
   - ô phiên bản đã điền sẵn `1.0.0`, đường tải về là trang tải về;
   - bấm **Phát hành**.
2. Mở `https://localhost:8443/download` (hoặc bấm **"Tải Toolkit cho máy tính"** trên site Web `https://localhost:9443`) → **Tải bộ cài (Setup.exe)** → mở `ToolkitApp-win-Setup.exe`.
3. Windows hiện *"Windows protected your PC"* (bộ cài chưa ký số) → **More info** → **Run anyway**. Mỗi máy chỉ hỏi một lần.
4. Toolkit tự mở trong cửa sổ riêng, ở màn hình **Kích hoạt** → bấm **Gửi yêu cầu kích hoạt**.
5. Quay lại site chính → **Yêu cầu kích hoạt** → bấm vào máy vừa gửi → **Duyệt và cấp bản quyền** → xác nhận. Khoảng 10 giây sau, Toolkit tự mở.
6. Trong Toolkit: **Cài đặt** → kết nối Jira → **Lưu**.
7. (Nếu có dữ liệu ở site Toolkit cũ)
   - ở site cũ: **Tài khoản → Dữ liệu cá nhân → Xuất dữ liệu Toolkit**;
   - ở app: **Tài khoản → Dữ liệu cá nhân → Nhập dữ liệu**.

Vị trí cài:
- app nằm ở `%LOCALAPPDATA%\ToolkitApp`, dữ liệu ở `%LOCALAPPDATA%\Toolkit`;
- gỡ app không xóa dữ liệu;
- đóng cửa sổ thì app vẫn chạy nền; thoát hẳn bằng menu tài khoản → **Thoát Toolkit**.

**Máy Windows khác cùng Wi-Fi** muốn tải bộ cài thì mở `https://<TÊN-MÁY>.local:8443/download` hoặc `https://<IP>:8443/download`; trước đó cài `toolkit-ca.crt` trên máy đó (`certutil -addstore -f Root toolkit-ca.crt`). Bộ cài hiện tại trỏ về `localhost`, nên **chỉ kích hoạt được trên máy chủ này**. Muốn dùng app ở máy khác, nhờ Claude đóng gói lại với `-ServerUrl https://<TÊN-MÁY>.local:8443`.

## C3. Cài Toolkit lên iPhone (cùng Wi-Fi)

**Một lần: cho iPhone tin chứng chỉ của máy bạn**
1. Gửi file `D:\ToolkitData\certs\toolkit-ca.crt` sang iPhone **bằng email** (ứng dụng Mail) hoặc iCloud Drive. Zalo hay Messenger thường không mở được loại file này.
2. Trên iPhone, chạm vào file đính kèm (Mail) hoặc mở bằng app **Tệp** → báo "Đã tải về hồ sơ".
3. **Cài đặt** → dòng **Đã tải về hồ sơ** ở trên cùng → **Cài đặt** → nhập mật mã → **Cài đặt**.
4. **Cài đặt → Cài đặt chung → Giới thiệu → Cài đặt tin cậy chứng nhận** → bật công tắc cho chứng chỉ **mkcert …** → Tiếp tục.

**Mở và cài app**
1. iPhone vào **cùng Wi-Fi** với máy tính.
2. Mở **Safari** (bắt buộc Safari) và vào địa chỉ Claude báo ở mục 4:
   - `https://<TÊN-MÁY>.local:9443`;
   - không vào được thì `https://<IP>:9443`.

   Phải thấy ổ khóa, không có cảnh báo.
3. Đăng nhập bằng tài khoản Jira.
4. Nút **Chia sẻ** (ô vuông có mũi tên) → **Thêm vào MH chính** → **Thêm**. Biểu tượng Toolkit xuất hiện như một app.

Bản trên iPhone có các màn hình Jira và hẹn giờ. Ghi chú, việc cần làm và công cụ DB chỉ có trên app máy tính.

---

## 7. Khi IP máy đổi (router cấp IP khác)

**Không cần đổi gì trên máy hay router.**
- App máy tính dùng `localhost` nên không ảnh hưởng.
- iPhone mở bằng `<TÊN-MÁY>.local` cũng không ảnh hưởng.
- Chỉ khi iPhone mở bằng IP mới cần làm mới chứng chỉ cho IP mới. Nhờ Claude chạy (PowerShell Administrator, sau khối "Biến dùng chung"):

```powershell
Import-Module WebAdministration
cd "$Data\certs"
mkcert -pkcs12 -p12-file toolkit.p12 localhost 127.0.0.1 $HostName "$HostName.local" $Ip
$pfx = Import-PfxCertificate -FilePath "$Data\certs\toolkit.p12" -CertStoreLocation Cert:\LocalMachine\My -Password (ConvertTo-SecureString 'changeit' -AsPlainText -Force)
$pfx.FriendlyName = 'Toolkit LAN'
foreach ($site in 'ToolkitLicense', 'ToolkitWeb') {
    $b = Get-WebBinding -Name $site -Protocol https
    $b.RemoveSslCertificate(); $b.AddSslCertificate($pfx.Thumbprint, 'My')
}
"Địa chỉ mới cho iPhone: https://${Ip}:$WebPort"
```

CA không đổi, nên iPhone **không** phải cài lại `toolkit-ca.crt`.

## 8. Cập nhật về sau

- **Site chính / site Web:**
  - `Stop-WebAppPool ToolkitLicense` (hoặc `ToolkitWeb`);
  - `dotnet publish` lại vào cùng thư mục;
  - `Start-WebAppPool ToolkitLicense`.

  Cấu hình `/commit:apphost` vẫn giữ nguyên.
- **App máy tính:**
  1. tăng `<Version>` trong csproj, ví dụ `1.1.0`;
  2. chạy lại `publish-desktop.ps1 -Version 1.1.0 …` (mục 5);
  3. trang **Phiên bản → Phát hành 1.1.0**.

  App tự hiện **"Đã có Toolkit 1.1.0 → Cập nhật ngay"**, trong vòng 6 giờ hoặc ngay khi bấm *Cài đặt → Kiểm tra ngay*.

## 9. Xử lý sự cố

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
|---|---|---|
| Trình duyệt hoặc iPhone báo "Kết nối không riêng tư" | Thiết bị chưa tin CA; trên iPhone chưa bật "tin cậy chứng nhận"; mở bằng IP mới chưa có trong chứng chỉ | Mục C3 bước 4; mục 7 |
| iPhone không mở được trang | Khác Wi-Fi; mạng của máy đang Public; chưa có luật tường lửa; router chặn các thiết bị nói chuyện với nhau (chế độ "khách" / AP isolation) | Mục 4; dùng Wi-Fi chính, không dùng Wi-Fi khách |
| `<TÊN-MÁY>.local` không vào được | Mạng không hỗ trợ tên `.local` | Dùng `https://<IP>:9443` |
| Không thấy "Thêm vào MH chính" dạng app | Không dùng Safari, hoặc trang chưa có ổ khóa | Mở bằng Safari; mục C3 |
| Không đăng nhập được trang quản trị | Mở bằng `http://` | Luôn dùng `https://…:8443` |
| Site báo 500.30 / 502.5 | Thiếu Hosting Bundle (hoặc cài trước IIS), thiếu quyền thư mục dữ liệu | Mục 0.1 và lệnh `icacls`; bật `stdoutLogEnabled` để xem lỗi |
| App máy tính báo "Không kết nối được máy chủ bản quyền" | Site chính đang dừng, hoặc bộ cài đóng gói với địa chỉ khác | `Get-Website \| Where-Object Name -eq 'ToolkitLicense'`; mục 5 |
| "Cập nhật ngay" báo chưa có gói | Chưa chạy script với `-CopyTo` hoặc chưa bấm Phát hành | Mục 8 |

## 10. Cần sao lưu

- `D:\ToolkitData\License`: database bản quyền, **`signing-key.pem`** (mất thì không cấp được bản quyền mà app tin), `keys\`, `updates\`.
- `%LOCALAPPDATA%\Toolkit`: dữ liệu app máy tính. Có thể dùng thêm "Xuất dữ liệu Toolkit".
- Thư mục `mkcert -CAROOT`, gồm **`rootCA-key.pem`**: giữ kín. Mất thì phải tạo CA mới và cài lại lên iPhone.
- Mã khôi phục xác thực hai lớp của tài khoản quản trị.
