# Bảng thuật ngữ

Dùng khi viết hoặc dịch chữ hiển thị (`Localization/vi-VN.json`, `en-US.json`). Quy tắc của `AGENTS.md`: không trộn hai thứ tiếng trong một nhãn. Các từ ở mục "Giữ nguyên" là **tên riêng của công cụ**, không tính là trộn tiếng.

Chốt ngày 21/10/2026 ở bước 4.1. Chủ dự án đổi được; khi đổi thì sửa cả bảng này và các khóa ngôn ngữ.

## Jira

### Giữ nguyên (tên trong Jira)

| Từ | Ghi chú | Ví dụ tiếng Việt |
|---|---|---|
| issue | Công việc cha trong Jira | "Mã issue", "Issue cha", "Issue giao cho tôi" |
| subtask | Việc con của một issue | "Danh sách subtask", "Mã subtask" |
| JQL | Ngôn ngữ truy vấn của Jira | "Bật JQL nâng cao" |
| log | Ghi giờ làm vào Jira (worklog) | "Số giờ đã log", "Ngày đã log" |
| Sub-task Type | Trường tùy chỉnh: loại công việc của subtask (Developing, Meeting…) | "Cột Sub-task Type", "Giờ đã log theo loại công việc" |
| Version | Phiên bản (đợt phát hành) của dự án | "Dự án lấy Version" |

Tên trạng thái lấy từ Jira ("To Do", "In Progress", "Done"…) giữ nguyên và đặt trong ngoặc kép khi nằm trong câu.

### Việt hóa

| Tiếng Anh | Tiếng Việt |
|---|---|
| Status | Trạng thái |
| Due date | Hạn |
| Estimate | Ước tính |
| Logged / Log time | Đã log |
| Summary | Tiêu đề |
| Assignee | Người thực hiện |
| Reporter | Người báo cáo |
| Project | Dự án |
| Type | Loại |
| Parent | Issue cha |
| Filter (saved filter) | Bộ lọc (bộ lọc đã lưu) |
| Current user / current Jira user | Bạn, tôi ("Issue giao cho tôi") |
| Jira session | Phiên đăng nhập Jira |
| Board | Bảng việc |
| Catalog (reference data synced from Jira) | Danh mục |
| Sync | Đồng bộ |
| Priority | Độ ưu tiên |
| Resolution | Kết quả xử lý |
| Status category | Nhóm trạng thái (chưa làm / đang làm / xong) |
| Waiting status | Trạng thái chờ |

## Công cụ DB (So sánh cấu trúc DB, Bắt câu lệnh SQL)

Người dùng là lập trình viên, nên tên đối tượng của SQL Server **giữ nguyên tiếng Anh**: Database, Schema, Table, View, Stored procedure, Function, Trigger, Index, Collation, Identity, Full-text, Script, User, Role. Phần còn lại của câu viết tiếng Việt.

## Tệp & Thư mục

| Tiếng Anh | Tiếng Việt |
|---|---|
| Root folder | Thư mục gốc |
| Tree | Cây thư mục |
| Read-only | Chỉ đọc |
| Recycle Bin | Thùng rác |
| Encoding | Mã hóa ký tự |
| Line ending (CRLF/LF) | Kiểu xuống dòng |
| Open with the default program | Mở bằng chương trình mặc định |
| Show in Explorer | Hiện trong Explorer |

## Chung

| Không dùng | Dùng |
|---|---|
| source, bundle, input file, file rác, grid (trong chữ cho người dùng) | mô tả việc người dùng thấy: "tệp", "bảng", "chưa lưu" |
| Save khi chưa lưu gì | Áp dụng |
| Task (cho việc cá nhân) | Việc cần làm |

## Định dạng

Theo `DESIGN_DECISIONS.md` 2.I: `vi-VN` dùng `dd/MM/yyyy`, `HH:mm`, `17,5`, giờ `17,5h`; `en-US` dùng `MM/dd/yyyy`, `h:mm AM`, `17.5`. Phía server dùng `Extensions/DisplayFormatExtensions.cs`, phía trình duyệt dùng `Toolbox.format` (`Content/js/format.js`). Múi giờ hiển thị là `App:TimeZone` (mặc định `Asia/Ho_Chi_Minh`, xem `Infrastructure/Time/AppClock.cs`).
