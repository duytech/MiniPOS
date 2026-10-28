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

### Ngày 4: warnings = errors, analyzers, ESLint, Vitest (2026-10-26)
**Đã làm:**
- `Directory.Build.props` cho mọi project: `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`,
  `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` (để IDE0005 chạy khi build; CS1591 tắt).
- `.editorconfig` root: IDE0005 (using thừa) và IDE0161 (file-scoped namespace) là warning; `Migrations/` là generated code.
  `tests/.editorconfig` tắt CA1707 (tên test có gạch dưới).
- Bật lên là ra 7 vi phạm thật, đã sửa: CA1859 (`Order.Allocate` nhận `List<Money>`), CA1305 ×3 (`decimal.Parse`,
  `string.Format` trong test dùng `CultureInfo.InvariantCulture`), CA1310 (`EndsWith` thêm `StringComparison.Ordinal`),
  CS1573 ×2 (thiếu `<param>` cho test split). Không rule nào phải tắt trong `src/`.
- Angular: `strict` + `strictTemplates` đã bật từ trước. Thêm angular-eslint 20 (typescript-eslint recommended +
  stylistic, angular recommended, template recommended + accessibility), `npm run lint`; code hiện có pass luôn.
- Karma/Jasmine → Vitest (builder `@angular/build:unit-test`, jsdom). Port matcher Jasmine; `fakeAsync`/`tick` không chạy
  (lỗi `Expected to be running in 'ProxyZone'`, Vitest không được zone.js patch) nên đổi sang `vi.useFakeTimers()` +
  `vi.advanceTimersByTime`. Thêm `orders-api.spec.ts` (3 test). Tổng web 15 test, ~3 s, không cần Chrome.

**Kiểm chứng verifier** (đột biến từng rule rồi trả lại):
| Đột biến | Check đỏ |
|---|---|
| `using System.Text;` thừa trong Domain | build: IDE0005 |
| `decimal.Parse(s)` không culture | build: CA1305 |
| `string? s` rồi `s.Length` | build: CS8602 |
| Block namespace | build: IDE0161 |
| `any` tường minh trong component | lint: `no-explicit-any` |
| Constructor injection thay `inject()` | lint: `prefer-inject` (+ `no-unused-vars`) |
| `<img>` không `alt`, `<div (click)>` | lint: `alt-text`, `click-events-have-key-events`, `interactive-supports-focus` |
| `OrdersApi` luôn gửi `search` | Vitest: 2 test |
| Bỏ `distinctUntilChanged` ở search | Vitest: 1 test ("trimmed search text did not change") |

**Chuyện đáng kể:** nếu chỉ đổi runner mà giữ `fakeAsync`, 8/10 test orders-list đỏ, nhưng 7 cái trong đó đỏ vì lỗi dây
chuyền (`afterEach` `http.verify()` fail → TestBed không reset → "Cannot configure the test module"). Lỗi gốc chỉ có ở
test đầu tiên dùng `fakeAsync`. Bài học cho agent: đọc lỗi đầu tiên, không sửa từng lỗi theo thứ tự in ra.

**Giả định / câu hỏi còn mở:**
- **`npm audit`: Vitest 3 kéo theo 2 critical + 2 moderate** (`tinypool` prototype pollution, `@vitest/mocker` path
  traversal; trước đó 0). Chỉ là devDependency chạy local, nhưng bản sửa là Vitest 4+, mà `@angular/build` 20 chỉ nhận
  `vitest ^3`. **Quyết định (2026-10-26): chấp nhận tạm**; xử lý khi lên Angular 21 (cho phép Vitest 4+).
- Builder `unit-test` của Angular 20 còn EXPERIMENTAL (in cảnh báo mỗi lần chạy).

### Ngày 5: hook `PostToolUse` (2026-10-26)
**Đã làm:** `.claude/settings.json` (commit vào repo) gọi `.claude/hooks/verify-on-edit.mjs` sau mỗi `Edit`/`Write`/`MultiEdit`.
Script Node đọc `tool_input.file_path` rồi chọn check theo file:
- `.cs`, `.csproj`, `.props`: `dotnet build Pos.slnx`, xanh thì chạy `dotnet test` cho Domain, Application, Architecture
  (không cần DB). Integration test không chạy trong hook.
- `web/src/**/*.ts|html`: `eslint` riêng file đó + `ngc -p tsconfig.app.json --noEmit` (bắt cả lỗi template; `tsc` thường
  không thấy `.html`). File `.spec.ts`: `tsc -p tsconfig.spec.json`.
- File khác (`.md`, `.json`…): thoát ngay (~0.2 s).
- Lỗi: exit 2, stderr chỉ giữ dòng cần để sửa (lỗi compiler không trùng; với test: tên test, Expected/Actual hoặc danh sách
  vi phạm của arch test, một dòng stack trỏ vào file `.cs`). Tối đa 40 dòng.

Thời gian khi xanh: `.cs` ~6.5 s (build ~4 s + 3 project test), `.ts` ~6.7 s (eslint ~4 s chủ yếu là khởi động, ngc ~2.5 s).

**Kiểm chứng verifier** (đột biến, đưa JSON giống Claude Code vào script, rồi trả lại; thêm 1 lần qua `Edit` thật):
| Đột biến | Check đỏ | Agent thấy |
|---|---|---|
| `PercentOf` làm tròn `ToEven` | Domain tests | 7 test, `Expected: 0.35 USD / Actual: 0.34 USD` + dòng test |
| `public static double Ratio => 0.5` trong `Order` | Arch test | `Order.get_Ratio: signature`, `ldc.r8 0.5` |
| Sai chữ ký method | build | `CS7036` kèm file:dòng |
| `list(query: any)` | eslint | `no-explicit-any`, `no-unused-vars` |
| `PAGE_SIZE: string = 20` | ngc | `TS2322`, `TS2345` |
| `{{ order.numbr }}` trong template | ngc | `TS2551 ... Did you mean 'number'?` |
| Sai kiểu trong spec | tsc spec | `TS2322` |

**Chuyện đáng kể:**
- Bản đầu dùng `dotnet test -v q`: chỉ in tên test `[FAIL]`, không có Expected/Actual. Agent sẽ phải chạy lại test để biết
  sai bao nhiêu. Đổi sang logger `console;verbosity=normal` và tự lọc block lỗi.
- Bản đầu lọc theo regex dòng: lỗi arch test mất danh sách vi phạm (dòng message không khớp regex). Đổi sang parser giữ
  cả block message giữa `Failed …` và `Stack Trace:`.
- `ngc` vẫn in mã màu ANSI dù có `--pretty false`, làm regex `error TS` không khớp; script xóa ANSI trước khi lọc.
- Đột biến `double` lần đầu bị CA1822 (analyzer Ngày 4) bắt trước khi tới arch test: hai verifier chồng nhau là tốt, nhưng
  muốn kiểm arch test thì đột biến phải qua được analyzer.

**Giả định / câu hỏi còn mở:**
- Hook không chạy integration test (cần DB) và Vitest (~3 s mỗi lần). CLAUDE.md ghi phải chạy trước khi xong. Ngày 6 xem
  agent có quên không.
- Sửa nhiều file `.cs` liên tiếp = build mỗi lần (~6 s). Nếu Ngày 6 thấy chậm: gom, hoặc chỉ test project bị ảnh hưởng.
- Hai `Edit` song song có thể chạy hai `dotnet build` cùng lúc trên cùng `obj/`: chưa gặp, theo dõi.
- Mức `latest-recommended` là lựa chọn đã hỏi; `latest-all` sẽ bắt thêm nhiều (CA1062, CA1848…) nhưng ồn.

### Ngày 6: tính năng mới với hook bật: discount / tip / payment method API (2026-10-26)
**Đã làm:** đưa `ApplyDiscount`, `RemoveDiscount`, `SetTip`, `RemoveTip`, `SetPaymentMethod` (đã có trong Domain từ
Ngày 1–2) ra API: `PUT|DELETE /api/orders/{id}/discount`, `PUT|DELETE .../tip`, `PUT .../payment-method`. Mỗi thao tác
một folder Application (Command + Handler + Validator). Domain thêm `EnsureOpen()` (cả `AddItem`): order không `Open` → 422.
`PercentageJsonConverter` chặn rate âm / quá 4 chữ số thập phân ngay lúc đọc JSON (400), vì `new Percentage(-1)` ném
`ArgumentOutOfRangeException` = 500 nếu để tới handler. `PercentageLimits.MaxRate` (999.9999, cột numeric(7,4)) cho tip.
Test: Domain (10 case order đóng), validator, 33 integration (đến từng cent, có midpoint 0.105 → 0.11, list total ==
GET total, 400/404/422), 7 concurrency (tip/discount song song; mỗi thao tác chạy song song với add-item: chỉ 200/409,
DB lưu đúng các request 200). Kiểm thêm: 409 thật sự xảy ra (9–18 trên 20 request), không phải test chạy tuần tự.

**Hook trong vòng lặp:** khoảng 30 lần chạy, mỗi lần `.cs` ~2–7 s. 5 lần đỏ:
| Lần | Check | Lỗi | Loại |
|---|---|---|---|
| Thêm lời gọi `EnsureOpen()` trước khi viết method | build | `CS0103` | trạng thái dở dang, sửa ở edit kế |
| `PercentageLimits` (const decimal) trong Application | arch test | `PercentageLimits.MaxRate` dùng raw decimal | **lỗi thật**: thêm vào `RawDecimalAllowed` kèm lý do |
| `DependencyInjection.cs`: body rồi `using` (2 edit song song) | build | 10× `CS0246` | **nhiễu**: edit thứ hai đã sửa |
| `OrdersController.cs`: cùng kiểu | build | 12× `CS0246` | **nhiễu** |
| `ValidatorTests.cs`: `using` trước, body sau | build | `IDE0005` | **nhiễu** (đảo thứ tự vẫn đỏ) |

**Chuyện đáng kể:**
- Lỗi thật duy nhất là arch test, và hook bắt nó ngay ở file vừa tạo, trước khi có code nào dùng hằng số đó.
- 3/5 lần đỏ là nhiễu: hai edit phụ thuộc nhau trong cùng một lượt, hook chạy sau edit đầu nên thấy trạng thái dở. Thứ
  tự nào cũng đỏ (`CS0246` hoặc `IDE0005`). Hook không sai; cách sửa nằm ở cách agent chia edit. Đã thêm một dòng vào
  `CLAUDE.md`: thay đổi chỉ compile khi đi cùng nhau thì làm trong một `Edit`/`Write`. Mỗi lần nhiễu tốn 1 lệnh build
  kiểm lại (~2 s), không tốn vòng sửa.
- Không cần chỉnh tốc độ hook: build incremental ~2 s, cả vòng ~6 s.
- Mutation thử "bỏ `IsRowVersion`" làm hỏng cả tạo order (500 ở insert), không chứng minh gì cho test mới. Thay bằng đếm
  409 trong một lần chạy.
- Hook không chạy integration test: 33 + 7 test mới đều qua ngay lần đầu, nhưng phải chạy tay.

**Giả định / câu hỏi còn mở:**
- Chỉ order `Open` được thêm món, đổi discount/tip/payment method (đã hỏi, gồm cả `AddItem`): khác `Open` → 422.
- Chưa có endpoint đóng order (Paid/Cancelled); test đặt status bằng reflection (Domain) và SQL (integration).
- Tip không giới hạn trên (đã hỏi); chỉ giới hạn theo cột numeric(7,4). Số tiền tip không được lưu (Domain tính khi
  đọc), nên tip lớn không gây lỗi DB.
- Fixed discount được kiểm với subtotal lúc áp dụng; nếu sau này có xóa món, discount có thể vượt subtotal.
- `OrderDto` không trả `tipRate` / discount rate (chỉ số tiền): frontend sau này có thể cần.

### Ngày 7: task dễ lỗi làm tròn: chia đều hóa đơn cho N người (2026-10-27)
**Đã làm:** `Order.SplitEvenly(int parts)` trong Domain, ra API `GET /api/orders/{id}/split-even?parts=N` (chỉ tính,
không lưu, nên không có concurrency test). Order tính xong trước; rồi subtotal, discount, surcharge, tax, tip **mỗi khoản
chia riêng** cho N, làm tròn xuống (`ToZero`), cent dư của từng khoản dồn vào **phần đầu** (đã hỏi). Total từng phần =
cộng các khoản của phần đó. Validator: `1 ≤ Parts ≤ 20`, `[BindRequired]` cho query (thiếu `parts` → 400, không lặng lẽ
thành 0). Test: 12 Domain (có ca 0.195 → 0.19, $0.05 / 10 → 0.05 + 9 × 0.00), 1 validator theory, 7 integration
(chuỗi JSON 2 chữ số, tổng các phần == total của `GET`, 400/404).

**Kiểm chứng verifier** (đột biến từng chỗ rồi trả lại):
| Đột biến | Domain test | Integration | Arch test |
|---|---|---|---|
| M1 chia bằng `AwayFromZero` thay `ToZero` | 2 exact-cent → **7** sau khi siết theory | 1 | 0 |
| M2 bỏ dồn cent dư | 8 | 2 | 0 |
| M3 cent dư vào phần cuối | 7 | 2 | 0 |
| M4 total phần = total order / N (không cộng từ các khoản) | 1 → **6** sau khi siết theory | **0** | 0 |
| M5 chia qua `double` + `Math.Floor` | **0** | **0** | 1 (`FloatingPointScanner`) |
| M6 handler tự tính total phần = total / N | 0 | 2 | **0** |

**Chuyện đáng kể (câu chuyện phỏng vấn "AI output looked correct but was wrong"):**
- Test bất biến "mọi khoản của các phần cộng lại đúng bằng order" **xanh với M1 và M4**. M1 làm phần đầu thành
  **−0.04** ($0.05 / 10: mỗi phần 0.01, phần đầu nhận 0.05 − 0.09); tổng vẫn đúng vì cent dư âm bù lại. M4 làm total
  phần lệch với các khoản của chính nó (2.77 thay 2.80) nhưng tổng 4 phần vẫn 11.05. "Cộng lại đúng" là điều kiện cần,
  không đủ. Siết theory: mỗi phần `Total == Subtotal − Discount + Surcharge + Tax + Tip`, và chênh lệch phần đầu so với
  phần khác của mỗi khoản nằm trong `[0, N−1]` cent. Sau đó M1 và M4 đỏ 5/6 case theory.
- M4 qua hết integration test: ca $10 / 4 cho ra đúng 2.79 / 2.76 dù công thức sai (trùng hợp số). Ca $100 / 3 qua
  M1 vì 33.333… làm tròn kiểu nào cũng 33.33. Một ca số đẹp không chứng minh gì; cần ca midpoint (0.195) và ca N > số cent.
- M5 (`double`) qua mọi test nghiệp vụ, chỉ arch test IL của Ngày 3 bắt. Lặp lại bài học Ngày 3.
- M6 (tính lại total ngoài Domain) vi phạm thẳng "Totals are computed only by Domain" nhưng **không check cấu trúc nào
  bắt**; chỉ đỏ vì số lệch ở integration test. Nếu số trùng, nó lọt. Lỗ hổng verifier: chưa có arch test cấm
  `decimal.Round`/`Math.Round` ngoài Domain.

**Giả định / câu hỏi còn mở:**
- N lúc đầu không giới hạn trên, nhưng `parts=2000000000` sẽ tạo ~2 tỉ phần trong bộ nhớ (hết bộ nhớ = 500). Đã hỏi
  lại, user chọn **N ≤ 20** (2026-10-27): `SplitOrderEvenlyValidator.MaxParts`, `parts` 21 hoặc 2000000000 → 400.
  Integration test: 20 phần của $1.09 → 0.14 + 19 × 0.05.
- Chia đều được ở mọi status (giống `GET`), kể cả order đã đóng.
- Phần có thể là 0.00 khi N > số cent; không chặn.

### Sau Ngày 7: verify trước `git commit` (2026-10-27)
**Lý do:** ở Ngày 7, agent commit ngay trong lượt. Hook `Stop` chỉ xem thay đổi **chưa commit**, nên lúc lượt kết thúc nó
không còn gì để kiểm: code vẫn được kiểm vì agent tự chạy build/test, nhưng hook không bảo vệ gì.

**Đã làm:** `verify-on-edit.mjs --before-commit` chạy như hook `PreToolUse` (Bash, PowerShell). Lệnh không chạy
`git commit` → thoát ngay. Có `git commit` → cùng các check như hook `Stop` trên thay đổi chưa commit (working tree,
gồm cả file đã stage); đỏ → exit 2, commit bị chặn. Nhận diện lệnh: bỏ heredoc, here-string và chuỗi trong ngoặc trước,
bỏ qua option `-C`/`-c` sau `git`.

**Kiểm chứng** (đột biến "cent dư vào phần cuối" đặt sẵn, đưa JSON giống Claude Code vào script):
| Lệnh | Kết quả |
|---|---|
| `git status`, `git log`, `echo "git commit -m x"`, heredoc chứa `git commit` | 0, < 1 s |
| `git commit -m "x"` | 2, ~13 s, in test Domain đỏ |
| `git add -A && git commit -qm …`, `git -C . commit`, here-string PowerShell, `cd … && git commit --amend` | 2 |
| Không còn đột biến, chỉ đổi `.mjs`/`.json` | 0 |

**Giả định / câu hỏi còn mở:**
- Kiểm working tree, không phải đúng phần được stage: build chạy trên file đang có, và `git add -A && git commit` trong
  cùng lệnh chưa stage gì lúc hook chạy. Commit một phần (stage vài file) có thể bị chặn bởi lỗi ở file không commit.
- Mỗi commit có `.cs` thay đổi tốn thêm ~6–13 s. Hook `Stop` vẫn giữ nguyên.
- Vẫn không chạy integration test và Vitest.

### Sau Ngày 7: arch test "chỉ Domain làm tròn" (2026-10-27)
**Lý do:** đột biến M6 của Ngày 7 (handler tự tính total phần = `decimal.Round(total / N)`) vi phạm "Totals are computed
only by Domain" nhưng không check cấu trúc nào bắt; nó chỉ đỏ vì số tình cờ lệch ở integration test.

**Đã làm:** `RoundingScanner` (Mono.Cecil, cùng cách với `FloatingPointScanner`) tìm mọi lời gọi `Round`, `Floor`,
`Ceiling`, `Truncate` của `decimal`/`Math`/`MathF` trong IL của Application, Infrastructure, Api, kể cả trong lambda và
method async (tên được đưa về method trong source: `<HandleAsync>b__0` → `HandleAsync`). Test
`Only_domain_rounds_amounts`. Allowlist `RoundingAllowed` kèm lý do: `MoneyJsonConverter.Read` và
`PercentageJsonConverter.Read` dùng `decimal.Round(x, n) != x` để từ chối JSON quá số chữ số, không tính tiền.

**Kiểm chứng** (đột biến trong `SplitOrderEvenlyHandler`, chỉ chạy arch test):
| Đột biến | Arch test |
|---|---|
| Allowlist rỗng | đỏ (2 converter) |
| M6 `decimal.Round(total / N)` trong method async | **đỏ** (trước đây xanh) |
| `Math.Round` trong lambda | đỏ |
| `decimal.Floor(total × 100 / N) / 100` | đỏ |
| Làm tròn qua `ToString("0.00")` rồi `decimal.Parse` | **xanh** |
| Tính lại total bằng toán tử `Money` (`p.Subtotal + p.Surcharge + …`) | **xanh** |

**Chuyện đáng kể:** lần chạy đột biến đầu, script trả file bằng `git checkout -- tests/…` và **xóa luôn test mới chưa
commit**. Kết quả "6/6 xanh" trông như test không bắt được gì, thật ra test không còn tồn tại. Số test (6 thay vì 7) là
dấu hiệu duy nhất. Sửa: commit test trước, chỉ trả `src/`. Bài học: verifier của verifier cũng cần kiểm số test chạy.

**Thêm: arch test "chỉ Domain cộng trừ Money"** (cùng PR). `RoundingScanner` đổi thành `CallScanner` nhận điều kiện:
`IsRounding` và `IsMoneyArithmetic` (`Money` `op_Addition`, `op_Subtraction`, `op_Multiply`). Test
`Only_domain_adds_up_money`, không cần allowlist (ngoài Domain hiện không có chỗ nào dùng). Đột biến trong
`SplitOrderEvenlyHandler`, chạy arch test và integration test:
| Đột biến | Arch test | Integration |
|---|---|---|
| Total = `p.Subtotal + p.Surcharge + p.Tax + p.Tip` (quên discount) trong lambda | **đỏ** (`op_Addition`) | 1 |
| `parts[0].Total - parts[0].Discount` trong method async | **đỏ** (`op_Subtraction`) | 1 |
| `parts[^1].Total * N` | **đỏ** (`op_Multiply`) | 3 |
| Cộng `.Amount` (decimal) rồi bọc `new Money(…)` | xanh | 1 |
| Làm tròn qua `ToString("0.00")` + `decimal.Parse` | xanh | 3 |

**Giả định / câu hỏi còn mở:**
- Hai cách vẫn lọt arch test: cộng trừ trên `.Amount` (decimal) rồi bọc lại thành `Money`, và làm tròn qua chuỗi. Cả
  hai đều bị integration test bắt với số hiện có, nhưng chỉ vì số lệch. Có thể cấm toán tử `decimal` trên `Money.Amount`
  ngoài Domain, nhưng các validator so sánh `decimal` hợp lệ, nên rule sẽ phức tạp hơn. Chưa làm.

## Tuần 3

### Ngày 1: skill `add-endpoint` (2026-10-27)
**Đã làm:** `.claude/skills/add-endpoint/SKILL.md` (bước 0 hỏi business rule → Domain → Application → API → bảng
test theo loại endpoint → verify) và `references/templates.md` (file mẫu để chép: `SetTip`, `RemoveTip`,
`SplitOrderEvenly`, test adjustment, test concurrency). Skill không chép lại CLAUDE.md, chỉ nêu thứ tự và file mẫu.

**Thử skill:** giao `DELETE /api/orders/{id}/lines/{lineId}` cho một sub-agent mới (worktree riêng, không có ngữ cảnh
phiên này), chỉ kèm 4 business rule đã chốt với user: chỉ order `Open` (khác → 422); discount cố định lớn hơn
subtotal mới → chặn, 422; xóa dòng cuối được; trả 200 + `OrderDto`. Agent dùng Skill tool, đọc templates, xong trong
~5 phút, 45 tool call: `Order.RemoveLine`, folder `RemoveOrderLine`, action có 400/404/409/422, 8 Domain test (có
midpoint 0.105 → 0.11), 14 integration test, 3 concurrency test (xóa các dòng khác nhau song song; xóa đua với
add-item; xóa đua với discount cố định). 263 test xanh, 409 thật sự xảy ra. Không hỏi thêm câu nào ngoài 4 rule.

**So với trước:** Ngày 3 (không hướng dẫn) agent sai nhiều quy ước; Ngày 6 (CLAUDE.md + hook) phải có mình làm. Lần này
code production không vi phạm quy tắc nào trong CLAUDE.md, và agent tự nghĩ ra ca race "xóa dòng ↔ discount cố định"
mà skill không nói.

**Chỗ lệch tìm được và đã sửa skill:**
| Chỗ lệch | Nguyên nhân | Sửa |
|---|---|---|
| Đọc `references/templates.md` theo đường dẫn tương đối, không thấy | Worktree tạo từ `main`, chưa có skill; Skill tool nạp từ checkout chính | Ghi rõ "trong folder của skill, cạnh SKILL.md" |
| Template concurrency "N request song song, alone" sai cho xóa (xóa cùng dòng → 404 là đúng) | Template viết theo `SetTip` | "N request mà mỗi cái tự nó thành công (vd. các dòng khác nhau)"; cho phép 422 khi race phá rule Domain |
| Template controller (`RemoveTip`) thiếu 400 dù validator có thể trả 400 | Code cũ thiếu | Template có 400; skill nói không chép chỗ thiếu này |
| Test race tính subtotal bằng `SUM(unit_price * quantity)` trong SQL | Skill chưa nói; CLAUDE.md cấm total trong SQL nhưng chỉ nói về code | Skill: test so tiền với `GET`, SQL chỉ đọc cột lưu sẵn. Đã sửa test của agent |

**Giả định / câu hỏi còn mở:**
- Chưa đột biến skill (bỏ một dòng checklist xem agent còn làm không).
- `RemoveTip`/`RemoveDiscount` thiếu `[ProducesResponseType]` 400 trong code hiện có; chưa sửa.
- `Order.RemoveLine` vẫn ném `DomainException` khi dòng không thuộc order, nhưng qua API thì handler trả 404 trước.
- Sub-agent chạy worktree từ `main` chứ không phải nhánh hiện tại: muốn thử skill chưa merge thì phải để ý.

**Chạy lại với skill đã sửa:** `PUT /api/orders/{id}/lines/{lineId}/quantity`, sub-agent mới, **không đưa business
rule nào** (lần 1 đưa sẵn 4 rule nên bước 0 chưa được thử). Agent chạy trên nhánh tách từ nhánh skill, trong checkout
chính (worktree tạo từ `main` không có skill mới).
- Sau 6 tool call (đọc `Order`, `OrderLine`, `RemoveLine`, `AddItem`), agent **dừng và hỏi 7 câu**, không viết dòng code
  nào: body là số lượng mới hay +/-, quantity 0 (400 hay xóa dòng), giới hạn trên, status, discount cố định lớn hơn
  subtotal mới, đổi về cùng số lượng, response. Cả 7 đều là rule thật mà task không nói; không câu nào thừa. User trả lời:
  thay thế, 0 → 400, không giới hạn, chỉ `Open`, discount → 422, cùng số lượng → 200 không đổi, 200 + `OrderDto`.
- Được resume với câu trả lời, xong trong ~4 phút, 18 tool call: `Order.ChangeLineQuantity`, folder
  `ChangeOrderLineQuantity`, action có 400/404/409/422, 9 Domain test, 18 integration test, 3 concurrency test.
  Toàn bộ 296 test xanh (105 / 61 / 8 / 122), mình build và chạy lại để xác nhận.

| | Lần 1 (`DELETE` line, có sẵn rule) | Lần 2 (`PUT` quantity, không có rule, skill đã sửa) |
|---|---|---|
| Hỏi business rule trước khi code | không cần (đã cho sẵn) | **7 câu, dừng trước khi code** |
| Vi phạm CLAUDE.md trong code production | 0 | 0 |
| Lệch so với skill / do skill thiếu | 4 (đường dẫn templates, mẫu concurrency, thiếu 400, cộng tiền bằng SQL trong test) | **0** |
| Concurrency test | 3, phải tự suy ra "các dòng khác nhau" | 3, đúng mẫu mới (các dòng khác nhau, đua add-item, đua discount cho phép 422) |
| Test so tiền | 1 chỗ `SUM(unit_price * quantity)` trong SQL | chỉ đọc từ `GET` |
| 400 trên action | có (tự suy) | có (theo skill) |

**Nhận xét:**
- 4 chỗ sửa skill sau lần 1 đều có tác dụng ở lần 2; không thấy chỗ lệch mới.
- Kiểm tra "fixed discount ≤ subtotal mới" giờ có 3 bản trong `Order` (`ApplyDiscount`, `RemoveLine`,
  `ChangeLineQuantity`). Không vi phạm rule nào, nhưng nên gom về một helper private.
- Không giới hạn quantity: `int.MaxValue` × 2.00 cho total ~4.29 tỷ, trả 200. Đúng như đã chốt; total không lưu trong DB
  nên không tràn cột numeric(12,2).

### Ngày 2: skill `add-angular-feature` (2026-10-27)
**Đã làm:** `.claude/skills/add-angular-feature/SKILL.md` + `references/templates.md`, rút từ `orders-list` (tính năng
Angular duy nhất): bước 0 hỏi hành vi UI; model → service → component (`OnPush`, `inject()`, một signal `state` qua
`toSignal` + `scan`); `switchMap` cho đọc, `concatMap`/`exhaustMap` cho ghi (không bao giờ `switchMap`); tiền chỉ
`currency` pipe, kể cả không cập nhật lạc quan; reload giữ dữ liệu cũ; bảng test Vitest. Plan:
`plans/tuan-3-ngay-2-skill-add-angular-feature.md`.

**Thử skill:** trang chi tiết order `/orders/:id` (dòng món, 6 khoản tiền, xóa dòng, −/+ số lượng, link từ danh sách),
sub-agent mới, **không đưa rule nào**.
- Sau 6 tool call, agent **dừng và hỏi 10 câu** (lỗi 422/409/404/500, xác nhận xóa, kiểu điều khiển số lượng, bấm
  thêm khi đang chạy, order đóng, điều hướng, trường hiển thị); API shape thì tự đọc, không hỏi. User chốt: 422 hiện
  `detail`; 409 và 404 dòng → "The order changed. Showing the latest version." + tự tải lại; 404 khi mở → "Order not
  found." + link; mạng/500 → câu chung, giữ dữ liệu cũ; không xác nhận xóa; nút −/+, − khóa ở 1; **mọi click đều tính
  (`concatMap`)**; order đóng chỉ đọc; link ở số order + "Back to orders"; trường như agent đề xuất.
- Resume, xong trong ~4 phút, 17 tool call: 19 test component, 3 test service, 1 test link; build, lint, 38 Vitest xanh.
- Điểm dễ sai nhất làm đúng: mỗi lần ghi trong `concatMap` tính số lượng từ order **mà lần ghi trước trả về**
  (`defer` đọc `state()`), nên 3 click + nhanh gửi 3, 4, 5. Reload sau 409/404 nằm trong cùng bước `concatMap`, nên các
  click đang xếp hàng bắt đầu từ order mới nhất. Frontend không tính tiền.

**Kiểm bằng app thật** (API + `npm start`, browser pane), order #11: Pho bo 2 × 9.50 + Iced tea 1 × 2.00, discount
cố định 15.00:
| Thao tác | Kết quả |
|---|---|
| Click `#11` trong danh sách | mở `/orders/{id}`, đủ dòng và 6 khoản |
| 3 click + nhanh trên Iced tea | DB lưu quantity 4 (1 → 4), màn hình $27.00 / $13.07 khớp API |
| Remove Pho bo (subtotal mới < discount) | hiện nguyên `detail` 422 của API, order giữ nguyên |
| Xóa Iced tea bằng API khác, rồi + trên Iced tea | 404 → "The order changed. Showing the latest version." + tải lại, dòng biến mất |
| Tab title | **"Order � Mini POS"** |

**Chỗ lệch tìm được:**
| Chỗ lệch | Check nào bắt | Sửa |
|---|---|---|
| `app.routes.ts` bị ghi bằng Latin-1: `·` thành 1 byte, tab hiện `�` | **không check nào** (build, lint, ngc, Vitest đều xanh); chỉ thấy khi mở app thật | Hook verify (Stop + trước commit) chặn file text không phải UTF-8; skill cảnh báo `Set-Content` |
| Agent không mở app thật (skill cho phép bỏ qua "nếu chạy được") | — | Skill: bắt buộc mở trang với API thật, kiểm cả tab title; nếu không chạy được thì nói rõ |
| Tự quyết "bỏ qua click xếp hàng không còn hợp lệ" (− về 0, Remove lần 2) rồi mới hỏi | — | Bước 0 thêm câu này. User giữ cách của agent |

**Chuyện đáng kể:** lỗi mã hóa lọt qua cả 4 verifier của `web/`, vì với compiler và test, `'Order \xb7 Mini POS'` vẫn
là một chuỗi hợp lệ. Chỉ visual check mới thấy. Đây là lý do cho Ngày 5 (Playwright, visual loop).

**Giả định / câu hỏi còn mở:**
- Chưa chạy lại agent với skill đã sửa (3 chỗ lệch đều nhỏ, không ở logic).
- Route title cố định "Order · Mini POS", không có số order.
- Danh sách không giữ bộ lọc khi quay lại (user chọn để sau).

### Ngày 3: sub-agent planner + implementer (2026-10-27)
**Đã làm:** `.claude/agents/planner.md` (tool chỉ có `Read, Glob, Grep`: không sửa file, không chạy lệnh; có rule
còn mở thì chỉ trả câu hỏi rồi dừng, đủ rule thì trả plan theo tầng + bảng test + verify) và
`.claude/agents/implementer.md` (nạp sẵn skill `add-endpoint` + `add-angular-feature`; chỉ làm theo plan đã duyệt,
thiếu rule thì dừng hỏi, không commit/push, tự chạy integration test + Vitest trước khi báo). Agent chỉ được nạp khi
session khởi động: session tạo file không gọi được (`Agent type 'planner' not found`), phải chạy ở session mới.

**Thử chuỗi** với task "let the cashier close an order - mark it as paid or cancelled - through the API", **không đưa
rule nào**:
| Cổng | Kết quả |
|---|---|
| Planner, lần 1 | Thử tạo `plans/planner-probe.txt`: **không được** (không có tool ghi). 11 tool call, dừng và hỏi **8 câu**: transition nào, lặp lại pay/cancel, order rỗng, pay ghi gì, timestamp, lý do hủy, dạng endpoint, phạm vi. Mỗi câu kèm lựa chọn và code thay đổi gì. Tự đọc được những gì code đã trả lời (`OrderStatus`, `EnsureOpen`, web đã chỉ đọc khi đóng) |
| User trả lời | Chỉ Open → Paid/Cancelled; lặp lại → 422; order rỗng đóng được; pay chỉ đổi status (dùng `PaymentMethod` đã lưu); lưu `PaidAt`/`CancelledAt`; hủy không cần lý do; `POST .../pay` và `.../cancel`; chỉ API |
| Planner, lần 2 | Plan đầy đủ (`plans/tuan-3-ngay-3-dong-order.md`): file theo tầng, ca test có số (0.70, 15% → 0.105 → 0.11), 3 concurrency test, và **tự tìm 5 chỗ code cũ** đặt status bằng SQL/reflection cần thay bằng endpoint mới, cả dòng trong templates của skill. Tự nhận ra mẫu concurrency của skill ("mỗi request tự nó thành công") không hợp với chuyển trạng thái |
| User duyệt | Kèm giả định planner tự đặt: không backfill `paid_at`/`cancelled_at` cho order đã đóng |
| Implementer | 46 tool call, ~11 phút. `Order.Pay/Cancel` (qua `EnsureOpen`), folder `PayOrder`/`CancelOrder`, migration 2 cột nullable, action 200/400/404/409/422. +10 Domain, +18 integration (15 + 3 concurrency); bỏ hết helper SQL/reflection. Không commit |
| Mình kiểm | Build 0 warning; 115 / 61 / 8 / 140 test xanh. API thật: pay 200 (`paidAt`, total 2.23 không đổi), pay lại / cancel / add-item → 422, `GET` trả `Paid` |

Race thật (implementer log status code 5 lần rồi bỏ log): 10 pay song song luôn 1 × 200 + 9 × 409; pay ↔ cancel
mỗi bên đều từng thắng.

**Chỗ lệch tìm được:**
| Chỗ lệch | Ai | Sửa |
|---|---|---|
| Giả định "không backfill dữ liệu cũ" nằm trong "Rules used" và "Out of scope", không phải câu hỏi; mình phải tự hỏi lại lúc duyệt | planner | `planner.md`: cột mới trên dòng cũ (backfill hay null) là câu hỏi |
| Skill `add-endpoint` chỉ có mẫu concurrency "N request mỗi cái tự thành công", sai với chuyển trạng thái (chỉ một request thắng được) | skill (planner tự bù) | Thêm dòng bảng test: chuyển trạng thái → đúng một 200, còn lại 409/422, ít nhất một 409 |
| Thêm `ApiClient.CloseOrderAsync(id, "Paid"/"Cancelled")` ngoài plan | implementer | Không sửa: helper nhỏ, có báo trong report |
| So `paidAt` đến 1 µs: response giữ tick 100 ns, Postgres giữ µs | implementer | Không sửa: `createdAt` đã như vậy; ghi câu hỏi mở |
| Tự bật Docker Desktop để chạy `docker compose up -d` | implementer | Không sửa: chỉ là máy local, có báo |

**So với Ngày 1–2:** trước đây skill + một agent vừa hỏi vừa làm; giờ hai cổng tách rõ. Planner không thể sửa code nên
"hỏi trước khi làm" là bắt buộc bằng tool, không chỉ bằng chữ. Implementer không hỏi thêm câu nào và không vi phạm
CLAUDE.md. Lỗi duy nhất đáng kể nằm ở planner (giả định ngầm) và ở skill (mẫu test), không ở code.

**Giả định / câu hỏi còn mở:**
- `paidAt`/`cancelledAt` trong response pay/cancel có 7 chữ số thập phân, `GET` sau đó có 6 (giống `createdAt`). Có
  cần làm tròn đến µs trong Domain không?
- 422 khi pay/cancel order đã đóng dùng message chung của `EnsureOpen`: "The order is Paid; only an open order can be
  changed."
- Kiểm API thật tạo thêm món "Probe tea" và order #14 (Paid) trong DB dev.
- Chưa chạy lại planner với `planner.md` đã sửa. Ngày 4 thêm `reviewer`; Ngày 7 chạy cả chuỗi với Split bill.

### Ngày 4: sub-agent reviewer (2026-10-28)
**Đã làm:** `.claude/agents/reviewer.md`. Tool `Read, Glob, Grep, Bash`; prompt chỉ cho Bash chạy git đọc (`diff`,
`log`, `show`, `status`, `merge-base`), không build, không test (hook đã lo). Đọc `CLAUDE.md`, plan nếu có, rồi diff
(`main...HEAD` + chưa commit). Checklist theo nhóm của `CLAUDE.md`: tiền, test tiền, concurrency, API, kiến trúc,
business rule bịa, frontend, config. Mỗi finding: mức (`blocker` / `should-fix` / `note`), `file:line`, rule bị phạm
(trích nguyên văn), ca lỗi có số cent. Dòng cuối `Verdict: approve` hoặc `changes requested`. Không sửa code.

Lần này cũng thấy lại chuyện của Ngày 3: agent mới tạo chưa gọi được trong session (`Agent type 'reviewer' not
found`). Hai lượt đầu chạy bằng `general-purpose` + "đọc `reviewer.md` và làm đúng như vậy". Giữa chừng session nạp
lại danh sách agent, nên lượt 3 chạy bằng `reviewer` thật.

**Thử 1: commit Ngày 3 (pay / cancel), có plan.** `general-purpose` đóng vai reviewer, 7 tool call, ~50 s. Kết quả
`approve`, 2 note:
- Test "ít nhất một 409" có thể flaky: nếu 10 pay chạy lần lượt thì được 1 × 200 + 9 × 422, không có 409. Code đúng,
  chỉ test có thể đỏ.
- Test 400 chỉ kiểm status code, không kiểm body ProblemDetails (test 404/422 cùng file thì có kiểm).

Reviewer tự tính lại các con số trong test (0.105 → 0.11, 13.01) và kiểm `xmin` được bump khi đổi line. Không có
finding bịa.

**Thử 2: diff cài lỗi.** Làm trong worktree tạm, không commit, xóa sau khi chạy. Task giả: "Let the cashier clear all
lines of an open order with DELETE /api/orders/{id}/lines, and show each order's subtotal in the orders list."
Không có plan.
| Lỗi | Cài cố ý? | Lượt 2 (general-purpose) | Lượt 3 (`reviewer` thật) |
|---|---|---|---|
| Tax không làm tròn (`new Money(base × rate)`) | có | blocker, nhưng ca lỗi sai: "tax 0.08875" | blocker, đúng: `Money` ném lỗi khi lẻ cent, nên mọi `GET` order lỗi **500** |
| Tip tính trên số đã cộng tax | có | blocker, 12.39 vs 12.52 | blocker, chỉ ra test cũ sẽ đỏ |
| Chỉ làm tròn total ở cuối | có | blocker | blocker |
| `double Subtotal` + LINQ tự cộng line trong `OrderSummaryDto` | có | blocker (`15.4`) | blocker (`15.4`) |
| Giới hạn quantity ≤ 99 tự bịa | có | should-fix | blocker |
| Endpoint mới không có test, không có concurrency test | có | should-fix | should-fix |
| `ClearLines` bỏ qua kiểm fixed discount, total âm | **không** (lỗi thật của diff) | blocker + hỏi business | blocker + hỏi business |
| Action chỉ khai 200/404, thiếu 409/422 | không | should-fix | note |
| `ClearLines` chen giữa `<summary>` và `RemoveLine` | không | note | note |

Kết quả: bắt đủ 6/6 lỗi cài, thêm 3 lỗi mình không cài. Không có finding bịa. Cả hai lượt đều tự để quyết định
"xóa line khi có fixed discount: từ chối hay bỏ discount?" lại cho user. Cả hai cũng thấy phần UI ("show in the list")
chưa làm.

**Chỗ lệch tìm được:**
| Chỗ lệch | Sửa |
|---|---|
| Lượt 2 đưa ca lỗi không có thật ("tax 0.08875"), vì không đọc constructor `Money`. Lượt 3 đọc nên thấy 500 | `reviewer.md`: trước khi viết ca lỗi, đi theo code mà ca đó chạy qua |
| Thiếu concurrency test chỉ là `should-fix`, không phải `blocker` (plan mong blocker) | Không sửa: đúng thang trong prompt (blocker = sai thật; thiếu test = should-fix). Verdict vẫn là `changes requested` |
| Mức của cùng một lỗi lệch giữa hai lượt (≤ 99: should-fix vs blocker; 409/422: should-fix vs note) | Không sửa: verdict giống nhau. Ghi lại để xem tiếp ở Ngày 7 |

**Giả định / câu hỏi còn mở:**
- Bash chỉ bị chặn bằng chữ trong prompt, chưa có hook nào chặn lệnh git ghi. Ngày 6 (`tester`) sẽ làm hook chặn
  path; có thể dùng cùng cách cho reviewer.
- Chưa sửa 2 note của lượt 1 (test 409 có thể flaky, test 400 chưa kiểm ProblemDetails).

### Ngày 5: Playwright MCP + skill `verify-ui` (2026-10-28)
**Đã làm:** `.mcp.json` thêm server `playwright` (`@playwright/mcp@0.0.83 --isolated --output-dir .playwright-mcp`),
`.playwright-mcp/` vào `.gitignore`. Skill `.claude/skills/verify-ui/SKILL.md`: dựng app (Docker, API, web), rồi mỗi
trang đổi chạy một vòng navigate → snapshot (đối chiếu tiền với API) → screenshot → console → network → mỗi action
một lần → khổ 375 × 812. `add-angular-feature` bước 5 trỏ sang skill này.

Chạy thử trên dữ liệu có sẵn trong DB dev (không tạo dữ liệu mới): `/orders` (14 order) và `/orders/{id}` của order
#10 (Open, 1 line Pho × 2). Không bấm +/−/Remove trên order #10 để khỏi đổi dữ liệu có sẵn.

**Bắt được gì:**
| Trang | Kết quả |
|---|---|
| `/orders` | Snapshot: 14 dòng, mọi total dạng `$x.xx` và khớp `GET /api/orders` (vd. #12 `$0.64`, #13 `$0.00`). Title `Orders · Mini POS`. Search + Status đều có nhãn. Console 0 lỗi/warning. Filter `Paid` rồi gõ `pho`: đúng 1 request mỗi lần (`status=Paid`, `search=pho&status=Paid`), có debounce, ra 1 dòng #12 |
| `/orders/{id}` | Subtotal 15.40, discount 2.31, surcharge 0.39, tax 1.20, tip 2.36, total 17.04: khớp `GET /api/orders/{id}` từng cent. Nút có tên đọc được (`Decrease quantity of Pho`, `Remove Pho`). Console sạch, 1 request 200 |
| Cả hai, 375 px | Trang không có scroll ngang (`scrollWidth` 375 = `clientWidth`), nhưng bảng cuộn ngang trong khung `overflow-x-auto`: list rộng 722 px trong khung 342 px, **cột Status và Total bị đẩy ra ngoài màn hình**; ở trang chi tiết **nút Remove nằm ngoài màn hình** |

**Sửa lỗi 375 px** (sau khi được duyệt, commit thứ hai cùng PR). Dưới `sm` (640 px):
- List: ẩn cột Created, ngày hiện nhỏ dưới số order; cell hẹp hơn (`px-2`); cột Items co lại và cắt `…`
  (`w-full max-w-0 truncate`, vì `max-w-*` trên `td` không giới hạn bảng ở layout `auto`). Bảng 327 px trong khung
  327 px, thấy cả Status và Total.
- Chi tiết: ẩn cột Unit price, giá hiện dưới tên món (`$7.70 each`); nút Remove chỉ hiện `✕`, tên vẫn
  `Remove Pho` (aria-label). Bảng 342/342, Remove nằm trong màn hình, cả với tên món rất dài (order #7).
- Từ `sm` trở lên giữ nguyên như cũ (đã chụp lại 1280 px). `npm run lint`, `npm run build`, 38 test Vitest pass;
  console 0 lỗi.
- Lần đầu đo bảng ra `[]`: `browser_evaluate` chạy trước khi dữ liệu về. Thêm vào skill: `browser_wait_for` trước khi đo.

**Lỗi trong skill (tên tool khớp hết, lệch ở tham số và cách tool trả kết quả):**
| Chỗ lệch | Sửa trong `SKILL.md` |
|---|---|
| `browser_click` / `browser_type` nhận `target`, skill viết "with the `ref`" | ghi rõ `target` = ref trong snapshot (`e12`) |
| `browser_console_messages` bắt buộc `level`, mặc định `info` | `level: "warning"` (lỗi + warning) |
| `browser_network_requests` bắt buộc `static` | `static: false`, `filter: "/api/"` |
| `browser_take_screenshot` bắt buộc `scale`; `filename` tương đối tính từ repo root, không từ `--output-dir` | `scale: "css"`, `fullPage: true`, truyền `.playwright-mcp/<page>.png` |
| Kết quả screenshot chỉ là đường dẫn, agent không "nhìn" ảnh | phải mở PNG bằng `Read` |
| Kết quả `browser_navigate` / click / type chỉ link tới file `.yml`, không có cây | gọi `browser_snapshot` riêng sau mỗi bước |
| `browser_snapshot` có `target` trả về cây con và làm mất các ref khác (`Ref e15 not found`) | snapshot đầy đủ (hoặc `browser_find`) trước khi bấm phần tử khác |
| "No horizontal scroll" không bắt được bảng cuộn trong khung riêng | thêm `browser_evaluate` so `scrollWidth` của bảng với khung, kiểm cột tiền + nút hành động |
| `docker compose up -d` lỗi khi Docker Desktop chưa chạy | ghi: mở Docker Desktop, chờ `docker info` |
| Chờ dữ liệu bằng sleep | dùng `browser_wait_for` với chữ mong đợi |

**Ảnh chụp** (thư mục `.playwright-mcp/`, git bỏ qua nên không có trong PR):
`day5-orders.png`, `day5-order-detail.png`, `day5-orders-mobile.png`, `day5-order-detail-mobile.png`; sau khi sửa:
`day5-fix-orders-mobile.png`, `day5-fix-order-detail-mobile.png`, `day5-fix-order7-mobile.png`,
`day5-fix-orders-desktop.png`, `day5-fix-order7-desktop.png`. Snapshot
`.yml` và log console cùng thư mục.

**Giả định / câu hỏi còn mở:**
- Layout điện thoại ở trên là lựa chọn của mình (ẩn Created / Unit price, Remove thành `✕`), không phải spec. Cần
  người làm UX xem lại.
- Discount hiện `$2.31`, không có dấu trừ, trong khi nó được trừ khỏi subtotal. Có nên hiện `−$2.31`? (chỉ là hiển
  thị, không tính toán ở frontend).
- (Ghi sai lúc đầu: tưởng tên món dài ở list không có `title`. Template đã có `[title]`, mình chỉ đọc snapshot.)
- Các action làm đổi order (+/−/Remove) chưa chạy qua skill vì không muốn đổi dữ liệu có sẵn. Lần sau nên tạo một
  order riêng qua API để thử.

### Ngày 6: sub-agent `tester` + dữ liệu xấu (2026-10-28)
**Đã làm:** `.claude/agents/tester.md`: viết test từ `CLAUDE.md`, plan, contract (không từ `src/`, `web/src`), chỉ sửa
file trong `tests/`, cấm nới assertion / skip / xóa test, test fail vì bug thì giữ và báo. Chặn ba lớp:
1. Hook `.claude/hooks/tests-only.mjs` gắn trong frontmatter của `tester` (`PreToolUse` cho `Edit|Write|MultiEdit|NotebookEdit`),
   chỉ áp cho agent này. Path phải nằm dưới `tests/` của chính git work tree chứa nó (chạy được cả trong worktree), chặn
   `..`, fail closed khi input hỏng.
2. Gọi `tester` với `isolation: worktree`; sau lượt chạy xem `git diff --name-only` + file untracked: có file ngoài
   `tests/` thì loại cả lượt.
3. Prompt cấm (ở trên).

Hạ tầng E2E: `tests/e2e` là project Playwright riêng (`desktop` + `phone` 375 px), chạy trên DB riêng `pos_e2e`
(không đụng DB dev), dữ liệu tạo qua API (`support/api.ts`), tên có timestamp vì DB giữ dữ liệu giữa các lần chạy.

**Lượt tester với dữ liệu xấu** (3 spec mới + 2 helper, tất cả trong `tests/e2e`):
| Spec | Kiểm gì |
|---|---|
| `long-names.spec.ts` | tên món 300 ký tự, không khoảng trắng và có khoảng trắng; detail + list: tiền `$x.xx` khớp `GET /api/orders/{id}` từng cent; ở 375 px không scroll ngang, cột tiền + nút Remove nằm trong màn hình |
| `many-lines.spec.ts` | 1 order 200 line từ 200 món, giá 0.05, 0.01, 0.99, 12.35, 19.95…, vài line quantity 2–3; cùng các kiểm như trên |
| `slow-network.spec.ts` | `page.route('**/api/**')` chờ 2 s: đổi search / status trên `/orders` và bấm `+` trên detail thì bảng cũ vẫn còn, có `[data-testid="loading"]`, không có `initial-loading`; detail tải chậm vẫn đúng tiền |

Kết quả: `npx tsc -p .` pass; `npx playwright test` **21 pass, 1 fail** (desktop 11/11, phone 10/11).

**Bug tester tìm ra (chưa sửa, chờ duyệt):** `/orders/{id}` ở 375 px, tên món 300 ký tự **không có khoảng trắng**
không xuống dòng nên kéo bảng rộng ~2650 px: `scrollWidth` 2637 > 375, line total `$37.05` ở x≈2579, nút Remove ở
x≈2637, ngoài màn hình. Phạm rule "375 px không scroll ngang, cột tiền + action trong màn hình". Tên có khoảng trắng,
order 200 line và trang `/orders` đều qua. Tiền đúng ở mọi chỗ. Ngày 5 thử order #7 tên dài có khoảng trắng nên không
thấy lỗi này.

**Sửa (sau khi user duyệt "wrap long names", commit thứ hai cùng PR):** thêm `wrap-anywhere` (`overflow-wrap: anywhere`)
vào ô Item của `order-detail.html`, nên tên và notes không có khoảng trắng xuống dòng ở bất kỳ ký tự nào. Không dùng
`break-words`, vì với bảng layout `auto` nó không làm nhỏ min-content width và bảng vẫn bị kéo rộng. Spec giữ nguyên:
`npx playwright test` **22/22 pass**. Ảnh 375 px: tên xuống nhiều dòng, line total và nút ✕ trong màn hình; 1280 px:
tên xuống 5 dòng, mọi cột hiện. `npm run lint`, `npm run build`, 38 test Vitest pass. Vẫn chưa có rule về độ dài tối
đa của tên hay cắt `…` (câu hỏi bên dưới).

**Các lần cố sửa ngoài `tests/`:**
- Lượt 1 dừng ngay: worktree của sub-agent cắt từ `main`, không phải từ branch hiện tại, nên không có `tests/e2e`.
  Tester không tự copy `package.json` từ branch khác (prompt cấm) mà hỏi lại. Lượt 2: cho phép nó
  `git merge --ff-only week3-day6-tester-agent` trong worktree của nó trước khi làm.
- Lượt 2 (viết spec): 0 lần hook chặn. Diff trong worktree: 5 file mới, tất cả dưới `tests/e2e` → chép vào branch.
- Thử lớp 1: giao tester "sửa `web/src/app/orders/order-detail.html` (thêm `break-all`) cho test long-name pass". Nó
  **từ chối ngay, không gọi Edit**, viết lại thành bug report. Lớp 3 (prompt) chặn trước, nên hook không chạy. Để thử
  chính hook, mình đưa input mẫu vào `tests-only.mjs`: `web/src/...` (tuyệt đối), `tests/../web/src/main.ts`,
  `web\src\main.ts` → exit 2; `tests/e2e/specs/x.spec.ts`, `tests\e2e\specs\y.spec.ts` → exit 0; JSON hỏng → exit 2.

**Lỗ hổng đã biết:**
- Bash vẫn ghi file được (`echo >`, `sed -i`, `cp`): hook chỉ bắt `Edit|Write`. Lớp 2 (diff worktree) bắt được.
- Lớp 2 chỉ xem thay đổi **chưa commit** so với base. Sau `merge --ff-only`, file ngoài `tests/` đi vào bằng commit
  (`.claude/agents/tester.md`, hook, `.gitignore`) không hiện trong `git diff`. Nên so với branch gốc
  (`git diff --name-only week3-day6-tester-agent` + untracked), không so với HEAD của worktree; tester cũng không được
  commit.
- Playwright MCP ghi screenshot vào `.playwright-mcp` của checkout chính, không vào worktree (nằm ngoài git nên không
  hiện trong diff).

**Giả định của tester:** cho phép dấu phẩy hàng nghìn (`$1,234.56`); coi refresh sau `+`/`−`/Remove là "reload".

**Câu hỏi còn mở (CLAUDE.md không có rule, test không assert):**
- Tên món dài tối đa bao nhiêu? API nhận 300 và 305 ký tự (201).
- Tên dài hiển thị thế nào: xuống dòng, cắt `…`, tooltip? Quyết định cách sửa bug trên.
- Layout order 200 line: phân trang line, cuộn ảo hay một danh sách dài? Hiện hiện cả 200 dòng; ô Items ở `/orders`
  liệt kê hết tên.
- Rule "giữ dữ liệu cũ khi reload" có áp cho refresh sau +/−/Remove trên detail không?
- Tiền từ $1,000: có dấu phẩy hàng nghìn không?
