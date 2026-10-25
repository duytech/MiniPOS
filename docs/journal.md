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

### Ngày 4: API + màn hình danh sách order khi chưa có hướng dẫn (2026-10-25)
Prompt giao cho agent mới (không có `CLAUDE.md`): "API endpoint to list orders, with filtering by status and a search box.
Orders list page in Angular calling the real API. Use RxJS: switchMap for the search input and combineLatest to combine
the search text with the status filter. Style it with Tailwind. Add tests."

Kết quả: `GET /api/orders?status=&search=&limit=`, trang `/orders`, proxy `/api` → 5191 cho `ng serve`.
Build sạch; test .NET: thêm 16 application + 11 integration, tất cả xanh (chỉ 2 test concurrency cũ đỏ); 12 spec Angular xanh.
Kiểm tra trong trình duyệt tích hợp: hiển thị dữ liệu thật, gõ "club" (4 phím) chỉ sinh **1** request nhờ debounce,
lọc Paid → trạng thái rỗng + nút "Clear filters", không có lỗi console.

**Làm tốt (không cần ghi vào `CLAUDE.md`):** RxJS đúng bài (`debounceTime` → `distinctUntilChanged` → `combineLatest` →
`switchMap`, `catchError` nằm trong `switchMap` nên stream không chết), OnPush + `toSignal` + `inject()`, label bọc input,
xử lý đủ loading/lỗi/rỗng, escape `%`/`_` khi tìm kiếm, tính tổng trong một query.

| # | Agent làm sai gì | Quy ước đúng | Mức độ |
|---|---|---|---|
| 1 | Tổng tiền được tính ở **hai nơi**: `Order.Total` trong Domain (C#) và `SUM(unit_price * quantity)` trong SQL của repository. Khi có thuế/giảm giá, hai chỗ sẽ lệch nhau. | Một nguồn duy nhất cho phép tính tiền (Domain); nếu cần SQL cho hiệu năng thì phải có test so khớp hai bên. | Cao (sẽ lệch khi có thuế) |
| 2 | Cùng một order: `/api/orders/{id}/total` trả `15.40`, `/api/orders` trả `15.4000`. Định dạng tiền trong JSON không nhất quán. | Tiền trong API luôn 2 chữ số thập phân (làm tròn/scale ở một chỗ). | Trung bình |
| 3 | Frontend giữ tiền là `number` của JS và hiển thị bằng `number: '1.2-2'`, không có ký hiệu tiền tệ hay locale. | Quy ước hiển thị tiền thống nhất (pipe `currency` + mã tiền tệ); không tính toán tiền ở frontend. | Trung bình |
| 4 | Repository (Infrastructure) trả thẳng `OrderSummaryDto` của Application; validate input bằng `if` + `ValidationException` trong `OrderService`. Tiếp tục lệch kiến trúc như Ngày 3. | Query + Handler + Validator; read model rõ ràng. | Trung bình (lệch kiến trúc) |
| 5 | Chỉ có `limit` (mặc định 50, tối đa 200), không có phân trang hay tổng số. Có hơn 50 order là UI cắt mất mà không báo. | Phân trang (cursor hoặc page + total) và UI cho biết còn dữ liệu. | Trung bình |
| 6 | Mỗi lần gõ/lọc, trạng thái `loading` thay hẳn bảng → bảng biến mất rồi hiện lại (nháy). | Giữ dữ liệu cũ, hiện chỉ báo loading nhỏ. | Thấp (UX) |
| 7 | Thông báo lỗi trong UI hard-code `http://localhost:5191`. | URL môi trường không xuất hiện trong UI/code; dùng environment/config. | Thấp |
| 8 | Tìm kiếm `ILIKE '%...%'` trên `id::text` và tên món → quét toàn bảng, không index. | Chấp nhận ở quy mô nhỏ; ghi rõ giới hạn hoặc dùng `pg_trgm`. | Thấp |
| 9 | Tự thêm luật nghiệp vụ không ai yêu cầu: tìm theo một phần order id, giới hạn độ dài tìm kiếm, limit 1–200. | Hỏi trước khi đặt luật/hằng số. | Thấp |
| 10 | Chỉ có proxy cho `ng serve`; build production gọi `/api` sẽ không chạy nếu không cùng origin hoặc không có CORS. Agent có nói, nhưng không giải quyết. | Ghi rõ cách deploy (cùng origin hay CORS) trong tài liệu dự án. | Thấp |

**Bài học cho `CLAUDE.md` (bổ sung Ngày 3):**
- Agent làm frontend/RxJS tốt khi prompt nêu rõ operator; không cần nhiều dòng về RxJS. Đúng tinh thần Ngày 7: chỉ giữ dòng thay đổi hành vi.
- Cần rule "một nguồn duy nhất cho phép tính tiền" và "định dạng tiền trong API/UI": lỗi 1–3 lặp lại cùng gốc với dòng 7 và 12 của Ngày 3.
- Kiến trúc Command/Query/Handler/Validator phải ghi rõ: hai ngày liên tiếp agent đều chọn Service.
- Ứng viên verifier Tuần 2: test so khớp tổng tiền giữa `GET /orders/{id}`, `/total` và danh sách (cùng giá trị, cùng scale).
