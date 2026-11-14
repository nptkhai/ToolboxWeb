# TK — Toolkit Assets

Bộ tài nguyên logo cho website, xuất từ file gốc `logo.svg`.

## Bảng màu
| Vai trò | Mã màu |
|---|---|
| Xanh nhạt (điểm đầu gradient) | `#8ED3F2` |
| Xanh đậm (điểm cuối gradient) | `#4B87BB` |
| Nền khối icon | `#0E1720` |
| Nền trang / nền đặc platform icon | `#0B1116` |

Gradient chạy chéo từ góc trên-trái (`#8ED3F2`) xuống góc dưới-phải (`#4B87BB`).

## Danh sách file

**Vector**
- `logo.svg` — file nguồn, chỉnh sửa được (màu, kích thước, tách lớp)

**Icon chính (nền trong suốt)**
- `icon-transparent-1024.png`, `-512.png`, `-256.png` — dùng chèn vào thiết kế, tài liệu, banner

**Favicon**
- `favicon.ico` — đa kích thước (16/32/48px), đặt vào thư mục gốc website
- `favicon-16.png`, `favicon-32.png`, `favicon-48.png` — dùng riêng nếu framework yêu cầu PNG tường minh

Thẻ HTML gợi ý:
```html
<link rel="icon" href="/favicon.ico" sizes="any">
<link rel="icon" type="image/png" sizes="32x32" href="/favicon-32.png">
<link rel="apple-touch-icon" href="/apple-touch-icon-180.png">
```

**Icon nền tảng (nền đặc — iOS/Android tự bo góc nên không dùng bản trong suốt)**
- `apple-touch-icon-180.png` — thêm vào màn hình chính iOS
- `android-chrome-192.png`, `android-chrome-512.png` — khai báo trong `manifest.json` (PWA/Android)

**Bản đơn sắc (nền trong suốt, dùng đè lên nền màu)**
- `icon-mono-white-1024.png` — đặt trên nền tối/ảnh nền có màu
- `icon-mono-dark-1024.png` — đặt trên nền sáng/trắng

**Chia sẻ mạng xã hội**
- `og-image-1200x630.png` — dùng cho thẻ `og:image` / Twitter card. **Cần thay chữ placeholder** ("your-domain.com · thay bằng tagline của bạn") trước khi dùng thật.

## Khoảng đệm tối thiểu
Chừa khoảng trống quanh icon tối thiểu bằng 1/4 chiều rộng icon để chữ/nét không bị nghẹt khi thu nhỏ.

## Không nên
- Không kéo giãn icon sai tỉ lệ (luôn giữ vuông 1:1)
- Không tách rời gradient thành 2 màu phẳng cho từng nét chữ
- Không dùng bản `icon-transparent` cho apple-touch-icon / android-chrome (iOS sẽ tô nền đen vào phần trong suốt) — dùng đúng bản nền đặc tương ứng
