# Toolkit — logo và tên

Tên sản phẩm hiển thị cho người dùng là **Toolkit** (đổi từ ToolboxWeb ngày 14/11/2026). Mã nguồn giữ tên cũ: repo `ToolboxWeb`, project `src/ToolboxWeb.Web`, namespace `ToolboxWeb.Web`, tiền tố CSS `tbx-`, biến JS `window.Toolbox`, khóa `localStorage` `toolbox.*`.

## File

| File | Dùng ở đâu |
|---|---|
| `source/` | Bộ file gốc chủ dự án đưa vào (giữ nguyên để đối chiếu) |
| `toolkit-logo.svg` | Logo đầy đủ: khối tối, viền gradient, chữ TK. Dùng ở cỡ từ 64px trở lên |
| `toolkit-mark.svg` | Bản cho cỡ nhỏ (16–48px): khối tràn viền, không viền, nét dày hơn. Bản sao ở `src/ToolboxWeb.Web/Content/brand/toolkit-mark.svg` (sidebar, trang đăng nhập, favicon SVG) |
| `toolkit-app-icon.svg` | Icon nền đặc cho iOS, Android, PWA, bộ cài Windows (hệ thống tự bo góc) |
| `toolkit.ico` | Icon bộ cài Windows (16/32/48/256) |
| `export/` | PNG xuất từ các SVG trên: `apple-touch-icon-180`, `icon-192`, `icon-512`, `toolkit-logo-512`, `og-image-1200x630` |

Trong app (`Content/`): `favicon.ico` (16/32/48 từ `toolkit-mark.svg`), `brand/toolkit-mark.svg`, `brand/favicon-32.png`, `brand/apple-touch-icon.png`. Icon 192/512 nằm trong `Content/brand/` cho manifest PWA (`Content/manifest.webmanifest`, giai đoạn M2).

## Màu

Gradient chéo từ `#8ED3F2` (trên trái) xuống `#4B87BB` (dưới phải), đúng bằng `--accent-brand` của chế độ tối và chế độ sáng trong `Content/css/tokens.css`. Khối nền `#0E1720`; nền icon nền tảng `#0B1116`. Màu chỉ nằm trong file logo; CSS của app không viết màu logo.

## Đã chỉnh so với bộ gốc (`source/`)

- **Chấm tối ở chỗ nối nét:** bộ gốc vẽ chữ bằng ba path chồng lên nhau, đầu nét tròn của path này đè lên path kia nên ở đỉnh chữ T và chỗ K rẽ nhánh có chấm tối. Nay ba nét là một path.
- **Gradient lệch theo từng nét:** gradient tính theo khung của từng phần (nét dọc chữ T gần như không có bề ngang). Nay gradient trải theo cả biểu tượng (`gradientUnits="userSpaceOnUse"`).
- **Không đọc được ở 16px:** khung chừa lề 8%, viền 1px chiếm điểm ảnh và nét mảnh nên favicon 16px thành một khối nhòe. Bản `toolkit-mark.svg` bỏ lề và viền, chữ to hơn 1,3 lần, nét dày 24/200.
- **Ảnh chia sẻ:** chữ mẫu "TK · your-domain.com" thay bằng "Toolkit · Jira · Ghi chú · Việc cần làm · Công cụ DB" (font Inter của app).
- Trên sidebar tối, khối logo có thêm viền 1px màu `--line` để không chìm vào nền.

## Quy tắc dùng

- Giữ tỉ lệ vuông, không kéo giãn, không tách gradient thành hai màu phẳng.
- Chừa khoảng trống quanh logo tối thiểu bằng 1/4 chiều rộng.
- Từ 48px trở xuống dùng `toolkit-mark.svg`; lớn hơn dùng `toolkit-logo.svg`.
- Icon nền tảng (iOS, Android) dùng bản nền đặc, không dùng bản trong suốt.
- Logo là tài sản của chủ dự án, không lấy từ sản phẩm khác.

## Xuất lại PNG/ICO

Mở SVG trong Chromium ở đúng kích thước rồi chụp nền trong suốt (Playwright `page.screenshot({ omitBackground: true })`); ghép ICO bằng Pillow (`Image.save(..., format="ICO", sizes=[...])`).
