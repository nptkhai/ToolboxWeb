# Kendo UI Core Self-Host

ToolboxWeb đang self-host Kendo UI Core miễn phí từ npm thay vì dùng Telerik CDN.

## Nguồn package

- `kendo-ui-core@2026.2.520`
- `@progress/kendo-theme-default@14.1.0`
- `@progress/kendo-font-icons@5.1.0`

## Asset đang dùng

- Browser bundle: `src/ToolboxWeb.Web/Content/vendor/kendo/js/kendo.ui.core.min.js`
- Culture: `src/ToolboxWeb.Web/Content/vendor/kendo/cultures/kendo.culture.vi-VN.min.js`
- Messages: `src/ToolboxWeb.Web/Content/vendor/kendo/messages/kendo.messages.vi-VN.min.js`
- Theme: `src/ToolboxWeb.Web/Content/vendor/kendo/theme/default-main.css`
- Icons: `src/ToolboxWeb.Web/Content/vendor/kendo/icons/index.css`

## Nguyên tắc quan trọng

- Với Razor view nạp thẳng bằng `<script>`, dùng nhánh `umd/` của package `kendo-ui-core`.
- Không dùng nhánh `js/` làm static browser script trực tiếp vì đây là entrypoint kiểu module `require(...)`.
- Không nạp thêm các widget `umd/*.js` rời nếu đã dùng `kendo.ui.core.min.js`, vì bundle này đã gom sẵn các widget Core và phụ thuộc của chúng.

## Quy trình cập nhật

1. Tải đúng version `kendo-ui-core` từ npm.
2. Copy `package/umd/kendo.ui.core.min.js` vào `Content/vendor/kendo/js/`.
3. Copy `package/umd/cultures/kendo.culture.vi-VN.min.js` vào `Content/vendor/kendo/cultures/`.
4. Copy `package/umd/messages/kendo.messages.vi-VN.min.js` vào `Content/vendor/kendo/messages/`.
5. Giữ theme và icon theo package đang dùng, tránh trộn phiên bản.
6. Build lại và hard refresh `/DemoKendo` để tránh cache giữ file cũ.
