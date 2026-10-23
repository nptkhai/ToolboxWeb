# Danh mục chức năng và đề xuất nâng cấp

Rà ngày 23/10/2026, trên nhánh `claude/relaxed-goodall-e9palp` (đã gồm Giai đoạn 0–5 và phần đã làm của Giai đoạn 6 trong `docs/UI_UX_REVIEW.md`).

File này để chủ dự án đánh giá những gì đang có và chọn việc làm tiếp. Sau khi bạn điền và push lên, mình sẽ đọc, đánh giá lại và đề xuất cách chia giai đoạn ở mục cuối.

## Cách điền

**Bảng "Chức năng đang có"** của mỗi màn hình:

| Cột | Điền gì |
|---|---|
| Đánh giá | Một trong: `Tốt` · `Cần sửa` · `Bỏ` · `Chưa dùng` |
| Ghi chú của bạn | Tự do: chỗ chưa ưng, lỗi gặp phải, cách bạn muốn nó chạy |

**Bảng "Đề xuất"** của mỗi màn hình:

| Cột | Điền gì |
|---|---|
| Chọn | `x` nếu muốn làm; để trống nếu không |
| Ưu tiên | `1` làm sớm · `2` làm sau · `3` khi rảnh |
| Ghi chú của bạn | Muốn khác đi thế nào, hoặc vì sao không làm |
| Công sức | Mình ước lượng trước: `S` nhỏ (ít file, không đổi dữ liệu) · `M` vừa (nhiều file hoặc cần migration) · `L` lớn (tính năng mới nhiều phần, hoặc ghi lên hệ thống ngoài như Jira/SQL Server) |

Mỗi màn hình còn có mục **"Đề xuất thêm của bạn"** để ghi ý mới, ngoài các dòng mình liệt kê.

Lưu ý:
- Giữ nguyên mã từng dòng (ví dụ `TODO-03`, `TODO-P02`) để mình đối chiếu khi đọc lại. Ý mới của bạn không cần mã, mình sẽ đặt sau.
- Mục **"Đã biết / chưa kiểm tra"** dưới mỗi màn hình lấy từ `docs/UI_UX_REVIEW.md`, để bạn xác nhận còn đúng hay không.

## Mục lục

1. [Khung app và phần dùng chung](#1-khung-app-và-phần-dùng-chung-app)
2. [Tổng quan](#2-tổng-quan-dash)
3. [Lịch sử hoạt động](#3-lịch-sử-hoạt-động-act)
4. [Ghi chú](#4-ghi-chú-note)
5. [Việc cần làm](#5-việc-cần-làm-todo)
6. [Hẹn giờ tập trung](#6-hẹn-giờ-tập-trung-focus)
7. [Liên kết nhanh](#7-liên-kết-nhanh-link)
8. [Không gian prompt](#8-không-gian-prompt-prompt)
9. [Jira Dashboard](#9-jira-dashboard-jdash)
10. [Công việc Jira – Tìm kiếm](#10-công-việc-jira--tìm-kiếm-jsearch)
11. [Công việc Jira – Quản lý công việc](#11-công-việc-jira--quản-lý-công-việc-jboard)
12. [Công việc Jira – Sắp xếp tự động](#12-công-việc-jira--sắp-xếp-tự-động-jauto)
13. [So sánh cấu trúc DB](#13-so-sánh-cấu-trúc-db-scmp)
14. [Bắt câu lệnh SQL](#14-bắt-câu-lệnh-sql-sqlp)
15. [Tài khoản](#15-tài-khoản-acc)
16. [Trang dành cho người phát triển](#16-trang-dành-cho-người-phát-triển-dev)
17. [Màn hình hoặc công cụ mới](#17-màn-hình-hoặc-công-cụ-mới-new)
18. [Đề xuất thêm của bạn (chung)](#18-đề-xuất-thêm-của-bạn-chung)
19. [Tổng hợp và chia giai đoạn](#19-tổng-hợp-và-chia-giai-đoạn)

## Bản đồ màn hình

| Màn hình | Đường dẫn | Nhóm menu | Ai thấy | Phím đi tới |
|---|---|---|---|---|
| Jira Dashboard | `/jira-dashboard.html` | JIRA | Đăng nhập bằng Jira | G J |
| Công việc Jira (3 tab) | `/jira-worklist.html` | JIRA | Đăng nhập bằng Jira | G W |
| Tổng quan | `/dashboard.html` | CÁ NHÂN | Mọi tài khoản | G D |
| Lịch sử hoạt động | `/activity.html` | (thuộc Tổng quan) | Mọi tài khoản | G A |
| Ghi chú | `/notes.html` | CÁ NHÂN | Mọi tài khoản | G N |
| Việc cần làm | `/checklist.html` | CÁ NHÂN | Mọi tài khoản | G T |
| Hẹn giờ tập trung | `/focus-timer.html` | CÁ NHÂN | Mọi tài khoản | G F |
| Liên kết nhanh | `/quick-links.html` | CÁ NHÂN | Mọi tài khoản | G L |
| Không gian prompt | `/prompt-templates.html` | CÁ NHÂN | Mọi tài khoản | G P |
| So sánh cấu trúc DB | `/schema-compare.html` | CÔNG CỤ DB | Mọi tài khoản | G S |
| Bắt câu lệnh SQL | `/sql-profiler.html` | CÔNG CỤ DB | Mọi tài khoản | G Q |
| Bảng mẫu component, Kendo Demo, Tabulator Demo | `/component.html` … | DEV | Chỉ ở môi trường Development | G C |
| Đăng nhập, đăng ký, tài khoản | `/Identity/Account/...` | Menu avatar | — | — |

---

## 1. Khung app và phần dùng chung (APP)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| APP-01 | Sidebar 4 nhóm (JIRA / CÁ NHÂN / CÔNG CỤ DB / DEV); đánh dấu trang đang mở; thu gọn còn icon (nhớ lựa chọn, có tooltip); dưới 900px thành ngăn trượt | | |
| APP-02 | Thanh trên cùng 48px: breadcrumb, ô "Tìm hoặc chạy lệnh…" (Ctrl/⌘+K), đồng hồ và ngày theo `App:TimeZone`, thời tiết theo vị trí, ô đếm giờ khi đang tập trung, menu avatar | | |
| APP-03 | Menu avatar: ảnh hoặc chữ viết tắt, đổi giao diện sáng/tối/theo máy, đổi ngôn ngữ Việt/Anh, tài khoản, đăng xuất | | |
| APP-04 | Bảng lệnh Ctrl/⌘+K: nhảy trang, lệnh nhanh (tạo ghi chú/việc/liên kết/prompt, bật/dừng hẹn giờ, giao diện, ngôn ngữ, phím tắt, đăng xuất), tìm trong dữ liệu của mình (ghi chú, việc, liên kết, prompt; không dấu), mục mở gần đây | | |
| APP-05 | Phím tắt chung: `?` xem toàn bộ phím tắt, G rồi một chữ để nhảy trang, gợi ý phím trên nút, dòng phím tắt dưới mỗi trang | | |
| APP-06 | Hai ngôn ngữ Việt/Anh cho toàn bộ giao diện; ngày giờ và số theo ngôn ngữ (`17,5`, `dd/MM/yyyy`) | | |
| APP-07 | Giao diện sáng/tối theo bộ token đã duyệt; lần đầu theo hệ điều hành, sau đó nhớ lựa chọn | | |
| APP-08 | Hình nền thời tiết động: luôn bật ở trang đăng nhập, tùy chọn trong app (mặc định tắt) | | |
| APP-09 | Thông báo nổi (toast), hộp xác nhận chung cho mọi thao tác xóa và ghi lên hệ thống ngoài | | |
| APP-10 | Trạng thái trống / đang tải / lỗi theo một kiểu chung trên mọi danh sách | | |
| APP-11 | File tĩnh được nén và lưu đệm dài (đổi phiên bản thì tự tải bản mới); Kendo chỉ nạp ở trang cần | | |
| APP-12 | Nhật ký hoạt động ghi lại các thao tác quan trọng (tạo, sửa, xóa, hoàn thành, dùng, ghi Jira) | | |

### Đã biết / chưa kiểm tra

- Các tùy chọn hiển thị (ẩn panel Tổng quan, hình nền, cài đặt hẹn giờ, bộ lọc đã nhớ) chỉ lưu trên từng trình duyệt; đổi máy là mất.
- Phiên Jira nằm trong bộ nhớ của app: khởi động lại app hoặc IIS tái chạy là phải đăng nhập Jira lại.
- Chưa thử trên Safari, Windows/IIS Express, máy Mac thật (phím ⌘).

### Đề xuất chung

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| APP-P01 | **Sao lưu và khôi phục toàn bộ dữ liệu cá nhân** thành một file (ghi chú, việc, liên kết, prompt, phiên tập trung) | Chuyển máy, phòng khi hỏng dữ liệu; hiện chỉ xuất được riêng prompt và liên kết | M | | | |
| APP-P02 | **Lưu tùy chọn theo tài khoản** thay vì theo trình duyệt (ẩn panel, hình nền, cài đặt hẹn giờ, sắp xếp) | Đổi máy hay trình duyệt vẫn giữ cài đặt | M | | | |
| APP-P03 | **Thùng rác chung**: ghi chú, liên kết, prompt xóa mềm và có "Hoàn tác" như việc cần làm | Tránh mất dữ liệu do bấm nhầm | M | | | |
| APP-P04 | **Nhắc việc bằng thông báo trình duyệt**: việc cần làm đến hạn, subtask Jira quá hạn, chưa log đủ giờ cuối ngày | Không phải mở app mới biết | M | | | |
| APP-P05 | **Thẻ (tag) dùng chung** cho ghi chú, việc cần làm, liên kết, prompt; lọc theo thẻ trên từng trang và trong Ctrl+K | Gom mọi thứ của một dự án lại một chỗ | L | | | |
| APP-P06 | **Ngày lễ và ngày nghỉ**: một danh sách dùng chung cho "giờ cần log" (Jira Dashboard, Tổng quan) và Sắp xếp tự động | Số giờ thiếu và lịch đề xuất đúng hơn vào tuần có lễ | M | | | |
| APP-P07 | **Giữ phiên Jira khi app khởi động lại** (lưu mã hóa, có hạn) | Không phải đăng nhập Jira lại sau mỗi lần cập nhật app | M | | | |
| APP-P08 | **Số phiên bản và trang "Có gì mới"** trong menu avatar | Biết bản đang chạy và thay đổi của mỗi lần cập nhật | S | | | |
| APP-P09 | **Mật độ hiển thị** gọn (36px) / thoáng (44px) cho mọi danh sách | Token đã có sẵn cả hai cỡ | S | | | |
| APP-P10 | **Thêm lệnh cho Ctrl+K**: mở issue Jira theo mã (gõ `ABC-123`), tìm trong issue Jira, mở kết nối gần đây ở So sánh DB / Bắt SQL | Một ô để đi tới mọi thứ | M | | | |
| APP-P11 | **Kiểm tra tự động trên mỗi PR** (build + `tests/ui`), siết ngưỡng so ảnh (hiện 2% là quá rộng) — mục 6 của GĐ6, đang hoãn | Phát hiện lỗi trước khi merge | M | | | |
| APP-P12 | **Dọn mã** — mục 5 của GĐ6: tách `site.css` (~2.900 dòng) theo màn hình, tách `Components/Index.cshtml` (~1.700 dòng), thêm stylelint | Sửa nhanh và ít đụng nhau hơn | M | | | |

### Đề xuất thêm của bạn

-
-

---

## 2. Tổng quan (DASH)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| DASH-01 | Lời chào theo buổi kèm tên và ngày hôm nay | | |
| DASH-02 | "Tùy chỉnh": ẩn/hiện từng panel (nhớ trên trình duyệt) | | |
| DASH-03 | Hàng số liệu (mỗi ô là link): việc quá hạn, hạn hôm nay, phút tập trung hôm nay/tuần này, giờ log Jira hôm nay so với 8h (khi có phiên Jira) | | |
| DASH-04 | Panel Việc cần làm: thêm nhanh (tên + hạn hôm nay/không hạn), chia nhóm Quá hạn / Hôm nay / Sắp tới / Chưa có hạn, tích để xong ngay | | |
| DASH-05 | Panel Ghi chú: 5 ghi chú (ghim lên đầu), bấm mở đúng ghi chú, "Ghi chú mới" trong popup | | |
| DASH-06 | Panel Liên kết nhanh dạng ô (ghim trước, rồi mở nhiều) | | |
| DASH-07 | Panel Prompt: sao chép một chạm | | |
| DASH-08 | Panel Tập trung: phút hôm nay/tuần này, nút "Bắt đầu 25 phút" | | |
| DASH-09 | Panel Lịch sử: 5 hoạt động gần nhất, "Xem tất cả" | | |

### Đã biết / chưa kiểm tra

- Bấm liên kết trên ô Tổng quan chưa được đếm lượt mở (trang Liên kết nhanh và Ctrl+K thì có).
- Giờ log Jira hôm nay mới thử với dữ liệu giả.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| DASH-P01 | Đếm lượt mở khi bấm liên kết trên Tổng quan | Số "mở nhiều nhất" đúng hơn | S | | | |
| DASH-P02 | Panel **Jira hôm nay** (tài khoản Jira): subtask hạn hôm nay, quá hạn, giờ còn phải log | Mở app là biết việc Jira cần làm | M | | | |
| DASH-P03 | Kéo thả để đổi thứ tự và độ rộng panel | Mỗi người tự sắp theo thói quen | M | | | |
| DASH-P04 | Panel **Tuần này**: biểu đồ nhỏ 7 ngày (việc xong, phút tập trung, giờ log Jira) | Thấy nhịp làm việc trong tuần | M | | | |
| DASH-P05 | Nút **"Tóm tắt cho daily"**: hôm qua đã làm / hôm nay sẽ làm, lấy từ việc xong, subtask Jira đã log và việc hạn hôm nay; sao chép một chạm | Đỡ mất công soạn báo cáo daily | M | | | |

### Đề xuất thêm của bạn

-

---

## 3. Lịch sử hoạt động (ACT)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| ACT-01 | Danh sách hoạt động (mới nhất trước), 30 dòng mỗi trang | | |
| ACT-02 | Lọc theo loại (ghi chú, việc, liên kết, prompt, phiên tập trung, Jira…) | | |
| ACT-03 | Trạng thái trống | | |

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| ACT-P01 | Lọc theo khoảng ngày và tìm theo nội dung | Tìm lại "hôm đó mình đã sửa gì" | S | | | |
| ACT-P02 | Bấm một dòng để mở đúng mục (nếu còn) | Đi thẳng tới thứ vừa sửa | M | | | |
| ACT-P03 | Gom theo ngày, có tổng số hoạt động mỗi ngày | Dễ đọc hơn danh sách dài | S | | | |
| ACT-P04 | Tự dọn nhật ký cũ hơn N tháng (có cài đặt) | Database không phình mãi | S | | | |

### Đề xuất thêm của bạn

-

---

## 4. Ghi chú (NOTE)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| NOTE-01 | 9 loại ghi chú: Văn bản, JavaScript, CSS, JSON, XML, Liên kết, Máy chủ, Nhóm hỗn hợp, Checklist (mỗi loại một màu) | | |
| NOTE-02 | Ghi chú con: một ghi chú làm nhóm, các ghi chú con cùng loại hoặc mỗi con một loại (nhóm hỗn hợp) | | |
| NOTE-03 | Loại Máy chủ: host, cổng, tài khoản, database, mật khẩu **được mã hóa**; hiện/ẩn, sao chép mật khẩu, sao chép chuỗi kết nối SQL Server; nhiều kết nối trong một nhóm (PROD, UAT…) | | |
| NOTE-04 | Loại Liên kết: nhiều dòng tên + địa chỉ, mở và sao chép từng dòng | | |
| NOTE-05 | Loại Checklist: mỗi dòng có trạng thái (Chưa làm, Đang làm, Chờ duyệt, Đang chờ, Ưu tiên cao, Xong, Bỏ), tiến độ "x/y xong" | | |
| NOTE-06 | Công cụ theo loại: định dạng lại và kiểm tra JSON, kiểm tra XML, sao chép, tải về file | | |
| NOTE-07 | Mô tả, thẻ (nhập cách nhau dấu phẩy), ghim lên đầu | | |
| NOTE-08 | Tìm không phân biệt dấu (cả trong ghi chú con), chip lọc theo loại, sắp xếp Mới sửa / Tên A–Z / Mới tạo (nhớ lựa chọn) | | |
| NOTE-09 | Danh sách bên trái, nội dung bên phải; Ctrl+S để lưu, cảnh báo khi rời trang mà chưa lưu | | |
| NOTE-10 | Phím: ↑ ↓ đi giữa ghi chú, Enter mở, `/` tìm, `N` tạo mới | | |
| NOTE-11 | Xóa có xác nhận (ghi rõ số ghi chú con sẽ bị xóa theo) | | |

### Đã biết / chưa kiểm tra

- Ảnh mốc `tests/ui` của trang chỉ có trạng thái trống; danh sách có dữ liệu được kiểm tra bằng script riêng.
- Xóa ghi chú là xóa hẳn, không hoàn tác được.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| NOTE-P01 | Loại **Markdown** có xem trước | Ghi chú dài có tiêu đề, danh sách, bảng | M | | | |
| NOTE-P02 | **Tô màu cú pháp** cho JS, CSS, JSON, XML (và SQL) | Đọc code dễ hơn; cần thư viện giấy phép BSD/MIT, ghi vào `THIRD-PARTY-NOTICES.md` | M | | | |
| NOTE-P03 | Loại **SQL** có định dạng lại câu lệnh | Hay lưu câu truy vấn | M | | | |
| NOTE-P04 | **Lịch sử phiên bản**: xem và khôi phục bản trước | Lỡ sửa sai vẫn lấy lại được | M | | | |
| NOTE-P05 | **Xóa mềm + Hoàn tác** (xem APP-P03) | Tránh mất ghi chú do bấm nhầm | S | | | |
| NOTE-P06 | Chip **lọc theo thẻ** | Thẻ hiện chỉ dùng được qua ô tìm | S | | | |
| NOTE-P07 | Ghi chú Máy chủ: nút **"Kiểm tra kết nối"** và **"Mở ở So sánh DB / Bắt SQL"** điền sẵn kết nối | Không phải gõ lại kết nối | M | | | |
| NOTE-P08 | Chuyển dòng checklist trong ghi chú thành **Việc cần làm** | Nối ghi chú với danh sách việc | S | | | |
| NOTE-P09 | **Xuất/nhập ghi chú** (JSON, hoặc Markdown cho ghi chú văn bản) | Sao lưu, chuyển máy | M | | | |
| NOTE-P10 | **Đính kèm ảnh/file** vào ghi chú | Lưu ảnh chụp lỗi, file cấu hình | L | | | |

### Đề xuất thêm của bạn

-

---

## 5. Việc cần làm (TODO)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| TODO-01 | Chia nhóm Quá hạn / Hôm nay / Sắp tới / Chưa có hạn / Đã xong; chip lọc có số đếm; tìm không dấu | | |
| TODO-02 | Sắp xếp theo hạn / mới tạo / ưu tiên cao (nhớ lựa chọn) | | |
| TODO-03 | Thêm nhanh ngay trên danh sách (tên + hạn hôm nay/không hạn, Enter) | | |
| TODO-04 | Thêm/sửa trong popup: tên, mô tả, hạn, mức ưu tiên Cao/Thường/Thấp | | |
| TODO-05 | Đổi hạn nhanh trên từng dòng: Hôm nay / Ngày mai / Thứ Hai tuần sau / Bỏ hạn / Chọn ngày | | |
| TODO-06 | "Trễ N ngày", "Xong dd/MM/yyyy", tiến độ "Tuần này: xong X/Y" | | |
| TODO-07 | Xóa mềm và "Hoàn tác" sau khi xóa hoặc đánh dấu xong; xóa hẳn sau một ngày | | |
| TODO-08 | "Dọn việc đã xong" có xác nhận và hoàn tác | | |
| TODO-09 | Chế độ "Chọn nhiều": đánh dấu xong hoặc xóa nhiều việc một lần | | |
| TODO-10 | Phím: ↑ ↓ / J K, X xong, E hoặc Enter sửa, Delete xóa, Space chọn, `/`, `N` | | |
| TODO-11 | Mở từ Ctrl+K thì tô sáng đúng việc | | |

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| TODO-P01 | **Việc lặp lại** (hằng ngày, hằng tuần, ngày X hằng tháng) | Việc định kỳ không phải tạo lại | M | | | |
| TODO-P02 | **Việc con / checklist** trong một việc | Chia việc lớn thành bước nhỏ | M | | | |
| TODO-P03 | **Hạn có giờ** và nhắc trước giờ (xem APP-P04) | Việc có giờ hẹn cụ thể | M | | | |
| TODO-P04 | **Gắn mã issue Jira** vào việc (link mở Jira) | Nối việc cá nhân với subtask | S | | | |
| TODO-P05 | Kéo thả **sắp thứ tự thủ công** trong một nhóm | Tự xếp việc làm trước | M | | | |
| TODO-P06 | **Dự án / danh sách riêng** (nhiều danh sách việc) | Tách việc công ty, việc cá nhân | M | | | |
| TODO-P07 | **Thống kê**: số việc xong theo tuần, việc trễ hạn nhiều nhất | Nhìn lại cách làm việc | S | | | |

### Đề xuất thêm của bạn

-

---

## 6. Hẹn giờ tập trung (FOCUS)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| FOCUS-01 | Đồng hồ chạy trên mọi trang (chuyển trang, tải lại vẫn chạy), ô đếm trên thanh tiêu đề, giờ trên tên tab | | |
| FOCUS-02 | Chu kỳ tập trung → nghỉ ngắn → nghỉ dài (mặc định 25/5/15, nghỉ dài sau 4 phiên), dòng "Phiên 2/4" | | |
| FOCUS-03 | Vòng tiến độ, Bắt đầu / Tạm dừng / Tiếp tục / Dừng (hỏi lưu số phút đã làm hay bỏ) / Đặt lại | | |
| FOCUS-04 | Mức nhanh 15/25/50 phút và ô nhập số phút | | |
| FOCUS-05 | Gắn phiên với một việc cần làm đang mở và ghi chú | | |
| FOCUS-06 | Hết giờ tự lưu phiên; mất kết nối thì giữ lại và tự gửi lại | | |
| FOCUS-07 | Âm báo khoảng 7 giây (bấm hoặc gõ phím để tắt), thông báo trình duyệt nếu cho phép | | |
| FOCUS-08 | Tùy chọn: thời gian nghỉ, số phiên trước nghỉ dài, mục tiêu phút mỗi ngày, âm báo, thông báo | | |
| FOCUS-09 | Số liệu: phút hôm nay/tuần này, thanh mục tiêu ngày, biểu đồ 7 ngày | | |
| FOCUS-10 | Lịch sử 14 ngày theo ngày, sửa ghi chú, xóa (có xác nhận) | | |
| FOCUS-11 | Phím: Space bắt đầu/tạm dừng, R đặt lại | | |

### Đã biết / chưa kiểm tra

- Âm báo và thông báo chưa nghe/thấy trên máy thật.
- Trình duyệt có thể làm chậm bộ đếm ở tab nền, nên việc lưu có thể trễ vài giây.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| FOCUS-P01 | Gắn phiên với **subtask Jira** và **ghi số phút lên Jira** (worklog, có xác nhận) | Tập trung xong là đã log giờ | L | | | |
| FOCUS-P02 | **Tự chuyển** sang nghỉ / phiên kế tiếp (tùy chọn) | Không phải bấm mỗi lần | S | | | |
| FOCUS-P03 | Chọn **âm báo và âm lượng** | Âm báo hợp tai hơn | S | | | |
| FOCUS-P04 | **Thống kê theo tháng và theo việc** (việc nào tốn nhiều phút nhất) | Biết thời gian đi đâu | M | | | |
| FOCUS-P05 | **Mục tiêu tuần** và chuỗi ngày đạt mục tiêu | Tạo thói quen | S | | | |

### Đề xuất thêm của bạn

-

---

## 7. Liên kết nhanh (LINK)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| LINK-01 | Danh sách chia khối "Đã ghim" rồi từng nhóm (có số đếm), "Chưa có nhóm" ở cuối | | |
| LINK-02 | Thêm/sửa trong popup; tự thêm `https://`; báo địa chỉ sai; cảnh báo địa chỉ trùng; tiêu đề trống thì lấy tên miền; ô Nhóm gợi ý nhóm có sẵn | | |
| LINK-03 | Tìm không dấu trên tên, địa chỉ, nhóm; chip theo nhóm; sắp xếp Tự xếp / Tên A–Z / Mở nhiều nhất | | |
| LINK-04 | Đếm số lần mở và lúc mở cuối; sao chép địa chỉ; ghim | | |
| LINK-05 | Kéo thả hoặc Alt + ↑ ↓ để đổi thứ tự trong một khối | | |
| LINK-06 | Nhập file bookmark của Chrome/Edge/Firefox (thư mục thành nhóm, bỏ địa chỉ đã có); xuất ra cùng định dạng | | |
| LINK-07 | Phím: ↑ ↓, Enter mở, C sao chép, E sửa, `/`, `N` | | |

### Đã biết / chưa kiểm tra

- Chưa thử file bookmark thật xuất từ từng trình duyệt (đã thử file mẫu).
- Không tải favicon từ trang ngoài (cố ý, vì quyền riêng tư); mỗi dòng có ô chữ cái đầu.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| LINK-P01 | **Mở cả nhóm** trong các tab mới | Mở bộ trang làm việc buổi sáng một lần | S | | | |
| LINK-P02 | **Ghi chú ngắn** cho từng liên kết (tài khoản dùng, mục đích) | Nhớ link dùng để làm gì | S | | | |
| LINK-P03 | **Kiểm tra link chết** (server thử truy cập, báo link lỗi) | Dọn danh sách cũ | M | | | |
| LINK-P04 | **Favicon lưu nội bộ** (tải một lần, lưu trong app; có thể tắt) | Nhận ra link nhanh hơn; đổi lại app phải gọi ra trang ngoài | M | | | |
| LINK-P05 | **Nhóm lồng nhau** (thư mục con) | Giữ đúng cấu trúc bookmark lớn | M | | | |
| LINK-P06 | Phím **1–9** mở nhanh liên kết ghim thứ 1–9 | Mở link hay dùng không cần chuột | S | | | |

### Đề xuất thêm của bạn

-

---

## 8. Không gian prompt (PROMPT)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| PROMPT-01 | Danh sách theo mẫu chung: số liệu, tìm không dấu, chip theo nhóm (nhóm trống tự ẩn), sắp xếp Tên / Dùng nhiều / Mới dùng | | |
| PROMPT-02 | Tạo/sửa trong popup: tên, nhóm, mô tả, nội dung, biến đầu vào, định dạng đầu ra; đếm ký tự, dòng, biến | | |
| PROMPT-03 | Xem đầy đủ ở panel bên phải | | |
| PROMPT-04 | Sao chép (tự tính một lần dùng); "Điền biến rồi sao chép" có xem trước | | |
| PROMPT-05 | Cảnh báo "Biến lệch" (dùng mà chưa khai báo, hoặc khai báo mà không dùng) | | |
| PROMPT-06 | Ghim, nhân bản, xóa (có xác nhận) | | |
| PROMPT-07 | Xuất toàn bộ ra JSON, nhập lại (bỏ prompt trùng) | | |
| PROMPT-08 | 7 prompt mẫu theo quy ước thiết kế (gồm "Màn hình mới"); **"Khôi phục prompt mẫu"** để thêm prompt thiếu hoặc đưa prompt về bản mẫu mới | | |
| PROMPT-09 | Phím: ↑ ↓, Enter xem, C sao chép, E sửa, `/`, `N` | | |

### Đã biết / chưa kiểm tra

- Tài khoản đang có giữ prompt mẫu cũ cho tới khi bấm "Khôi phục prompt mẫu".
- Chưa thử file JSON không do ToolboxWeb xuất ra.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| PROMPT-P01 | **Nhóm do người dùng tự đặt** (hiện cố định 7 nhóm) | Xếp theo dự án hay công cụ AI | M | | | |
| PROMPT-P02 | **Nhớ giá trị đã điền** cho từng biến ở lần trước | Điền lại nhanh | S | | | |
| PROMPT-P03 | Biến có **giá trị mặc định** hoặc **danh sách chọn** (`{{env: dev, uat, prod}}`) | Ít gõ, ít sai | M | | | |
| PROMPT-P04 | **Lịch sử phiên bản** của prompt | So sánh và lấy lại bản cũ | M | | | |
| PROMPT-P05 | **Xem trước dạng Markdown** của nội dung | Prompt dài dễ đọc hơn | S | | | |
| PROMPT-P06 | **Chia sẻ prompt** cho tài khoản khác trong app | Cả nhóm dùng chung bộ prompt chuẩn | L | | | |

### Đề xuất thêm của bạn

-

---

## 9. Jira Dashboard (JDASH)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| JDASH-01 | Chọn kỳ Ngày / Tuần / Tháng / Năm, nút trước/sau, "Hôm nay"; ô tuần ghi khoảng ngày | | |
| JDASH-02 | Ô **Đã log so với ước tính**: chỉ subtask đã xong có ước tính (toàn bộ giờ log của subtask); dòng "Hỗ trợ" cho subtask không ước tính; nút (i) giải thích cách tính | | |
| JDASH-03 | Ô **Tiến độ subtask**: đã xong / tổng, so với kỳ trước | | |
| JDASH-04 | Ô **Giờ log trong kỳ**: so với giờ cần log tới hôm nay (ngày làm việc × 8h), "Thiếu Xh" / "Đủ giờ", tăng/giảm so với kỳ trước | | |
| JDASH-05 | Ô **Subtask quá hạn** (đỏ khi > 0), kèm số subtask đang mở chưa ước tính | | |
| JDASH-06 | Biểu đồ giờ log theo ngày (Năm: theo tháng), đường 8h, ngày thiếu tô cam, câu tóm tắt "N/M ngày đủ 8h" | | |
| JDASH-07 | Bảng subtask nhóm theo issue cha, chip Tất cả / Chưa xong / Quá hạn / Đã xong, sắp xếp, "Trễ N ngày", "Chưa ước tính" | | |
| JDASH-08 | Khối JQL đang dùng (xem, sao chép) | | |
| JDASH-09 | Trên điện thoại bấm "+" xem các cột bị ẩn | | |

### Đã biết / chưa kiểm tra

- Mới kiểm tra với dữ liệu Jira giả; chủ dự án kiểm tra cuối trên Jira thật.
- Ngày lễ chưa được trừ khỏi số giờ cần log.
- Dòng "N subtask đang mở chưa ước tính" chưa quyết: với cách tính mới, subtask hỗ trợ không ước tính là bình thường.
- Mỗi lần tải gọi Jira 2 lần (kỳ này và kỳ trước).

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| JDASH-P01 | Bỏ hoặc đổi dòng "N subtask đang mở chưa ước tính" thành câu trung tính ("N subtask hỗ trợ đang mở") | Hết mâu thuẫn với cách tính mới | S | | | |
| JDASH-P02 | Trừ **ngày lễ / nghỉ phép** (xem APP-P06) | "Thiếu Xh" đúng vào tuần có lễ | M | | | |
| JDASH-P03 | **Biểu đồ 2 màu**: phần việc có ước tính và phần hỗ trợ trong cột mỗi ngày (đã đề xuất 23/10, chưa chọn) | Thấy ngày nào bị hỗ trợ chiếm hết | S | | | |
| JDASH-P04 | **Bấm một cột** của biểu đồ để lọc bảng theo ngày đó | Xem ngay đã log vào việc gì | S | | | |
| JDASH-P05 | **Xuất Excel** bảng subtask (dùng lại SheetJS của tab Tìm kiếm) | Gửi báo cáo | S | | | |
| JDASH-P06 | **Ghi giờ (worklog) lên Jira** ngay từ bảng hoặc biểu đồ (có xác nhận) | Log bù ngày thiếu không cần mở Jira | L | | | |
| JDASH-P07 | **Xu hướng 8 tuần**: giờ log, tỉ lệ hỗ trợ, độ chính xác ước tính | Thấy chiều hướng, không chỉ một kỳ | M | | | |

### Đề xuất thêm của bạn

-

---

## 10. Công việc Jira – Tìm kiếm (JSEARCH)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| JSEARCH-01 | Hai chế độ: **Bộ lọc** (người thực hiện, hạn từ/đến, trạng thái, dự án, mã issue, người báo cáo) và **JQL** (bộ lọc Jira đã lưu, xuống dòng theo điều kiện, Ctrl+Enter) | | |
| JSEARCH-02 | Tìm không tải lại trang; form thu thành một dòng tóm tắt kèm "Sửa bộ lọc" | | |
| JSEARCH-03 | Bộ lọc nằm trên URL và được nhớ; mở lại tab là tự tìm lại | | |
| JSEARCH-04 | Bảng nhóm theo issue cha, sắp xếp, "Đã log" (rê chuột xem từng ngày), "Chưa ước tính", "Trễ N ngày"; 50 dòng mỗi trang; báo khi Jira cắt ở 200 kết quả | | |
| JSEARCH-05 | Chip Tất cả / Chưa xong / Quá hạn / Đã xong | | |
| JSEARCH-06 | Xuất Excel đúng những dòng đang lọc và sắp xếp | | |
| JSEARCH-07 | Trạng thái đang tìm, lỗi (Thử lại), không có kết quả, lỗi nhập | | |

### Đã biết / chưa kiểm tra

- File Excel mới kiểm tra nội dung bằng script, chưa mở bằng Excel.
- Tối đa 200 kết quả một lần tìm.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| JSEARCH-P01 | **Lưu bộ lọc trong ToolboxWeb** (đặt tên, gọi lại một chạm) | Bộ lọc hay dùng không phải nhập lại | S | | | |
| JSEARCH-P02 | **Chọn cột** hiện/ẩn, nhớ lựa chọn | Mỗi người xem cột mình cần | S | | | |
| JSEARCH-P03 | Lọc thêm theo **sprint, epic, nhãn (label), ưu tiên** | Tìm sát hơn mà không cần viết JQL | S | | | |
| JSEARCH-P04 | **Lấy hết kết quả** bằng phân trang qua API (bỏ giới hạn 200) | Tìm theo dự án lớn | M | | | |
| JSEARCH-P05 | **Sửa nhanh hạn** trong bảng, ghi lên Jira bằng luồng xác nhận chung | Đổi hạn ngay sau khi tìm | M | | | |

### Đề xuất thêm của bạn

-

---

## 11. Công việc Jira – Quản lý công việc (JBOARD)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| JBOARD-01 | Bảng Kanban theo ngày (Tuần / Tháng), cột "Hôm nay", Thứ 7/CN hẹp lại khi trống | | |
| JBOARD-02 | Thanh tải mỗi ngày "6h / 8h" (đã log + ước tính còn lại), vượt 8h thì báo đỏ | | |
| JBOARD-03 | Thẻ: quá hạn có vạch đỏ và "Trễ N ngày"; đã đổi hạn có viền nét đứt "19/10 → 21/10"; việc xong lùi xuống nền xám | | |
| JBOARD-04 | Đổi hạn bằng kéo thả, nút lịch trên thẻ, hoặc bàn phím (Space nhấc, ← → đổi ngày, Enter thả, Esc hủy) | | |
| JBOARD-05 | Chip Tất cả / Chưa xong / Quá hạn; "Ẩn việc đã xong", "Ẩn cuối tuần" (nhớ lựa chọn); tổng tuần/tháng: đã log, còn ước tính, công suất | | |
| JBOARD-06 | Chế độ Tháng: ô lịch gọn, bấm mở bảng bên với đủ thẻ của ngày | | |
| JBOARD-07 | Thanh "N thay đổi hạn chưa ghi lên Jira": Bỏ các thay đổi / **Ghi lên Jira** (xác nhận liệt kê từng issue hạn cũ → hạn mới, báo kết quả từng dòng, ghi nhật ký) | | |
| JBOARD-08 | Điện thoại: dải 7 ngày, xem từng ngày | | |

### Đã biết / chưa kiểm tra

- Mới kiểm tra với dữ liệu giả (gồm cả trường hợp Jira từ chối một issue).

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| JBOARD-P01 | **Ghi giờ (worklog)** ngay từ thẻ (có xác nhận) | Log giờ khi đang xem lịch tuần | L | | | |
| JBOARD-P02 | **Đổi trạng thái** subtask từ thẻ (To Do → In Progress → Done) | Không phải mở Jira cho thao tác nhỏ | M | | | |
| JBOARD-P03 | **Gợi ý cân tải**: chỉ ra ngày quá tải và ngày còn trống, đề xuất dời | Chia việc đều trong tuần | M | | | |
| JBOARD-P04 | Hiện **ngày lễ / nghỉ phép** trên lịch (xem APP-P06) | Không xếp việc vào ngày nghỉ | S | | | |
| JBOARD-P05 | **Xem việc của người khác** trong nhóm (nếu tài khoản Jira có quyền) | Trưởng nhóm theo dõi tải từng người | L | | | |

### Đề xuất thêm của bạn

-

---

## 12. Công việc Jira – Sắp xếp tự động (JAUTO)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| JAUTO-01 | Luồng 3 bước: Chọn subtask → Xếp lịch → Lịch đề xuất | | |
| JAUTO-02 | Bước 1: bộ lọc (dự án, trạng thái, mã/tên, khoảng hạn), bảng có Ước tính / Đã log / Còn lại; cảnh báo subtask chưa ước tính (link mở Jira); nhớ bộ lọc | | |
| JAUTO-03 | Bước 2: ngày bắt đầu, số giờ mỗi ngày, bỏ cuối tuần, tính cả giờ của việc đã có hạn, "Xếp trước" một số subtask, thứ tự tiêu chí; nhớ lựa chọn | | |
| JAUTO-04 | Bước 3: xem dạng Bảng (nhóm theo ngày, "6h / 8h") hoặc Lịch (kéo thẻ sang ngày khác); "Hạn cũ → Hạn mới" | | |
| JAUTO-05 | Bỏ đề xuất / **Ghi lên Jira** (dùng chung hộp xác nhận và báo kết quả với tab Quản lý) | | |

### Đã biết / chưa kiểm tra

- Mới kiểm tra với dữ liệu giả (dữ liệu giả không lọc theo mã/tên subtask).

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| JAUTO-P01 | Bỏ qua **ngày lễ / nghỉ phép** khi xếp (xem APP-P06) | Lịch đề xuất khả thi hơn | M | | | |
| JAUTO-P02 | **Khóa ngày** (không xếp vào ngày đã kín họp) hoặc đặt số giờ riêng cho từng ngày | Sát lịch thật | M | | | |
| JAUTO-P03 | Xếp theo **độ ưu tiên Jira** | Việc gấp lên trước mà không phải chọn tay | S | | | |
| JAUTO-P04 | **Lưu nhiều cấu hình** (ví dụ "Tuần thường", "Tuần release") | Đổi kiểu xếp nhanh | S | | | |

### Đề xuất thêm của bạn

-

---

## 13. So sánh cấu trúc DB (SCMP)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| SCMP-01 | Hai form kết nối Nguồn / Đích dùng chung: kiểm tra kết nối, chọn database, menu "Gần đây" (không lưu mật khẩu), "Tin cậy chứng chỉ" mặc định tắt và có cảnh báo | | |
| SCMP-02 | Nút So sánh chỉ mở khi cả hai kết nối đã kiểm tra; báo khi Nguồn trùng Đích; "Đổi chiều" | | |
| SCMP-03 | Phạm vi, loại đối tượng, giảm nhiễu, mã ánh xạ trong "Tùy chọn nâng cao" (nhớ lựa chọn, có "Đặt lại mặc định") | | |
| SCMP-04 | So sánh bằng DacFx, có tiến trình và nút hủy | | |
| SCMP-05 | Danh sách khác biệt có bộ lọc, chọn/bỏ từng đối tượng đưa vào script, xem chi tiết khác biệt; J / K đi giữa đối tượng | | |
| SCMP-06 | Script có số dòng, tô đỏ lệnh xóa (DROP, DROP COLUMN), đếm lệnh, "Đến lệnh xóa kế" | | |
| SCMP-07 | Tải về hoặc sao chép script; có lệnh xóa thì phải xác nhận trong hộp liệt kê các lệnh | | |

### Đã biết / chưa kiểm tra

- Môi trường thử không có SQL Server: mọi lời gọi được giả lập; chưa chạy với DacFx thật trong lần làm lại này.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| SCMP-P01 | **Lưu cặp so sánh** (ví dụ "DEV → UAT dự án A") gồm hai kết nối và tùy chọn; không lưu mật khẩu | So sánh lặp lại một chạm | S | | | |
| SCMP-P02 | **Lịch sử so sánh**: thời điểm, số khác biệt, xem lại script | Theo dõi thay đổi qua các đợt release | M | | | |
| SCMP-P03 | **Xuất báo cáo khác biệt** (HTML/Excel) để gửi review | Review trước khi deploy | M | | | |
| SCMP-P04 | **So sánh dữ liệu** của bảng danh mục (cấu hình, tham số) giữa hai DB | Lệch dữ liệu cấu hình rất hay gặp | L | | | |
| SCMP-P05 | **Chạy script lên Đích** ngay trong app (xác nhận nêu từng lệnh, có giao dịch và ghi nhật ký) | Không phải mở SSMS; rủi ro cao nên cần bạn quyết | L | | | |

### Đề xuất thêm của bạn

-

---

## 14. Bắt câu lệnh SQL (SQLP)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| SQLP-01 | Kết nối dùng form chung (kiểm tra quyền, "Gần đây") | | |
| SQLP-02 | **Ghi phiên** bằng Extended Events: xác nhận trước khi ghi (nêu máy chủ, database, số phút tự dừng), đếm ngược tự dừng | | |
| SQLP-03 | **Xem nhanh**: các thủ tục chạy gần đây, xem định nghĩa | | |
| SQLP-04 | Bộ lọc nâng cao: người dùng, máy trạm, ứng dụng, thời gian tối thiểu, loại sự kiện; ngưỡng cam/đỏ | | |
| SQLP-05 | Kết quả mới nhất ở trên; chip Tất cả / Chậm / Lỗi; tìm; số liệu theo danh sách đang hiện | | |
| SQLP-06 | "Gộp theo đối tượng": số lần gọi, trung bình, chậm nhất, tổng, lỗi | | |
| SQLP-07 | Panel chi tiết bên phải; J / K đi giữa dòng | | |
| SQLP-08 | Tạm dừng cập nhật, "N dòng mới · Hiện ngay", xóa danh sách, che dữ liệu nhạy cảm, xuất kết quả | | |

### Đã biết / chưa kiểm tra

- Môi trường thử không có SQL Server: mọi lời gọi được giả lập; chưa chạy phiên Extended Events thật trong lần làm lại này.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| SQLP-P01 | **Lưu phiên ghi** để mở lại sau | Phân tích sau, so trước/sau khi tối ưu | M | | | |
| SQLP-P02 | **So sánh hai phiên** (đối tượng nào chậm đi, gọi nhiều hơn) | Đo hiệu quả của bản sửa | M | | | |
| SQLP-P03 | **Định dạng lại câu SQL** trong panel chi tiết, tô màu cú pháp | Câu dài dễ đọc | S | | | |
| SQLP-P04 | **Thay tham số** vào câu lệnh để chạy lại được (sinh câu `EXEC` đầy đủ) | Tái hiện lỗi nhanh | M | | | |
| SQLP-P05 | **Xem kế hoạch thực thi** (execution plan) của câu chậm | Tìm nguyên nhân chậm | L | | | |

### Đề xuất thêm của bạn

-

---

## 15. Tài khoản (ACC)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| ACC-01 | Đăng nhập 3 cách trên một thẻ: tài khoản ToolboxWeb, tài khoản Jira, đăng ký mới; "Ghi nhớ đăng nhập" | | |
| ACC-02 | Trang trước đăng nhập: logo, đồng hồ, thời tiết, đổi ngôn ngữ và giao diện, hình nền động | | |
| ACC-03 | Xác thực hai lớp bằng ứng dụng (mã QR, mã khôi phục, tắt/đặt lại) | | |
| ACC-04 | Hồ sơ: tên đăng nhập, email (chỉ xem), ảnh đại diện kéo thả (tối đa 5 MB), tùy chọn hình nền động | | |
| ACC-05 | Đổi mật khẩu (tài khoản Jira không có các tab mật khẩu và hai lớp) | | |
| ACC-06 | Dữ liệu cá nhân: tải về, xóa tài khoản (xóa mọi dữ liệu của mình và ảnh) | | |
| ACC-07 | Trang Khóa tài khoản, trang Không có quyền (có nút "Đăng nhập bằng Jira") | | |
| ACC-08 | Trang Chính sách riêng tư (nội dung hiện là câu mẫu) | | |

### Đã biết / chưa kiểm tra

- Chưa có máy chủ gửi email nên không có "Quên mật khẩu" và không đổi được email.
- Tài khoản ToolboxWeb không dùng được trang Jira; tài khoản Jira là một tài khoản riêng.
- Role `Developer` chưa có tác dụng vì app chưa bật roles.

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| ACC-P01 | **Kết nối Jira vào tài khoản ToolboxWeb** (một tài khoản dùng cả ghi chú lẫn Jira) | Không phải chọn giữa hai kiểu đăng nhập | M | | | |
| ACC-P02 | **Quên mật khẩu** và đổi email (cần cấu hình SMTP) | Tự lấy lại tài khoản | M | | | |
| ACC-P03 | **Tên hiển thị** riêng (khác tên đăng nhập) dùng trong lời chào và avatar | Thân thiện hơn | S | | | |
| ACC-P04 | **Các phiên đăng nhập đang mở**, đăng xuất khỏi mọi thiết bị | An toàn khi quên đăng xuất máy lạ | M | | | |
| ACC-P05 | Viết **nội dung thật cho trang Chính sách riêng tư** (dữ liệu nào lưu ở đâu, thời tiết dùng vị trí thế nào) | Trang hiện là câu mẫu | S | | | |
| ACC-P06 | **Bật roles** (Admin, Developer) và trang quản lý người dùng đơn giản | Mở trang dev ngoài Development, quản trị tài khoản | M | | | |

### Đề xuất thêm của bạn

-

---

## 16. Trang dành cho người phát triển (DEV)

### Chức năng đang có

| Mã | Chức năng | Đánh giá | Ghi chú của bạn |
|---|---|---|---|
| DEV-01 | Bảng mẫu component: tab Nền tảng (màu, chữ, khoảng cách, nút, ô nhập, nhãn, chip, dòng, trạng thái, thông báo, xác nhận, panel bên, icon) và các tab theo nhóm control | | |
| DEV-02 | Danh mục 68 component kèm giấy phép và trạng thái (Dùng được / Có sẵn / Phụ thuộc thương mại / Không có trong Kendo Core) | | |
| DEV-03 | Kendo Demo: 4 tab, danh sách mẫu, Window, Validator, ghi chú khi widget không có trong bộ Kendo | | |
| DEV-04 | Tabulator Demo: sửa trực tiếp, lưu theo lô, xóa có xác nhận, trạng thái tải/lỗi/trống | | |
| DEV-05 | Chỉ mở ở Development (nơi khác trả 404) | | |

### Đề xuất

| Mã | Đề xuất | Lợi ích | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| DEV-P01 | Chuyển các thẻ demo cũ còn chữ viết thẳng trong view sang khóa ngôn ngữ | Đồng bộ với phần còn lại | S | | | |
| DEV-P02 | Thêm mẫu **trang danh sách** và **luồng 3 bước** chạy được vào Bảng mẫu | Làm màn hình mới nhanh và đúng chuẩn | M | | | |

### Đề xuất thêm của bạn

-

---

## 17. Màn hình hoặc công cụ mới (NEW)

| Mã | Đề xuất | Nội dung chính | Công sức | Chọn | Ưu tiên | Ghi chú của bạn |
|---|---|---|---|---|---|---|
| NEW-01 | **Hộp công cụ cho dev** (nhóm DEV hoặc CÔNG CỤ) | Định dạng/kiểm tra JSON, XML, SQL; Base64; mã hóa URL; giải mã JWT; tạo GUID; đổi Unix time ↔ ngày giờ; so sánh hai đoạn văn bản (diff); thử biểu thức regex. Tất cả chạy trong trình duyệt, không gửi dữ liệu ra ngoài | M–L | | | |
| NEW-02 | **Lịch tổng hợp** | Một lịch tháng/tuần gồm việc cần làm, hạn subtask Jira, phiên tập trung (FullCalendar đã có sẵn trong app) | M | | | |
| NEW-03 | **Báo cáo tuần / timesheet** | Giờ log Jira theo ngày và theo issue, việc đã xong, phút tập trung; xuất Excel hoặc sao chép văn bản gửi báo cáo | M | | | |
| NEW-04 | **Ghi giờ Jira (worklog)** dùng chung | Một hộp "Log giờ" dùng ở Jira Dashboard, Quản lý công việc, Hẹn giờ (JDASH-P06, JBOARD-P01, FOCUS-P01); xác nhận trước khi ghi | L | | | |
| NEW-05 | **Tổng quan nhóm (Jira)** | Tải công việc và giờ log của từng thành viên (cần quyền Jira) | L | | | |
| NEW-06 | **Trang Cài đặt** | Gom mọi tùy chọn (hiển thị, ngôn ngữ, giờ làm mỗi ngày, ngày lễ, mục tiêu tập trung, nhắc việc) về một chỗ, lưu theo tài khoản (APP-P02, APP-P06) | M | | | |

### Đề xuất thêm của bạn

-

---

## 18. Đề xuất thêm của bạn (chung)

Ghi ở đây những ý không thuộc màn hình nào, hoặc vấn đề bạn gặp khi dùng hằng ngày.

-
-
-

---

## 19. Tổng hợp và chia giai đoạn

*Mình sẽ điền mục này sau khi bạn đánh giá và push file lên: gom các dòng được chọn, xếp theo ưu tiên của bạn, công sức và phụ thuộc giữa các việc (ví dụ APP-P06 Ngày lễ đi trước JDASH-P02 và JAUTO-P01; NEW-04 Ghi giờ Jira đi trước JDASH-P06, JBOARD-P01, FOCUS-P01).*

| Giai đoạn | Việc | Ghi chú |
|---|---|---|
| | | |
