# Đánh giá UI/UX ToolboxWeb

> Ngày đánh giá: 09/10/2026
> Phạm vi: toàn bộ màn hình của ToolboxWeb, gồm nhóm Jira, cùng các prompt mẫu, `docs/*_PROMPTS.md` và `AGENTS.md`.
> Phương pháp:
> - Build và chạy app thật, đăng ký tài khoản thử, nhập dữ liệu mẫu.
> - Chụp 26 màn hình ở desktop 1440px, mobile 390px, chế độ tối và sidebar thu gọn.
> - Đọc `site.css`, các view Razor và JavaScript.
> - Nhóm Jira được đánh giá từ ảnh chụp do người dùng cung cấp, kết hợp đọc mã nguồn, vì môi trường đánh giá không đăng nhập được Jira.
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
- **Nguyên nhân thật không nằm ở trang này.** Bundle `kendo.ui.core.min.js` cần thêm thư viện `@progress/kendo-drawing` (biến toàn cục `_progress_kendo_drawing`) nhưng thư viện này không được nạp. Bundle dừng ở module màu (byte ~1.227.548), nên các widget đứng sau điểm đó (TextBox ở ~1.251.900, ListView ~1.310.070, Window ~1.507.757) không bao giờ được đăng ký. Khi thử bổ sung biến này, trang chạy đầy đủ: ô tìm nhanh, bảng ListView có nhãn trạng thái và thanh tiến độ, phân trang, nút "Chi tiết"; không còn lỗi console.
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

### P0: lỗi, cần sửa ngay

| # | Việc | Màn hình |
|---|---|---|
| 1 | Sửa sidebar thu gọn mất menu; thêm menu trượt cho mobile | Toàn app |
| 2 | Thêm trạng thái active cho menu và `aria-current` | Toàn app |
| 3 | Làm lại trang Đăng ký; sửa label đè chữ ở Đăng ký và trang Email | Identity |
| 4 | Sửa lỗi nạp Kendo (thiếu `@progress/kendo-drawing`), rồi chuyển Kendo Demo và Component Hub sang nhóm Dev | Dev, Jira, DB |
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
  1. Tạo một partial duy nhất `Views/Shared/_KendoScripts.cshtml`, gồm: file UMD của `@progress/kendo-drawing` (đúng phiên bản mà `kendo-ui-core@2026.2.520` khai báo trong dependencies), `kendo.ui.core.min.js`, rồi culture và messages vi-VN. Thay mọi chỗ đang nạp core bằng partial này: `_Layout.cshtml`, `Components/Index.cshtml`, `DemoKendo/Index.cshtml`, `Jira/Dashboard.cshtml`, `Jira/Worklist.cshtml`, `SchemaCompare/Index.cshtml`, `SqlProfiler/Index.cshtml`, và `loadScriptOnce` trong `site.js` (khoảng dòng 666). Kiểm tra file UMD của kendo-drawing gán đúng `window._progress_kendo_drawing`; nếu tên khác thì gán lại trước khi nạp core.
  2. Nếu cần sửa nóng trong lúc chờ: đặt `<script>window._progress_kendo_drawing = window._progress_kendo_drawing || {};</script>` ngay trước core. Cách này **đã được kiểm chứng**: hết lỗi, `kendoTextBox`, `kendoListView`, `kendoWindow` đều có, trang Kendo Demo chạy đầy đủ. Đổi lại, các tính năng màu và vẽ (ColorPicker) sẽ không chạy.
  3. Cập nhật `docs/kendo-self-host.md` (thêm kendo-drawing vào danh sách asset) và các nhãn "Usable now / Not in current Kendo bundle" trong Component Hub.
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
| Kendo Demo hỏng vì thiếu widget, đề xuất xóa trang | Các widget có trong bundle; bundle dừng do thiếu `@progress/kendo-drawing`. Đề xuất đổi thành sửa lỗi nạp | 2.6, 3.9, 3.10, 8 (1.1) |
| "Lưu đề xuất" ghi thật vào Jira mà không xác nhận | Nút chỉ đổi dữ liệu trên trình duyệt, không ghi lên Jira; vấn đề là tên nút gây hiểu nhầm | 4.5, 8 (1.2) |
| Mobile không có cách ẩn sidebar | Có nút ☰ để ẩn, nhưng sidebar mặc định mở và trạng thái lưu chung với desktop | 2.1 |
| Avatar Jira vỡ do URL Jira cần đăng nhập | Ảnh lấy từ file upload trong hồ sơ; nguyên nhân cụ thể cần kiểm tra trên máy thật | 2.2, 8 (1.8) |
