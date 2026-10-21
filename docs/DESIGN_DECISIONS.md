# Quyết định thiết kế ToolboxWeb

> Trạng thái: **ĐÃ DUYỆT ngày 21/10/2026** (Giai đoạn 2 trong lộ trình ở `docs/UI_UX_REVIEW.md`, mục 10.6).
> Chủ dự án duyệt toàn bộ đề xuất, **trừ màu nhấn**: chủ dự án chọn cặp màu riêng (xem 2.B). Các giá trị đã được ghi vào `AGENTS.md` (mục UI Standard).
> Bản mẫu tương tác: https://claude.ai/artifact/DZ6V7aPhiwrhBLPF4bYD3u (riêng tư; chỉ chủ dự án mở được). Trên khung Jira Dashboard có công tắc đổi giao diện sáng/tối, màu nhấn, font và mật độ để so sánh trực tiếp.

Mỗi quyết định ghi: phương án đề xuất, các lựa chọn khác, lý do, và hệ quả khi triển khai ở Giai đoạn 3.

---

## 2.A Hướng phong cách

- **Đã chọn:** lấy cảm hứng từ các nguyên tắc của Linear:
  - nền trung tính, một màu nhấn duy nhất;
  - màu chỉ dùng cho trạng thái, mức ưu tiên và phản hồi;
  - danh sách gọn (dòng 36px), form rộng rãi;
  - trạng thái trống điềm tĩnh, chuyển động nhẹ (khoảng 150ms);
  - thao tác được bằng bàn phím.
- **Không làm:** sao chép logo, tên, bộ icon, hình minh họa, màu thương hiệu hay CSS của Linear (xem chính sách giấy phép trong `AGENTS.md`).
- **Lý do:** ToolboxWeb là công cụ làm việc dày thông tin; nhóm Jira bản chất là quản lý issue. Một ngôn ngữ thiết kế tiết chế chữa đúng bệnh lớn nhất hiện nay: mỗi trang một kiểu.
- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.B Màu nhấn

**Chủ dự án chọn:** `#4B87BB` cho chế độ sáng và `#8ED3F2` cho chế độ tối, thay cho 3 phương án đề xuất (xanh dương `#2563EB`, xanh biển `#0369A1`, xanh ngọc `#0F766E`).

Hai màu này chưa đủ tương phản để làm chữ trên nền sáng, nên chế độ sáng có thêm hai sắc độ đậm hơn **cùng tông màu**, chỉ dùng cho chữ và nút. Cách gán này được chủ dự án duyệt ngày 21/10/2026.

| Token | Sáng | Tối | Dùng cho |
|---|---|---|---|
| `--accent-brand` | `#4B87BB` | `#8ED3F2` | Trang trí: biểu đồ, thanh tiến độ, logo, viền nhấn |
| `--accent` | `#417BAD` | `#8ED3F2` | Nút chính, link, viền ô đang focus |
| `--accent-strong` | `#3A6E9B` | `#9FDAF4` | Chữ trên nền nhạt pha từ màu nhấn (nhãn "Đang làm", menu đang chọn, chip đang chọn); hover của nút chính |
| `--on-accent` | `#FFFFFF` | `#0E1014` | Chữ trên nút màu nhấn |

Độ tương phản đã kiểm tra (WCAG, tối thiểu 4,5:1):

| Cặp màu | Sáng | Tối |
|---|---|---|
| `--accent` trên nền trắng / `#15181E` | 4,5 | 10,8 |
| `--on-accent` trên `--accent` (nút) | 4,5 | 11,6 |
| `--accent-strong` trên nền nhạt 14% (nhãn) | 4,6 | – |
| `--accent` trên nền nhạt 14% (nhãn, tối) | – | 8,0 |
| `--on-accent` trên `--accent-strong` (hover) | 5,4 | 12,5 |

Riêng `#4B87BB` (3,8:1 trên nền trắng) và `#8ED3F2` (1,65:1 trên nền trắng) **không được dùng làm màu chữ ở chế độ sáng**.

- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.C Bảng màu nền, chữ và trạng thái

| Token | Sáng | Tối |
|---|---|---|
| `--bg` | `#F7F8FA` | `#0E1014` |
| `--surface` | `#FFFFFF` | `#15181E` |
| `--surface-2` | `#F2F4F7` | `#1C2028` |
| `--line` | `#E3E6EB` | `#2A2F39` |
| `--text` | `#16181D` (17,8:1) | `#E8EAEE` (14,8:1) |
| `--muted` | `#5B6270` (6,1:1) | `#A2A9B6` (7,5:1) |
| `--success` | `#15803D` (5,0:1) | `#4ADE80` (10,2:1) |
| `--warning` | `#B45309` (5,0:1) | `#FBBF24` (10,6:1) |
| `--danger` | `#C62828` (5,6:1) | `#F87171` (6,4:1) |

- Nền nhạt của nhãn trạng thái là màu trạng thái pha 12–14% với nền (`color-mix`). Không thêm mã màu mới.
- Các biến Jira hiện có (`--jira-blue`, `--jira-mint`…) sẽ gộp về bộ này ở Giai đoạn 3.
- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.D Font

| Phương án | Ưu | Nhược |
|---|---|---|
| **Inter, tự host (đề xuất)** | Giấy phép SIL OFL 1.1; hiển thị giống nhau trên mọi máy; số đều nhau (tabular) cho bảng giờ | Thêm khoảng 100–200 KB font (chỉ tải các độ đậm 400/500/600) |
| Font hệ thống (`system-ui, Segoe UI, Roboto`) | Không phải tải gì | Windows và macOS hiển thị khác nhau |

- Tải từ gói `@fontsource/inter` (OFL), đặt trong `Content/vendor/fonts/inter/`, ghi vào `THIRD-PARTY-NOTICES.md`. Không tải từ trang của Linear hay Google Fonts lúc chạy.
- Bỏ khai báo `"Roboto"` hiện không được tải.
- **Trạng thái:** Đã duyệt 21/10/2026 (Inter).

## 2.E Cỡ chữ, độ đậm, khoảng cách, bo góc, kích thước

- **Cỡ chữ** (rem, gốc 16px):

  | Token | Giá trị | Dùng cho |
  |---|---|---|
  | `--text-xs` | 0.75rem / 12px | nhãn phụ, thời gian |
  | `--text-sm` | 0.8125rem / 13px | dòng danh sách, nút, menu |
  | `--text-md` | 0.875rem / 14px | nội dung, ô nhập |
  | `--text-lg` | 1rem / 16px | tiêu đề panel |
  | `--text-xl` | 1.25rem / 20px | tiêu đề trang (hiếm) |
  | `--text-2xl` | 1.5rem / 24px | số liệu chính |

  Không có chữ nhỏ hơn 12px.
- **Độ đậm:** 400 / 500 / 600; 700 chỉ cho logo.
- **Khoảng cách:** `--space-1…6` = 4 / 8 / 12 / 16 / 24 / 32px.
- **Bo góc:** 4px (nhãn), 6px (nút, ô nhập), 8px (panel), 999px (chip).
- **Kích thước:**
  - dòng danh sách 36px (gọn) hoặc 44px (thoáng), xem 2.H;
  - ô nhập trong form 38px; nút 34px;
  - header 48px; sidebar 232px.
- **Điểm ngắt màn hình:** 600 / 900 / 1200px.
- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.F Bộ icon

| Phương án | Giấy phép | Ghi chú |
|---|---|---|
| **Lucide (đề xuất)** | ISC | Nét mảnh, đồng đều, hợp phong cách đã chọn. Chỉ đưa vào các icon dùng thật, dạng sprite SVG trong `Content/vendor/lucide/` |
| Bootstrap Icons | MIT | Đã có sẵn 10 icon trong repo; nét dày hơn, kém đồng bộ với Inter |
| Kendo font icons | Apache-2.0 | Đã có; chỉ nên dùng bên trong widget Kendo |

- Bản mẫu đang dùng icon nét mảnh tự vẽ để minh họa; khi triển khai sẽ thay bằng bộ được chọn.
- **Trạng thái:** Đã duyệt 21/10/2026 (Lucide).

## 2.G Thư viện control

- **Giữ:**
  - Bootstrap (nền, grid, modal).
  - Tabulator (bảng).
  - Kendo UI Core, chỉ dùng DatePicker, ComboBox, MultiSelect, AutoComplete, NumericTextBox, Notification. Giao diện chỉnh qua `vendor-sync.css` theo token.
- **Không dùng:** ColorPicker và mọi thứ cần `@progress/kendo-drawing`; các widget Kendo thương mại.
- **Để dành, chỉ nối khi có màn hình cần:** ECharts (biểu đồ giờ log ở 5.1), FullCalendar, Quill, SheetJS.
- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.H Khung app, mật độ và hình nền

- **Sidebar:**
  - Chia 4 nhóm: JIRA (chỉ tài khoản Jira) / CÁ NHÂN / CÔNG CỤ DB / DEV (chỉ ở Development hoặc theo role).
  - Đổi tên: "Danh sách công việc" → "Công việc Jira", "Công việc" → "Việc cần làm".
  - Thêm "Không gian prompt" vào nhóm CÁ NHÂN.
  - Thu gọn thì chỉ còn icon; trên mobile thành ngăn trượt.
- **Header 48px:** breadcrumb bên trái, thao tác của trang bên phải, avatar có menu chứa đổi giao diện, ngôn ngữ và đăng xuất. **Bỏ** dòng "BỘ CÔNG CỤ NĂNG SUẤT CÁ NHÂN" và widget thời tiết; chỉ hỏi quyền vị trí khi người dùng chủ động bật.
- **Hình nền động:** thành tùy chọn trong trang tài khoản, **mặc định tắt**.
- **Giao diện sáng/tối:** lần đầu theo cài đặt hệ điều hành, sau đó nhớ lựa chọn của người dùng.
- **Mật độ mặc định:** Gọn (36px).
- **Trạng thái:** Đã duyệt 21/10/2026.
- **Điều chỉnh của chủ dự án (21/10/2026, sau khi xem khung app):** các trang **trước khi đăng nhập** (Đăng nhập, Đăng ký…) giữ hình nền động theo thời tiết, luôn bật, có đồng hồ và thời tiết trên thanh trên cùng, **không có sidebar**. Trình duyệt hỏi quyền vị trí ở các trang này để lấy thời tiết, như trước đây. Bên trong sau khi đăng nhập: hình nền mặc định tắt và bật được trong Hồ sơ.
- **Điều chỉnh tiếp (21/10/2026):** thanh trên cùng **bên trong** app cũng có đồng hồ và thời tiết (thời tiết ẩn trên màn hình dưới 600px, ngày ẩn dưới 900px). Hình nền theo thời tiết bên trong **không** bật lại. Vì thời tiết luôn hiện, công tắc "Hiện thời tiết theo vị trí" trong Hồ sơ được bỏ; trình duyệt hỏi quyền vị trí một lần cho cả trang ngoài và trang trong. Avatar có ảnh thì hiện thêm chữ viết tắt tên người dùng bên cạnh.

## 2.I Múi giờ và định dạng

- **Múi giờ:** một cấu hình duy nhất `App:TimeZone`, mặc định `Asia/Ho_Chi_Minh`. Server dùng nó cho mọi ngày giờ hiển thị, đồng hồ trên client cũng dùng chính nó, để không còn lệch ngày giữa hai bên.
- **Định dạng theo ngôn ngữ giao diện:**
  - `vi-VN`: `dd/MM/yyyy`, `HH:mm`, số `17,5`;
  - `en-US`: `MM/dd/yyyy`, `h:mm tt`, số `17.5`;
  - giờ: `17,5h`.
- **Trạng thái:** Đã duyệt 21/10/2026.

## 2.J Bản mẫu tĩnh

- 3 khung trong bản mẫu:
  1. **Jira Dashboard (tương tác):** dữ liệu tuần 43 giống chế độ Jira giả. Có thẻ tiến độ 6/9, giờ log/ước tính 14,5h/15,5h, quá hạn, chưa ước tính; biểu đồ giờ log theo ngày so với 8h; danh sách nhóm theo issue cha; nút "Ẩn việc đã xong"; JQL thu gọn.
  2. **Trang danh sách + form (Việc cần làm):** ô tìm kiếm, chip lọc có số đếm, checkbox ở đầu dòng, form trong modal có label cho mọi ô, modal xác nhận xóa, trạng thái trống.
  3. **Bảng token:** màu (kèm độ tương phản), cỡ chữ, khoảng cách, bo góc, nút, ô nhập, nhãn trạng thái.
- **Trạng thái:** Đã xem và duyệt 21/10/2026. Bản mẫu đã được cập nhật theo màu nhấn đã chọn.

---

## Bước tiếp theo

1. ~~Cập nhật trạng thái từng mục ở trên.~~ Xong 21/10/2026.
2. ~~Điền giá trị vào `AGENTS.md`, mục UI Standard.~~ Xong 21/10/2026. Danh sách component dùng chung sẽ được bổ sung khi làm xong ở Giai đoạn 3.
3. Bắt đầu Giai đoạn 3 theo thứ tự ở `docs/UI_UX_REVIEW.md`, mục 10.7: `tokens.css` → font → giao diện thư viện bên thứ ba → component nền → khung app → bảng mẫu component.
