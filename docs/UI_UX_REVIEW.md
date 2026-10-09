# Đánh giá UI/UX ToolboxWeb

> Ngày đánh giá: 09/10/2026
> Phạm vi: toàn bộ màn hình của ToolboxWeb, gồm nhóm Jira, cùng các prompt mẫu, `docs/*_PROMPTS.md` và `AGENTS.md`.
> Phương pháp:
> - Build và chạy app thật, đăng ký tài khoản thử, nhập dữ liệu mẫu.
> - Chụp 26 màn hình ở desktop 1440px, mobile 390px, chế độ tối và sidebar thu gọn.
> - Đọc `site.css`, các view Razor và JavaScript.
> - Nhóm Jira được đánh giá từ ảnh chụp do người dùng cung cấp, kết hợp đọc mã nguồn, vì môi trường đánh giá không đăng nhập được Jira.

---

## Mục lục

1. [Kết luận tổng quan](#1-kết-luận-tổng-quan)
2. [Khung chung (áp dụng cho mọi trang)](#2-khung-chung-áp-dụng-cho-mọi-trang)
3. [Đánh giá từng màn hình](#3-đánh-giá-từng-màn-hình)
4. [Nhóm màn hình Jira](#4-nhóm-màn-hình-jira)
5. [Đánh giá các prompt đang dùng](#5-đánh-giá-các-prompt-đang-dùng)
6. [Thứ tự ưu tiên cải tiến](#6-thứ-tự-ưu-tiên-cải-tiến)
7. [Phụ lục: bảng điểm](#7-phụ-lục-bảng-điểm)

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
| 3 | **Mobile không có menu trượt.** Ở 390px, sidebar xếp chồng lên trên cùng và chiếm khoảng 500px; người dùng phải cuộn qua cả menu mới tới nội dung. Header bị vỡ thành 4 dòng, cao khoảng 240px. | Cao |
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
- Với tài khoản Jira, **ảnh đại diện bị vỡ**: cạnh tên người dùng là icon ảnh lỗi, nhiều khả năng vì URL avatar của Jira cần đăng nhập mới tải được. Cần tải ảnh qua server, hoặc khi lỗi thì quay về hiện chữ viết tắt.

### 2.3 Hình nền hoàng hôn (adaptive background)

- Hình đẹp nhưng **không hợp** với một công cụ làm việc, nhất là công cụ DB/SQL. Mặt trời và núi nằm sau nội dung, cạnh tranh sự chú ý.
- **Chữ đặt thẳng lên nền gần như không đọc được**: phụ đề trang Kendo Demo, phụ đề trang Quản lý tài khoản, chữ đỏ của trang Access denied.
- Hình nền chỉ cao 900px. Ở các trang dài (Tổng quan, Prompt, Component), phần bên dưới chuyển sang một dải xanh navy, tạo thành vệt cắt ngang rõ ràng.
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
- Console báo lỗi JavaScript `Cannot read properties of undefined (reading 'parseColor')` trên 5 trang: Công việc, Component, Kendo Demo, So sánh DB, Bắt SQL.
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
- Nhãn đỏ "Not in current Kendo bundle" và thông báo lỗi kiểu dev hiện ngay trong demo.

### 3.10 Kendo UI Demo: 2/10

- Phụ đề **không đọc được** trên nền.
- **Hộp lỗi đỏ khắp nơi** ("kendoTextBox / kendoListView chưa có trong bundle"), có 2 hộp lỗi lạc hẳn ra ngoài panel ở cuối trang.
- Bảng chính chỉ có header và một dòng lỗi.
- **Đề xuất:** xóa trang này hoặc ẩn hẳn. Một trang "demo hỏng" làm giảm uy tín toàn bộ app.

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
- Trong ảnh chụp thấy **2 mục cùng được tô sáng** (Profile + Password, hoặc Email + Two-factor), nên người dùng không biết mình đang ở đâu.
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
- Lưu đề xuất là **ghi thật vào Jira** nhưng không có bước xác nhận. Cần một hộp thoại: "Sẽ cập nhật hạn của 5 subtask trên Jira", kèm danh sách.
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
| 4 | Ẩn Kendo Demo và Component Hub khỏi người dùng thường | Dev |
| 5 | Sửa lỗi JS `parseColor` | 5 trang |
| 6 | Thêm xác nhận khi xóa (Công việc, Liên kết nhanh, Prompt) | Cá nhân |
| 7 | Trang Access denied: dịch sang tiếng Việt, giải thích lý do, thêm nút hành động | Identity |
| 8 | Sửa avatar Jira bị vỡ; đổi tên menu Jira và Công việc cho khỏi nhầm | Jira |
| 9 | Tab Tìm kiếm: mã issue có link và không bẻ dòng; dùng nhãn trạng thái màu; bỏ các cột trống; đổi đúng tên cột "Logged" | Jira |
| 10 | Kéo thả ở tab Quản lý phải có trạng thái "chưa lưu" và thanh Lưu/Hủy, nếu không thì tắt | Jira |
| 11 | Thêm xác nhận trước khi "Lưu đề xuất" ghi vào Jira | Jira |
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
| Jira – Sắp xếp tự động | 5 | Luồng không rõ, không thấy thay đổi, không xác nhận |
| Bắt câu lệnh SQL | 4.5 | Lệch với So sánh DB, nút trông như bị vô hiệu |
| Tổng quan | 4 | Thiếu tóm tắt, dữ liệu "hôm nay" sai nghĩa |
| Hẹn giờ tập trung | 4 | Lưu được phiên giả, thiếu trạng thái trống |
| Không gian prompt | 4 | Dài, không tìm kiếm, sao chép thiếu định dạng đầu ra |
| Quản lý tài khoản | 4 | Tiếng Anh, label đè chữ, active không rõ |
| Công việc (checklist) | 3.5 | Thiếu label, lọc, xác nhận xóa |
| Liên kết nhanh | 3.5 | Ô "0" không label, không sửa được |
| Jira – Tìm kiếm | 3.5 | Bảng thường, mã không có link, JQL lặp |
| Đăng ký | 2 | Trang scaffold tiếng Anh, label đè chữ |
| Kendo UI Demo | 2 | Hộp lỗi đỏ khắp trang |
| Access denied | 1 | Tiếng Anh, không giải thích, không có hành động |
