# Agent `reviewer` và CodeRabbit trên cùng một PR

Ghi chú ngày 2026-11-03. Cả hai cùng review PR [#81](https://github.com/duytech/MiniPOS/pull/81) (README, issue #80).
Đây là lần đầu hai bên review một PR thật, không phải PR có lỗi cố ý như Tuần 4 Ngày 3–4 (xem
[journal.md](journal.md)). Xem thêm [skill-hay-agent.md](skill-hay-agent.md).

## Cách chạy

- **`reviewer`** ([`.claude/agents/reviewer.md`](../.claude/agents/reviewer.md)): gọi từ session chính. Đầu vào gồm diff
  `main...80-week4-readme` và plan đã duyệt. Prompt yêu cầu kiểm README có đúng với repo không: path, tên test, hook,
  lệnh, số liệu so với journal, cú pháp Mermaid.
- **CodeRabbit**: tự chạy khi PR mở, dùng cấu hình [`.coderabbit.yaml`](../.coderabbit.yaml) (`profile: chill`,
  `docs/journal.md` nằm trong `path_filters` nên bị bỏ qua).

## Kết quả

| | CodeRabbit | `reviewer` |
|---|---|---|
| Thời gian | ~11 phút (PR mở 14:03, review xong 14:14) | ~2,3 phút |
| Số finding | 4, đều Minor | 7: 2 should-fix, 5 note |
| Finding sai | 0 | 0, nhưng có 1 khẳng định sai ("sơ đồ khớp") |
| Verdict | Comment | Changes requested |

| Finding | CodeRabbit | `reviewer` | Kiểm lại |
|---|---|---|---|
| README cho dùng biến môi trường `ConnectionStrings__Pos` thay user-secrets, nhưng `init.sh` chỉ nhận user-secrets cho API | Minor | note | Đúng (`init.sh:47`) |
| Sơ đồ vẽ CodeRabbit như một cổng merge, trong khi `DECISIONS.md` ghi nó chỉ là lời khuyên | Minor | — (còn ghi "sơ đồ khớp") | Đúng (`DECISIONS.md:72`) |
| Verifier dùng lại API đang chạy ở 5191 mà không kiểm API đó có dùng DB `pos_e2e` không, nên E2E có thể ghi vào DB dev | Minor | — | Đúng (`scripts/services.mjs:98` chỉ kiểm có trả lời). Lỗi có từ trước PR |
| `.env.example` có mật khẩu dev, Postgres publish cổng 5433 trên mọi interface (CWE-798) | Minor, Security | — | Đúng một phần: chỉ lộ khi máy cho bên ngoài vào cổng 5433. Lỗi có từ trước PR |
| Số 22 lỗi gán cho riêng tính năng Order, thật ra là tổng của Ngày 3 và Ngày 4; lỗi 2 không phải "mất tiền âm thầm" (nó trả 500) | — | should-fix | Đúng (`journal.md:165`) |
| Tiêu đề cột "Rule (from `CLAUDE.md`)" sai cho 5 dòng: rule nằm ở `architecture.md`, `business-rules.md`, `reviewer.md`, hoặc đã bị bỏ | — | should-fix | Đúng |
| "with no possible test" nói quá: journal chỉ nói lúc đó chưa có test nào bắt được | — | note | Đúng |
| Dòng midpoint thiếu `OrderSurchargeTests.cs` | — | note | Đúng (`OrderSurchargeTests.cs:35`) |
| "No `double` / `float` anywhere" rộng hơn check: test chỉ quét assembly .NET | — | note | Đúng |
| Không có agent `integrator`; cổng "fix or accept" không có mũi tên quay về implementer | — | note | Đúng |

Chỉ 1/10 finding trùng nhau.

## Nhận xét

- **`reviewer` mạnh ở độ chính xác so với tài liệu nội bộ.** Nó đối chiếu từng con số với journal và từng rule với
  file nguồn. CodeRabbit không thể bắt lỗi số liệu, vì `journal.md` bị loại khỏi review.
- **CodeRabbit mạnh ở vận hành và bảo mật, và đọc ra ngoài diff.** Nó tự mở `services.mjs`, `docker-compose.yml`,
  `DECISIONS.md` và tìm ra 2 lỗi có sẵn trong repo. `reviewer` chỉ kiểm những gì README nói, đúng phạm vi prompt giao.
- Kết quả lặp lại pattern của Tuần 4 Ngày 3–4: `reviewer` mạnh về rule và test của repo, CodeRabbit mạnh về bảo mật.
- `reviewer` khẳng định "sơ đồ khớp" nhưng bỏ sót lỗi cổng merge. Phần "Verified correct" của reviewer không chứng
  minh được là không có lỗi.
- Về tốc độ, `reviewer` nhanh gấp khoảng 5 lần. CodeRabbit CLI không giúp được: docs ghi một lần review mất 7–30+ phút,
  vẫn chạy trên server của CodeRabbit và cần tài khoản.

## Cách dùng

- `reviewer` chạy trước khi push: vòng nhanh, bắt lỗi rule và số liệu.
- CodeRabbit là vòng cuối trên PR: bắt lỗi vận hành và bảo mật. Không chờ nó ở giữa vòng sửa code.
- Finding nằm ngoài phạm vi PR (2 lỗi có sẵn của CodeRabbit) tách thành task riêng, không sửa trong PR đang review.
