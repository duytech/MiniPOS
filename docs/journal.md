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

### Ngày 5: viết `CLAUDE.md` (2026-10-25)
**Quyết định đã chốt** (trước đây agent tự chọn):
- Tiền: `Money` value object (`decimal Amount` + `Currency`, hiện chỉ USD).
- Làm tròn: ở cấp order, ngay sau mỗi bước (discount → surcharge → tax → tip), `MidpointRounding.AwayFromZero`, chỉ ở một chỗ trong Domain.
- Application: Command/Query + Handler tự viết + FluentValidation, không Service, không MediatR.
- Thêm món: dòng giống hệt (cùng món, cùng giá chụp lại, cùng ghi chú/modifier) → tăng số lượng; khác → dòng mới.
- Concurrency: `xmin` của Postgres, xung đột → 409.
- `CLAUDE.md` viết bằng tiếng Anh, 75 dòng.

**Mỗi rule trong `CLAUDE.md` đến từ lỗi nào:**
| Mục trong `CLAUDE.md` | Lỗi gốc |
|---|---|
| Commands (cổng 5433, test nào đang đỏ) | Agent Ngày 3 mất công dò cổng 5432 bị chiếm |
| "Follow this file, not the surrounding code" | Ngày 4 agent chép lại kiểu Service của Ngày 3 |
| Architecture: Command/Query + Handler + Validator | N3 #4, #5; N4 #4 |
| Repository không trả DTO của Application | N4 #4 |
| Xóa code mẫu template | N3 #11 |
| Money VO, không `double`/`float`/`decimal` trần | N3 #12 |
| Thứ tự tính + làm tròn ở một chỗ | N3 #7 |
| Cơ sở tính của mỗi bước phải lấy từ task hoặc hỏi | Phát hiện khi viết chính `CLAUDE.md` (xem dưới) |
| Tổng chỉ tính trong Domain + test list total = GET total | N4 #1 |
| API luôn 2 chữ số thập phân | N4 #2 |
| Frontend chỉ hiển thị tiền bằng `currency` | N4 #3 |
| `xmin` + 409, lỗi DB dự đoán được → 4xx, test song song | N3 #1, #2 |
| Không tự đặt business rule/giới hạn/phạm vi | N3 #3, #8, #9; N4 #9 |
| Rule gộp dòng | N3 #3 |
| List phải phân trang + `totalCount` | N4 #5 |
| Giữ dữ liệu cũ khi tải lại | N4 #6 |
| Không có URL môi trường trong code/UI | N4 #7 |
| Không commit mật khẩu, kể cả mặc định trong test | N3 #6 |

**Cố ý không đưa vào** (agent đã tự làm đúng ở Ngày 4): chi tiết RxJS (`debounceTime`, `switchMap`, `catchError` trong
`switchMap`), label/accessibility, xử lý loading/lỗi/rỗng, escape LIKE. Frontend chỉ còn 1 dòng về `OnPush`/`@if`/`toSignal`.
Chưa xử lý: N3 #10 (migrate khi khởi động, chỉ ở Development), N4 #8 (`ILIKE` quét toàn bảng), N4 #10 (CORS khi deploy):
mức thấp, để sau.

**Lỗi của chính tôi khi viết:** bản đầu ghi "mỗi bước tính trên kết quả bước trước", vô tình quy định tip tính trên số tiền
**sau thuế** (ở Mỹ tip thường tính trước thuế). Đây đúng là lỗi "tự quyết business rule" mà file đang cấm agent. Đã sửa:
cơ sở tính của mỗi bước phải lấy từ task, không có thì hỏi. Bài học: người viết instruction cũng tự đặt rule mà không để ý.

**Kiểm tra nhanh:** giao một agent mới (chỉ đọc, không nhắc tới `CLAUDE.md`) task "thêm thuế 8.875% + tip, hai thu ngân sửa
cùng order" và hỏi kế hoạch.
- Agent tự tìm thấy và trích `CLAUDE.md` làm nguồn chính.
- Kiến trúc: Query/Command + Handler + Validator, không MediatR.
- Tiền: `Money`, chỉ tính trong `Order.CalculatePricing()`. Với subtotal $10.00, thuế = 0.8875 → 0.89.
- Concurrency: `xmin` + 409, thêm test song song.
- Có JSON converter cho đúng 2 chữ số và test list total = GET total.
- Liệt kê 5 câu hỏi thay vì tự quyết: tip trước hay sau thuế, thuế suất lấy ở đâu, giới hạn tip, retry hay để client tải lại
  khi 409, có lưu tổng trên order không.
- Agent đọc bản **trước** khi sửa rule tip, nhưng vẫn tự nhận ra mâu thuẫn và đưa vào câu hỏi.
So với Ngày 3–4: các lỗi kiến trúc, tiền và concurrency đều được xử lý **ngay trong kế hoạch**. Đo thật ở Ngày 6.

### Ngày 6: làm lại Order với `CLAUDE.md` (2026-10-26)
**Cách làm:** nhánh `day6-order-with-claude-md`, đưa `src/`, `tests/`, `web/src` về scaffold `0186bc8` (commit `d455947`).
Hai session mới, mỗi session chỉ nhận **nguyên văn** prompt Ngày 3 rồi Ngày 4, không nhắc `CLAUDE.md`.
Biến thí nghiệm thay đổi so với Ngày 3–4:
- `ConcurrentAddItemTests` bị cất đi làm verifier ẩn. `CLAUDE.md` bỏ dòng "Known red" và thay "see `ConcurrentAddItemTests`"
  bằng mô tả một dòng của cùng phép kiểm tra (N request song song: chỉ 200/409, số lượng lưu = số 200).
- `docs/journal.md` bị gỡ khỏi nhánh trong lúc agent làm: nó chứa bảng lỗi, tức là đáp án.

**Kết quả:** build 0 warning; A: 47 test, B: tổng 86 test .NET + 12 spec web, tất cả xanh. Verifier ẩn (chỉ sửa route,
tên bảng `order_lines`, tạo món qua API; invariant giữ nguyên): **2/2 xanh, 3/3 lần chạy** (Ngày 3: đỏ ổn định).
Kiểm tra trong trình duyệt: tiền `15.40` ở cả `GET /orders/{id}` và list, UI `$15.40`, tìm "pho" lọc đúng, không lỗi console.

**Đối chiếu lỗi cũ:**
| Lỗi cũ | Ngày 6 |
|---|---|
| N3 #1 mất số lượng khi song song | Sửa. Agent tự thấy chỗ khó: thêm dòng không đụng hàng `orders` nên `xmin` không đổi → `SaveChanges` đánh dấu order Modified khi dòng đổi. Agent còn tắt cơ chế đó để chứng minh test của nó bắt được lỗi (21 mong đợi, 4 lưu). |
| N3 #2 500 khi thêm món mới song song | Sửa: không còn unique index; unique/FK violation → 409. |
| N3 #3 tự đặt rule gộp dòng | Theo rule trong `CLAUDE.md` (cùng món + giá + ghi chú → tăng số lượng). |
| N3 #4–5, N4 #4 Service, validate bằng `if`, repo trả DTO | Sửa: mỗi use case một thư mục Command/Query + Handler + Validator; repo trả entity, handler chiếu DTO. |
| N3 #6 mật khẩu trong repo | Sửa: user-secrets / `ConnectionStrings__Pos`; mỗi lần test tạo DB `pos_test_<guid>` riêng. |
| N3 #7, #12 làm tròn, Money | Sửa: `Money` VO (bắt buộc đủ cent), một `Order.CalculateTotals()`, ghi rõ chỗ thêm discount → tip. |
| N3 #8 tự thêm MenuItem | **Một phần**: vẫn thêm (tối thiểu: tạo + lấy), nhưng nêu ra làm câu hỏi mở thay vì im lặng; không seed. |
| N3 #9, N4 #9 tự đặt hằng số | Phần lớn sửa: không giới hạn số lượng/độ dài; giới hạn giá lấy từ `numeric(12,2)`. Session B **dừng lại hỏi** status, cách tìm, thứ tự sắp xếp. Còn sót: `MaxPageSize = 100` (trả 400, không cắt im lặng). |
| N3 #10 migrate khi khởi động | Còn, đúng như `CLAUDE.md` mô tả (chỉ Development). |
| N3 #11 `WeatherForecast` | Sửa: đã xóa. |
| N4 #1 tổng tính hai nơi | Sửa: list tải order + dòng của trang hiện tại rồi gọi Domain; có test list total = GET total. |
| N4 #2 `15.4000` vs `15.40` | Sửa: `MoneyJsonConverter` luôn 2 chữ số; test đọc raw JSON. |
| N4 #3 frontend không có tiền tệ | Sửa: `currency: 'USD'`, frontend không tính tiền. |
| N4 #5 chỉ có `limit` | Sửa: `page`/`pageSize`/`totalCount`, UI có Previous/Next và "Showing 1–7 of 7". |
| N4 #6 bảng nháy khi tải | Sửa: giữ dữ liệu cũ, mờ bảng + chỉ báo nhỏ. |
| N4 #7 URL môi trường trong UI | Sửa. |
| N4 #8 `ILIKE` quét toàn bảng | Còn (thấp, chấp nhận ở quy mô này). |
| N4 #10 cách deploy / CORS | Còn (không có trong `CLAUDE.md`). |

**Đếm:** Ngày 3–4 có 22 lỗi. Ngày 6: 16 sửa hẳn, 3 sửa một phần (N3 #8, N3 #9, N4 #9), 3 còn nhưng mức thấp hoặc cố ý (N3 #10,
N4 #8, N4 #10). Lỗi mới: 1 nhỏ (`MaxPageSize = 100` tự đặt). Không còn lỗi mức Cao.

**Câu hỏi agent nêu thay vì tự quyết** (đúng hành vi mong muốn, xem dưới):
- A: cơ sở tính tip/surcharge (chưa có rounding nên chưa có test x.xx5), phạm vi MenuItem, vòng đời order, giới hạn
  số lượng/độ dài, chuẩn hóa ghi chú, món không tồn tại trả 404 hay 422.
- B (dừng giữa chừng, tôi trả lời): status = Open | Paid | Cancelled (tạo mới là Open, chuyển trạng thái ngoài phạm vi);
  tìm theo tên món chứa chuỗi + số order tuần tự (`#12`); sắp xếp mới nhất trước (thêm `CreatedAt`).

**Đầu vào cho Ngày 7 (rút gọn `CLAUDE.md`):**
- Có tác dụng rõ (hành vi đổi hẳn so với Ngày 3–4): Command/Handler/Validator, Money + một chỗ tính tổng, 2 chữ số trong
  API, `xmin` + 409 + test song song, phân trang + `totalCount`, không tự đặt business rule, giữ dữ liệu cũ khi reload,
  không commit mật khẩu, xóa template.
- Rule "không tự thêm phạm vi" chưa đủ mạnh với entity bắt buộc phải có (MenuItem): agent thêm rồi hỏi sau. Cân nhắc nói
  rõ "nếu cần entity mới, dừng lại hỏi trước".
- Rule "không tự đặt giới hạn" vẫn sót ở page size: thêm ví dụ `pageSize` tối đa vào dòng đó.
- Không đo được dòng nào thừa vì không có lần chạy thiếu từng dòng; Ngày 7 chỉ bỏ dòng mà Ngày 4 agent đã tự làm đúng.

### Ngày 7: rút gọn `CLAUDE.md` (2026-10-26)
Tiêu chí bỏ dòng: agent Ngày 3–4 (chưa có `CLAUDE.md`) đã tự làm đúng, tức là dòng đó không đổi hành vi.
74 → 70 dòng.

| Thay đổi | Lý do |
|---|---|
| Bỏ "Dependencies point inward…" | Ngày 3 tự chia tầng đúng. Tuần 2 Ngày 3 sẽ có arch test (NetArchTest) kiểm tra thay cho chữ. |
| Bỏ "Controllers are thin…" | Ngày 3–4 controller đều mỏng; lỗi nằm ở Service, đã có dòng Command/Handler/Validator chặn. |
| Bỏ "Unit prices are snapshotted… `numeric(12,2)`" | Ngày 3 tự chụp giá và dùng `numeric(12,2)`. Rule gộp dòng vẫn nhắc "same snapshotted unit price". |
| Bỏ dòng Angular (standalone, `OnPush`, `inject()`, `@if`, `toSignal`, `takeUntilDestroyed`) | Ngày 4 tự làm đúng hết; đó là mặc định của Angular 20. |
| Sửa rule "không tự đặt business rule" | Ngày 6: agent A vẫn thêm `MenuItem` rồi mới hỏi; agent B tự đặt `MaxPageSize = 100`. Thêm ví dụ "max page size" và "phải hỏi **trước** khi xây, liệt kê sau là chưa đủ". |

**Giữ nguyên** mọi dòng có lỗi gốc trong bảng Ngày 5, kể cả những dòng Ngày 6 làm đúng: làm đúng *khi có* dòng đó
không chứng minh được dòng đó thừa.

**Rủi ro:** không có lần chạy lại sau khi rút gọn. Bỏ dòng dựa trên Ngày 3–4 (một lần chạy mỗi task), nên nếu Tuần 2
thấy lệch kiến trúc hay sai kiểu cột tiền, đó là dấu hiệu cần trả lại dòng. Arch test và verifier Tuần 2 sẽ bắt các lỗi
này tự động thay vì dựa vào chữ.

## Tổng kết Tuần 1
**Kết quả:** `CLAUDE.md` 70 dòng, mỗi dòng có nguồn (lỗi thật hoặc quyết định đã chốt), đã kiểm chứng bằng cách cho agent
mới làm lại cùng tính năng.

| | Ngày 3–4 (không hướng dẫn) | Ngày 6 (có `CLAUDE.md`) |
|---|---|---|
| Lỗi từ bảng cũ | 22 (4 mức Cao) | 3 còn (mức thấp/cố ý), 3 sửa một phần, 16 sửa hẳn; 0 mức Cao |
| Lỗi mới | – | 1 nhỏ (`MaxPageSize = 100`) |
| Verifier đồng thời ẩn | Đỏ 3/3 | Xanh 3/3 |
| Câu hỏi nghiệp vụ agent tự nêu | 0 (tự quyết hết) | A: 6 câu sau khi làm; B: 3 câu, dừng lại hỏi trước |

**Bài học:**
1. **Test xanh không có nghĩa là đúng.** Ngày 3 có 37 test xanh mà vẫn mất tiền khi chạy song song. Verifier phải kiểm
   tra invariant nghiệp vụ (tổng lưu trong DB), không chỉ code chạy được.
2. **Rule có tác dụng nhất là rule nêu cái agent không tự nghĩ tới:** concurrency, một chỗ tính tiền, định dạng tiền trong
   API. Rule về thứ agent vốn làm tốt (RxJS, Angular style, chia tầng) chỉ tốn chỗ.
3. **Rule cụ thể thắng rule chung.** "Không tự đặt giới hạn" vẫn sót page size; có ví dụ cụ thể thì agent mới dừng lại.
   Ví dụ trong rule concurrency (N request song song, chỉ 200/409, số lượng = số 200) đủ để agent tự viết test tương
   đương verifier ẩn.
4. **Người viết instruction cũng tự đặt rule** (tip trước/sau thuế ở Ngày 5). Rule "hỏi khi task không nói" áp dụng cho
   cả mình.
5. **Thí nghiệm phải kín:** journal chứa đáp án nên phải gỡ khỏi nhánh khi agent làm lại; verifier cũng phải ẩn.

**Mang sang Tuần 2:** biến rule thành check tự động: arch test cho chia tầng và không `double`/`float` (thay cho dòng đã
bỏ), test tiền đến từng cent khi thêm thuế/tip/giảm giá (cần trả lời câu hỏi cơ sở tính tip/surcharge trước), hook
build + test sau mỗi lần sửa file.

**Câu hỏi nghiệp vụ còn mở:** cơ sở tính tip/surcharge; phạm vi MenuItem; vòng đời order (chuyển trạng thái, xóa món);
giới hạn số lượng; món không tồn tại trả 404 hay 422; page size tối đa (hiện code đang để 100).

## Tuần 2

### Chuẩn bị: cài Angular skills (2026-10-26)
- `angular-developer` + `angular-new-app` từ [angular/skills](https://github.com/angular/skills) @ `3c880aa`, copy
  nguyên văn vào `.claude/skills/` (clone + đọc trước, không chạy `npx skills add`). Chỉ có markdown, không có script.
- Lệch với repo, cần để ý khi agent dùng skill: skill nói test bằng **Vitest**, repo đang dùng Karma/Jasmine; skill khuyên
  Signal Forms (v22+), repo là Angular 20; skill bảo dùng `ng generate` và chạy `ng build` sau khi sửa (khớp
  `npm run build` trong Definition of done). `CLAUDE.md` vẫn là nguồn chính khi hai bên khác nhau.

### Ngày 1: discount → tax → tip, test đến từng cent (2026-10-26)
**Quyết định nghiệp vụ** (hỏi trước khi làm, đúng rule `CLAUDE.md`):
- Discount trên cả order: % của subtotal **hoặc** số tiền cố định; vượt subtotal → 422 (`DomainException`), không tự kẹp.
- Tax = rate × (subtotal − discount). Rate cấu hình `Pricing:TaxRatePercent` = 8.875, **chụp vào order lúc tạo**.
- Tip = % × (subtotal − discount), tức là trước thuế.
- Phạm vi: Domain + unit test. Chưa có endpoint đặt discount/tip.

**Đã làm:** `Percentage` VO (tối đa 4 chữ số thập phân = `numeric(7,4)`), `OrderDiscount`, `Order.ApplyDiscount/SetTip`,
`CalculateTotals()` trả `Subtotal/Discount/Tax/Tip/Total`, biến `taxBase`/`tipBase` đặt tên rõ; API trả thêm
`discount`, `tax`, `tip`. Migration `OrderPricing`. 34 domain + 35 application + 39 integration test, 0 warning.

**Test chọn số để bắt lỗi thật** (mọi midpoint có chữ số cent chẵn, nên `ToEven` làm tròn ngược chiều):
| Test | Số | Bắt lỗi gì |
|---|---|---|
| Discount 15% của 11.50 | 1.725 → 1.73 | Sai `MidpointRounding` |
| Tax 8.875% của 12.00 | 1.065 → 1.07 | Sai `MidpointRounding` |
| Tip 15% của 12.30 | 1.845 → 1.85 | Sai `MidpointRounding` |
| Tax/tip sau discount 2.00 trên 12.00 | 0.89 / 1.80 | Tính trên subtotal thay vì sau discount |
| Tip 18% của 12.00 với tax 0% và 8.875% | 2.16 cả hai | Tip tính sau thuế |
| 5.00, discount 15%, tax 8.875%, tip 18% | 5.40 (làm tròn cuối chỉ ra 5.39) | Làm tròn một lần ở cuối |

**Kiểm chứng verifier:** đổi tạm `AwayFromZero` → `ToEven`: **5 test đỏ**; trả lại thì xanh. Test cũ
`OrderConcurrencyTests` tự tính `7.70 × n` rồi so với `total`: vỡ khi có thuế. Đó là một chỗ test tái tính tổng (trái rule
"chỉ Domain tính tổng"); đã đổi sang so `subtotal`.
Chạy API thật: order 12.00 → `"tax":1.07,"total":13.07`, list total bằng GET.

**Giả định / câu hỏi còn mở:**
- Order tạo trước migration có `tax_rate = 0` (DB dev, không có dữ liệu thật). Nếu có dữ liệu thật phải hỏi cách xử lý.
- Discount cố định chỉ kiểm tra ≤ subtotal lúc áp dụng; hiện chưa xóa được món nên subtotal chỉ tăng. Khi thêm xóa món
  phải hỏi: discount vượt subtotal thì chặn, kẹp hay bỏ discount?
- Surcharge (Ngày 2) có bị đánh thuế không, có nằm trong tip base không: chưa hỏi.
- Giới hạn tip % / tax rate (vd tip 300%?) và quyền ai được giảm giá: chưa có, sẽ hỏi khi làm endpoint.

### Ngày 2: card surcharge + split bill theo món (2026-10-26)
**Quyết định nghiệp vụ** (hỏi trước khi làm, 2 lượt câu hỏi):
- Card surcharge 3% (`Pricing:CardSurchargePercent`), chụp vào order lúc tạo như tax rate. Chỉ áp dụng khi
  `PaymentMethod = Card`; mặc định `Cash`. Base = subtotal − discount.
- Surcharge **bị tính thuế, không tính tip**: taxBase = subtotal − discount + surcharge; tipBase = subtotal − discount.
- Split theo món: mỗi phần gồm nguyên line, mỗi line thuộc đúng một phần; thiếu, trùng, lạ hoặc phần rỗng → 422.
- Phân bổ: discount, surcharge, tax, tip chia riêng từng khoản theo tỷ lệ subtotal của phần, làm tròn xuống cent; xu dư
  vào phần có subtotal lớn nhất (hòa → phần đứng trước).
- Phạm vi: Domain + unit test. Chưa có endpoint chọn phương thức thanh toán hay split.

**Đã làm:** `PaymentMethod`, `Order.SetPaymentMethod`, `CardSurchargeRate`; bước 4 trong `CalculateTotals()` với
`surchargeBase`/`taxBase`/`tipBase` đặt tên rõ; `Order.Split(...)` trả `SplitPart` và dùng một helper `Allocate`. API trả
thêm `paymentMethod`, `surcharge`. Migration `OrderSurcharge` (order cũ: rate 0, `Cash`). 63 domain + 35 application
+ 39 integration test, 0 warning. Chạy API thật: order 12.00 → `"surcharge":0.00,"tax":1.07,"total":13.07`, list = GET.

**Test chọn số để bắt lỗi thật:**
| Test | Số | Bắt lỗi gì |
|---|---|---|
| Surcharge 3% của 11.50 | 0.345 → 0.35 | Sai `MidpointRounding` |
| 12.00, discount 2.00, card | 0.30 (không phải 0.36) | Surcharge tính trên subtotal |
| 10.00 card, tax 8.875% | 0.91 (không phải 0.89) | Quên đánh thuế surcharge |
| 10.00 card, tip 18% | 1.80 (không phải 1.85) | Tính tip trên surcharge |
| 5.00, 15%, card, tax, tip 18% | 5.54 (làm tròn cuối chỉ ra 5.53) | Làm tròn một lần ở cuối |
| Split 3 × 3.33, tax 0.89 | 0.31 / 0.29 / 0.29 | Làm tròn thường thay vì xuống; sai quy tắc hòa |
| Split 1.00 + 2.33, tax 0.30 | 0.09 / 0.21 | Xu dư dồn vào phần đầu thay vì phần lớn nhất |
| 12 tổ hợp discount × card × tip | tổng từng khoản các phần = order | Lệch 1 cent khi chia |

**Kiểm chứng verifier** (đột biến từng rule rồi trả lại):
| Đột biến | Test đỏ |
|---|---|
| `AwayFromZero` → `ToEven` | 7 |
| Allocation làm tròn thường thay vì xuống | 1 |
| Surcharge không bị thuế | 3 |
| Surcharge bị tính tip | 3 |
| Xu dư vào phần đầu tiên | 1 |

Bài học: test "tổng các phần = tổng order" **không** bắt được đột biến allocation, vì xu dư (kể cả âm) luôn bù đúng
phần chênh. Bất biến tổng là cần nhưng không đủ; phải có test chốt số từng phần.

**Giả định / câu hỏi còn mở:**
- Split có bắt buộc ≥ 2 phần không? Hiện cho phép 1 phần (bằng nguyên order).
- Split chỉ tính khi gọi, không lưu. Có cần lưu và khóa order sau khi split không?
- Đổi phương thức thanh toán sau khi split hoặc sau khi thanh toán: chưa có vòng đời order.
- Một phần trả thẻ, phần khác trả tiền mặt: hiện surcharge tính theo phương thức của cả order.
- Order tạo trước migration có `card_surcharge_rate = 0`: chỉ là DB dev.

### Ngày 3: architecture test (2026-10-26)
**Đã làm:** project `tests/Pos.ArchitectureTests` (NetArchTest + Mono.Cecil), 6 test, chạy ~1 s, không cần DB:
- Layer: Domain không phụ thuộc Application/Infrastructure/Api/EF Core/ASP.NET/FluentValidation; Application không
  phụ thuộc Infrastructure/Api/EF Core/Npgsql/ASP.NET; Infrastructure không phụ thuộc Api.
- Không `double`/`float`/`Half` ở bất kỳ assembly `src/` nào: quét IL (field, signature, local, hằng, `conv.r8`, lời gọi).
- Không `decimal` thô trong signature public/protected của Domain + Application. Allowlist có lý do: `Money`,
  `Percentage`, `PricingOptions`, `MoneyLimits`.
- Interface `*Repository` không trả type của Application (DTO, read model).
- Message lỗi in đủ từng member vi phạm và cách sửa, để agent tự sửa không cần đọc test.

**Kiểm chứng verifier** (đột biến từng rule rồi trả lại):
| Đột biến | Test đỏ |
|---|---|
| `[Owned]` của EF Core trên `OrderLine` (Domain thêm package EF) | 1 (layer Domain) |
| Class trong Application có property `DbContext` | 1 (layer Application) |
| Field + property `double` trong `Order` | 1 (floating point) |
| `PercentOf` làm tròn qua `(double)` + `Math.Round` | 1 (floating point), **sau khi đổi sang quét IL** |
| `Order.TipFor(decimal percent)` | 1 (decimal signature) |
| `IOrderReportRepository` trả `OrderSummaryDto` | 1 (repository) |

**Chuyện đáng kể:** bản đầu dùng `HaveDependencyOnAny("System.Double")` của NetArchTest. Đột biến field `double` bị bắt,
nhưng đột biến `PercentOf` tính qua `double` thì **qua hết**: NetArchTest không thấy lệnh `conv.r8` và lời gọi
`Math.Round(double, …)` trong thân method. Cả 63 domain test (có ca midpoint x.xx5) cũng **xanh** với đột biến đó. Tức là
một lỗi tiền thật lọt qua cả test nghiệp vụ lẫn arch test "chuẩn". Sửa bằng `FloatingPointScanner` đọc IL bằng Mono.Cecil;
đột biến giờ đỏ và chỉ đúng `Order.PercentOf: call System.Math::Round(System.Double, …)`.
Bài học: verifier cũng phải được kiểm bằng đột biến; check "trông đúng" có thể chỉ quét bề mặt.

**Giả định / câu hỏi còn mở:**
- Allowlist decimal là phán đoán của mình. `PricingOptions` có thể bind thẳng sang `Percentage` để bỏ khỏi allowlist.
- Rule decimal chỉ quét public/protected; helper private trong `Order` dùng `decimal` để làm tròn là cố ý.
- Domain test không bắt được làm tròn qua `double` với các số hiện có: có nên thêm ca số mà `double` làm lệch cent không?
