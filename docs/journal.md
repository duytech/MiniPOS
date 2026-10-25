# Nhật ký harness

## Tuần 1

### Ngày 1–2: khung repo (2026-10-25)
- .NET 10 (LTS) thay cho .NET 8 trong lộ trình: máy đã có SDK 10, .NET 8 hết hỗ trợ 11/2026.
- Angular 20 thay cho 18: bản mới nhất cần Node >= 24.15, máy đang có 24.13.
- Tailwind v4 qua PostCSS (`web/.postcssrc.json`).
- PostgreSQL 17 qua `docker-compose.yml`, biến môi trường trong `.env` (mẫu: `.env.example`).
- Chưa có `CLAUDE.md`: cố ý, để Ngày 3–4 ghi lỗi khi agent làm việc mà không có hướng dẫn.
- Máy có PostgreSQL cài thẳng trên Windows chiếm cổng 5432, nên container Docker map ra **5433**.

### Ngày 3: Order khi chưa có hướng dẫn (2026-10-25)
Prompt giao cho agent mới (không có `CLAUDE.md`): "implement the Order feature on the backend: create an order,
add items, calculate the total. Expose through the API, persist in PostgreSQL, add tests."

Kết quả: build sạch, 37 test xanh (16 domain, 12 application, 9 integration). Code gọn, dùng `decimal`,
`numeric(12,2)`, chụp lại giá lúc thêm món. **Test xanh nhưng vẫn có lỗi mất tiền**: phải chạy thử đồng thời mới thấy.

| # | Agent làm sai gì | Quy ước đúng | Mức độ |
|---|---|---|---|
| 1 | Tăng số lượng theo kiểu đọc → sửa → ghi, không có concurrency token. 6 request thêm cùng món đồng thời: cả 6 trả 200 nhưng order chỉ có 5 ly. | Aggregate Order có concurrency token (`xmin` của Postgres); xung đột → retry hoặc 409. | Cao (mất doanh thu, không báo lỗi) |
| 2 | Unique index `(order_id, menu_item_id)` + gộp dòng: 5 request thêm món mới đồng thời → 1 thành công, 4 trả **500**. | Lỗi DB do xung đột phải map thành 409, không để lọt 500. | Cao |
| 3 | Tự quyết business rule: thêm cùng món thì gộp vào dòng cũ. POS thật thường cần dòng riêng (ghi chú, modifier, gửi bếp đợt sau). | Rule nghiệp vụ không có trong yêu cầu thì hỏi, không tự chọn. | Trung bình |
| 4 | Dùng `OrderService` gọi thẳng repository, không theo Command/Query → Handler → Validator. | Mỗi use case một Command/Query + Handler + Validator. | Trung bình (lệch kiến trúc) |
| 5 | Validate bằng `if` + `DomainException` rải trong entity, không có lớp validator cho input. | Validate input ở Validator (FluentValidation), invariant ở Domain. | Thấp |
| 6 | Mật khẩu DB hard-code trong `appsettings.Development.json` và `PosApiFactory.cs`, được commit vào repo. | Chuỗi kết nối lấy từ user-secrets / biến môi trường; repo chỉ có file mẫu. | Trung bình (bảo mật) |
| 7 | Không có quy tắc làm tròn: `LineTotal = UnitPrice * Quantity`, chưa có thuế nên chưa lộ, nhưng không có chỗ nào định nghĩa làm tròn ở bước nào. | Một chỗ duy nhất làm tròn tiền, `MidpointRounding.AwayFromZero`, rõ ở bước nào. | Thấp lúc này, sẽ cao khi có thuế |
| 8 | Tự thêm phạm vi: entity `MenuItem`, CRUD menu, seed 4 món trong migration. | Phạm vi lớn hơn yêu cầu thì đề xuất trước. | Thấp (hợp lý nhưng không hỏi) |
| 9 | Tự chọn hằng số nghiệp vụ không ai yêu cầu: tối đa 1000/dòng, giá tối đa 2 chữ số thập phân. | Hằng số nghiệp vụ phải có nguồn (spec hoặc hỏi). | Thấp |
| 10 | `MigrateOnStartup=true` trong Development: an toàn ở dev, dễ bị copy sang môi trường khác. | Migration chạy bằng lệnh riêng / bước deploy. | Thấp |
| 11 | Để nguyên `WeatherForecastController` của template. | Xóa code mẫu không dùng. | Thấp |
| 12 | Không có Money value object: tiền là `decimal` trần, không có tiền tệ. | Tùy quyết định; ghi vào `CLAUDE.md` dù chọn cách nào. | Cần quyết định |

**Bài học cho `CLAUDE.md` (Ngày 5):**
- Concurrency là rule bắt buộc với mọi aggregate có tiền: dòng 1–2 là lỗi agent không bao giờ tự nghĩ tới, và test của chính nó không bắt được.
- Liệt kê rõ kiến trúc Application (Command/Handler/Validator), nếu không agent chọn Service.
- "Không tự đặt business rule; hỏi khi yêu cầu không nói" sẽ chặn dòng 3, 8, 9.
- Ứng viên verifier Tuần 2: test đồng thời (N request song song → tổng đúng N) cho mọi thao tác thay đổi order.

**Test tái hiện lỗi 1–2:** `tests/Pos.Api.IntegrationTests/ConcurrentAddItemTests.cs`, đỏ ổn định 3/3 lần chạy.
- 20 request tăng số lượng cùng lúc: cả 20 trả 200, DB chỉ có 2 ly (mong đợi 21).
- 20 request thêm món mới cùng lúc: 19 trả 500.
- Test kiểm tra invariant (chỉ 200/409, số lượng trong DB = số request 200), không ép cách sửa.
