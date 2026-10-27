# Multi-agent, sub-agent và chạy song song

Ghi chú sau Tuần 3 Ngày 3 (2026-10-27), khi đã có hai sub-agent `planner` và `implementer` trong `.claude/agents/`.

## 1. Multi-agent có phải là sub-agent không?

Không hẳn. **Multi-agent là khái niệm rộng; sub-agent là một cách tổ chức multi-agent.**

Multi-agent là bất kỳ hệ thống nào có nhiều agent cùng làm một việc. Các cách tổ chức thường gặp:

| Kiểu | Cách chạy | Ví dụ |
|---|---|---|
| Orchestrator → sub-agent (phân cấp) | Agent chính giao việc. Agent con có context riêng, làm xong trả bản tóm tắt rồi kết thúc. Các agent con không nói chuyện với nhau. | Agent chính gọi `planner`, rồi gọi `implementer` |
| Pipeline | Đầu ra của agent này là đầu vào của agent sau. Có thể chạy bằng sub-agent hoặc bằng session riêng. | plan → implement → review |
| Ngang hàng / team | Các agent sống lâu, gửi tin nhắn cho nhau, không agent nào là "cha". | Agent teams |
| Song song độc lập | Nhiều agent làm các việc tách biệt, mỗi agent một nhánh hoặc một PR, sau đó ghép lại. | Backend và frontend làm cùng lúc trong hai worktree |

Trong Claude Code:
- **Sub-agent** được gọi qua tool `Agent` (ví dụ `planner`, `implementer`). Nó chạy bên trong lượt của agent chính, trả kết quả rồi kết thúc. Agent định nghĩa trong `.claude/agents/` chỉ được nạp khi session khởi động: tạo agent mới giữa session thì phải mở session mới mới gọi được.
- **Session riêng** là một agent đầy đủ. Người dùng mở và trả lời nó trực tiếp; nó tự làm đến khi mở PR. Ngày 3 dùng một session riêng để chạy chuỗi planner → implementer và mở PR #21.
- **Agent teams** là các teammate gửi tin nhắn qua lại, gần với kiểu ngang hàng.

Chuỗi của Tuần 3 là multi-agent kiểu **pipeline**. Agent chính điều phối các sub-agent theo thứ tự: planner → người duyệt → implementer → reviewer (Ngày 4).

### Sub-agent có tốn token hơn không?

Tính theo tổng token thì thường tốn hơn:
- Mỗi sub-agent bắt đầu với context trống. Nó nạp lại system prompt, `CLAUDE.md`, prompt của agent và các skill được nạp sẵn (`implementer` nạp `add-endpoint` và `add-angular-feature`).
- Nó đọc lại các file mà agent chính hoặc agent trước đã đọc. Ở Ngày 3, `planner` và `implementer` đều đọc gần cùng một bộ file.
- Chạy song song thì chi phí khởi động nhân theo số agent.

Đổi lại, agent chính chỉ nhận bản tóm tắt, không nhận các lần đọc file và log build/test. Mỗi lượt sau gửi lại context nhỏ hơn, nên session dài sẽ rẻ hơn. Có thể giảm chi phí bằng cách đặt model rẻ hơn cho agent chỉ đọc (`model: sonnet` trong frontmatter của `planner`). Khi cần hỏi thêm, gửi tiếp cho agent đang có context thay vì tạo agent mới.

Với repo này, lý do chính để dùng `planner`/`implementer` không phải là tiết kiệm token. Lý do là **giới hạn quyền** (`planner` chỉ có `Read, Glob, Grep`, không sửa được file) và **cổng duyệt** giữa plan và code.

## 2. `planner` và `implementer` đang chạy tuần tự. Làm sao để một team chạy song song trên một web project?

Đúng, hai agent đang chạy tuần tự, và phải tuần tự: `implementer` cần plan đã duyệt mới làm được. Phần chạy song song được nằm **sau bước plan**, khi việc đã được chia thành những phần độc lập.

### Bước 1: chốt contract trước khi chia việc

Ngoài plan theo từng file, `planner` cần ra thêm:
- **Contract API**: route, request/response (ví dụ `OrderDto` thêm field gì), các status code 400/404/409/422.
- **Danh sách task**, mỗi task ghi rõ những file **chỉ task đó được sửa**.

Khi đã có contract, backend và frontend không phải chờ nhau. Frontend test bằng `HttpTestingController` với response đúng contract, không cần API thật.

### Bước 2: chạy song song, mỗi agent một worktree

```
planner ──► người duyệt plan + contract
              ├─► implementer-backend  (worktree A: Domain → Application → API → test .NET)
              └─► implementer-frontend (worktree B: model → service → component → Vitest)
                        │
              integrator: merge A + B, chạy toàn bộ test, mở app thật
                        │
              reviewer ──► người duyệt PR
```

- Trong Claude Code: gọi nhiều `Agent` trong **cùng một message**, mỗi agent có `isolation: "worktree"`. Hoặc tách mỗi phần thành một session riêng.
- Mỗi worktree có verifier riêng: hook build/lint chạy trên đúng nhánh của agent đó.

### Bước 3: biết trước file nào dễ conflict

| File | Vì sao dễ conflict | Cách xử lý |
|---|---|---|
| `src/Pos.Infrastructure/Persistence/Migrations/PosDbContextModelSnapshot.cs` | Hai migration làm song song chắc chắn conflict | Thay đổi Domain/migration chỉ một agent làm, tuần tự |
| `src/Pos.Application/DependencyInjection.cs`, `src/Pos.Api/Controllers/OrdersController.cs` | Hai endpoint mới cùng thêm dòng | Giao cho một agent, hoặc chấp nhận merge tay (thường dễ) |
| `tests/Pos.Api.IntegrationTests/ApiClient.cs` | Helper HTTP dùng chung | Như trên |
| `web/src/app/app.routes.ts`, `web/src/app/orders/order.models.ts` | Hai trang mới cùng sửa | Chốt model trong contract trước |

Nguyên tắc: **hai agent không bao giờ sửa cùng một file.** Phần dùng chung (Domain, migration, model) làm trước và tuần tự, sau đó mới tách ra song song.

### Bước 4: những điểm cần lưu ý

- **Worktree tạo từ `main`** không có skill hoặc agent chưa merge (đã gặp ở Tuần 3 Ngày 1). Phải commit và push trước, hoặc tạo worktree từ nhánh hiện tại.
- **Token**: mỗi agent đọc lại `CLAUDE.md`, skill và code. Chạy song song xong nhanh hơn nhưng tổng token nhiều hơn.
- **Bước integrator là bắt buộc.** Từng nhánh xanh không có nghĩa là ghép lại sẽ xanh. Lệch contract (backend trả `paidAt`, frontend đọc `paidOn`) chỉ lộ ra khi ghép và chạy app thật.
- **Cổng duyệt của người** vẫn giữ ở hai chỗ: duyệt plan + contract, và duyệt PR.

### Áp dụng vào lộ trình

Lộ trình chưa có bài nào chạy song song. Tuần 3 Ngày 7 (Split bill qua cả chuỗi) hợp để thử: phần backend (lưu split, endpoint) và phần frontend (trang chia hóa đơn) có thể chạy song song sau khi chốt contract. Muốn làm vậy cần:
- Sửa `planner` để xuất contract và danh sách file của từng task.
- Thêm bước integrator (agent hoặc người).
- Ghi lại trong journal những conflict gặp phải và cách xử lý.
