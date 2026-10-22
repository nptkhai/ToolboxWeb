# Đánh giá UI/UX ToolboxWeb

> Ngày đánh giá: 09/10/2026
> Phạm vi: toàn bộ màn hình của ToolboxWeb, gồm nhóm Jira, cùng các prompt mẫu, `docs/*_PROMPTS.md` và `AGENTS.md`.
> Phương pháp:
> - Build và chạy app thật, đăng ký tài khoản thử, nhập dữ liệu mẫu.
> - Chụp 26 màn hình ở desktop 1440px, mobile 390px, chế độ tối và sidebar thu gọn.
> - Đọc `site.css`, các view Razor và JavaScript.
> - Nhóm Jira được đánh giá từ ảnh chụp do người dùng cung cấp, kết hợp đọc mã nguồn, vì môi trường đánh giá không đăng nhập được Jira.
>
> **Cập nhật 20/10/2026:** thêm [mục 10 – Lộ trình triển khai chốt](#10-lộ-trình-triển-khai-chốt), thay thế thứ tự ưu tiên ở mục 6 và 8.
>
> **Cập nhật 13/10/2026:** bổ sung [mục 8 – Kế hoạch xử lý chi tiết](#8-kế-hoạch-xử-lý-chi-tiết-theo-thứ-tự-ưu-tiên), và đính chính một số nhận định của bản đầu sau khi đối chiếu lại mã nguồn (xem [mục 9](#9-đính-chính-so-với-bản-đầu)).

---

## Mục lục

1. [Kết luận tổng quan](#1-kết-luận-tổng-quan)
2. [Khung chung (áp dụng cho mọi trang)](#2-khung-chung-áp-dụng-cho-mọi-trang)
3. [Đánh giá từng màn hình](#3-đánh-giá-từng-màn-hình)
4. [Nhóm màn hình Jira](#4-nhóm-màn-hình-jira)
5. [Đánh giá các prompt đang dùng](#5-đánh-giá-các-prompt-đang-dùng)
6. [Thứ tự ưu tiên cải tiến](#6-thứ-tự-ưu-tiên-cải-tiến)
7. [Phụ lục: bảng điểm](#7-phụ-lục-bảng-điểm)
8. [Kế hoạch xử lý chi tiết theo thứ tự ưu tiên](#8-kế-hoạch-xử-lý-chi-tiết-theo-thứ-tự-ưu-tiên)
9. [Đính chính so với bản đầu](#9-đính-chính-so-với-bản-đầu)
10. [Lộ trình triển khai chốt](#10-lộ-trình-triển-khai-chốt) ← **thứ tự làm việc chính thức**

---

## 1. Kết luận tổng quan

**Điểm tổng: 5/10.** App có nền tảng kỹ thuật tốt, nhưng **chưa có một hệ thống thiết kế thống nhất**. Mỗi module như được làm ở một thời điểm khác nhau với một phong cách khác nhau:

| Thế hệ | Màn hình | Đặc điểm |
|---|---|---|
| Kiểu cũ (scaffold) | Công việc, Liên kết nhanh, Hẹn giờ, Prompt, Đăng ký | Form 4 cột bên trái, danh sách 8 cột bên phải, ô nhập chỉ có placeholder, không có trạng thái trống, xóa không hỏi lại |
| Kiểu mới | Ghi chú, So sánh DB, Tabulator, Jira Dashboard | Có toolbar, ô tìm kiếm, icon, trạng thái trống, chia bước 1-2-3, thẻ số liệu |
| Kiểu "demo/dev" | Component Hub, Kendo Demo | Nội dung dành cho lập trình viên nhưng lại nằm ngay trong menu của người dùng |

Người dùng chuyển qua lại giữa các trang sẽ thấy như đang dùng 3 sản phẩm khác nhau. Đây là vấn đề lớn nhất, lớn hơn mọi lỗi riêng lẻ bên dưới. **Nguyên nhân gốc** nằm ở prompt và hướng dẫn: không có tiêu chuẩn thiết kế nào được ghi lại (xem [mục 5](#5-đánh-giá-các-prompt-đang-dùng)).

---

## 2. Khung chung (áp dụng cho mọi trang)

### 2.1 Sidebar

| # | Vấn đề | Mức độ |
|---|---|---|
| 1 | **Không biết mình đang ở trang nào.** Mục của trang hiện tại không được tô sáng. Trong CSS không có `.side-nav a.active`, cũng không có `aria-current`. | Cao |
| 2 | **Thu gọn sidebar thì menu biến mất hoàn toàn**, chỉ còn chữ "TW". Nguyên nhân: CSS đặt `font-size: 0` rồi dùng `::first-letter` để hiện lại chữ cái đầu, nhưng `::first-letter` không áp dụng cho phần tử `display: flex`. | Cao |
| 3 | **Mobile không có menu trượt.** Dưới 900px, sidebar mặc định mở và xếp chồng lên trên cùng, chiếm khoảng 500px; người dùng phải cuộn qua cả menu mới tới nội dung. Nút ☰ ẩn được sidebar, nhưng trạng thái này lưu chung với desktop. Header bị vỡ thành 4 dòng, cao khoảng 240px; ở 1280px, dòng chữ nhỏ trên topbar cũng đã bị xuống 2 dòng. | Cao |
| 4 | Không có icon, không phân nhóm. Công cụ cá nhân (Ghi chú, Công việc), công cụ kỹ thuật (So sánh DB, Bắt SQL, Component) và Jira nằm lẫn với nhau. | Trung bình |
| 5 | Thiếu "Không gian prompt" trong menu; trang này chỉ vào được qua widget ở Tổng quan. | Trung bình |
| 6 | Tên dễ nhầm: "Danh sách công việc" (Jira) đứng cạnh "Công việc" (checklist cá nhân). | Trung bình |
| 7 | Khi chưa đăng nhập, sidebar vẫn chiếm 248px nhưng hoàn toàn trống, chỉ có chữ "ToolboxWeb". | Thấp |

**Đề xuất cấu trúc menu:**

```text
JIRA (chỉ hiện cho tài khoản Jira)
  Tổng quan Jira
  Công việc Jira
CÁ NHÂN
  Tổng quan
  Ghi chú
  Việc cần làm
  Hẹn giờ tập trung
  Liên kết nhanh
  Không gian prompt
CÔNG CỤ DB
  So sánh cấu trúc DB
  Bắt câu lệnh SQL
DEV (chỉ hiện ở môi trường Development hoặc theo role)
  Component Hub
```

### 2.2 Topbar

- Dòng chữ nhỏ "BỘ CÔNG CỤ NĂNG SUẤT CÁ NHÂN" lặp lại trên mọi trang mà không mang thông tin gì. Nên bỏ hoặc thay bằng breadcrumb.
- Đồng hồ và thời tiết chiếm vị trí trung tâm. Khi không lấy được vị trí, nó hiện vĩnh viễn **"Chưa có dữ liệu thời tiết"**, tức là một trạng thái lỗi được trưng ra ở chỗ dễ thấy nhất. Với một công cụ làm việc, widget này không cần thiết: nên bỏ, hoặc thu nhỏ và ẩn khi không có dữ liệu.
- Nút đổi giao diện dùng ký tự `◑`, hiển thị thành một chấm nhỏ khó hiểu. Nên dùng icon mặt trời/mặt trăng có tooltip.
- **Tiêu đề bị trùng 2 lần** ở nhiều trang: topbar ghi "Component Hub" rồi nội dung lại có h1 "Component Hub". Tương tự với "Kendo UI Demo" và "Quản lý tài khoản". Trang đăng nhập còn lặp chữ "Đăng nhập" tới 4 lần.
- Trang đăng nhập vẫn hiện đồng hồ, thời tiết, cùng lúc cả link "Đăng ký" và "Đăng nhập" trên header.
- Với tài khoản Jira, **ảnh đại diện bị vỡ**: cạnh tên người dùng là icon ảnh lỗi. Ảnh lấy từ `AvatarUrl` trong hồ sơ (file đã upload, phục vụ qua `/uploads`), nên nhiều khả năng file không còn ở thư mục `Storage:UploadsPath` hiện tại; cần kiểm tra trên máy chạy thật. Dù nguyên nhân là gì, giao diện cũng phải tự quay về hiện chữ viết tắt khi ảnh lỗi.
- Đồng hồ thời tiết gọi `navigator.geolocation` ngay khi tải trang, nên trình duyệt hỏi quyền vị trí ở lần vào đầu tiên mà không giải thích lý do.

### 2.3 Hình nền hoàng hôn (adaptive background)

- Hình đẹp nhưng **không hợp** với một công cụ làm việc, nhất là công cụ DB/SQL. Mặt trời và núi nằm sau nội dung, cạnh tranh sự chú ý.
- **Chữ đặt thẳng lên nền gần như không đọc được**: phụ đề trang Kendo Demo, phụ đề trang Quản lý tài khoản, chữ đỏ của trang Access denied.
- Hình nền đổi theo giờ (hoàng hôn, đêm trăng…), nên cùng một trang có độ tương phản khác nhau tùy thời điểm mở. Kiểm tra giao diện vì thế khó hơn.
- **Đề xuất:** mặc định dùng nền trung tính, cho người dùng tự bật hình nền trong Cài đặt. Mọi chữ phải nằm trên một bề mặt (`--surface`).

### 2.4 Màu sắc và chữ

- CSS dùng tới **129 mã màu hex khác nhau**.
- Nút chính có ít nhất 3 màu xanh khác nhau: xanh gradient đậm (Tạo, Lưu hồ sơ), `#2563eb` (Ghi chú mới, So sánh), và xanh nhạt trông như đang bị vô hiệu (Bắt đầu ghi, Lưu đề xuất).
- Độ đậm chữ dùng lung tung: 500, 600, 650, 700, 750, 800.
- Cỡ chữ trộn lẫn px và rem: `10.5px`, `11px`, `11.5px`, `12px`, `12.5px`, `13px`, `.78rem`, `.95rem`… Nhiều chữ phụ chỉ khoảng 9–11px, khó đọc.
- **Thứ bậc bị đảo ngược**: tiêu đề panel ("Ghi chú gần đây", "Tạo việc") có độ đậm thường, *nhẹ hơn* tiêu đề từng dòng bên trong (đậm 700).
- Khai báo font `"Roboto"` nhưng không có chỗ nào tải font này, nên thực tế đang hiện font hệ thống. Mỗi máy sẽ thấy khác nhau.
- Dùng quá nhiều "thẻ lồng thẻ": mỗi dòng là một khung có viền, nằm trong panel cũng có viền và đổ bóng. Giao diện nặng và nhiều đường kẻ.

**Đề xuất thang thiết kế tối thiểu:**

| Nhóm | Giá trị |
|---|---|
| Khoảng cách | 4 / 8 / 12 / 16 / 24 / 32 px |
| Cỡ chữ | 12 (phụ) / 14 (nội dung) / 16 (tiêu đề panel) / 20 (tiêu đề trang), dùng `rem` |
| Độ đậm | 400 / 600 / 700 |
| Bo góc | 6 (ô nhập, nút) / 10 (panel) / 999 (nhãn) |
| Màu | 1 màu chính, 1 màu phụ, nhóm màu trạng thái (thành công, cảnh báo, nguy hiểm, thông tin), 3 mức xám cho chữ |

### 2.5 Ngôn ngữ và nội dung

- Giao diện tiếng Việt nhưng còn nhiều tiếng Anh: trang Đăng ký (toàn bộ), "Manage Email", "Change email", "Send verification email", menu tài khoản (Profile / Password / Two-factor authentication / Personal data; các key `Profile.Nav.*` trong `vi-VN.json` đang để nguyên tiếng Anh), "Access denied", "Usable now", "Legacy pages".
- Trộn Anh–Việt trong nhãn: "Từ ngày due", "Due date từ", "Tên dự án (Project)", "Mã công việc (Issue Key)".
- **Thuật ngữ lập trình lọt ra cho người dùng**: "Ảnh đại diện chỉ được lưu vào **source** khi bạn bấm lưu… **input file** để tránh tạo **file rác**", "bundle", "kendoTextBox chưa có trong bundle", "Lưới tìm kiếm".
- Định dạng ngày không đồng nhất: topbar ghi `Th 5, 08/10/2026`, danh sách ghi `9/10/2026 00:07` (không có số 0 đầu).
- `FormatVietnamDateTime` ép múi giờ +7, còn đồng hồ dùng múi giờ trình duyệt, nên người ở múi giờ khác sẽ thấy lệch ngày.
- Số chưa theo định dạng tiếng Việt: `17.50`, `25.00`. Nên hiện `17,5 giờ`, có đơn vị.

### 2.6 Khả năng tiếp cận (accessibility) và hành vi

- Form ở Công việc, Liên kết nhanh và Prompt **chỉ có placeholder, không có label**. Gõ vào là mất gợi ý, và không đạt chuẩn WCAG.
- **Xóa không có bước xác nhận** ở Công việc, Liên kết nhanh và Prompt. Riêng trang Ghi chú có modal xác nhận, nên đây cũng là một điểm thiếu nhất quán.
- Console báo lỗi JavaScript `Cannot read properties of undefined (reading 'parseColor')` trên mọi trang nạp Kendo: Công việc (khi có thông báo), Component, Kendo Demo, So sánh DB, Bắt SQL, và hai trang Jira. **Đây là lỗi gốc nghiêm trọng**, xem mục 8, việc 1.1: bundle Kendo dừng giữa chừng khi tải, nên TextBox, ListView, Window không được đăng ký.
- Nhiều chữ có độ tương phản dưới 4.5:1 so với nền, nhất là chữ đặt thẳng lên hình nền.
- Ô chọn ngày native hiện `mm/dd/yyyy` trong giao diện tiếng Việt (tùy theo ngôn ngữ trình duyệt), không theo quy định dùng Kendo DatePicker trong `AGENTS.md`.

---

## 3. Đánh giá từng màn hình

### 3.1 Đăng nhập: 6/10

**Tốt:** chuyển tab Toolbox/Jira rõ ràng, có hiệu ứng kính mờ.

**Cần sửa:**
- Chữ "Đăng nhập" lặp 4 lần (topbar, h2, h3, nút bấm). Chỉ nên giữ một tiêu đề, kèm một dòng mô tả.
- Ô nhập kiểu gạch chân không khớp với phần còn lại của app (ô bo góc).
- Nút chính màu trắng trên nền hồng nên nhìn yếu.
- Mặt trời trang trí đè lên mép thẻ đăng nhập.
- Thiếu "Quên mật khẩu", thiếu link "Chưa có tài khoản? Đăng ký" ngay trong thẻ, thiếu nút ẩn/hiện mật khẩu.
- Nên ẩn sidebar và topbar trên trang này, dùng layout riêng toàn màn hình.

### 3.2 Đăng ký: 2/10 (tệ nhất)

- Vẫn là trang mặc định của ASP.NET, 100% tiếng Anh, không theo phong cách của trang Đăng nhập.
- **Label đè lên placeholder** ("Email" chồng lên "name@example.com", "Password" chồng lên "password").
- Đoạn "Use another service to register… see this article about setting up this ASP.NET application" là hướng dẫn cho lập trình viên, không bao giờ nên xuất hiện trước người dùng.
- **Đề xuất:** làm lại theo đúng khung của trang Đăng nhập, có thể thêm một tab thứ ba.

### 3.3 Tổng quan: 4/10

- Thiếu hẳn phần tóm tắt: không có số việc cần làm, việc quá hạn, số phút tập trung hôm nay, hay thao tác nhanh.
- "Công việc hôm nay" hiện cả những việc **không có hạn**. Tiêu đề nói một đằng, dữ liệu một nẻo.
- Widget Prompt lặp lại tên và loại ("Báo lỗi / Báo lỗi", "Review code / Review code").
- Các panel cùng hàng bị kéo cao bằng nhau, nên "Ghi chú gần đây" khi trống là một khoảng trắng khoảng 250px; "Phiên tập trung" cũng vậy.
- "Lịch sử hoạt động" kéo dài hết chiều rộng và chiếm 1/3 trang nhưng giá trị sử dụng thấp. Nên thu gọn thành dòng thời gian 5 mục, có link "Xem tất cả".
- Liên kết nhanh nên hiện dạng ô có favicon, không nên là danh sách khung.

### 3.4 Ghi chú: 7.5/10 (tốt nhất, nên lấy làm chuẩn)

- Có toolbar gồm số liệu, ô tìm kiếm và nút chính; có trạng thái trống với icon, mô tả và nút hành động. **Đây là mẫu nên áp dụng cho Công việc, Liên kết nhanh và Prompt.**
- Điểm trừ: "0 định dạng" khó hiểu nghĩa; dòng số liệu chữ nhỏ và nhạt.

### 3.5 Công việc (checklist): 3.5/10

- Ô chọn ngày native hiện `mm/dd/yyyy`.
- Nút "Chuyển trạng thái" mơ hồ. Nên thay bằng **checkbox** ở đầu dòng, gạch ngang khi đã xong.
- Không có bộ lọc Cần làm / Đã xong / Quá hạn, không sắp xếp theo hạn, không có mức ưu tiên, không có trạng thái trống.
- Khoảng cách giữa ô Tiêu đề và Mô tả không đều, vì thẻ báo lỗi validation vẫn chiếm chỗ dù đang trống.
- Mô tả việc hiện thành chữ thô ngay dưới dòng tiêu đề.
- Xóa không có xác nhận.

### 3.6 Hẹn giờ tập trung: 4/10

- Nút "Lưu phiên đã hoàn thành" bấm được ngay cả khi chưa chạy hẹn giờ, nên có thể lưu phiên giả.
- Panel "Các phiên gần đây" khi trống chỉ là một khối trắng lớn, không có thông báo gì.
- Bộ đếm giờ chưa có vòng tiến độ và các mức nhanh (25/5/15 phút); ô "Số phút" nằm tách rời khỏi bộ đếm.
- Nên thêm âm báo hoặc thông báo trình duyệt khi hết giờ, và đổi tiêu đề tab thành `12:34 – Tập trung`.

### 3.7 Liên kết nhanh: 3.5/10

- Ô thứ tư chỉ hiện **"0"**, không có label, nên không ai biết đó là "Thứ tự sắp xếp".
- Lỗi khoảng trắng: `AI· https://…` thiếu dấu cách trước dấu chấm giữa.
- Không sửa được liên kết (chỉ có Xóa), không gom nhóm theo Nhóm, không có favicon, không mở nhanh.
- Xóa không có xác nhận.

### 3.8 Không gian prompt: 4/10

- Trang dài khoảng 3.500px, không có tìm kiếm, lọc theo loại, hay thu gọn từng prompt.
- Cột nút bên phải có độ rộng khác nhau ở mỗi thẻ; nút "Đánh dấu đã dùng" bị xuống 3 dòng, nên mép phải các thẻ lởm chởm.
- Form tạo prompt ở cột trái không cố định khi cuộn và chỉ có placeholder.
- Hai nút "Sao chép" và "Đánh dấu đã dùng" lẽ ra nên là một: sao chép thì tự tăng số lần dùng.
- Không có chức năng điền biến (xem [mục 5](#5-đánh-giá-các-prompt-đang-dùng)).
- Không có trong menu sidebar.

### 3.9 Component Hub: 5/10

- Dàn trang khá tốt: tab theo nhóm, có nhãn trạng thái.
- Nhưng đây là **công cụ nội bộ cho lập trình viên**, không nên nằm trong menu chính của người dùng. Nên ẩn theo role hoặc chỉ hiện ở môi trường Development.
- Bố cục thẻ không đều: một thẻ chỉ rộng nửa trang, nửa còn lại bỏ trống.
- Nhãn đỏ "Not in current Kendo bundle" và thông báo lỗi kiểu dev hiện ngay trong demo. Nhãn này còn **sai sự thật**: TextBox có trong bundle, chỉ không được đăng ký do lỗi tải (mục 8, việc 1.1).

### 3.10 Kendo UI Demo: 2/10

- Phụ đề **không đọc được** trên nền.
- **Hộp lỗi đỏ khắp nơi** ("kendoTextBox / kendoListView chưa có trong bundle"), có 2 hộp lỗi lạc hẳn ra ngoài panel ở cuối trang.
- Bảng chính chỉ có header và một dòng lỗi.
- **Nguyên nhân thật không nằm ở trang này.** Bundle `kendo.ui.core.min.js` cần thêm thư viện `@progress/kendo-drawing` (biến toàn cục `_progress_kendo_drawing`) nhưng thư viện này không được nạp (và cũng không nên nạp, vì đây là phần mềm thương mại). Bundle dừng ở module màu (byte ~1.227.548), nên các widget đứng sau điểm đó (TextBox ở ~1.251.900, ListView ~1.310.070, Window ~1.507.757) không bao giờ được đăng ký. Khi thử bổ sung biến này, trang chạy đầy đủ: ô tìm nhanh, bảng ListView có nhãn trạng thái và thanh tiến độ, phân trang, nút "Chi tiết"; không còn lỗi console.
- **Đề xuất:** sửa lỗi nạp Kendo (mục 8, việc 1.1) thay vì xóa trang; sau đó vẫn chuyển trang này sang nhóm Dev.

### 3.11 Tabulator Demo: 6/10

- Toolbar rõ ràng, có trạng thái trống.
- Nhãn "Sạch 0" khó hiểu; nên ghi "Không có thay đổi chưa lưu".
- Nút "Xóa dòng chọn" màu đỏ nằm sát nút chính và vẫn bấm được khi chưa chọn dòng nào.
- Bảng trống cao khoảng 530px.

### 3.12 So sánh cấu trúc DB: 7/10 (tốt thứ hai)

**Tốt:** chia bước 1-2 rõ ràng, có màu phân biệt Nguồn/Đích, các chip lựa chọn dễ thao tác.

**Cần sửa:**
- Ô Database trống, không có gợi ý. Nên ghi "Kiểm tra kết nối để tải danh sách" và vô hiệu ô này cho tới khi kết nối xong.
- Nút "So sánh" bật sẵn khi chưa kết nối.
- Thanh hành động không có số "3" như hai bước trước.
- "Tin cậy certificate" **được tick sẵn**: về bảo mật, mặc định nên tắt, hoặc ít nhất kèm cảnh báo.
- Chip đã chọn và chưa chọn chênh nhau quá ít.
- Trên mobile, thanh "So sánh" xuất hiện trước cả bước 1.

### 3.13 Bắt câu lệnh SQL: 4.5/10

- Cùng là công cụ DB nhưng form kết nối **khác hẳn** trang So sánh DB: kích thước label khác, không có bước, nút "Kiểm tra kết nối" nhỏ hơn. Hai trang này phải dùng chung một component kết nối.
- Nút chính "Bắt đầu ghi" màu nhạt trông như đang bị vô hiệu. Nút "Dừng" đang vô hiệu nhưng lại có viền đỏ trông như đang bấm được.
- Khoảng trống lớn khoảng 250px ở vùng kết quả, không có trạng thái trống.
- Panel chi tiết lặp câu "Bấm vào một dòng để xem câu lệnh và tham số." ở cả tiêu đề lẫn nội dung.
- Nút "Xuất file" bấm được khi chưa có dữ liệu.
- Label các ô lọc rất nhỏ; tiêu đề "Ngưỡng cam / Ngưỡng đỏ" khó hiểu với người không biết ngữ cảnh.

### 3.14 Access denied (tài khoản thường mở trang Jira): 1/10

- Tiếng Anh, chữ đỏ trên nền gradient, không giải thích vì sao bị chặn, không có nút hành động.
- **Đề xuất:** ghi rõ "Trang này dành cho tài khoản đăng nhập bằng Jira", kèm nút **[Đăng nhập Jira]** và **[Về Tổng quan]**.

### 3.15 Quản lý tài khoản: 4/10

- Các mục menu bên trái là thẻ cao khoảng 95px với nhiều khoảng trống, do grid bị kéo giãn.
- Nhìn như có **2 mục cùng được tô sáng** (Profile + Password, hoặc Email + Two-factor). Kiểm tra CSS cho thấy chỉ một mục có class `active`; các mục còn lại có nền trong suốt, nên mục nào nằm trên vùng sáng của hình nền cũng trông như đang được chọn. Người dùng vẫn không biết mình đang ở đâu. Cần cho mục thường một nền đặc (`--surface`) và tăng độ đậm cho mục active.
- Tiêu đề trùng ở topbar và trong nội dung; phụ đề không đọc được.
- Các trang Email và Password vẫn là trang mặc định, tiếng Anh; **label đè lên giá trị** ("Email" đè lên "demo.user@…").
- Tên đăng nhập hiện trong ô trông như sửa được nhưng thực tế không sửa được.

---

## 4. Nhóm màn hình Jira

### 4.1 Nhận xét chung

Đây là nhóm màn hình **có giá trị nghiệp vụ cao nhất** của app, và về ý tưởng cũng tốt nhất: có thẻ số liệu, bảng công việc theo ngày có thanh công suất 8h, và chức năng tự sắp lịch. Nhưng nhóm này gặp đúng căn bệnh chung của cả app: **cùng một khái niệm mà mỗi tab làm một kiểu**.

| Khái niệm | Jira Dashboard | Tìm kiếm | Quản lý | Sắp xếp tự động |
|---|---|---|---|---|
| Chọn kỳ | Nút Ngày/Tuần/Tháng/Năm + ô chọn tuần | Ô ngày native "Từ/Đến ngày due" | Nút Tuần/Tháng + ô chọn tuần | Ô ngày native "Due date từ/đến" |
| Bảng | Tabulator | **Bảng Bootstrap thường** | Thẻ kanban | Tabulator |
| Trạng thái | Nhãn màu | **Chữ thường, xuống dòng** | Nhãn màu | Nhãn màu |
| Mã issue | Có link màu xanh | **Chữ đen, không có link** | Có link màu xanh | Có link màu xanh |

Chỉ cần thống nhất 4 thứ này (một bộ chọn kỳ, một kiểu bảng, một nhãn trạng thái, một link mã issue) là nhóm Jira đã chuyên nghiệp hơn rất nhiều.

**Lỗi chung trên cả 4 màn hình:**
1. Ảnh đại diện Jira bị vỡ (xem 2.2).
2. Tên menu dễ nhầm giữa "Danh sách công việc" và "Công việc" (xem 2.1).
3. Chữ quá nhỏ: phần mô tả dưới thẻ số liệu, chữ trong thẻ kanban, khối JQL đều chỉ khoảng 9–11px. Mức tối thiểu nên là 12px cho chữ phụ và 13–14px cho nội dung.
4. Ngôn ngữ trộn Anh–Việt, và tiêu đề bảng tiếng Anh viết hoa (SUBTASK SUMMARY, ESTIMATE TIME, LOGTIME). Nên chọn một: hoặc dùng thuật ngữ Jira tiếng Anh nhất quán, hoặc Việt hóa hẳn ("Hạn xử lý", "Ước tính", "Đã log").
5. Định dạng số chưa theo tiếng Việt: `17.50`, `25.00`.
6. Khung lồng nhau ba lớp (trang → thẻ tab → panel → bảng, mỗi lớp một viền và bo góc), tốn không gian theo chiều ngang và gây rối mắt.

### 4.2 Jira Dashboard: 6.5/10

**Tốt:** thanh chọn kỳ rõ ràng, có dòng "Dữ liệu từ … đến …", 6 thẻ số liệu, nhãn trạng thái có màu, hạn quá hạn tô đỏ.

**Thanh công cụ**
- Có 7 nút điều khiển kỳ (`« ‹ [Tuần 41] › »` + nút về hôm nay + **Tìm kiếm**). Khi đã bấm chuyển kỳ thì dữ liệu nên **tự tải lại**, không cần nút "Tìm kiếm".
- Hai nút `«` và `»` (lùi/tiến 2 kỳ) chỉ giải thích được bằng tooltip. Nên bỏ.
- Ô "Tuần 41, 2026" có nút **xóa (×)**, nhưng xóa kỳ đi thì không có nghĩa gì. Nên bỏ nút ×.
- Có thể hiện ngày ngay trong ô: "Tuần 41 (05–11/10)", rồi bỏ dòng "Dữ liệu từ … đến …" bên trái cho đỡ lặp.

**Thẻ số liệu: chưa trả lời được câu hỏi quan trọng nhất**
- **Đã log 25 giờ so với ước tính 17,5 giờ, tức vượt 43%**, nhưng giao diện không làm nổi bật điều này; hai con số chỉ nằm cạnh nhau như thông tin ngang hàng. Nên có thẻ "Log / Ước tính = 143%" tô màu cảnh báo, hoặc một thanh so sánh.
- "Subtask đã xử lý 15" và "chưa xử lý 1" nên gộp thành một thẻ có thanh tiến độ **15/16 (94%)**.
- "Số lượng yêu cầu 15" và "Subtask đã xử lý 15" trùng số nên dễ đọc nhầm.
- Không so sánh với kỳ trước (▲/▼ so với tuần 40).
- Thiếu **giờ log theo từng ngày so với mức 8h/ngày**. Đây là thông tin người dùng Jira quan tâm nhất (đủ giờ log chưa), và dữ liệu đã có sẵn bên tab Quản lý. Nên thêm một biểu đồ cột nhỏ 7 ngày.
- Viền màu trên đầu mỗi thẻ (xanh, xanh lá, cam, đỏ, tím) không mang ý nghĩa. Thẻ "Chưa xử lý" chỉ nên chuyển cam hoặc đỏ khi giá trị lớn hơn 0.

**Bảng subtask**
- **Hai cột rộng nhất bị trùng dữ liệu**: ở gần như mọi dòng, "Subtask summary" và "Issue summary" giống hệt nhau, nên khoảng 40% chiều rộng bảng là lặp lại. Nên **nhóm theo issue cha** (dòng nhóm "DNTUM-1006 · tên issue", các subtask bên dưới), hoặc ẩn cột issue summary khi trùng.
- Do hai cột tóm tắt xuống dòng, mỗi dòng cao khoảng 70px, nên màn hình chỉ hiện được 6 dòng. Bảng có chiều cao cố định với thanh cuộn riêng, nên trang có **hai thanh cuộn chồng nhau**.
- Cột **Ước tính và Log là số thập phân nhưng đang canh giữa** (`hozAlign: "center"` trong `Content/js/jira-dashboard.js`). Điều này vi phạm quy ước của repo: số thập phân canh phải.
- Mũi tên sắp xếp ▲ hiện thường trực ở mọi cột, nhìn như cột nào cũng đang được sắp xếp. Chỉ nên hiện ở cột đang sắp xếp, hoặc khi rê chuột.
- Nhãn Project màu xám giống nhau cho mọi dự án, không giúp phân biệt được dự án nào.
- Cột STT chiếm chỗ mà không có giá trị.
- Ước tính để trống thì hiện `-`. Nên hiện "Chưa ước tính" màu cam, vì đây là việc người dùng cần đi bổ sung.
- Hạn đỏ "05/10/2026" nên kèm thêm "Trễ 4 ngày".

**Khối JQL**: thông tin kỹ thuật nhưng lại hiện cố định ở cuối trang. Nên thu gọn thành nút "Xem JQL" kèm "Sao chép" và **"Mở trong Jira"**.

### 4.3 Danh sách công việc, tab Tìm kiếm: 3.5/10 (yếu nhất nhóm Jira)

**Form lọc**
- Có 7 ô lọc trong lưới 7 cột, nên ô "Đến ngày due" **rơi xuống dòng thứ hai một mình**. Bố cục bị lẻ.
- Ô ngày dùng native, khác với ô chọn kỳ bên Dashboard.
- **Bật "JQL nâng cao" xong thì các ô lọc đơn giản vẫn sửa được**, và người dùng không biết bộ lọc nào đang thực sự được áp dụng. Khi bật JQL, nên ẩn hoặc vô hiệu các ô lọc kia, hoặc làm hai tab "Bộ lọc" / "JQL".
- Ô JQL là một chuỗi rất dài, không xuống dòng theo mệnh đề, không tô màu cú pháp.
- Nút "Tìm kiếm" nằm dưới ô JQL, cách xa phần lớn các ô lọc.

**"JQL đã áp dụng"**: lặp lại **đúng chuỗi vừa nhập ở trên**, đặt trong khối nền đen với cỡ chữ khoảng 10px, rất khó đọc. Có thể bỏ hẳn, hoặc thu gọn như đề xuất ở Dashboard.

**Bảng kết quả** (là `<table>` Bootstrap thường trong `Views/Jira/Worklist.cshtml`, không phải Tabulator):
- **Mã issue không có link**, chỉ là chữ đen, và còn bị bẻ dòng thành "NTTM-" / "2462". Cần `white-space: nowrap` và mở sang Jira.
- Trạng thái là chữ thường, bị bẻ dòng ("In / Developing"), không có nhãn màu như các tab khác.
- Cột Parent, Estimate, Logged toàn hiện `-`, tức là ba cột trống chiếm chỗ. Ngoài ra cột "Logged" thực chất hiển thị **danh sách ngày đã log** (`LoggedDates`), không phải số giờ, nên tên cột đang sai nghĩa.
- Tiêu đề cột canh trái, trái với quy ước canh giữa.
- Không có số lượng kết quả, không sắp xếp, không phân trang.
- **Kết quả nằm dưới màn hình đầu tiên**: người dùng phải cuộn qua form và khối JQL mới thấy. Sau khi tìm, nên tự thu gọn form lại thành một dòng tóm tắt các bộ lọc đang áp dụng.

### 4.4 Danh sách công việc, tab Quản lý (kanban theo ngày): 6/10

**Tốt:** ý tưởng rất hay. Mỗi cột là một ngày, có thanh công suất `6.0h/8h`, cột hôm nay được tô nổi.

**Cần sửa**
- 7 cột vượt chiều ngang, cột Chủ nhật bị cắt. Thứ 7 và Chủ nhật lại rộng bằng ngày thường dù hầu như trống. Nên thu hẹp cột cuối tuần, hoặc có tùy chọn ẩn cuối tuần.
- **Chữ trong thẻ quá nhỏ**: tóm tắt khoảng 10px, dòng "Est: 0h · Log: 1h" khoảng 8px.
- Nhãn "Done" là thanh xanh rộng gần hết thẻ, nên lấn át cả tiêu đề. Phần lớn thẻ đã Done mà vẫn hiện nổi bật như việc đang làm. Thẻ Done nên làm mờ đi hoặc thu gọn, có nút lọc "Ẩn việc đã xong".
- Ngày "05-10" trong thẻ lặp lại ngày của cột.
- **Thanh công suất không có chú thích**: thứ Hai có đoạn cam nhưng không rõ nghĩa (vượt? chưa log?), và khi vượt 8h không thấy cảnh báo. Cần ghi chú màu, và tô đỏ khi vượt mức.
- Dấu "★" ở cột thứ Sáu khó hiểu. Nên ghi chữ "Hôm nay".
- **"Kéo thẻ sang cột khác… (chưa lưu vào Jira)"**: kéo thả được nhưng không lưu, nên người dùng không biết trạng thái nào là thật. Cần đánh dấu thẻ đã di chuyển (viền nét đứt kèm "05/10 → 07/10") và một thanh nổi "N thay đổi chưa lưu · [Hủy] [Lưu vào Jira]". Nếu chưa hỗ trợ lưu thì nên tắt kéo thả.
- Thanh chọn kỳ ở đây chỉ có Tuần/Tháng, trong khi Dashboard có thêm Ngày/Năm. Dùng cùng component nhưng khác bộ lựa chọn mà không giải thích lý do.
- Ở chế độ "Tháng", 30 cột sẽ không dùng được. Cần chuyển sang giao diện lịch tháng.

### 4.5 Danh sách công việc, tab Sắp xếp tự động: 5/10

**Luồng thao tác không rõ.** Thực tế đây là quy trình 3 bước: Tìm → Sắp xếp (mở hộp cấu hình) → Lưu đề xuất. Nhưng giao diện lại giống một form tìm kiếm cộng hai bảng. Nên làm theo dạng **các bước 1-2-3 như trang So sánh DB**, mỗi bước chỉ có một nút chính.

- Nút "Sắp xếp" là hành động quan trọng nhất nhưng lại là nút phụ màu trắng. Nút "Lưu đề xuất" khi bị vô hiệu có màu tím nhạt, nhìn như vẫn bấm được.
- **Bảng kết quả không cho thấy thay đổi gì.** Cột cần có nhất là **"Hạn cũ → Hạn mới"** (hoặc ngày được xếp), kèm đánh dấu dòng nào thay đổi. Tốt hơn nữa là xem trước ngay trên bảng kanban theo ngày.
- **Nút "Lưu đề xuất" thực ra không lưu gì.** Hàm `saveProposal()` trong `Content/js/jira-auto-schedule.js` chỉ gán hạn mới vào dữ liệu trên trình duyệt; thông báo ghi "(chưa lưu vào Jira)", tải lại trang là mất. Tên nút "Lưu" khiến người dùng tưởng đã cập nhật Jira. Kéo thả ở tab Quản lý (`jira-task-board.js`) cũng chỉ đổi dữ liệu tại chỗ. Cần chọn một trong hai hướng: ghi thật lên Jira (có hộp thoại xác nhận kèm danh sách), hoặc đổi tên thành "Áp dụng vào lưới" và hiện rõ trạng thái "chưa lưu".
- Bảng bên trái hẹp nên tóm tắt bị cắt cụt ("[Khai - P.CNTT - 110820260015] -Kiểm…") mà không có tooltip. Tiêu đề "ESTIMATE …" cũng bị cắt.
- Bảng bên phải trống, chiếm khoảng 600px.
- Ước tính `0h` ở các việc To Do sẽ làm hỏng thuật toán sắp lịch. Cần cảnh báo "3 subtask chưa có ước tính", kèm link sang Jira để bổ sung.
- Ô chọn trạng thái bị cắt chữ: "Tất cả (chưa xử lý & đang xử lý)" đè lên mũi tên.
- Ô tìm theo mã thì kéo giãn quá rộng, trong khi hai ô ngày lại hẹp.
- Tiêu đề "Lưới tìm kiếm" / "Lưới kết quả sau sắp xếp" dùng từ ngữ của lập trình viên, và cỡ chữ khoảng 24px, lệch với các panel khác (16px). Nên đổi thành "Subtask cần xếp lịch (5)" và "Lịch đề xuất".

---

## 5. Đánh giá các prompt đang dùng

App đang có 3 nguồn prompt:

1. 6 prompt mẫu tạo sẵn cho người dùng: `SeedPrompt.*` trong `src/ToolboxWeb.Web/Localization/vi-VN.json` và `en-US.json`, được nạp bởi `Data/SeedData.cs`.
2. `docs/FEATURE_PROMPTS.md` và `docs/MAINTENANCE_PROMPTS.md`.
3. `AGENTS.md`.

### 5.1 Điểm tốt

- Prompt có cấu trúc: biến đầu vào, định dạng đầu ra, ràng buộc kiến trúc.
- Nhấn mạnh đúng các rủi ro của dự án: controller mỏng, lọc dữ liệu theo `UserId`, rủi ro migration.
- Prompt Review code đúng hướng: phát hiện theo file/dòng trước, rủi ro còn lại sau.

### 5.2 Vấn đề

1. **Prompt UI không có tiêu chuẩn thiết kế. Đây là nguyên nhân gốc khiến giao diện mỗi trang một kiểu.** Prompt "Thay đổi giao diện" chỉ yêu cầu canh lề bảng và "giữ phong cách gọn". Nó không nhắc tới design token, khoảng cách, cỡ chữ, các trạng thái trống/đang tải/lỗi, label cho form, xác nhận khi xóa, độ tương phản, trạng thái active của menu, mobile, chế độ tối, hay việc lấy trang mẫu nào làm chuẩn. AI làm đúng những gì prompt yêu cầu, và vì prompt không yêu cầu nhất quán nên kết quả không nhất quán.
2. **Nút Sao chép chỉ chép phần Nội dung.** Hai trường "Biến đầu vào" và "Định dạng đầu ra" được lưu riêng nên **không đi kèm** khi dán sang AI. Ví dụ prompt Báo lỗi: phần định dạng đầu ra (nguyên nhân gốc, đề xuất sửa, test) bị mất khi copy.
3. **Tên biến không khớp**: phần biến ghi `goal`, `in/out`, `steps`, còn nội dung dùng `[goal]`, `[mô tả]`, `[danh sách]`. Lại trộn Anh và Việt. Không có tính năng điền biến, nên người dùng phải tự sửa từng `[...]`.
4. **Hai nguồn sự thật bị lệch nhau**: bản tiếng Anh trong `docs/` có "localization impact", "responsive", còn bản tiếng Việt tạo sẵn thì không. Sửa một bên, bên kia không được cập nhật theo.
5. **Prompt giả định AI đọc được repo** ("Hãy đọc docs/ARCHITECTURE.md"). Dán vào ChatGPT hay Claude bản web thì câu này vô nghĩa. Nên thêm một đoạn ngữ cảnh ngắn (ASP.NET Core MVC .NET 9, SQLite, Razor, Bootstrap + Kendo Core + Tabulator).
6. **Thiếu các prompt quan trọng**: đánh giá UX/accessibility, kiểm tra bản địa hóa, viết test, đánh giá hiệu năng.
7. **`AGENTS.md`**: rất chặt về dữ liệu, kiến trúc và thư viện, nhưng **không có phần thiết kế giao diện**. Ngoài ra còn chứa đường dẫn riêng của một máy (`D:\Source_Toolbox\.codegraph`). Bản thân nó cũng chưa được tuân thủ: trang Công việc và tab Jira vẫn dùng ô ngày native thay vì Kendo DatePicker; cột thập phân trong Jira Dashboard vẫn canh giữa.

### 5.3 Đề xuất sửa prompt "Thay đổi giao diện"

Áp dụng cho cả prompt tạo sẵn lẫn `docs/FEATURE_PROMPTS.md`, và thêm một mục tương tự vào `AGENTS.md`:

```text
Ngữ cảnh: ASP.NET Core MVC (.NET 9), Razor, Bootstrap 5 + Kendo UI Core + Tabulator, i18n vi-VN/en-US.
Thay đổi UI màn hình [screen] để đạt mục tiêu [goal]. Lấy trang Notes làm mẫu chuẩn.
Bắt buộc:
- Chỉ dùng biến CSS trong :root (màu, khoảng cách 4/8/12/16/24, bo góc, độ đậm 400/600/700); không thêm mã hex mới.
- Cỡ chữ tối thiểu 12px, dùng rem.
- Mỗi trang có 1 tiêu đề (topbar), không lặp h1 trong nội dung.
- Form: luôn có label; placeholder chỉ là ví dụ; validation không làm lệch khoảng cách.
- Ô ngày dùng Kendo DatePicker hoặc component chọn kỳ dùng chung, không dùng input date native.
- Đủ trạng thái: trống (icon + mô tả + CTA), đang tải, lỗi, thành công (toast).
- Hành động xóa hoặc ghi ra hệ thống ngoài (Jira, DB): modal xác nhận. Nút chính 1 màu duy nhất.
- Chữ không đặt trực tiếp trên ảnh nền; tương phản ≥ 4.5:1; có focus-visible.
- Text người dùng qua key vi-VN/en-US, không dùng thuật ngữ dev, không trộn Anh–Việt trong một nhãn.
- Kiểm tra ở 1440px, 768px, 390px và chế độ tối.
- Lưới: header canh giữa, text trái, số/tiền phải, còn lại giữa.
Trả lời: (1) thay đổi chính, (2) file sửa, (3) ảnh hưởng responsive/i18n/dark, (4) checklist kiểm tra.
```

### 5.4 Đề xuất cải tiến tính năng prompt

- Nút "Sao chép" ghép đủ Nội dung + Định dạng đầu ra thành một prompt hoàn chỉnh, và tự tăng số lần dùng.
- Đặt tên biến thống nhất dạng `{{ten_bien}}`. Khi sao chép, mở form điền biến trước.
- Một nguồn duy nhất cho prompt: hoặc tạo seed từ `docs/*_PROMPTS.md`, hoặc ngược lại.
- Bổ sung prompt: "Đánh giá UX/accessibility", "Kiểm tra bản địa hóa", "Viết test", "Đánh giá hiệu năng".

---

## 6. Thứ tự ưu tiên cải tiến

> **Đã được thay thế** bởi [mục 10 – Lộ trình triển khai chốt](#10-lộ-trình-triển-khai-chốt). Phần dưới đây giữ lại để tham khảo.

### P0: lỗi, cần sửa ngay

| # | Việc | Màn hình |
|---|---|---|
| 1 | Sửa sidebar thu gọn mất menu; thêm menu trượt cho mobile | Toàn app |
| 2 | Thêm trạng thái active cho menu và `aria-current` | Toàn app |
| 3 | Làm lại trang Đăng ký; sửa label đè chữ ở Đăng ký và trang Email | Identity |
| 4 | Sửa lỗi nạp Kendo (thêm biến rỗng `_progress_kendo_drawing`, không nạp gói thương mại), rồi chuyển Kendo Demo và Component Hub sang nhóm Dev | Dev, Jira, DB |
| 5 | Sửa lỗi JS `parseColor` (chính là việc 4) | 7 trang |
| 6 | Thêm xác nhận khi xóa (Công việc, Liên kết nhanh, Prompt) | Cá nhân |
| 7 | Trang Access denied: dịch sang tiếng Việt, giải thích lý do, thêm nút hành động | Identity |
| 8 | Sửa avatar Jira bị vỡ; đổi tên menu Jira và Công việc cho khỏi nhầm | Jira |
| 9 | Tab Tìm kiếm: mã issue có link và không bẻ dòng; dùng nhãn trạng thái màu; bỏ các cột trống; đổi đúng tên cột "Logged" | Jira |
| 10 | Kéo thả ở tab Quản lý phải có trạng thái "chưa lưu" và thanh Lưu/Hủy, nếu không thì tắt | Jira |
| 11 | "Lưu đề xuất" và kéo thả chỉ đổi dữ liệu trên trình duyệt: ghi thật lên Jira (có xác nhận), hoặc đổi tên và hiện trạng thái "chưa lưu" | Jira |
| 12 | Canh phải cột Ước tính và Log theo đúng quy ước lưới | Jira |

### P1: thống nhất giao diện

1. Thu gọn bảng màu về một bộ biến CSS duy nhất, chỉ một màu cho nút chính; thang cỡ chữ bằng rem, tối thiểu 12px; tải font thật sự hoặc dùng font hệ thống.
2. Tạo một khung tiêu đề trang chung và bỏ các tiêu đề trùng.
3. Đưa Công việc, Liên kết nhanh và Prompt về khuôn của trang Ghi chú: toolbar, tìm kiếm, trạng thái trống, form trong modal.
4. Dịch hết các trang tài khoản; bỏ thuật ngữ dev khỏi giao diện; thống nhất thuật ngữ Việt/Anh; định dạng số `17,5 giờ`.
5. Hình nền mặc định trung tính, cho phép tự bật.
6. Dùng chung một component kết nối cho So sánh DB và Bắt SQL.
7. Jira: một component chọn kỳ dùng chung cho cả 4 màn hình; một cấu hình Tabulator dùng chung (chuyển bảng Tìm kiếm sang Tabulator); một bảng màu trạng thái dùng chung.
8. Cập nhật prompt UI và `AGENTS.md` theo mục 5.3.

### P2: nâng cấp

1. Tổng quan cá nhân có số liệu tóm tắt và thao tác nhanh.
2. Thanh lệnh **Ctrl+K** để mở nhanh công cụ, ghi chú, liên kết.
3. Prompt: điền biến bằng form, sao chép kèm định dạng đầu ra, tìm kiếm và lọc.
4. Hẹn giờ: vòng tiến độ, các mức nhanh, thông báo khi hết giờ.
5. Liên kết nhanh: favicon, gom nhóm, sửa được.
6. Jira Dashboard: thẻ "Log/Ước tính %", biểu đồ giờ log theo ngày so với 8h, so sánh với kỳ trước, nhóm subtask theo issue cha.
7. Jira kanban: thẻ Done mờ đi, chú thích thanh công suất, giao diện lịch cho chế độ tháng.
8. Jira sắp lịch tự động: dạng các bước, cột "Hạn cũ → Hạn mới", cảnh báo việc chưa có ước tính.

---

## 7. Phụ lục: bảng điểm

| Màn hình | Điểm | Ghi chú ngắn |
|---|---|---|
| Ghi chú | 7.5 | Mẫu chuẩn nên áp dụng cho các trang khác |
| So sánh cấu trúc DB | 7 | Chia bước tốt; cần khóa nút khi chưa kết nối |
| Jira Dashboard | 6.5 | Số liệu tốt nhưng chưa làm nổi bật log vượt ước tính |
| Đăng nhập | 6 | Lặp tiêu đề, thiếu quên mật khẩu |
| Tabulator Demo | 6 | Ổn, nhãn trạng thái khó hiểu |
| Jira – Quản lý (kanban) | 6 | Ý tưởng hay, chữ quá nhỏ, kéo thả không lưu |
| Component Hub | 5 | Công cụ dev nằm trong menu người dùng |
| Jira – Sắp xếp tự động | 5 | Luồng không rõ, không thấy thay đổi, nút "Lưu" không lưu |
| Bắt câu lệnh SQL | 4.5 | Lệch với So sánh DB, nút trông như bị vô hiệu |
| Tổng quan | 4 | Thiếu tóm tắt, dữ liệu "hôm nay" sai nghĩa |
| Hẹn giờ tập trung | 4 | Lưu được phiên giả, thiếu trạng thái trống |
| Không gian prompt | 4 | Dài, không tìm kiếm, sao chép thiếu định dạng đầu ra |
| Quản lý tài khoản | 4 | Tiếng Anh, label đè chữ, active không rõ |
| Công việc (checklist) | 3.5 | Thiếu label, lọc, xác nhận xóa |
| Liên kết nhanh | 3.5 | Ô "0" không label, không sửa được |
| Jira – Tìm kiếm | 3.5 | Bảng thường, mã không có link, JQL lặp |
| Đăng ký | 2 | Trang scaffold tiếng Anh, label đè chữ |
| Kendo UI Demo | 2 | Hộp lỗi đỏ khắp trang (do lỗi nạp Kendo; sửa xong ước khoảng 6) |
| Access denied | 1 | Tiếng Anh, không giải thích, không có hành động |

---

## 8. Kế hoạch xử lý chi tiết theo thứ tự ưu tiên

> **Thứ tự giai đoạn ở mục này đã được thay thế** bởi [mục 10](#10-lộ-trình-triển-khai-chốt). Mục 8 vẫn là danh mục chi tiết (nguyên nhân, cách sửa, cách kiểm tra) cho từng mã việc.

Mỗi việc ghi theo 5 ý:
- **Vấn đề:** người dùng gặp gì.
- **Nguyên nhân / vị trí:** file và dòng code liên quan, đã đối chiếu với mã nguồn.
- **Cách xử lý:** hướng sửa cụ thể.
- **Kiểm tra:** làm sao biết đã sửa xong.
- **Công sức:** **S** dưới nửa ngày, **M** từ nửa ngày tới 2 ngày, **L** trên 2 ngày.

Thứ tự giai đoạn là thứ tự nên làm. Trong mỗi giai đoạn, việc đứng trước nên làm trước.

| Giai đoạn | Nội dung | Ước lượng | Kết quả sau giai đoạn |
|---|---|---|---|
| 1 | Lỗi gốc và lỗi chức năng | 3–4 ngày | Không còn lỗi console, không còn thao tác gây hiểu nhầm hay mất dữ liệu, dùng được trên mobile |
| 2 | Nền tảng thiết kế | khoảng 1 tuần | Có bộ token, component dùng chung và quy chuẩn ghi vào `AGENTS.md` |
| 3 | Làm lại từng màn hình theo chuẩn | 1–2 tuần | Mọi trang cùng một ngôn ngữ thiết kế |
| 4 | Tối ưu và nâng cấp | liên tục | Nhẹ hơn, có kiểm tra tự động, thêm tính năng giá trị |

### Giai đoạn 1 – Lỗi gốc và lỗi chức năng

#### 1.1 Bundle Kendo dừng giữa chừng: mất TextBox, ListView, Window — **S**
- **Vấn đề:** lỗi `Cannot read properties of undefined (reading 'parseColor')` trên 7 trang. Kendo Demo và Component Hub hiện hộp lỗi "chưa có trong bundle". Widget Window/TextBox/ListView không dùng được ở bất kỳ đâu.
- **Nguyên nhân / vị trí:** `Content/vendor/kendo/js/kendo.ui.core.min.js` là bản UMD, cần thư viện `@progress/kendo-drawing` dưới tên biến toàn cục `_progress_kendo_drawing`. App không nạp thư viện này, nên bundle ném lỗi tại `kendo.deepExtend(kendo,{parseColor:t.parseColor…})` (byte ~1.227.548). Các widget đứng sau điểm này không được đăng ký: TextBox ~1.251.900, ListView ~1.310.070, Window ~1.507.757. Các widget đứng trước vẫn chạy: DatePicker, ComboBox, MultiSelect, AutoComplete, NumericTextBox, Notification.
- **Cách xử lý:**
  > **Đính chính 20/10/2026:** `@progress/kendo-drawing` **là phần mềm thương mại**. File `LICENSE.md` của gói ghi: *"This is commercial software… If you do not own a commercial license, this file shall be governed by the trial license terms"*. Vì ToolboxWeb không dùng thư viện có ràng buộc giấy phép thương mại, **không nạp gói này**. Chỉ dùng cách 1 bên dưới.
  1. Tạo một partial duy nhất `Views/Shared/_KendoScripts.cshtml`, gồm: một dòng `<script>window._progress_kendo_drawing = window._progress_kendo_drawing || {};</script>` (chỉ là một biến rỗng do mình tự viết, không chứa mã của bên thứ ba), rồi `kendo.ui.core.min.js` (Apache-2.0), culture và messages vi-VN. Thay mọi chỗ đang nạp core bằng partial này: `_Layout.cshtml`, `Components/Index.cshtml`, `DemoKendo/Index.cshtml`, `Jira/Dashboard.cshtml`, `Jira/Worklist.cshtml`, `SchemaCompare/Index.cshtml`, `SqlProfiler/Index.cshtml`, và `loadScriptOnce` trong `site.js` (khoảng dòng 666). Cách này **đã được kiểm chứng**: hết lỗi, `kendoTextBox`, `kendoListView`, `kendoWindow` đều có, trang Kendo Demo chạy đầy đủ.
  2. Những gì cần kendo-drawing (ColorPicker, xuất PDF, vẽ) sẽ không chạy. Đánh dấu chúng là "Không dùng – phụ thuộc thư viện thương mại" trong Component Hub; nếu cần chọn màu thì dùng `<input type="color">` (đúng như danh sách control ưu tiên trong `AGENTS.md`).
  3. Cập nhật `docs/kendo-self-host.md`: ghi rõ không dùng `@progress/kendo-drawing` vì lý do giấy phép, mô tả biến rỗng thay thế, và sửa các nhãn "Usable now / Not in current Kendo bundle" trong Component Hub.
- **Kiểm tra:** console sạch trên 7 trang; `jQuery.fn.kendoTextBox`, `kendoListView` và `kendoWindow` đều tồn tại; Kendo Demo không còn hộp đỏ.

#### 1.2 Jira: nút "Lưu đề xuất" và kéo thả không lưu gì — **M (đổi tên) / L (ghi thật lên Jira)**
- **Vấn đề:** người dùng bấm "Lưu đề xuất" hoặc kéo thẻ sang ngày khác và tưởng đã cập nhật Jira. Tải lại trang là mất hết.
- **Nguyên nhân / vị trí:** `saveProposal()` trong `Content/js/jira-auto-schedule.js` (khoảng dòng 527) và sự kiện `drop` trong `Content/js/jira-task-board.js` (khoảng dòng 437) chỉ gán `row.dueDate` trên trình duyệt. Thông báo `Jira.AutoSchedule.SavedToast` có ghi "(chưa lưu vào Jira)", nhưng chữ quá nhỏ và chỉ hiện thoáng qua.
- **Cách xử lý (khuyến nghị theo thứ tự):**
  1. *Ngay lập tức:* đổi nhãn nút thành "Áp dụng vào lưới". Hiện một thanh cố định: "N thay đổi chưa lưu lên Jira · [Hủy]". Đánh dấu thẻ hoặc dòng đã đổi (viền nét đứt kèm "05/10 → 07/10"). Cảnh báo `beforeunload` khi còn thay đổi chưa lưu.
  2. *Tiếp theo:* thêm hàm `UpdateDueDatesAsync` trong service Jira (gọi REST `PUT /rest/api/2/issue/{key}` với trường `duedate`, dùng phiên Jira hiện tại) và một action controller mỏng. Trước khi gửi, mở modal xác nhận liệt kê từng subtask: hạn cũ → hạn mới. Báo kết quả từng dòng (thành công/lỗi), và ghi activity log.
- **Kiểm tra:** sau khi lưu, tải lại trang thì hạn mới vẫn còn, và trên Jira hạn cũng đã đổi. Khi còn thay đổi chưa lưu, rời trang phải có cảnh báo.

#### 1.3 Sidebar thu gọn mất toàn bộ menu — **S**
- **Nguyên nhân / vị trí:** `site.css` dòng 93–100 đặt `font-size: 0` rồi dùng `::first-letter` để hiện lại chữ cái đầu. Nhưng `::first-letter` không áp dụng cho phần tử `display: flex`, nên chữ biến mất hoàn toàn.
- **Cách xử lý:** trong `_Layout.cshtml`, mỗi link gồm `<span class="nav-icon">` (dùng Kendo font icons, đã có sẵn trong `vendor/kendo/icons`) và `<span class="nav-label">`. Khi thu gọn thì ẩn `.nav-label`, giữ icon, và thêm `title` để hiện tooltip. Bỏ hẳn cách dùng `::first-letter`.
- **Kiểm tra:** bấm ☰ thì vẫn thấy icon, rê chuột hiện tên trang, tải lại trang vẫn giữ trạng thái thu gọn.

#### 1.4 Menu không có trạng thái đang chọn — **S**
- **Nguyên nhân / vị trí:** `_Layout.cshtml` dựng link nhưng không so sánh với trang hiện tại; `site.css` không có `.side-nav a.active`.
- **Cách xử lý:** viết một helper (ví dụ `NavLinkTagHelper`, hoặc hàm trong layout) so sánh `ViewContext.RouteData` hoặc `Context.Request.Path` với route của link, rồi thêm `class="active" aria-current="page"`. CSS: nền `color-mix(accent 12%)`, chữ màu accent, có thanh dọc 3px bên trái.
- **Kiểm tra:** mỗi trang (kể cả trang con của Jira và Identity) có đúng một mục được tô.

#### 1.5 Mobile: sidebar đẩy nội dung xuống, header 4 dòng — **M**
- **Nguyên nhân / vị trí:** `site.css` dòng 1774 (`@media (max-width: 900px)`) chỉ chuyển `.app-shell` thành `display:block` và sidebar rộng 100%. Trạng thái ẩn/hiện lưu chung một khóa `toolbox.sidebar` với desktop.
- **Cách xử lý:**
  - Dưới 900px: sidebar thành ngăn trượt (`position: fixed; transform: translateX(-100%)`), có lớp phủ mờ, và dùng class riêng `sidebar-open`, không ghi vào localStorage. Đóng ngăn khi bấm link, bấm lớp phủ hoặc phím Esc.
  - Topbar trên mobile chỉ còn ☰, tên trang và avatar. Đổi theme, ngôn ngữ và đăng xuất chuyển vào menu thả xuống của avatar. Ẩn dòng chữ nhỏ trên topbar và widget thời tiết.
  - Kiểm tra thêm ở 1280px, vì ở độ rộng này dòng chữ nhỏ trên topbar đã xuống 2 dòng.
- **Kiểm tra:** ở 390px, nội dung bắt đầu ngay dưới topbar (cao khoảng 56px); không có thanh cuộn ngang.

#### 1.6 Xóa không hỏi lại — **S**
- **Nguyên nhân / vị trí:** form `Delete` ở `Checklist/Index.cshtml`, `QuickLinks/Index.cshtml` và `PromptTemplates/Index.cshtml` gửi thẳng lên server. Trang Ghi chú đã có `_NoteDeleteModal.cshtml`.
- **Cách xử lý:** tổng quát hóa thành `Views/Shared/_ConfirmModal.cshtml`, kèm một đoạn JS trong `site.js`: nút nào có `data-confirm="Xóa liên kết 'GitHub'?"` thì mở modal, bấm xác nhận mới submit form. Áp dụng cho cả nút "Xóa dòng chọn" của Tabulator (nút này cũng phải bị vô hiệu khi chưa chọn dòng nào).
- **Kiểm tra:** mọi hành động xóa đều hiện modal; bấm Hủy thì không mất dữ liệu.

#### 1.7 Label đè lên chữ ở Đăng ký và các trang tài khoản — **S**
- **Nguyên nhân / vị trí:** `vendor-sync.css` dòng 39–50 đặt `.content-area .form-control { padding: .62rem .9rem }`, độ ưu tiên cao hơn padding riêng của `.form-floating > .form-control` trong Bootstrap. Dòng 71 còn tô màu cho placeholder, trong khi Bootstrap cố ý làm placeholder trong suốt ở dạng floating. Kết quả là label, placeholder và giá trị chồng lên nhau.
- **Cách xử lý:** thêm `:not(.form-floating > *)` vào hai selector trên, hoặc viết riêng một rule cho `.content-area .form-floating > .form-control` gồm `padding-top: 1.625rem; padding-bottom: .625rem` và `::placeholder { color: transparent }`.
- **Kiểm tra:** Register, Manage/Email và Manage/ChangePassword hiển thị label nhỏ phía trên giá trị, không chồng chữ.

#### 1.8 Ảnh đại diện vỡ — **S**
- **Nguyên nhân / vị trí:** `_LoginPartial.cshtml` hiện `<img src="@avatarUrl">` mà không xử lý khi ảnh lỗi. Ảnh lấy từ `AvatarUrl` trong hồ sơ, phục vụ qua `/uploads`; nhiều khả năng file đã không còn trong `Storage:UploadsPath`. Cần kiểm tra trên máy thật.
- **Cách xử lý:** thêm `onerror` để ẩn ảnh và hiện chữ viết tắt (luôn render sẵn chữ viết tắt bên dưới). Ở `UserProfileService`, chỉ trả về `AvatarUrl` khi file thực sự tồn tại.
- **Kiểm tra:** xóa thử file avatar thì header hiện chữ viết tắt, không còn icon ảnh lỗi.

#### 1.9 Tab Tìm kiếm Jira: mã issue không có link, trạng thái thô, cột sai nghĩa — **S–M**
- **Nguyên nhân / vị trí:** `Views/Jira/Worklist.cshtml` (khoảng dòng 207–235) là `<table>` thuần: `@issue.IssueKey` không có link; trạng thái là chữ trơn; cột "Logged" in ra `LoggedDates` (danh sách ngày, không phải số giờ).
- **Cách xử lý:**
  - *Nhanh (S):* bọc mã issue bằng link `target="_blank"` có `white-space: nowrap`; dùng chung lớp nhãn trạng thái với Dashboard; đổi tên cột thành "Ngày đã log" hoặc hiện tổng giờ; ẩn cột nào trống toàn bộ.
  - *Đầy đủ (M):* chuyển bảng sang Tabulator với cấu hình dùng chung (xem 2.4) để có sắp xếp, đếm số dòng và phân trang.
- **Kiểm tra:** mã issue mở đúng trang Jira; không còn chữ bị bẻ dòng như "NTTM-/2462".

#### 1.10 Canh lề cột số trong bảng Jira — **S**
- **Nguyên nhân / vị trí:** `Content/js/jira-dashboard.js` (khoảng dòng 529–640) đặt `hozAlign: "center"` cho cả cột Ước tính và Log; bảng của tab Sắp xếp tự động cũng vậy.
- **Cách xử lý:** đổi thành `hozAlign: "right"` và định dạng `toLocaleString('vi-VN', { maximumFractionDigits: 1 })` kèm hậu tố "h". Mũi tên sắp xếp chỉ hiện ở cột đang được sắp xếp.
- **Kiểm tra:** cột số thẳng hàng theo hàng đơn vị, hiện dạng `17,5h`.

#### 1.11 Tổng quan: "Công việc hôm nay" sai dữ liệu — **S**
- **Nguyên nhân / vị trí:** `Services/DashboardService.cs` (khoảng dòng 55) lọc `Status == Todo || DueDate == today`, tức là lấy **mọi** việc chưa xong.
- **Cách xử lý:** hoặc đổi tiêu đề thành "Việc đang mở", hoặc tách thành 3 nhóm: "Quá hạn" (`DueDate < today`), "Hôm nay" (`DueDate == today`), "Chưa có hạn". Khuyến nghị tách nhóm.
- **Kiểm tra:** việc không có hạn không còn nằm dưới tiêu đề "hôm nay".

#### 1.12 Hẹn giờ cho lưu phiên khi chưa chạy — **S**
- **Nguyên nhân / vị trí:** nút submit trong `FocusTimer/Index.cshtml` luôn bấm được.
- **Cách xử lý:** vô hiệu nút cho tới khi bộ đếm chạy hết, hoặc cho phép lưu số phút thực tế đã chạy (gửi kèm một trường ẩn chứa số giây đã chạy, server kiểm tra lớn hơn 0).
- **Kiểm tra:** chưa bấm Bắt đầu thì không lưu được phiên.

#### 1.13 Lỗi nhỏ hiển thị — **S**
- Liên kết nhanh `AI· https…`: Razor cắt mất khoảng trắng trước `<text>`. Sửa bằng `@($"{link.GroupName} · ")`. Ô thứ tự sắp xếp cần có label "Thứ tự".
- Xóa file thừa `Views/DemoKendo/Index.cshtml.bak` khỏi repo.
- Trang Access denied: tạo trang riêng `Areas/Identity/Pages/Account/AccessDenied.cshtml` bằng tiếng Việt, giải thích "Trang này dành cho tài khoản đăng nhập bằng Jira", kèm nút [Đăng nhập Jira] và [Về Tổng quan].

### Giai đoạn 2 – Nền tảng thiết kế

#### 2.1 Bộ token màu, khoảng cách, bo góc — **M**
- **Vấn đề:** 129 mã hex khác nhau; nút chính có 3 màu xanh; độ đậm chữ dùng 6 mức.
- **Cách xử lý:** gom về `Content/css/tokens.css`: `--color-primary`, `--color-primary-hover`, nhóm màu trạng thái (`--color-success/warning/danger/info` kèm nền nhạt tương ứng), 3 mức xám cho chữ, khoảng cách `--space-1…6` (4/8/12/16/24/32), bo góc `--radius-sm/md/lg/pill`. Dùng find/replace để đổi hex sang biến, mỗi lần một file. Gộp `vendor-sync.css` theo token. Chỉ giữ một kiểu `.btn-primary` (bỏ gradient).
- **Kiểm tra:** `grep -o "#[0-9a-fA-F]\{6\}"` ngoài `tokens.css` còn gần bằng 0; nút chính ở mọi trang cùng một màu.

#### 2.2 Chữ và font — **S**
- **Cách xử lý:** hoặc self-host Roboto (`Content/fonts/*.woff2` kèm `@font-face`), hoặc đổi sang `system-ui, "Segoe UI", Roboto, sans-serif` và bỏ khai báo "Roboto" trơ trọi. Thang cỡ chữ bằng rem: 0.75 / 0.875 / 1 / 1.25 / 1.5; không còn cỡ px lẻ (10.5, 11.5, 12.5). Độ đậm chỉ dùng 400/600/700. Tiêu đề panel đậm 600 (đang nhẹ hơn tiêu đề dòng bên trong).
- **Kiểm tra:** cùng một trang hiển thị giống nhau trên Windows và macOS; không còn chữ dưới 12px.

#### 2.3 Khung tiêu đề trang thống nhất — **S**
- **Cách xử lý:** tạo partial `_PageHeader` nhận tiêu đề, mô tả và vùng nút. Topbar chỉ hiện tiêu đề trang; bỏ dòng "BỘ CÔNG CỤ NĂNG SUẤT CÁ NHÂN"; bỏ h1 trùng ở Component Hub, Kendo Demo và trang Quản lý tài khoản.
- **Kiểm tra:** mỗi trang chỉ có một tiêu đề chính.

#### 2.4 Component dùng chung — **M**
- `_EmptyState` (icon, tiêu đề, mô tả, nút hành động): lấy từ trang Ghi chú.
- `_ConfirmModal`: từ việc 1.6.
- `_FormField` (label, ô nhập, gợi ý, thông báo lỗi giữ chỗ cố định): sửa luôn lỗi khoảng cách không đều khi có thẻ validation.
- `_Toolbar` (số liệu, ô tìm kiếm, nút chính).
- `jira-status.js`: một bảng ánh xạ trạng thái Jira sang màu dùng chung cho Dashboard, Tìm kiếm, kanban và Sắp xếp tự động.
- `jira-grid-defaults.js`: cấu hình Tabulator dùng chung (canh lề theo quy ước, định dạng số và ngày, trạng thái trống, tooltip khi chữ bị cắt).
- `jira-period-picker.js`: một bộ chọn kỳ duy nhất (Ngày/Tuần/Tháng/Năm, tự tải lại khi đổi kỳ, không có nút ×, bỏ « »).
- `_SqlConnectionForm`: form kết nối dùng chung cho So sánh DB và Bắt SQL.

#### 2.5 Hình nền và độ tương phản — **S**
- **Cách xử lý:** thêm tùy chọn "Hình nền động" trong trang tài khoản (lưu localStorage, mặc định tắt) và dùng nền trung tính `--bg`. Mọi chữ phải nằm trên `--surface`. Các mục menu trong trang Quản lý tài khoản dùng nền đặc thay vì trong suốt.
- **Kiểm tra:** chạy axe-core hoặc Lighthouse, không còn lỗi tương phản.

#### 2.6 Topbar gọn lại — **S**
- **Cách xử lý:** bỏ widget thời tiết, hoặc chỉ hiện khi người dùng bật; chỉ gọi `navigator.geolocation` khi người dùng chủ động bấm. Nút theme dùng icon SVG kèm tooltip. Ngôn ngữ, theme và đăng xuất gom vào menu thả xuống của avatar.

#### 2.7 Sidebar phân nhóm và icon — **S**
- **Cách xử lý:** chia nhóm theo cấu trúc menu đề xuất ở mục 2.1 phía trên của tài liệu (JIRA / CÁ NHÂN / CÔNG CỤ DB / DEV). Đổi tên "Danh sách công việc" thành "Công việc Jira", "Công việc" thành "Việc cần làm". Thêm "Không gian prompt" vào menu. Nhóm DEV chỉ hiện khi `IWebHostEnvironment.IsDevelopment()` hoặc người dùng có role riêng.

#### 2.8 Bản địa hóa và định dạng — **M**
- **Cách xử lý:**
  - Dịch `Profile.Nav.*` trong `vi-VN.json`.
  - Scaffold các trang Identity còn dùng mặc định (Register, Manage/Email, ChangePassword, TwoFactorAuthentication, PersonalData, AccessDenied) bằng `dotnet aspnet-codegenerator identity --files "..."` rồi chuyển chữ sang key bản địa hóa.
  - Lập bảng thuật ngữ Jira (giữ nguyên hoặc Việt hóa hẳn), không trộn hai thứ tiếng trong một nhãn.
  - Viết một helper định dạng số và ngày theo `CultureInfo.CurrentUICulture` (`17,5`, `dd/MM/yyyy HH:mm`). Thống nhất múi giờ: hoặc dùng múi giờ trình duyệt ở mọi chỗ, hoặc dùng +7 ở mọi chỗ.
- **Kiểm tra:** đổi qua lại VI/EN thì không còn chữ của ngôn ngữ kia; ngày giờ hiển thị cùng một định dạng.

#### 2.9 Ghi quy chuẩn vào `AGENTS.md` và prompt — **S**
- **Cách xử lý:** thêm mục "UI Standard" vào `AGENTS.md` theo bản đề xuất ở 5.3. Sửa prompt UI tạo sẵn (`SeedPrompt.UiChange.*`) và `docs/FEATURE_PROMPTS.md` cho khớp nhau. Nút Sao chép ghép đủ Nội dung + Định dạng đầu ra. Bỏ đường dẫn riêng của máy (`D:\Source_Toolbox\.codegraph`).
- **Lý do làm ở giai đoạn 2:** đây là nguyên nhân gốc của sự thiếu nhất quán; nếu không sửa, các màn hình làm mới ở giai đoạn 3 sẽ lại lệch nhau.

### Giai đoạn 3 – Làm lại từng màn hình theo chuẩn

Thứ tự làm theo tần suất sử dụng và mức độ hỏng hiện tại:

| # | Màn hình | Việc chính | Công sức |
|---|---|---|---|
| 3.1 | Jira Dashboard | Thẻ "Log/Ước tính %" có cảnh báo; thẻ tiến độ 15/16; so sánh với kỳ trước; biểu đồ giờ log theo ngày so với 8h; nhóm subtask theo issue cha (`groupBy` của Tabulator); khối JQL thu gọn kèm "Sao chép" và "Mở trong Jira"; "Chưa ước tính" màu cam; "Trễ N ngày" | M–L |
| 3.2 | Jira – Tìm kiếm | Chế độ Bộ lọc / JQL loại trừ nhau; sau khi tìm thì thu form thành một dòng tóm tắt; bỏ khối "JQL đã áp dụng" trùng; bảng theo việc 1.9 | M |
| 3.3 | Jira – Kanban | Thẻ Done mờ đi kèm nút "Ẩn việc đã xong"; chú thích màu thanh công suất, tô đỏ khi vượt 8h; ghi "Hôm nay" thay "★"; thu hẹp cột cuối tuần; chế độ Tháng dùng lịch; chữ trong thẻ tối thiểu 12px | M |
| 3.4 | Jira – Sắp xếp tự động | Dạng các bước 1-2-3; cột "Hạn cũ → Hạn mới"; cảnh báo subtask chưa có ước tính; tooltip khi chữ bị cắt; cỡ tiêu đề đồng bộ; luồng lưu theo việc 1.2 | M |
| 3.5 | Đăng nhập / Đăng ký | Layout riêng `_AuthLayout` không có sidebar và topbar; Đăng ký thành tab thứ ba; quên mật khẩu; ẩn/hiện mật khẩu; một tiêu đề | M |
| 3.6 | Việc cần làm (checklist) | Áp dụng khuôn trang Ghi chú: toolbar, tìm kiếm, chip lọc (Cần làm / Quá hạn / Đã xong), checkbox ở đầu dòng, form trong modal, Kendo DatePicker, trạng thái trống | M |
| 3.7 | Tổng quan | Hàng số liệu (việc quá hạn, việc hôm nay, phút tập trung, giờ log Jira nếu có); panel không kéo cao bằng nhau (`align-items: start`); lịch sử hoạt động 5 dòng kèm "Xem tất cả"; liên kết nhanh dạng ô | M |
| 3.8 | Không gian prompt | Tìm kiếm, lọc theo loại, thu gọn từng prompt; Sao chép đầy đủ và tự tăng số lần dùng; form điền biến `{{ten_bien}}`; form tạo trong modal | M |
| 3.9 | So sánh DB và Bắt SQL | Dùng `_SqlConnectionForm`; khóa ô Database và nút hành động cho tới khi kết nối thành công; "Tin cậy certificate" mặc định tắt kèm cảnh báo; đánh số bước 3; Bắt SQL có trạng thái trống và trạng thái nút đúng (Dừng chỉ đỏ khi đang ghi; Xuất file bị vô hiệu khi chưa có dữ liệu) | M |
| 3.10 | Quản lý tài khoản | Trang Identity dịch xong (2.8); menu dạng danh sách gọn có nền đặc; tên đăng nhập là chữ thường thay vì ô nhập; bỏ thuật ngữ "source / input file / file rác" | S–M |
| 3.11 | Liên kết nhanh | Sửa được; gom theo nhóm; ô có chữ cái đầu (không gọi dịch vụ favicon bên ngoài); có label đầy đủ | S |
| 3.12 | Hẹn giờ tập trung | Vòng tiến độ; mức nhanh 25/5/15; thông báo trình duyệt và âm báo; tiêu đề tab hiện thời gian còn lại; trạng thái trống cho danh sách phiên | S–M |
| 3.13 | Component Hub và Kendo Demo | Chuyển sang nhóm Dev; cập nhật nhãn sau khi xong 1.1; bỏ thông báo lỗi kiểu dev; sửa thẻ chỉ rộng nửa trang | S |

### Giai đoạn 4 – Tối ưu và nâng cấp

#### 4.1 Hiệu năng tải trang — **S–M**
- **Hiện trạng:** `kendo.ui.core.min.js` khoảng 1,5 MB và `default-main.css` khoảng 900 KB được nạp ở 7 trang; Tabulator khoảng 440 KB.
- **Cách xử lý:**
  - Chỉ nạp qua partial (việc 1.1) ở trang thực sự cần.
  - Vì đã có `asp-append-version`, có thể đặt `Cache-Control: max-age=31536000, immutable` cho `/vendor/*` trong `UseStaticFiles`.
  - Cân nhắc build Kendo theme chỉ gồm các widget đang dùng.
  - Đồng hồ trên topbar có thể cập nhật mỗi phút thay vì mỗi giây (giờ chỉ hiện tới phút).

#### 4.2 Cấu trúc CSS — **M**
- **Hiện trạng:** `site.css` khoảng 2.480 dòng (gồm cả Jira, Identity, Login); `vendor-sync.css` khoảng 1.100 dòng.
- **Cách xử lý:** tách theo module (`jira.css`, `identity.css`, `auth.css`) và chỉ nạp qua `@section Styles`. Xóa rule chết. Thêm stylelint với quy tắc cấm mã hex ngoài `tokens.css`.

#### 4.3 Cấu trúc view — **M**
- **Hiện trạng:** `Views/Components/Index.cshtml` dài 2.868 dòng (khoảng 154 KB); vẫn còn inline style ở Components và DemoKendo.
- **Cách xử lý:** tách mỗi tab thành một partial `_Tab*.cshtml`; chuyển inline style thành class.

#### 4.4 Kiểm tra tự động giao diện — **M**
- **Cách xử lý:** thêm vào `tests/` một bộ Playwright:
  1. Chụp các trang chính ở 1440px và 390px, sáng và tối, rồi so sánh ảnh (visual regression).
  2. Chạy axe-core để bắt lỗi tương phản, label, `aria-current`.
  3. Đảm bảo console không có lỗi JS.

  Script chụp màn hình dùng cho bản đánh giá này có thể làm điểm khởi đầu.
- **Lý do:** ngăn các lỗi như 1.1 hay 1.3 quay lại mà không ai phát hiện.

#### 4.5 Tính năng nâng giá trị — **M–L**
- Thanh lệnh **Ctrl+K**: mở nhanh trang, ghi chú, liên kết, prompt, issue Jira.
- Tổng quan Jira theo tháng: biểu đồ giờ log theo ngày. ECharts đã có sẵn trong asset nhưng chưa được nối vào; cần nối theo đúng quy định của `AGENTS.md`.
- Nhắc log giờ: cuối ngày nếu chưa đủ 8h thì hiện thông báo trên Tổng quan Jira.

---

## 9. Đính chính so với bản đầu

Sau khi đối chiếu lại mã nguồn, các nhận định sau của bản đầu đã được sửa trong tài liệu này:

| Bản đầu | Thực tế | Đã sửa ở |
|---|---|---|
| Hình nền chỉ cao 900px, các trang dài lộ dải navy | Nền dùng `position: fixed`, luôn phủ khung nhìn khi cuộn. Dải navy chỉ xuất hiện do cách chụp ảnh toàn trang | 2.3 |
| Trang Quản lý tài khoản có 2 mục cùng được tô sáng | Chỉ một mục `active`; các mục khác nền trong suốt nên trông như được chọn khi nằm trên vùng sáng của hình nền | 3.15 |
| Kendo Demo hỏng vì thiếu widget, đề xuất xóa trang | Các widget có trong bundle; bundle dừng do thiếu biến `_progress_kendo_drawing`. Đề xuất đổi thành sửa lỗi nạp | 2.6, 3.9, 3.10, 8 (1.1) |
| "Lưu đề xuất" ghi thật vào Jira mà không xác nhận | Nút chỉ đổi dữ liệu trên trình duyệt, không ghi lên Jira; vấn đề là tên nút gây hiểu nhầm | 4.5, 8 (1.2) |
| Mobile không có cách ẩn sidebar | Có nút ☰ để ẩn, nhưng sidebar mặc định mở và trạng thái lưu chung với desktop | 2.1 |
| Avatar Jira vỡ do URL Jira cần đăng nhập | Ảnh lấy từ file upload trong hồ sơ; nguyên nhân cụ thể cần kiểm tra trên máy thật | 2.2, 8 (1.8) |
| Việc 1.1 khuyên nạp thêm `@progress/kendo-drawing` | Gói này là phần mềm thương mại của Progress, trái với nguyên tắc không dùng thư viện có ràng buộc giấy phép. Chỉ dùng biến rỗng tự viết | 8 (1.1) |

---

## 10. Lộ trình triển khai chốt

> Mục này **thay thế thứ tự ưu tiên** ở mục 6 và thứ tự giai đoạn ở mục 8. Mục 8 vẫn được giữ làm **danh mục chi tiết** (nguyên nhân, cách sửa, cách kiểm tra); các mã việc như `1.1`, `2.4` bên dưới trỏ về mục 8.

### 10.1 Vì sao phải xếp lại

Bản kế hoạch ở mục 8 có bốn lỗi về thứ tự:

1. **Quy chuẩn prompt đứng cuối Giai đoạn 2**, trong khi đây là nguyên nhân gốc. Mọi bản sửa sau đó đều do AI thực hiện, nên nếu chưa có chuẩn thì vừa sửa vừa tạo thêm sự thiếu nhất quán.
2. **Có việc "đánh bóng thứ sắp bị thay".** Ví dụ: sửa mẹo `::first-letter` của sidebar thu gọn (1.3), hay làm menu trượt cho mobile trên sidebar cũ (1.5), trong khi toàn bộ khung app sẽ được làm lại ở bước nền tảng. Làm hai lần là lãng phí.
3. **Xây nền tảng trước khi chốt quyết định.** Bộ token (2.1) cần biết trước màu nhấn, font, bộ icon và hướng phong cách (Linear). Các quyết định này chưa được ghi lại ở đâu.
4. **Kiểm tra tự động đứng cuối (4.4).** Đáng lẽ phải có ảnh "trước khi sửa" ngay từ đầu để so sánh và để phát hiện khi một bản sửa làm hỏng trang khác.

### 10.2 Tám nguyên tắc xếp ưu tiên

| # | Nguyên tắc | Hệ quả |
|---|---|---|
| 1 | **Luật trước, code sau** | `AGENTS.md`, chính sách giấy phép và prompt được chuẩn hóa đầu tiên |
| 2 | **Đo trước khi sửa** | Chụp ảnh mốc và kiểm tra lỗi console trước mọi thay đổi |
| 3 | **Mức độ nghiêm trọng**: mất dữ liệu hoặc gây hiểu sai > chặn chức năng > sai hiển thị > thẩm mỹ | Nút "Lưu" không lưu và xóa không hỏi lại được xử lý ngay |
| 4 | **Không đánh bóng thứ sắp bị thay** | Lỗi nằm trong phần sẽ làm lại (sidebar, topbar) thì gộp vào lần làm lại, trừ khi lần làm lại còn xa |
| 5 | **Quyết định trước khi xây** | Có một bước riêng để chốt phong cách, màu, font, icon và thư viện |
| 6 | **Chung trước riêng**: token → component → khung app → mẫu theo nhóm → từng màn hình | Sửa một chỗ chung thì nhiều trang được hưởng; làm màn hình trước nền tảng thì phải làm lại |
| 7 | **Theo giá trị**: tần suất dùng × giá trị nghiệp vụ × mức hỏng hiện tại | Nhóm Jira đi trước trong các màn hình |
| 8 | **Nhỏ và độc lập** | Mỗi việc là một PR riêng, merge được riêng, có tiêu chí hoàn thành |

### 10.3 Tổng quan lộ trình

| Giai đoạn | Mục tiêu | Ước lượng | Ai quyết | Tiêu chí hoàn thành |
|---|---|---|---|---|
| **0. Quy trình và luật** | AI và người cùng làm theo một chuẩn; có ảnh mốc | 0,5–1 ngày | Chủ dự án duyệt `AGENTS.md` | `AGENTS.md` mới đã merge; script kiểm tra chạy được và có ảnh mốc |
| **1. Lỗi độc lập với giao diện** | Hết lỗi gây hiểu sai, mất dữ liệu, chặn chức năng | 2–3 ngày | — | Console sạch mọi trang; mọi thao tác xóa đều hỏi lại; không còn nút "Lưu" mà không lưu |
| **2. Chốt quyết định thiết kế** | Có đủ dữ kiện để xây nền tảng | 1–2 ngày (+ thời gian chủ dự án xem) | **Chủ dự án** | `docs/DESIGN_DECISIONS.md` được duyệt; bản mẫu tĩnh được duyệt |
| **3. Nền tảng chung** | Toàn app đổi sang ngôn ngữ thiết kế mới cùng lúc | 5–7 ngày | Chủ dự án duyệt khung app | Mọi trang chạy trong khung mới; trang Component Hub thành bảng mẫu component |
| **4. Mẫu dùng chung theo nhóm** | Có sẵn khối dựng cho Jira, DB, Identity, trang danh sách | 4–6 ngày | — | Mỗi mẫu đã được dùng ở ít nhất một màn hình |
| **5. Từng màn hình** | Áp dụng nền tảng và mẫu vào từng trang | 8–12 ngày | Chủ dự án duyệt trang thí điểm (Jira Dashboard) | Không còn trang nào dùng kiểu cũ |
| **6. Tính năng mới và tối ưu** | Nâng giá trị, giảm dung lượng tải, tự động hóa kiểm tra | liên tục | Chủ dự án chọn thứ tự | Theo từng việc |

**Tổng ước lượng:** khoảng **4–6 tuần làm việc** cho giai đoạn 0–5 với một người. Đây là ước lượng thô, chưa tính thời gian chờ duyệt.

**Bốn điểm dừng để chủ dự án duyệt:** sau GĐ0 (luật), sau GĐ2 (quyết định thiết kế và bản mẫu), sau khung app ở GĐ3, sau trang thí điểm Jira Dashboard ở GĐ5. Không làm tiếp khi điểm dừng trước chưa được duyệt.

### 10.4 Giai đoạn 0 – Quy trình và luật (làm đầu tiên)

| # | Việc | Chi tiết | Công sức |
|---|---|---|---|
| 0.1 | **Chính sách giấy phép** trong `AGENTS.md` | Chỉ chấp nhận MIT, Apache-2.0, BSD, ISC, OFL. Cấm `@progress/kendo-drawing` và mọi gói Kendo thương mại. Trước khi thêm thư viện phải kiểm tra `LICENSE` của **cả các gói phụ thuộc**: bài học từ việc 1.1 là Kendo Core dùng Apache-2.0 nhưng lại cần một gói thương mại | S |
| 0.2 | **`THIRD-PARTY-NOTICES.md`** | Liệt kê thư viện, phiên bản, giấy phép (theo bảng đã kiểm tra); giữ header bản quyền trong file `.min.js` | S |
| 0.3 | **Quy chuẩn UI** trong `AGENTS.md` (phần không phụ thuộc giá trị cụ thể) | Ô nhập luôn có label; xóa và ghi ra hệ thống ngoài phải xác nhận; mỗi trang một tiêu đề; đủ trạng thái trống/đang tải/lỗi; không đặt chữ lên ảnh nền; không thêm mã hex mới; chữ tối thiểu 12px; không trộn Anh–Việt trong một nhãn; trang Ghi chú là mẫu; kiểm tra ở 1440px, 390px và chế độ tối; định hướng phong cách Linear (học nguyên tắc, không sao chép tài sản). Bỏ đường dẫn `D:\Source_Toolbox\.codegraph` | S |
| 0.4 | **Định nghĩa "hoàn thành"** trong `AGENTS.md` | `dotnet build` sạch; script kiểm tra (0.6) không báo lỗi console; ảnh mới đã so với ảnh mốc; với trang Jira, chủ dự án đã kiểm tra trên Jira thật | S |
| 0.5 | **Đồng bộ prompt** | `docs/FEATURE_PROMPTS.md`, `docs/MAINTENANCE_PROMPTS.md` và `SeedPrompt.*` (vi/en) cùng một nội dung; thống nhất tên biến `{{ten_bien}}`; thêm đoạn ngữ cảnh về stack; thêm prompt "Đánh giá UX/accessibility" và "Kiểm tra giấy phép thư viện". **Lưu ý:** `Data/SeedData.cs` chỉ thêm prompt khi người dùng **chưa có** prompt cùng loại, nên tài khoản cũ sẽ không nhận bản mới. Cần chọn một trong hai: chấp nhận điều đó, hoặc thêm nút "Khôi phục prompt mẫu" (làm ở 5.10) | S–M |
| 0.6 | **Script kiểm tra giao diện và ảnh mốc** | Script Playwright đặt trong `tests/ui/` (kết quả ghi ra thư mục tạm, không ghi vào repo): đăng nhập tài khoản thử, chụp các trang chính ở 1440px/390px, sáng/tối; báo lỗi console; chạy axe-core. Lưu bộ ảnh "trước khi sửa" | M |
| 0.7 | **Dữ liệu Jira giả cho môi trường Development** *(khuyến nghị)* | Một chế độ chỉ bật ở Development, trả dữ liệu Jira mẫu, để chụp và kiểm tra các trang Jira mà không cần tài khoản Jira thật | M |

**Vì sao 0.7 đáng làm sớm:** Jira là nhóm màn hình ưu tiên số một ở GĐ5 nhưng hiện chỉ kiểm tra được khi có tài khoản Jira thật. Không có 0.7 thì mọi thay đổi Jira đều phải chờ chủ dự án tự kiểm tra.

### 10.5 Giai đoạn 1 – Lỗi độc lập với giao diện

Chỉ gồm những bản sửa **vẫn còn giá trị sau khi làm lại giao diện**. Xếp theo mức độ nghiêm trọng:

| Thứ tự | Mã (mục 8) | Việc | Loại lỗi | Công sức |
|---|---|---|---|---|
| 1 | 1.2 (phần đổi tên) | Jira: đổi "Lưu đề xuất" thành "Áp dụng vào lưới"; thanh "N thay đổi chưa lưu"; cảnh báo khi rời trang. Ghi thật lên Jira chuyển sang GĐ6 | Gây hiểu sai, mất thay đổi | S–M |
| 2 | 1.6 | Xác nhận khi xóa, dùng `_ConfirmModal` dùng chung. Đây là component chung **đầu tiên**, nên làm đúng quy chuẩn GĐ0 để không phải làm lại | Mất dữ liệu | S |
| 3 | 1.1 | Biến rỗng `_progress_kendo_drawing` trong partial `_KendoScripts` (**không** nạp gói thương mại) | Chặn chức năng, lỗi trên 7 trang | S |
| 4 | 1.11 | "Công việc hôm nay" lọc đúng dữ liệu (sửa ở service) | Hiển thị sai dữ liệu | S |
| 5 | 1.12 | Hẹn giờ không cho lưu phiên khi chưa chạy | Dữ liệu sai | S |
| 6 | 1.9 (phần dữ liệu) | Jira Tìm kiếm: mã issue có link và không bẻ dòng; đổi tên cột "Logged" cho đúng nghĩa. Chuyển sang Tabulator để ở GĐ5 | Hiển thị sai nghĩa | S |
| 7 | 1.10 | Jira: cột số canh phải, định dạng `17,5h` (sửa trong cấu hình bảng, vẫn giữ sau khi đổi giao diện) | Sai quy ước | S |
| 8 | 1.7 | Label đè chữ ở các ô floating (sửa 2 selector CSS); người dùng mới đăng ký đang gặp lỗi này | Sai hiển thị | S |
| 9 | 1.8 | Avatar lỗi thì hiện chữ viết tắt; kiểm tra file trong `Storage:UploadsPath` | Sai hiển thị | S |
| 10 | 1.4 (phần logic) | Helper xác định trang đang mở và thêm `aria-current`. Phần logic này dùng lại được cho khung app mới; phần màu sắc làm ở GĐ3 | Điều hướng | S |
| 11 | 1.13 | Trang Access denied tối thiểu (tiếng Việt, có giải thích, có nút); lỗi khoảng trắng ở Liên kết nhanh; xóa `Index.cshtml.bak` | Sai hiển thị | S |

**Chuyển sang giai đoạn sau, không sửa ở GĐ1:**
- **1.3** (sidebar thu gọn) và **1.5** (mobile): gộp vào khung app ở GĐ3. Sửa mẹo `::first-letter` bây giờ thì GĐ3 sẽ bỏ đi. *Ngoại lệ:* nếu GĐ3 phải lùi quá 3 tuần, làm bản tạm cho 1.5 vì mobile đang gần như không dùng được.

### 10.6 Giai đoạn 2 – Chốt quyết định thiết kế (chủ dự án quyết)

Kết quả của giai đoạn này là file `docs/DESIGN_DECISIONS.md`, ghi lại từng quyết định cùng lý do:

| # | Cần quyết định | Phương án đề xuất | Lý do |
|---|---|---|---|
| 2.A | Hướng phong cách | Lấy cảm hứng từ Linear: nền trung tính, một màu nhấn, mật độ cao cho danh sách, rộng rãi cho form | Hợp với công cụ quản lý issue; chữa trực tiếp bệnh thiếu nhất quán |
| 2.B | Màu nhấn | Màu riêng của ToolboxWeb, **không** dùng tông tím đặc trưng của Linear | Tránh bị coi là nhái nhận diện thương hiệu |
| 2.C | Font | Inter (OFL), tự host | Miễn phí, hợp phong cách |
| 2.D | Bộ icon | Lucide (ISC), hoặc mở rộng Bootstrap Icons (MIT, đã có trong repo) | Giấy phép mở; Lucide gần phong cách Linear nhất |
| 2.E | Thư viện control | **Giữ:** Bootstrap (nền), Tabulator (bảng), Kendo Core (chỉ DatePicker, ComboBox, MultiSelect, AutoComplete, NumericTextBox, Notification). **Không dùng:** ColorPicker và mọi thứ cần kendo-drawing. **Để dành:** ECharts, FullCalendar, Quill — chỉ nối vào khi có màn hình cần | Đúng chính sách giấy phép; giảm phạm vi |
| 2.F | Widget thời tiết và hình nền động | Bỏ widget thời tiết; hình nền động thành tùy chọn, mặc định tắt | Không phục vụ công việc; làm giảm độ tương phản |
| 2.G | Mật độ và điểm ngắt màn hình | Dòng danh sách 36–40px; form 40px; điểm ngắt 900px và 600px | Cân bằng giữa danh sách dày và form dễ nhập |
| 2.H | Múi giờ và định dạng | Một cách duy nhất (khuyến nghị: múi giờ trình duyệt; `dd/MM/yyyy HH:mm`; số `17,5`) | Hết tình trạng lệch ngày |
| 2.I | **Bản mẫu tĩnh** | Mẫu Jira Dashboard và một trang form, dùng token dự kiến | Kiểm tra token trên giao diện thật trước khi áp cho cả app |

Sau khi các quyết định được duyệt, **điền giá trị cụ thể vào `AGENTS.md`**: tên biến token, thang cỡ chữ, danh sách component dùng chung.

### 10.7 Giai đoạn 3 – Nền tảng chung

Làm theo đúng thứ tự, vì mỗi bước dùng kết quả của bước trước:

| Thứ tự | Việc | Mã gốc | Ghi chú |
|---|---|---|---|
| 3.1 | `tokens.css`: màu (sáng/tối), khoảng cách, bo góc, cỡ chữ, bóng, chuyển động | 2.1 | Nền móng của mọi thứ phía sau |
| 3.2 | Font tự host, kiểu chữ nền | 2.2 | |
| 3.3 | Giao diện thư viện bên thứ ba theo token: Bootstrap, Kendo (viết lại `vendor-sync.css`), Tabulator | 2.1 | Không làm bước này thì ô Kendo và bảng Tabulator lạc tông |
| 3.4 | Component nền: nút, ô nhập (label/gợi ý/lỗi), tiêu đề trang, trạng thái trống, toast, modal xác nhận (đã có từ GĐ1), nhãn/icon trạng thái, dòng danh sách, chip lọc, panel trượt | 2.3, 2.4 | |
| 3.5 | **Khung app**: sidebar phân nhóm có icon, trạng thái đang chọn, thu gọn còn icon, menu trượt trên mobile, header mỏng có breadcrumb, menu tài khoản (theme/ngôn ngữ/đăng xuất), bỏ dòng chữ nhỏ và widget thời tiết, hình nền mặc định tắt | 1.3, 1.4, 1.5, 2.5, 2.6, 2.7 | **Cả app đổi giọng cùng lúc**, tránh cảnh nửa cũ nửa mới. Điểm dừng duyệt |
| 3.6 | Component Hub thành **bảng mẫu component** (chỉ hiện ở Development hoặc theo role), có cột "Giấy phép" và trạng thái: *Dùng được* / *Không dùng – phụ thuộc thư viện thương mại* / *Không có trong Kendo Core* | 3.13 | Giải quyết phần demo "dang dở" |

### 10.8 Giai đoạn 4 – Mẫu dùng chung theo nhóm màn hình

| Thứ tự | Việc | Mã gốc | Dùng cho |
|---|---|---|---|
| 4.1 | Bản địa hóa và định dạng: helper số/ngày, múi giờ theo quyết định 2.H, bảng thuật ngữ Jira, dịch `Profile.Nav.*` | 2.8 | Mọi trang |
| 4.2 | Identity: `_AuthLayout`; Đăng nhập, Đăng ký (tab), Access denied, các trang Manage được scaffold và dịch; menu tài khoản có nền đặc | 2.8, 3.5, 3.10 | Đăng nhập, tài khoản |
| 4.3 | Module Jira dùng chung: bộ chọn kỳ, cấu hình bảng mặc định, bảng màu trạng thái, khối JQL thu gọn, thanh chip lọc | 2.4 | 4 màn hình Jira |
| 4.4 | Form kết nối SQL dùng chung | 2.4 | So sánh DB, Bắt SQL |
| 4.5 | Mẫu trang danh sách | Lấy từ trang Ghi chú, mô tả ở `docs/LIST_PAGE_TEMPLATE.md`: `_ListToolbar` (số liệu, ô tìm kiếm, nút chính), `_FilterChips` (chip là liên kết, có số đếm), dòng `.tbx-list`/`.tbx-row` (checkbox ở đầu dòng, dòng mô tả phụ, nút xóa hiện khi rê chuột), form trong modal, `_EmptyState`, `Content/js/list-page.js` (phím `/` và `N`, mở lại form khi nhập sai, Kendo DatePicker gửi ngày dạng ISO). **Áp dụng cho Việc cần làm:** thanh công cụ + tìm kiếm, chip Tất cả / Cần làm / Quá hạn / Đã xong, đánh dấu xong bằng checkbox (giữ nguyên bộ lọc), nhãn "Quá hạn · 20/10/2026" và "Hạn hôm nay", form "Thêm việc" trong modal có label, thông báo lỗi tiếng Việt/Anh và Kendo DatePicker theo định dạng ngôn ngữ, trạng thái trống khi chưa có việc và khi không khớp bộ lọc, xóa có hộp xác nhận nêu tên việc. "Quá hạn" và "hôm nay" tính theo múi giờ của app. **Chưa làm:** Liên kết nhanh và Prompt (để GĐ5 theo cùng mẫu); sửa nội dung một việc (trang chưa từng có chức năng này) |

#### Giai đoạn 5 – đang làm

Cách làm từ 5.2 (chủ dự án chốt 21/10/2026): trước mỗi màn hình, liệt kê các việc định sửa thành danh sách ngắn để chủ dự án đánh dấu chọn trong hộp hỏi; chỉ làm những việc được chọn.

| # | Màn hình | Kết quả |
|---|---|---|
| 5.1 | **Jira Dashboard (thí điểm, dừng chờ chủ dự án duyệt)** | **Thẻ số liệu** (4 thẻ thay cho 6 thẻ cũ, chỉ có màu khi có điều cần báo): *Đã log so với ước tính* (103%, thanh so sánh, "Vượt ước tính 3%" màu cam); *Tiến độ subtask* (10/13, thanh tiến độ, "Kỳ trước: …"); *Giờ log trong kỳ* (19h so với số giờ cần log tính tới hôm nay = ngày làm việc × 8h, "Thiếu 5h" hoặc "Đủ giờ", tăng/giảm so với kỳ trước); *Subtask quá hạn* (số đỏ khi > 0, kèm số subtask đang mở chưa ước tính). **Biểu đồ giờ log theo ngày** bằng Apache ECharts (Năm: theo tháng), đường 8h/ngày, ngày làm việc đã qua mà chưa đủ 8h tô cam, ngày tương lai xám, câu tóm tắt "1/3 ngày làm việc đã log đủ 8h", mô tả cho trình đọc màn hình theo ngôn ngữ giao diện. **Bảng subtask** nhóm theo issue cha (dòng nhóm: mã + tên issue + số subtask + giờ đã log), bỏ cột STT, Dự án và hai cột issue trùng nên hết cuộn ngang ở 1440px; "Chưa ước tính" màu cam; "Trễ N ngày"; mũi tên sắp xếp chỉ hiện ở cột đang sắp xếp (áp cho mọi bảng Tabulator); bỏ chiều cao cố định nên không còn hai thanh cuộn; trên điện thoại bấm "+" để xem các cột bị ẩn. **Thanh công cụ:** ô tuần hiện luôn khoảng ngày ("Tuần 43 · 19/10 – 25/10", dùng chung cho Bảng việc), bỏ dòng "Dữ liệu từ … đến …". **Server:** số liệu tính ở `Services/JiraDashboardMetrics.cs`; tải thêm kỳ trước song song với kỳ hiện tại (mỗi lần tải gọi Jira 2 lần); ngày của worklog tính theo múi giờ của app. **Thư viện:** `Content/js/charts.js` là lớp vẽ biểu đồ dùng chung (màu lấy từ token, tự đổi theo sáng/tối). File ECharts trước đây thực ra là bản 5.5.1 dù ghi 5.6.0; đã thay bằng bản 5.6.0 chính thức kèm `LICENSE`, `NOTICE` và giấy phép d3. **Chưa kiểm tra:** Jira thật (chỉ dùng dữ liệu giả); ngày lễ chưa được trừ khỏi số giờ cần log |
| 5.2 | Jira – Tìm kiếm (chủ dự án chọn cả 16 việc) | **Form:** hai chế độ loại trừ nhau "Bộ lọc" / "JQL"; lưới 4 cột không còn ô lẻ (Người thực hiện, Hạn từ/đến ngày bằng Kendo DatePicker, Trạng thái, Dự án, Mã issue, Người báo cáo, nút Tìm kiếm/Đặt lại); bộ lọc Jira đã lưu chuyển vào chế độ JQL; ô JQL chữ đơn cách, nút "Xuống dòng theo điều kiện", Ctrl+Enter để tìm, chuyển sang JQL thì điền sẵn JQL vừa tìm. **Sau khi tìm:** không tải lại trang (POST `/Jira/Search`, có chống giả mạo yêu cầu); form thu thành một dòng tóm tắt "Người thực hiện: Tôi · Hạn: …" kèm nút "Sửa bộ lọc"; bộ lọc nằm trên URL và được nhớ trong trình duyệt, mở lại tab là tự tìm lại; chip Tất cả / Chưa xong / Quá hạn / Đã xong. **Bảng:** Tabulator nhóm theo issue cha (khi có), sắp xếp được, cột "Đã log" là số giờ (rê chuột xem từng ngày), "Chưa ước tính", "Trễ N ngày", trên điện thoại bấm "+" xem cột ẩn; chia trang 50 dòng khi nhiều hơn 50; báo khi Jira cắt ở 200 kết quả. **Trạng thái:** đang tìm, lỗi (có nút Thử lại), không có kết quả (có nút Sửa bộ lọc), lỗi nhập (thiếu người thực hiện, ngày bắt đầu sau ngày kết thúc). **Xuất Excel** bằng SheetJS (chỉ nạp khi bấm), xuất đúng những dòng đang lọc và đang sắp xếp. **Dùng chung với Tổng quan Jira** (`jira-common.js`): dòng nhóm theo issue cha, "Trễ N ngày", "Chưa ước tính", cột "+" trên điện thoại, bộ lọc trạng thái, định dạng JQL. **Kiểm tra bộ `tests/ui`:** phát hiện ảnh mốc trang Tìm kiếm trên desktop vẫn là bản cũ mà test vẫn qua, vì mức cho phép (2% điểm ảnh, dung sai màu mặc định) quá rộng với trang chữ nhạt trên nền trắng; đã làm mới toàn bộ ảnh mốc. Nên siết mức này ở mục 6 của GĐ6. **Chưa kiểm tra:** Jira thật; file Excel mới kiểm tra nội dung bằng script, chưa mở bằng Excel |
| 5.3 | Jira – Quản lý công việc (Kanban theo ngày; chủ dự án chọn cả 16 việc) | **Thẻ:** gọn hơn, chữ không nhỏ hơn 12px (trước đây 204 chỗ dưới 12px trên các trang, nay 0); bỏ ngày hạn lặp lại trong thẻ (cột đã cho biết ngày); việc xong lùi xuống nền xám, chữ nhạt nhưng vẫn đủ tương phản; thẻ quá hạn có vạch đỏ bên trái và "Trễ N ngày"; thẻ đã đổi hạn có viền nét đứt và ghi "19/10 → 21/10"; nút lịch trên thẻ mở hộp "Đổi hạn" (Kendo DatePicker, nút "Áp dụng"). **Cột ngày:** đầu cột ghi "Hôm nay 22/10" thay cho ★; thanh tải "6h / 8h" có chú thích màu (xanh lá = đã log của việc xong, màu nhấn = ước tính còn lại), vượt 8h thì chữ đỏ "Vượt Xh"; Thứ 7, CN hẹp lại khi không có việc. **Lọc:** chip Tất cả / Chưa xong / Quá hạn (có số đếm), "Ẩn việc đã xong" và "Ẩn cuối tuần" (nhớ trong trình duyệt); tổng tuần/tháng trên thanh công cụ: Đã log, Còn ước tính, Công suất (ngày làm việc × 8h). **Tháng:** ô lịch gọn (số ngày, tổng giờ, đỏ khi vượt 8h, tối đa 3 mã issue, "+N việc"), bấm mở bảng bên với đủ thẻ của ngày. **Trạng thái:** khung xương khi đang tải, lỗi có nút Thử lại, trống. **Lưu:** thanh nổi "N thay đổi hạn chưa ghi lên Jira" với "Bỏ các thay đổi" (trả thẻ về chỗ cũ, không tải lại trang) và "Ghi lên Jira"; hộp xác nhận liệt kê từng issue "hạn cũ → hạn mới", ghi thật lên Jira (`POST /Jira/UpdateDueDates`, chống giả mạo yêu cầu, tối đa 100 thay đổi, `IJiraDueDateService`), báo kết quả từng dòng; thẻ lỗi giữ trạng thái chưa ghi và hiện lý do; mỗi lần ghi thành công có một dòng nhật ký hoạt động. Đây là mục 1 của GĐ6, kéo lên làm ở 5.3. **Điện thoại:** dải 7 ngày, xem từng ngày; lịch tháng chỉ hiện số giờ và số việc. **Bàn phím:** chọn thẻ, Space để nhấc, ← → đổi ngày (↑ ↓ đổi tuần ở chế độ Tháng), Enter để thả, Esc để hủy; trình đọc màn hình được báo từng bước. **Chưa làm:** tab Sắp xếp tự động vẫn chỉ có "Bỏ các thay đổi" (tải lại trang), chưa có nút Ghi lên Jira (để 5.4). **Chưa kiểm tra:** Jira thật (chỉ kiểm tra với dữ liệu giả, gồm cả trường hợp Jira từ chối một issue) |
| 5.4 | Jira – Sắp xếp tự động (chủ dự án chọn cả 16 việc) | **Luồng 3 bước** (khối `.tbx-steps` mới trong `components.css`), mỗi bước một nút chính: 1 *Chọn subtask cần xếp lịch* (bộ lọc dạng lưới đều, ô ngày bằng Kendo DatePicker, nút "Tìm subtask"), 2 *Xếp lịch* (nút "Xếp lịch…" kèm dòng tóm tắt cấu hình: từ ngày nào, mấy giờ mỗi ngày, bỏ cuối tuần, tính việc đã có hạn), 3 *Lịch đề xuất* (chỉ hiện khi đã có đề xuất, thay cho bảng trống bên phải). **Bước 1:** tiêu đề "Subtask cần xếp lịch (N)"; bảng có cột Ước tính / Đã log / Còn lại, "Trễ N ngày", chỉ nhóm theo issue cha khi một issue cha có từ 2 subtask; cảnh báo "N subtask chưa có ước tính" kèm link mở từng issue trên Jira; trạng thái đang tải, lỗi (Thử lại), trống (Sửa bộ lọc); bộ lọc được nhớ cho lần sau; ô trạng thái đổi chữ thành "Tất cả việc chưa xong" (hết bị cắt). **Hộp cấu hình gọn:** ngày bắt đầu (mặc định Thứ Hai tới), số giờ mỗi ngày (Kendo NumericTextBox, 0,5–24), bỏ Thứ 7/CN, *tính cả giờ của việc đã có hạn trong ngày* (đọc các subtask của bạn đã có hạn trong 120 ngày tới qua `GET /Jira/ScheduledSubtasks`, đọc lỗi thì báo và xếp như ngày trống), "Xếp trước" (Kendo MultiSelect), thứ tự tiêu chí có nút lên/xuống; mọi lựa chọn trừ ngày bắt đầu và "Xếp trước" được nhớ. Bỏ chế độ "theo %" (thay bằng số giờ) và lựa chọn "chỉ subtask chưa đổi hạn trong phiên" (giờ chỉ có một chỗ sửa ngày). Việc xếp dùng giờ *còn lại* (ước tính trừ đã log). **Bước 3:** xem dạng Bảng (nhóm theo ngày, mỗi nhóm ghi "N subtask · 6h / 8h", đỏ khi vượt) hoặc Lịch (cột theo ngày như tab Quản lý, thanh tải gồm việc đã có hạn + việc vừa xếp, vượt thì đỏ, kéo thẻ sang ngày khác); cột "Hạn cũ → Hạn mới", dòng đổi hạn có vạch màu nhấn, dòng giữ nguyên ghi "giữ nguyên"; nút lịch trên từng dòng/thẻ mở hộp đổi ngày đề xuất; "Bỏ đề xuất" và "Ghi lên Jira" dùng chung hộp xác nhận và cách báo kết quả từng dòng với 5.3; dòng lỗi giữ lại kèm lý do. Trên điện thoại mặc định xem dạng Lịch (các ngày xếp dọc). **Sửa thêm:** thanh "N thay đổi chưa ghi" trên điện thoại trải hết bề ngang và trang chừa chỗ để thanh không che nút cuối; ô Kendo NumericTextBox không còn cao gấp đôi ô khác (`vendor-sync.css`); thanh tải của tab Quản lý giờ tính ước tính *còn lại* cho khớp chú thích (`ToolboxJira.loadHours`, `remainingHours`); lọc dự án trong JQL thoát dấu ngoặc kép. Xóa 25 khóa ngôn ngữ cũ. **Chưa kiểm tra:** Jira thật (chỉ dữ liệu giả; dữ liệu giả không lọc theo mã/tên subtask) |
| 5.5 | Tổng quan (chủ dự án chọn cả 16 việc) | **Đầu trang:** lời chào theo buổi kèm tên và ngày hôm nay (không lặp tiêu đề trang), nút "Tùy chỉnh" để ẩn/hiện từng panel (nhớ trên trình duyệt, `toolbox.dashboard.hidden`). **Hàng số liệu**, mỗi ô là link: việc quá hạn (đỏ khi có), hạn hôm nay kèm số việc đang mở, phút tập trung hôm nay và tuần này, và khi đang có phiên Jira là *giờ log Jira hôm nay* so với 8h (thanh tiến độ; tải riêng qua `GET /Jira/TodayHours` nên Jira chậm không làm chậm trang; ngày nghỉ ghi "ngày nghỉ"). **Panel giữ chiều cao tự nhiên** (`align-items: start`), lưới 12 cột: Việc cần làm 7 + Ghi chú 5, rồi Liên kết / Prompt / Tập trung, rồi Lịch sử. **Việc cần làm:** ô thêm việc nhanh (tên + hạn Hôm nay/Không có hạn), việc chia nhóm *Quá hạn / Hôm nay / Sắp tới / Chưa có hạn* (sửa lỗi 1.11: việc không hạn không còn nằm dưới "hôm nay"), mỗi nhóm tối đa 5 việc kèm "+N việc nữa", ô tích để đánh dấu xong ngay tại Tổng quan. **Ghi chú:** 5 ghi chú, ghim lên đầu, ghi "Sửa …" theo giờ ứng dụng, bấm mở đúng ghi chú; nút "Ghi chú mới" mở form trong popup (ghi chú văn bản; thiếu tiêu đề thì mở lại popup kèm lỗi). **Liên kết nhanh** dạng ô: chữ cái đầu + tên + tên miền (không tải favicon từ máy chủ ngoài). **Prompt:** không còn lặp "Báo lỗi / Báo lỗi" (dòng phụ là mô tả, hoặc loại khi khác tên), nút Sao chép một chạm. **Tập trung:** phút hôm nay/tuần này, nút "Bắt đầu 25 phút" mở trang Hẹn giờ và tự chạy (`?minutes=25&start=true`). **Lịch sử:** 5 dòng dạng dòng thời gian, "Xem tất cả" mở trang mới **Lịch sử hoạt động** (`/activity.html`: lọc theo loại, 30 dòng mỗi trang, trạng thái trống; trong menu đánh dấu thuộc Tổng quan). Mọi panel có trạng thái trống kèm nút. Phần dữ liệu chuyển vào service (`DashboardService`, `IActivityLogService.GetRecentAsync/GetPageAsync`, `IFocusSessionService.GetMinutesSinceAsync`). `tests/ui` thêm trang `activity`. **Chưa kiểm tra:** giờ log Jira với Jira thật (chỉ dữ liệu giả); lựa chọn ẩn panel chỉ nhớ trên từng trình duyệt |
| 5.6 | Việc cần làm (chủ dự án chọn cả 16 việc) | **Danh sách:** việc chia nhóm *Quá hạn / Hôm nay / Sắp tới / Chưa có hạn / Đã xong*; chip mới "Hôm nay"; ô "Sắp xếp" (theo hạn, mới tạo trước, ưu tiên cao trước) tự áp dụng và được nhớ (cookie); mô tả chỉ hiện một dòng, bấm để mở hết. **Thao tác:** sửa việc trong popup (dùng chung popup với "Thêm việc"); menu đổi hạn nhanh trên từng dòng (Hôm nay / Ngày mai / Thứ Hai tuần sau / Bỏ hạn / Chọn ngày…); sau khi đánh dấu xong hoặc xóa có thanh "Hoàn tác" (xóa là xóa mềm, giữ một ngày rồi mới xóa hẳn: cột `DeletedAt`); nút "Dọn việc đã xong" có xác nhận và hoàn tác được. **Thông tin:** "Trễ N ngày" thay cho ngày quá hạn (rê chuột thấy ngày); việc xong ghi "Xong dd/MM/yyyy"; "Tuần này: xong X/Y" kèm thanh tiến độ (xong trong tuần so với việc hạn đến hết tuần, kể cả quá hạn); **mức ưu tiên** Cao / Thường / Thấp (cột mới `Priority`, việc cũ thành "Thường"), nhãn "Ưu tiên cao" / "Ưu tiên thấp" trên dòng. **Bàn phím và điện thoại:** dòng nhận focus; ↑ ↓ (hoặc J K) đi giữa các dòng, X xong, E hoặc Enter sửa, Delete xóa (có xác nhận), Space chọn; ô "Thêm nhanh" ngay trên danh sách (tên + hạn Hôm nay/Không có hạn, Enter để thêm); chế độ "Chọn nhiều" với thanh nổi "Đã chọn N việc · Đánh dấu xong · Xóa"; trên điện thoại nút thao tác luôn hiện, ô tích to hơn, ngày nằm dưới tiêu đề. **Phía server:** `ChecklistService` thêm Update, SetDue, Complete (nhiều việc), Delete mềm, ClearDone, Undo; migration `AddChecklistPriorityAndSoftDelete` (thêm 2 cột, không đổi dữ liệu cũ ngoài mức ưu tiên mặc định). Ô "Đánh dấu xong" ở Tổng quan dùng `CompleteAsync` (bấm hai lần không mở lại việc). **Chưa kiểm tra:** migration trên CSDL thật của chủ dự án (chỉ chạy trên CSDL thử); tên phím "Delete", "Space" trong dòng phím tắt giữ nguyên chữ in trên bàn phím |
| 5.7 | Ghi chú (chủ dự án chọn cả 16 việc) | **Token:** 9 màu định dạng và 2 màu trạng thái checklist chuyển vào `tokens.css` (`--format-*`, `--state-review`, `--state-high`, có giá trị tối), `notes.css` không còn mã màu viết thẳng; mọi cỡ chữ theo `--text-*` (hết chữ 11px), độ đậm theo `--weight-*` (bỏ 700), bán kính và chuyển động theo token, focus dùng `--focus-ring`; bỏ các biến `--nv-1…8`, `--nv-radius`, `--nv-mono`, `--nv-ok`, `--nv-danger` trùng token (còn lại `--nv-fmt`, `--nv-row` là màu theo từng phần tử). **Thành phần chung:** nút riêng `.nv-btn` thay bằng `.btn` / `.tbx-icon-button`; bộ icon vẽ tay `_NoteIcons` thay bằng sprite Lucide (thêm pin, pin-off, arrow-left, plug, lock, wand-sparkles); chip định dạng dùng `.tbx-chip` (giữ chấm màu định dạng); popup xóa riêng `_NoteDeleteModal` thay bằng hộp xác nhận chung (`data-confirm`), vẫn bỏ qua cảnh báo "chưa lưu" khi đã xác nhận xóa; trạng thái trống dùng `_EmptyState` / `.tbx-empty`; nhãn và ô nhập dùng `.tbx-field`. **Danh sách:** dòng số liệu "N ghi chú · M đã ghim" (bỏ "N định dạng" khó hiểu), chữ màu thường và số đậm; nút ghim ngay trên từng dòng (hiện khi rê chuột, luôn hiện với ghi chú đã ghim; ghim từ dòng không đổi ghi chú đang mở); ô "Sắp xếp" (Mới sửa trước / Tên A–Z / Mới tạo trước), tự áp dụng, nhớ bằng cookie, ghi chú ghim luôn ở đầu; ↑ ↓ đi giữa các ghi chú, Enter mở; dòng phím tắt nằm dưới danh sách. **Chưa kiểm tra:** ảnh mốc `tests/ui` của trang Ghi chú chỉ có trạng thái trống (tài khoản thử không có ghi chú); danh sách có dữ liệu kiểm tra bằng script riêng |
| 5.8 | So sánh DB (chủ dự án chọn cả 16 việc) | **Token và thành phần chung:** `schema-compare.css` viết lại theo token (hết `#fff`/`rgba`, hết cỡ chữ rem viết thẳng, bỏ toàn bộ biến `--sc-*`), nút riêng `.sc-btn` thay bằng `.btn`, ba bước dùng `.tbx-steps` (bước 3 "So sánh" chứa thanh chạy và kết quả), chip phạm vi, loại đối tượng và bộ lọc thay đổi dùng `.tbx-chip` (`aria-pressed`), icon Lucide (thêm arrow-left-right, chevron-up). **Luồng:** nút So sánh khóa tới khi cả hai kết nối đã kiểm tra thành công và đã chọn database, dòng bên dưới nói còn thiếu gì; sửa máy chủ, tài khoản hoặc mật khẩu sau khi kiểm tra thì khóa lại; Nguồn trùng Đích (cùng máy chủ và database) thì cảnh báo và khóa; nút "Đổi chiều" hoán đổi hai kết nối (kể cả danh sách database, trạng thái kiểm tra, mã ánh xạ). **Form kết nối dùng chung:** "Tin cậy chứng chỉ của máy chủ" mặc định tắt (cả phía server), bật thì hiện cảnh báo; lỗi chứng chỉ báo đúng nguyên nhân thay vì "không tìm thấy máy chủ"; menu "Gần đây" (tối đa 6 kết nối đã kiểm tra, không lưu mật khẩu, xóa từng dòng), bật cho cả trang Bắt SQL. **Kết quả:** trạng thái trống/đang tải/lỗi theo chuẩn (chưa chạy, hai DB giống nhau, lọc không ra, chi tiết, script, lỗi so sánh); trạng thái tiến trình dịch sang tiếng Việt; J / K đi giữa các đối tượng theo thứ tự đang hiện. **Script:** hiện dạng mã có số dòng, lệnh xóa (DROP và ALTER TABLE … DROP COLUMN ngoài thân procedure) tô đỏ, đếm số lệnh, nút "Đến lệnh xóa kế"; cảnh báo từ server trả về có cấu trúc và hiển thị theo ngôn ngữ giao diện (server thêm nhận diện DROP COLUMN); tải về hoặc sao chép script có lệnh xóa phải xác nhận trong hộp thoại liệt kê các lệnh. **Tiện ích:** nhớ phạm vi, giảm nhiễu, mã ánh xạ và trạng thái mở "Tuỳ chọn nâng cao"; nút "Đặt lại mặc định". **Chưa kiểm tra:** không có SQL Server trong môi trường thử nên mọi lời gọi SQL được giả lập trong Playwright; chưa chạy với DacFx thật; người dùng cũ có "Tin cậy chứng chỉ" đã lưu trong trình duyệt vẫn giữ giá trị đó |
| 5.9 | Bắt SQL (chủ dự án chọn cả 16 việc) | **Token và thành phần chung:** `sql-profiler.css` viết lại theo token (hết `rgba`/`#hex`, px và cỡ chữ viết thẳng), pill loại dùng `.tbx-pill`, nhãn ô nhập `.tbx-field`, trang thành 3 bước `.tbx-steps` (1 Kết nối · 2 Cách xem: Ghi phiên / Xem nhanh · 3 Kết quả); trạng thái trống/đang tải/lỗi cho bảng ghi, bảng xem nhanh, định nghĩa và lỗi đọc dữ liệu khi đang ghi (trước đây lỗi này bị bỏ qua). **Luồng:** "Bắt đầu ghi" khóa tới khi kết nối đã kiểm tra và đủ quyền, dòng bên dưới nói lý do; sửa kết nối sau khi kiểm tra thì khóa lại; bấm ghi mở hộp xác nhận nêu máy chủ, database, số phút tự dừng (vì ghi tạo phiên Extended Events trên máy chủ); bộ lọc người dùng/máy trạm/ứng dụng/thời gian tối thiểu và các loại cần ghi gom vào "Bộ lọc nâng cao" (có số bộ lọc đang bật), chỉ để ngoài ngưỡng cam/đỏ và thời gian tự dừng; đếm ngược "Tự dừng sau mm:ss". **Kết quả:** mới nhất ở trên; chip Tất cả / Chậm / Lỗi có số đếm; ô tìm theo đối tượng, người dùng, máy trạm, ứng dụng; dòng số liệu tính trên danh sách đang hiện; chế độ "Gộp theo đối tượng" (số lần gọi, trung bình, chậm nhất, tổng, lỗi; bấm một dòng để xem các lần gọi của đối tượng đó); chi tiết là panel bên phải (`.tbx-sidepanel`, không che danh sách, toàn màn hình trên điện thoại). **Tiện ích:** nhớ ngưỡng, bộ lọc, số phút xem nhanh và trạng thái mở bộ lọc; "Đặt lại mặc định"; J / K đi giữa các dòng và cập nhật panel; "Xóa danh sách" (chỉ ẩn các dòng đang có, file xuất vẫn đủ); "Tạm dừng cập nhật", và tự giữ dòng mới khi đang mở chi tiết, kèm thanh "N dòng mới · Hiện ngay"; menu "Gần đây" của form kết nối. Sửa thêm: màu dòng chọn ở So sánh DB bị quy tắc zebra của `vendor-sync.css` đè. **Chưa kiểm tra:** không có SQL Server nên mọi lời gọi được giả lập trong Playwright; chưa chạy phiên Extended Events thật |
| 4.5 | **Mẫu trang danh sách** (toolbar, tìm kiếm, chip lọc, danh sách, form trong modal, trạng thái trống), lấy từ trang Ghi chú | 2.4 | Việc cần làm, Liên kết nhanh, Prompt |

### 10.9 Giai đoạn 5 – Từng màn hình

Thứ tự tính theo **tần suất dùng × giá trị nghiệp vụ × mức hỏng hiện tại**. Tần suất là **ước đoán** từ vai trò của từng trang; chủ dự án nên điều chỉnh theo thực tế sử dụng.

| Thứ tự | Màn hình | Tần suất (ước) | Giá trị | Mức hỏng | Mã gốc | Công sức |
|---|---|---|---|---|---|---|
| 5.1 | **Jira Dashboard** – trang thí điểm, điểm dừng duyệt | Hằng ngày | Cao | Trung bình | 3.1 (mục 8) | M–L |
| 5.2 | Jira – Tìm kiếm (chuyển sang Tabulator, chế độ chip lọc / JQL) | Hằng ngày | Cao | Cao | 3.2 | M |
| 5.3 | Jira – Kanban | Hằng ngày | Cao | Trung bình | 3.3 | M |
| 5.4 | Jira – Sắp xếp tự động (dạng các bước, cột "Hạn cũ → Hạn mới") | Hằng tuần | Cao | Cao | 3.4 | M |
| 5.5 | Tổng quan (trang đầu của tài khoản thường) | Hằng ngày | Trung bình | Cao | 3.7 | M |
| 5.6 | Việc cần làm | Hằng ngày | Trung bình | Cao | 3.6 | M |
| 5.7 | Ghi chú (chỉ áp token, vì đã là mẫu) | Hằng ngày | Trung bình | Thấp | — | S |
| 5.8 | So sánh DB | Theo đợt | Cao | Thấp | 3.9 | S–M |
| 5.9 | Bắt SQL | Theo đợt | Cao | Trung bình | 3.9 | M |
| 5.10 | Không gian prompt (kèm tính năng: sao chép đầy đủ, điền biến, tìm kiếm, khôi phục prompt mẫu) | Hằng tuần | Trung bình | Cao | 3.8 | M |
| 5.11 | Liên kết nhanh | Hằng tuần | Thấp | Cao | 3.11 | S |
| 5.12 | Hẹn giờ tập trung | Thỉnh thoảng | Thấp | Trung bình | 3.12 | S–M |
| 5.13 | Kendo Demo, Tabulator Demo (trang Dev) | Hiếm | Thấp | — | 3.13 | S |

### 10.10 Giai đoạn 6 – Tính năng mới và tối ưu

Chủ dự án chọn thứ tự. Đề xuất theo giá trị:

1. **Ghi hạn mới lên Jira thật** (phần còn lại của 1.2): modal xác nhận "hạn cũ → hạn mới", báo kết quả từng dòng, ghi activity log — **L**. *Đã làm ở 5.3 (tab Quản lý công việc) và 5.4 (tab Sắp xếp tự động), 22/10/2026.*
2. **Ctrl/⌘+K và phím tắt** — M.
3. **Biểu đồ giờ log theo ngày** bằng ECharts (Apache-2.0); nhắc khi chưa đủ 8h — M.
4. **Hiệu năng**: chỉ nạp Kendo/Tabulator ở trang cần; cache dài cho `/vendor`; cân nhắc thay widget Kendo bằng flatpickr (MIT) và Tom Select (Apache-2.0) nếu muốn bỏ hẳn Kendo — M.
5. **Dọn mã**: tách CSS theo module, tách `Components/Index.cshtml` thành partial, thêm stylelint — M.
6. **Chạy script kiểm tra (0.6) tự động trên mỗi PR** — M.

### 10.11 Những việc quyết định không làm

| Việc | Lý do |
|---|---|
| Nạp `@progress/kendo-drawing` | Phần mềm thương mại |
| Dùng ColorPicker của Kendo | Phụ thuộc kendo-drawing; dùng `<input type="color">` thay thế |
| Sửa mẹo `::first-letter` của sidebar | Sidebar được làm lại ở GĐ3 |
| Giữ widget thời tiết trên topbar | Không phục vụ công việc; hỏi quyền vị trí không cần thiết |
| Sao chép icon, logo, màu hay CSS của Linear | Rủi ro bản quyền và nhận diện thương hiệu; chỉ học nguyên tắc |

### 10.12 Rủi ro và cách giảm

| Rủi ro | Cách giảm |
|---|---|
| Giai đoạn chuyển tiếp có trang cũ lẫn trang mới | Đổi khung app và token cho cả app cùng lúc (GĐ3), để trang cũ ít nhất cũng cùng màu, chữ và khung |
| Ghi đè giao diện Kendo/Tabulator dễ vỡ khi nâng phiên bản | Bảng mẫu component (3.6) và ảnh mốc (0.6) để phát hiện ngay |
| Không kiểm tra được trang Jira nếu không có tài khoản thật | Dữ liệu Jira giả ở Development (0.7); chủ dự án kiểm tra lần cuối trên Jira thật |
| Tài khoản cũ không nhận prompt mẫu mới | Nút "Khôi phục prompt mẫu" (5.10), hoặc chấp nhận và ghi rõ trong tài liệu |
| AI làm lệch chuẩn ở các bản sửa sau | Chuẩn trong `AGENTS.md` (GĐ0) cộng script kiểm tra trong định nghĩa "hoàn thành" |
| Phạm vi phình to | Mỗi việc một PR; GĐ6 chỉ bắt đầu khi GĐ5 xong |

### 10.13 Bảng chuyển đổi từ kế hoạch cũ (mục 8)

| Mã cũ | Vị trí mới | Thay đổi |
|---|---|---|
| 1.1 | GĐ1 #3 | Chỉ dùng biến rỗng, không nạp gói thương mại |
| 1.2 | GĐ1 #1 + GĐ6 #1 | Tách: đổi tên ngay, ghi thật lên Jira để sau |
| 1.3 | GĐ3 (3.5) | Không sửa riêng, gộp vào khung app |
| 1.4 | GĐ1 #10 (logic) + GĐ3 (giao diện) | Tách |
| 1.5 | GĐ3 (3.5) | Gộp vào khung app; có ngoại lệ nếu GĐ3 bị lùi |
| 1.6–1.12 | GĐ1 | Giữ, sắp lại theo mức nghiêm trọng |
| 1.13 | GĐ1 #11 | Giữ |
| 2.1–2.7 | GĐ2 (quyết định) + GĐ3 (xây) | Thêm bước chốt quyết định trước khi xây |
| 2.8 | GĐ4 (4.1, 4.2) | Giữ |
| 2.9 | **GĐ0** | **Đưa lên đầu tiên**; riêng tính năng trang Prompt chuyển sang 5.10 |
| 3.1–3.13 | GĐ4 + GĐ5 | Phần dùng chung tách sang GĐ4; Đăng nhập/Đăng ký và tài khoản chuyển vào 4.2 |
| 4.1–4.3, 4.5 | GĐ6 | Giữ |
| 4.4 | **GĐ0 (0.6)** + GĐ6 #6 | **Đưa lên đầu** để có ảnh mốc; chạy tự động trên PR để sau |

### 10.14 Trạng thái thực hiện

#### Giai đoạn 0 – đã làm xong (20/10/2026), chờ chủ dự án duyệt

| # | Việc | Kết quả |
|---|---|---|
| 0.1 | Chính sách giấy phép | Mục "Third-Party License Policy" trong `AGENTS.md`: 3 mức (được phép / cần duyệt / cấm), bắt buộc đọc `LICENSE` của cả gói phụ thuộc |
| 0.2 | `THIRD-PARTY-NOTICES.md` | Liệt kê 13 tài nguyên giao diện, 8 gói NuGet trực tiếp; đã kiểm tra **138 gói NuGet** trong cây phụ thuộc |
| 0.3 | Quy chuẩn UI | Mục "UI Standard" trong `AGENTS.md`. Đường dẫn CodeGraph `D:\...` được **giữ lại** nhưng ghi rõ là riêng máy của chủ dự án, vì kỹ năng CodeGraph trên máy đó vẫn dùng |
| 0.4 | Định nghĩa "hoàn thành" | Mục "Definition Of Done" trong `AGENTS.md` |
| 0.5 | Đồng bộ prompt | `docs/FEATURE_PROMPTS.md` và `docs/MAINTENANCE_PROMPTS.md` là nguồn chính, dùng biến `{{ten_bien}}`, có đoạn ngữ cảnh và định dạng đầu ra; 6 prompt tạo sẵn (vi/en) đã khớp; thêm 2 prompt chỉ có trong tài liệu (đánh giá UX, kiểm tra giấy phép). Tài khoản cũ **không** tự nhận bản mới |
| 0.6 | Script kiểm tra và ảnh mốc | `tests/ui/`: 22 trang × 3 kiểu màn hình = 66 lượt; cơ chế "chỉ được giảm"; ảnh mốc đã tạo, chạy lại 2 lần đều ổn định |
| 0.7 | Dữ liệu Jira giả | `Infrastructure/Jira/FakeJiraClient.cs`, bật bằng `Jira__UseFakeData=true`, **chỉ có tác dụng ở Development**; đã thử đăng nhập Jira và chụp đủ 4 màn hình Jira |

Ghi chú khác:
- `docs/kendo-self-host.md` đã ghi cách dùng biến rỗng thay cho `@progress/kendo-drawing`.
- `docs/ARCHITECTURE.md` đã bỏ ý "thay bằng Aspose sau này", vì Aspose là phần mềm thương mại.

#### Phát hiện mới trong Giai đoạn 0

1. **Giấy phép NuGet: chủ dự án cần quyết** (chi tiết trong `THIRD-PARTY-NOTICES.md`, mục "Open decisions"):
   - `Microsoft.SqlServer.DacFx` (trang So sánh DB) dùng *Microsoft Software License Terms*: miễn phí, nhưng không phải giấy phép mã nguồn mở.
   - `Microsoft.SqlServer.Types` 170.1000.7 (DacFx bắt buộc kéo theo) đi kèm file giấy phép **pre-release**: *"You may not test the software in a live operating environment…"*.
   - `Microsoft.Data.SqlClient.SNI.runtime` dùng giấy phép riêng của Microsoft, và là thành phần bắt buộc để kết nối SQL Server trên Windows.
2. **Jira Dashboard tràn ngang ở 1440px** (688px; trên mobile 1.474px). Thanh công cụ và hàng thẻ số liệu không co lại theo khung. Ảnh chụp của chủ dự án ở 1712px không thấy lỗi này vì màn hình đủ rộng. **Bổ sung vào GĐ1** (mức "chặn chức năng" trên màn hình laptop phổ biến).
3. **Kendo Demo tràn ngang trên mobile** (210px). Để vào GĐ5 (trang Dev).
4. **Ảnh mốc (số lỗi hiện có, theo trang):**
   - Lỗi console `parseColor`: 8 trang. Hết ở GĐ1 (việc 1.1).
   - Ô nhập không có label: Prompt 6, Liên kết nhanh 4, Công việc 3, Hồ sơ 2, Jira Sắp xếp 5, Jira Dashboard 1, Kanban 1.
   - Chữ nhỏ hơn 12px: mọi trang có ít nhất 1 (ngày trên topbar 10,88px); Kanban Jira có **69**.

#### Quyết định của chủ dự án sau Giai đoạn 0 (21/10/2026)

- Duyệt `AGENTS.md`.
- **Chấp nhận** cả ba thành phần Microsoft (DacFx và chuỗi phụ thuộc, `Microsoft.SqlServer.Types`, `Microsoft.Data.SqlClient.SNI.runtime`). Đã ghi vào `THIRD-PARTY-NOTICES.md`.
- Đồng ý đưa lỗi tràn ngang của Jira Dashboard vào Giai đoạn 1.

#### Giai đoạn 1 – đã làm xong (21/10/2026)

| # | Việc | Kết quả |
|---|---|---|
| 1.6 | Xác nhận khi xóa | `_ConfirmModal` dùng chung, kèm `Toolbox.confirm()` và thuộc tính `data-confirm`. Đã gắn cho Công việc, Liên kết nhanh, Prompt. Nút "Xóa dòng chọn" của Tabulator bị vô hiệu khi chưa chọn dòng. *Làm trước 1.2 vì 1.2 dùng lại modal này.* |
| 1.2 | Nút "Lưu" của Jira không lưu | Đổi tên thành "Áp dụng vào lưới". Thêm thanh cảnh báo "N thay đổi chưa lưu lên Jira" và nút "Bỏ các thay đổi". Hỏi lại trước khi chuyển tab, tìm lại, về "Hôm nay"; trình duyệt cảnh báo khi rời trang. Thẻ đã kéo có viền vàng nét đứt và dòng "Hạn cũ". Thông báo dùng kiểu "thông tin" thay vì "thành công" |
| 1.1 | Lỗi nạp Kendo | `_KendoScripts.cshtml` là nơi duy nhất nạp Kendo (có biến rỗng thay cho gói thương mại; mỗi request chỉ render một lần). **Hết lỗi console trên mọi trang.** Catalog trong Component Hub đã sửa cho đúng |
| 1.11 | "Công việc hôm nay" sai dữ liệu | Đổi thành "Việc đang mở", xếp theo thứ tự quá hạn → hôm nay → sắp tới → chưa có hạn, có nhãn "Quá hạn · ngày" |
| 1.12 | Hẹn giờ lưu phiên giả | Nút lưu chỉ bật khi bộ đếm chạy hết |
| 1.9 | Bảng Tìm kiếm Jira | Mã issue có link và không bẻ dòng, trạng thái có nhãn màu, cột ngày log đổi đúng tên, ẩn cột trống. Nhận diện trạng thái chuyển sang `Services/JiraStatus.cs`; "In Developing" được nhận là "đang làm" |
| 1.10 | Số trong bảng Jira | `18,5` thay cho `18.50`; cột Ước tính và Log canh phải |
| *mới* | Tràn ngang và bị cắt ở Jira | Jira Dashboard không còn tràn ngang ở 1440px và trên mobile. **Tìm thêm được và đã sửa:** 7 nút trên thanh công cụ tab kanban (có cả Tìm kiếm, Hôm nay) và 6–9 nút ở tab Sắp xếp bị cắt khuất ở 1440px. Cùng nguyên nhân: grid không khai báo cột |
| *mới* | Ô chọn tuần nhân bản nút × | Mỗi lần quay lại tab kanban lại thêm một nút × (lỗi có từ trước). Đã sửa |
| 1.7 | Label đè chữ | Đăng ký và Manage/Email hiển thị đúng |
| 1.8 | Avatar vỡ | Server không trả ảnh khi file đã mất; trình duyệt tự gỡ ảnh lỗi và hiện chữ viết tắt |
| 1.4 | Menu đang chọn | `active` và `aria-current="page"` theo controller/action. Màu sắc chính thức làm ở GĐ3 |
| 1.13 | Lỗi nhỏ | Trang Access denied tiếng Việt, có nút "Đăng nhập bằng Jira" (đăng xuất, mở sẵn tab Jira, đăng nhập xong quay về đúng trang) và "Về Tổng quan". Form Liên kết nhanh có đủ label. Sửa lỗi "AI· https". Xóa file `.bak` |

**Chưa làm trong Giai đoạn 1, theo đúng kế hoạch:** 1.3 (sidebar thu gọn) và 1.5 (menu trượt trên mobile), gộp vào khung app ở GĐ3.

**Chưa kiểm tra:** các màn hình Jira trên Jira thật (mới chỉ kiểm tra với dữ liệu giả); hành vi trên Windows/IIS Express.

**Còn tồn tại, đã ghi nhận cho giai đoạn sau:**
- Ở 1440px, bảng Jira Dashboard phải cuộn ngang mới thấy các cột Trạng thái, Hạn, Ước tính, Log. Gốc rễ là hai cột tóm tắt trùng nhau; xử lý ở 5.1 (nhóm theo issue cha).
- Kendo Demo tràn ngang trên mobile; xử lý ở 5.13.
- Lỗi chập chờn: trang Tabulator Demo trên mobile **một lần** báo lỗi console `Cannot read properties of null (reading 'verticalFillMode')` (lỗi nội bộ của Tabulator) trong một lượt chạy bộ kiểm tra đầy đủ. Chạy lại hơn 20 lần, cả trên code trước và sau thay đổi, đều không tái hiện được. Có thể là lỗi tranh chấp thời điểm của Tabulator khi khung nhìn đổi kích thước lúc chụp ảnh toàn trang. Cần theo dõi; nếu lặp lại thì điều tra ở 5.13.

**Kết quả bộ kiểm tra `tests/ui` (66 lượt), trước và sau Giai đoạn 1:**

| Chỉ số | Trước | Sau |
|---|---|---|
| Lỗi console | 24 | **0** |
| Tràn ngang (tổng px) | 3.060 | **167** (chỉ còn Kendo Demo trên mobile) |
| Ô nhập thiếu label | 66 | 54 |
| Chữ nhỏ hơn 12px | 276 | 276 (xử lý bằng thang cỡ chữ ở GĐ3) |

Ảnh mốc đã được tạo lại theo giao diện sau Giai đoạn 1.

#### Giai đoạn 3 (3.1–3.5) – đã làm xong (21/10/2026), **dừng chờ chủ dự án duyệt khung app**

| # | Việc | Kết quả |
|---|---|---|
| 3.1 | `tokens.css` | Toàn bộ token đã duyệt (màu sáng/tối, cỡ chữ, khoảng cách, bo góc, kích thước) nằm trong `Content/css/tokens.css`, nạp đầu tiên. Biến cũ (`--ui-*`, `--jira-*`, `--nv-*`, `--sc-*`) trỏ về token. Lần đầu vào app theo chế độ sáng/tối của máy; sau đó nhớ lựa chọn |
| 3.2 | Font Inter | Tự host từ `@fontsource/inter` 5.3.0 (OFL 1.1): latin, latin-ext, vietnamese; 400/500/600/700. Có `LICENSE` và dòng trong `THIRD-PARTY-NOTICES.md` |
| 3.3 | Giao diện thư viện | Bootstrap (`--bs-*`, nút, tab, dropdown, modal, alert, bảng), Kendo (`--kendo-color-*`, nút, lịch, thông báo) và Tabulator (đầu bảng, dòng 36px, bỏ sọc ngựa vằn) đọc cùng token trong `vendor-sync.css`. **Một kiểu nút chính** trên toàn app, bỏ gradient. Thông báo (toast) thành thẻ nền đặc có vạch màu trạng thái |
| 3.4 | Component nền | `components.css`: panel, toolbar + ô tìm kiếm, dòng danh sách, nhãn trạng thái, chip, ô form có chỗ dành sẵn cho lỗi, side panel, trạng thái trống / lỗi / đang tải. Icon Lucide 1.60.0 (ISC) dạng sprite 43 icon, dùng bằng thẻ `<tbx-icon>`. `_EmptyState` dùng chung |
| 3.5 | Khung app | Sidebar 232px chia 4 nhóm có icon (JIRA / CÁ NHÂN / CÔNG CỤ DB / DEV); DEV chỉ hiện ở Development. Đổi tên "Công việc Jira", "Việc cần làm"; thêm "Không gian prompt". Thu gọn còn icon (nhớ lựa chọn, có tooltip); dưới 900px thành ngăn trượt (Esc để đóng, focus quay về nút mở). Header 48px có breadcrumb "Nhóm › Trang" và một tiêu đề trang. Menu avatar: hồ sơ, giao diện Sáng / Tối / Theo máy, ngôn ngữ, đăng xuất. **Bỏ** dòng chữ nhỏ trên topbar, đồng hồ và thời tiết. Hình nền động **mặc định tắt**, bật trong Hồ sơ tài khoản › Hiển thị; thời tiết là công tắc riêng và chỉ khi bật mới hỏi quyền vị trí |

**Sửa thêm trong lúc làm:**
- Kendo Demo hết tràn ngang trên mobile (lưới `1fr` → `minmax(0, 1fr)` ở bố cục chung `.tool-grid`), nên việc này không còn nằm ở 5.13.
- **Lỗi Tabulator `verticalFillMode` đã tìm ra nguyên nhân:** trang Tabulator Demo và bản demo trong Component Hub gọi `setData` trước khi Tabulator dựng xong bảng. Sau khi đổi font và khung, lỗi xuất hiện 2/3 lần tải trang. Đã sửa bằng cách chờ sự kiện `tableBuilt`; 8/8 lần tải sau đó không lỗi.
- Trang đăng nhập được thiết kế cho nền bầu trời, nên khi nền tắt thì chế độ tối gần như không đọc được. Đã chuyển khung đăng nhập sang nền đặc theo token; thiết kế lại đầy đủ vẫn để ở 4.2.

**Kết quả bộ kiểm tra `tests/ui` (66 lượt):** chỉ còn khác biệt ảnh chụp, đúng như dự kiến vì cả app đổi giao diện; không trang nào có chỉ số xấu hơn mốc.

| Chỉ số | Sau GĐ1 | Sau GĐ3 (3.1–3.5) |
|---|---|---|
| Lỗi console | 0 | **0** |
| Tràn ngang (tổng px) | 167 | **0** |
| Ô nhập thiếu label | 54 | 54 (sửa theo từng màn hình ở GĐ4–5) |
| Chữ nhỏ hơn 12px | 276 | **210** (còn lại: thẻ Kanban Jira 68 × 3 và So sánh DB 2 × 3) |

**Chưa kiểm tra:** Jira thật (mới dùng dữ liệu giả); Windows/IIS Express; Safari.

**Ghi chú:**
- Tùy chọn hình nền và thời tiết lưu trong trình duyệt (`localStorage`), không lưu vào tài khoản, nên không cần migration. Đổi máy thì phải bật lại.
- Ở Production, nhóm DEV bị ẩn khỏi menu nhưng đường dẫn `/component.html` vẫn mở được. Việc chặn hẳn làm ở 3.6.
- Thêm một token dẫn xuất `--on-danger` (chữ trên nút đỏ: trắng ở chế độ sáng, gần đen ở chế độ tối), vì `#F87171` không đủ tương phản với chữ trắng.
- Các màn hình bên trong (Ghi chú, Việc cần làm, Jira…) mới chỉ được "đổi giọng" qua token và component chung. Bố cục từng màn hình làm ở GĐ4–5.

Chủ dự án đã duyệt khung app và cho làm tiếp 3.6.

#### Giai đoạn 3.6 – đã làm xong (21/10/2026)

| Việc | Kết quả |
|---|---|
| Chỉ dành cho người phát triển | Component Hub, Kendo Demo, Tabulator Demo (kể cả API dữ liệu của Tabulator Demo) có `[DevToolsOnly]`: mở ở Development hoặc với role `Developer`, nơi khác trả **404**. Nhóm DEV trong sidebar dùng cùng điều kiện. Đã chạy thử bản Production: 5 đường dẫn dev đều 404, nhóm DEV không hiện, Tổng quan vẫn 200 |
| Bảng mẫu component | Tab mặc định mới **Nền tảng**: màu (17 token), cỡ chữ, khoảng cách và bo góc, nút, ô nhập (gợi ý và lỗi), nhãn trạng thái, chip lọc, dòng danh sách, trạng thái trống / lỗi / đang tải, thông báo, modal xác nhận, panel bên, 43 icon |
| Danh mục và giấy phép | Một bảng duy nhất 68 component, cột **Giấy phép**, tìm kiếm không dấu, lọc theo 4 trạng thái: *Dùng được* (27) / *Có sẵn, chưa dùng* (36) / *Không dùng – phụ thuộc thư viện thương mại* (2) / *Không có trong Kendo Core* (3). Có trạng thái trống khi không khớp bộ lọc |
| Một nguồn dữ liệu | `ViewModels/ComponentCatalog.cs`; nhãn trên các thẻ demo (`<tbx-component-status>`) đọc cùng nguồn nên không thể lệch với bảng. Trang chỉ đổi nhãn thành "Không tải được" khi widget thật sự không khởi tạo được |
| Dọn trang (3.13 cũ) | Bỏ tiêu đề trùng với topbar và 4 ô "Chế độ / Cách nhóm…"; bỏ 2 thẻ ẩn chứa chữ lỗi mã hóa; nút của demo Tabulator dùng kiểu nút chuẩn; CSS inline (có nhiều mã màu cứng) chuyển sang `component-hub.css` chỉ dùng token; tên tab và tên trang tiếng Việt ("Bảng mẫu component") |

**Kiểm tra:** 8 tab × 1440/390px × sáng/tối: không lỗi console, không tràn ngang, không chữ nhỏ hơn 12px, mọi widget Kendo khởi tạo được. Đã thử tìm kiếm, lọc, bỏ lọc, toast, modal xác nhận, panel bên.

**Còn lại / chưa làm:**
- Role `Developer` chưa có tác dụng vì ASP.NET Identity của app chưa bật roles; hiện tại ngoài Development không ai mở được trang dev.
- Nội dung các thẻ demo cũ vẫn dùng chuỗi song ngữ viết thẳng trong view (`LocalText`), chưa chuyển sang khóa ngôn ngữ. Trang Kendo Demo và Tabulator Demo chưa đổi (để ở 5.13).

#### Điều chỉnh sau khi chủ dự án xem khung app (21/10/2026)

Ở 3.5, đồng hồ, thời tiết và hình nền bị tắt trên **mọi** trang, kể cả trang đăng nhập; đồng hồ bị bỏ hẳn. Như vậy là áp dụng quá tay. Chủ dự án chốt lại:
- **Trước khi đăng nhập** (Đăng nhập, Đăng ký): không còn sidebar trống; thanh trên cùng có logo, đồng hồ + ngày, thời tiết, đổi ngôn ngữ VI/EN, đổi sáng/tối, và nút Đăng ký hoặc Đăng nhập (bỏ nút trùng với trang đang mở). Hình nền thời tiết luôn bật. Trên màn hình dưới 600px chỉ hiện giờ, ẩn ô thời tiết để không tràn.
- **Bên trong sau khi đăng nhập:** giữ như 3.5.
- Đã kiểm tra: 1440/390px, sáng/tối, không lỗi console, không tràn ngang. Phần thời tiết kiểm tra bằng phản hồi giả của Open-Meteo vì môi trường thử nghiệm chặn mạng ra ngoài; chưa thử với Open-Meteo thật.
- **Bổ sung theo yêu cầu (21/10/2026):** thanh trên cùng bên trong app cũng có đồng hồ và thời tiết (dưới 900px ẩn ngày, dưới 600px chỉ còn giờ); hình nền thời tiết bên trong vẫn tắt mặc định. Bỏ công tắc "Hiện thời tiết theo vị trí" trong Hồ sơ vì thời tiết giờ luôn hiện. Avatar có ảnh thì hiện thêm chữ viết tắt tên bên cạnh. `tests/ui`: chỉ số không đổi (lỗi console 0, tràn ngang 0, thiếu label 54, chữ nhỏ 210); ảnh mốc đã tạo lại.

#### Giai đoạn 4 – đã làm xong (21/10/2026)

| # | Việc | Kết quả |
|---|---|---|
| 4.1 | Bản địa hóa và định dạng | **Múi giờ:** cấu hình `App:TimeZone` (mặc định `Asia/Ho_Chi_Minh`, `Infrastructure/Time/AppClock.cs`); "hôm nay" và giờ lưu dạng UTC ở server đều đi qua đó; đồng hồ trên trình duyệt dùng cùng múi giờ (thử với trình duyệt đặt múi giờ New York vẫn hiện giờ Việt Nam). **Định dạng:** `DisplayFormatExtensions` ở server và `Toolbox.format` (`format.js`) ở trình duyệt: `19/10/2026`, `15:19`, `18,5`, `6,5h` (vi) và `10/19/2026`, `3:19 PM`, `18.5` (en). Đã áp dụng cho Jira (ngày hạn, khoảng kỳ, số giờ, bảng Kanban), Tổng quan, Việc cần làm, Ghi chú, Hẹn giờ, Bắt SQL, Tabulator Demo. Bảng Kanban hết tên thứ viết cứng tiếng Việt và hết chữ "done/todo" tiếng Anh. **Thuật ngữ:** `docs/GLOSSARY.md` (giữ nguyên issue, subtask, JQL, log; Việt hóa phần còn lại); sửa 55 nhãn Jira, tab Hồ sơ và đăng nhập. **Quét ngôn ngữ** từng trang ở cả VI/EN: chỉ còn các trang tài khoản lấy sẵn từ thư viện Identity (Email, Đổi mật khẩu, Xác thực hai lớp, Dữ liệu cá nhân, Đăng ký) — xử lý ở 4.2 |
| 4.2 | Identity | **Mọi trang tài khoản giờ là trang của ToolboxWeb, đủ tiếng Việt/Anh** (trước đây Email, Đổi mật khẩu, Xác thực hai lớp, Dữ liệu cá nhân, Đăng ký là trang tiếng Anh của thư viện). Thẻ đăng nhập có ba lựa chọn Toolbox / Jira / Đăng ký, một tiêu đề, nút ẩn/hiện mật khẩu. Hồ sơ: menu nền đặc có icon, tên đăng nhập hiện dạng chữ, bỏ chữ "source / input file / file rác". Tài khoản Jira không thấy tab Mật khẩu và Xác thực hai lớp (không có mật khẩu ToolboxWeb). Thông báo lỗi Identity theo ngôn ngữ giao diện. **Sửa lỗi có sẵn:** tài khoản đã bật xác thực hai lớp không đăng nhập được (trang Đăng nhập báo sai mật khẩu); nay có bước nhập mã và bước dùng mã khôi phục. **Xóa tài khoản** giờ xóa cả ghi chú, việc cần làm, liên kết, prompt, phiên tập trung, lịch sử và ảnh đại diện (trước đây để lại dữ liệu mồ côi). Đã thử đầu-cuối 21 bước (đăng ký, lỗi, đổi mật khẩu, bật 2FA bằng mã TOTP thật, đăng nhập bằng mã và mã khôi phục, tải dữ liệu, xóa tài khoản và kiểm tra DB). **Không làm:** "Quên mật khẩu" và đổi email, vì app chưa cấu hình gửi email; mã QR cho ứng dụng xác thực (nhập khóa thủ công) |
| 4.3 | Module Jira dùng chung | `Content/js/jira-common.js` (`window.ToolboxJira`) gom phần trước đây chép ở ba file: ngày theo tuần/tháng ISO, **một bảng màu trạng thái** (xong = xanh lá, đang làm = màu nhấn, chưa làm = trung tính; dùng nhãn `tbx-pill`, phía server là `JiraStatus.PillClass`), **cấu hình bảng Tabulator mặc định** (canh lề theo quy ước, số giờ và ngày theo ngôn ngữ, trạng thái trống có icon, chỉ hiện tooltip khi chữ bị cắt), **bộ chọn kỳ** (`_JiraPeriodPicker`: Ngày/Tuần/Tháng/Năm, đổi là tự tải lại, không còn nút Tìm kiếm, nút × và « », thêm nút "Hôm nay"), **khối JQL thu gọn** (`_JiraJqlBlock`: sao chép, mở trong Jira) và **thanh chip lọc** (Tất cả / Chưa xong / Quá hạn / Đã xong, có số đếm). Áp dụng: Tổng quan Jira (bộ chọn kỳ, bảng, chip, JQL), Bảng việc (bộ chọn kỳ Tuần/Tháng, vẫn hỏi trước khi bỏ thay đổi chưa lưu), Tìm kiếm (nhãn trạng thái, khối JQL), Sắp xếp tự động (bảng mặc định, nhãn trạng thái, số giờ canh phải). Xóa khoảng 450 dòng CSS cũ có màu viết thẳng. **Sửa thêm:** tên thứ tiếng Việt viết cứng và thông báo tiếng Việt viết cứng ở Sắp xếp tự động; chữ "Est/Log" tiếng Anh trên thẻ Bảng việc. **Chưa làm (để 5.1/5.2):** bố cục lại thẻ số liệu và cột bảng Tổng quan (ở 1440px bảng vẫn cuộn ngang trong khung), chuyển bảng Tìm kiếm sang Tabulator |
| 4.4 | Form kết nối SQL dùng chung | `Views/Shared/_SqlConnectionForm.cshtml` + `Content/js/sql-connection.js` (`window.ToolboxSqlConnection`) + `Content/css/sql-connection.css`, dùng cho hai thẻ Nguồn/Đích của So sánh cấu trúc DB và cho Bắt câu lệnh SQL. Cùng một thứ tự ô (Máy chủ, Tài khoản, Mật khẩu, Database), label luôn hiện, dòng gợi ý giữ chỗ ("Kiểm tra kết nối để chọn từ danh sách"), nút ẩn/hiện mật khẩu, kết quả kiểm tra có ký hiệu kèm màu. Khóa ngôn ngữ chung `SqlConnection.*` thay cho các khóa `SchemaCompare.Field.*` mà Bắt SQL đang mượn. Thẻ Nguồn/Đích không còn kéo giãn các ô khi thẻ bên kia cao hơn. Tiêu đề panel (`.panel-header`) được xuống dòng thay vì bị ép hẹp trên điện thoại. **Chưa kiểm tra:** kết nối thành công tới SQL Server thật (môi trường thử không có SQL Server); đã thử đường lỗi, gửi dữ liệu, chống giả mạo yêu cầu, lưu và khôi phục kết nối |

