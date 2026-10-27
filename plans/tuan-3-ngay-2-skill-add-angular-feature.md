# Tuần 3 Ngày 2: skill `add-angular-feature`

## Mục tiêu

Agent thêm một tính năng Angular đúng quy ước của repo ngay từ lần đầu (component + service + RxJS + Tailwind, theo
`CLAUDE.md`), mà không phải đọc lại cả `web/`. Kiểm chứng giống Ngày 1: giao tính năng cho một sub-agent mới không có
ngữ cảnh, đếm chỗ lệch, sửa skill, chạy lại.

## Hiện trạng `web/` (khung để rút quy ước)

- Angular 20, standalone, `ChangeDetectionStrategy.OnPush`, `inject()`, signals + `toSignal`, Tailwind 4.
- Một tính năng duy nhất: `orders/orders-list` + `orders/orders-api.ts` + `orders/order.models.ts`.
  - Service `OrdersApi`: `HttpClient`, URL tương đối `/api/...` (proxy sang API), `HttpParams`.
  - Component: RxJS (`debounceTime`, `distinctUntilChanged`, `combineLatest`, `switchMap`) dồn vào một
    `state` qua `scan`; reload giữ dữ liệu cũ + chỉ báo "Updating…", không thay bảng bằng spinner.
  - Tiền: chỉ hiển thị bằng `currency` pipe `'USD'`; model ghi rõ "total computed by the API".
  - Template: `data-testid`, `role="status"` / `role="alert"`, `sr-only` label (ESLint template accessibility).
- Test: Vitest + jsdom, `HttpTestingController`, `vi.useFakeTimers()` (không dùng `fakeAsync`).
- Verifier: hook trước commit và hook `Stop` chạy eslint + `ngc`; Vitest, `npm run build`, `npm run lint` phải tự chạy.

## Bước

1. **Nhánh** `week3-day2-add-angular-feature-skill` từ `main`.

2. **Viết skill** `.claude/skills/add-angular-feature/SKILL.md` (giữ ngắn, dẫn tới `CLAUDE.md`, không chép lại):
   - **Bước 0, hỏi trước khi code:** hành vi UI mà task chưa nói: thông báo khi lỗi 409/422/404, có cần xác nhận
     trước thao tác xóa không, sau khi đổi dữ liệu thì làm mới gì, điều hướng tới/đi từ đâu. Không tự đặt giới hạn,
     mặc định hay văn bản nghiệp vụ.
   - **Thứ tự:**
     1. Model trong `<area>/<area>.models.ts`; field tiền là `number` kèm ghi chú "computed by the API".
     2. Method trong service `<Area>Api` (`inject(HttpClient)`, URL tương đối `/api/...`, không host/port).
     3. Component standalone: `OnPush`, `inject()`; luồng RxJS dồn vào một signal `state` (`toSignal` + `scan`);
        chọn operator theo ý nghĩa: `switchMap` cho đọc (hủy request cũ), `concatMap`/`exhaustMap` cho ghi (không
        hủy thao tác đang chạy).
     4. Route lazy (`loadComponent`) trong `app.routes.ts`, có `title`.
     5. Template Tailwind theo style của `orders-list.html`: `data-testid`, label `sr-only`, `role` cho trạng thái/lỗi.
   - **Quy tắc tiền:** chỉ `currency` pipe `'USD'`; không cộng, nhân, làm tròn, không tự tính lại total sau thao tác
     (dùng `OrderDto` API trả về).
   - **Reload:** giữ dữ liệu cũ + chỉ báo nhỏ, không đổi bảng thành spinner.
   - **Test bắt buộc** (Vitest, `HttpTestingController`, `vi.useFakeTimers()`): service gửi đúng method/URL/body;
     component hiển thị tiền đúng chuỗi API trả (`15.40` → `$15.40`); giữ dữ liệu cũ khi reload; mỗi mã lỗi đã
     chốt ở bước 0; hành vi RxJS đã chọn (hủy hoặc không hủy request).
   - **Verify:** `npm run build`, `npm run lint`, `npx ng test --watch=false`; mở trang thật (`npm start` + API) khi
     có thể. Visual loop bằng Playwright để dành cho Ngày 5.
   - `references/templates.md`: file mẫu để chép (`orders-api.ts`, `orders-list.ts|html|spec.ts`,
     `orders-api.spec.ts`) và đoạn khung `state` + `scan`.

3. **Thử skill bằng sub-agent mới** trên một tính năng dùng endpoint của Ngày 1: **trang chi tiết order**
   `/orders/:id` (danh sách dòng món, subtotal/discount/surcharge/tax/tip/total từ API, nút xóa dòng
   `DELETE .../lines/{lineId}`, đổi số lượng `PUT .../lines/{lineId}/quantity`, link từ dòng trong danh sách order).
   - Chạy trong checkout chính trên nhánh tách từ nhánh skill (worktree tạo từ `main` sẽ không có skill mới).
   - **Không đưa business rule nào**, để thử bước 0. Câu hỏi của agent chuyển cho user; resume agent với câu trả lời.

4. **Review như Ngày 1:** đối chiếu diff với skill và `CLAUDE.md`. Kiểm riêng các điểm dễ sai:
   - Frontend có tự cộng/làm tròn tiền không (kể cả "cập nhật lạc quan" total trước khi API trả về).
   - Toán tử RxJS cho thao tác ghi: `switchMap` có làm mất một lần xóa/đổi số lượng khi bấm nhanh không.
   - 409 và 422 hiển thị thế nào; dữ liệu cũ có được giữ khi lỗi không.
   - URL môi trường (host/port) trong component hoặc message.

5. **Sửa skill** theo các chỗ lệch; nếu có lệch đáng kể, chạy lại với một tính năng nhỏ khác (ví dụ thêm/bỏ tip trên
   trang chi tiết) và so sánh như Ngày 1.

6. **Đóng ngày:** `docs/journal.md` mục "Tuần 3 Ngày 2" (Đã làm / bảng chỗ lệch / so sánh hai lần chạy / câu hỏi mở),
   tick Ngày 2 trong `lo-trinh-ai-agent-harness.md`, commit, push, mở PR (không Co-Authored-By).

## Verify cuối

- `cd web && npm run build && npm run lint && npx ng test --watch=false` xanh.
- `dotnet test Pos.slnx` xanh nếu agent có đụng API (không nên).
- Chạy `docker compose up -d`, API (`dotnet run --project src/Pos.Api --launch-profile http`), `npm start`; mở
  `/orders/:id` trong browser pane, thử xóa dòng và đổi số lượng, chụp màn hình làm bằng chứng.

## Câu hỏi mở (để agent hỏi ở bước 0, không tự quyết trước)

- Xóa dòng có cần hộp xác nhận không?
- 409 (xung đột): tự tải lại order và báo, hay chỉ báo lỗi?
- 422 (vd. discount cố định lớn hơn subtotal mới): hiển thị message từ ProblemDetails hay văn bản cố định?
- Order không `Open`: ẩn hay vô hiệu hóa các nút sửa?
