# Lộ trình 4 tuần: AI Agent Harness cho .NET + Angular

Oct 24, 2026 · @Duy Pham

## Mục tiêu và repo thực hành

Sau 4 tuần (1–2 giờ/ngày) bạn có một repo GitHub chứng minh đủ các yêu cầu "Working with AI" trong JD của Blogic Systems. Mọi bài tập làm trên **một repo duy nhất**: mini POS gồm .NET 8 API (Clean Architecture), Angular 18 + Tailwind, PostgreSQL. Tính năng cốt lõi: Order, thêm món, thuế, tip, giảm giá, surcharge/dual pricing, Split bill.

| Thuật ngữ trong JD | Thực hiện trong repo |
| --- | --- |
| Project-level instruction files | `CLAUDE.md` / `AGENTS.md` ở gốc repo |
| Custom commands & skills | `.claude/skills/<tên>/SKILL.md` |
| Sub-agents with scoped tools | `.claude/agents/*.md` (planner, implementer, reviewer) |
| Hooks | Lệnh tự chạy build/test sau mỗi lần agent sửa file |
| Loop engineering | xUnit, arch test, analyzers, ESLint, `tsc --strict` làm verifier |
| Visual loop (front end) | Playwright MCP: chụp màn hình, accessibility snapshot |
| AI code review | Review tự động trên GitHub PR |

Cú pháp hooks, skills, sub-agents thay đổi theo phiên bản: tra [tài liệu Claude Code](https://docs.claude.com) khi bắt tay làm.

## Tuần 1: Nền tảng và instruction file

Kết quả: một `CLAUDE.md` ngắn, cụ thể, đã kiểm chứng bằng cách cho agent làm lại cùng một tính năng.

- [x] Ngày 1: Cài Claude Code (hoặc Copilot agent mode). Tạo solution: Domain / Application / Infrastructure / Api + project test xUnit.
- [x] Ngày 2: Tạo app Angular 18 + Tailwind, PostgreSQL bằng Docker Compose. Commit khung repo lên GitHub.
- [x] Ngày 3: Giao agent làm tính năng Order (tạo order, thêm món, tính tổng) mà chưa có hướng dẫn gì. Ghi lại mọi chỗ sai quy ước.
- [x] Ngày 4: Giao agent làm API + màn hình danh sách order bằng RxJS (`switchMap`, `combineLatest`). Tiếp tục ghi lỗi.
- [x] Ngày 5: Viết `CLAUDE.md`: lệnh build/test, cấu trúc thư mục, quy tắc tiền (`decimal`, `MidpointRounding.AwayFromZero`, làm tròn ở bước nào), cách đặt tên, những điều cấm.
- [x] Ngày 6: Xóa tính năng Order, cho agent làm lại với `CLAUDE.md`. So sánh số lỗi với lần đầu.
- [x] Ngày 7: Rút gọn `CLAUDE.md`: bỏ dòng agent đã tự làm đúng, giữ dòng thực sự thay đổi hành vi. Ghi nhật ký tuần.

## Tuần 2: Verifier và loop engineering

Kết quả: agent tự sửa đến khi mọi check pass; ít nhất một rule review đã được biến thành check tự động. Đây là kỹ năng JD gọi là cốt lõi.

- [x] Ngày 1: Viết xUnit test cho thuế, tip, giảm giá. Mỗi test so sánh đến từng cent.
- [x] Ngày 2: Thêm test cho surcharge/dual pricing và Split bill (tổng các phần phải bằng đúng tổng hóa đơn).
- [x] Ngày 3: Thêm architecture test (NetArchTest): Domain không tham chiếu Infrastructure; không dùng `double`/`float` cho tiền.
- [x] Ngày 4: Bật `TreatWarningsAsErrors` + Roslyn analyzers. Angular: `strict`, ESLint, unit test (Jest hoặc Vitest).
- [x] Ngày 5: Tạo hook `PostToolUse`: sau mỗi lần sửa file `.cs` chạy `dotnet build` + test; file `.ts` chạy lint + `tsc`.
- [x] Ngày 6: Giao agent một tính năng mới với hook bật. Quan sát vòng lặp tự sửa; chỉnh hook nếu quá chậm hoặc quá ồn.
- [ ] Ngày 7: Cố ý giao task dễ gây lỗi làm tròn. Ghi lại verifier đã bắt lỗi thế nào: đây là câu chuyện phỏng vấn số 1.

## Tuần 3: Skills, sub-agents và visual loop

Kết quả: một tính năng chạy trọn chuỗi plan → implement → review → verify, có bạn duyệt ở mỗi cổng, và agent tự xem được UI đã render.

- [ ] Ngày 1: Viết skill `add-endpoint`: Controller → Command/Query → Handler → validator → test.
- [ ] Ngày 2: Viết skill `add-angular-feature`: component + service + RxJS + Tailwind, theo quy ước trong `CLAUDE.md`.
- [ ] Ngày 3: Tạo sub-agent `planner` (chỉ đọc, xuất kế hoạch) và `implementer` (được sửa code).
- [ ] Ngày 4: Tạo sub-agent `reviewer` (chỉ đọc), đối chiếu diff với `CLAUDE.md` và business rule về tiền.
- [ ] Ngày 5: Cài Playwright MCP. Viết skill bắt agent mở trang, chụp màn hình và đọc accessibility snapshot sau mỗi lần sửa UI.
- [ ] Ngày 6: Thử visual loop với dữ liệu xấu: tên món rất dài, 200 món, mạng chậm (throttle). Ghi lại lỗi agent tự phát hiện.
- [ ] Ngày 7: Chạy trọn tính năng Split bill qua cả chuỗi agent. Ghi lại chỗ các agent trả về không nhất quán và cách bạn sửa harness.

## Tuần 4: AI code review và đóng gói hồ sơ

Kết quả: repo công khai có CI, AI review trên PR và README giải thích toàn bộ harness, sẵn sàng gửi kèm CV.

- [ ] Ngày 1: Thiết lập GitHub Actions: build + test + arch test + lint làm quality gate bắt buộc trước khi merge.
- [ ] Ngày 2: Thêm AI review tự động trên PR (Claude Code GitHub Action hoặc Copilot review).
- [ ] Ngày 3: Mở 3–4 PR có lỗi cố ý (lệch 1 cent, lộ dữ liệu, thiếu xử lý null). Ghi lại bot bắt được gì, bỏ sót gì.
- [ ] Ngày 4: Chỉnh prompt review: tập trung vào business rule và bảo mật, bỏ qua style mà linter đã lo. Chạy lại các PR lỗi.
- [ ] Ngày 5: Viết README: sơ đồ agent, danh sách verifier, bảng "rule review → check tự động", hướng dẫn chạy.
- [ ] Ngày 6: Thêm vào CV một mục dự án ngắn: harness đã xây, verifier, kết quả cụ thể (ví dụ số lỗi bị chặn trước khi merge).
- [ ] Ngày 7: Luyện phỏng vấn (phần dưới).

## Chuẩn bị phỏng vấn

Mỗi câu trả lời nên dựa trên một ví dụ thật trong repo, nói bằng tiếng Anh, dài 1–2 phút. Tập nói thành tiếng và ghi âm lại.

- [ ] "How do you stop agents from drifting off project conventions?" → `CLAUDE.md` + skills + hooks + arch test.
- [ ] "Tell me about a time AI output looked correct but was wrong." → câu chuyện lỗi làm tròn ở Tuần 2.
- [ ] "How do you verify front-end work done by an agent?" → visual loop với Playwright ở Tuần 3.
- [ ] "How do you keep AI code review from being noise?" → kết quả chỉnh prompt ở Tuần 4.
- [ ] "How would you split one feature across several agents?" → chuỗi planner / implementer / reviewer và các cổng duyệt.
- [ ] "Who is accountable when AI-written code breaks production?" → bạn, luôn luôn; nêu cách verifier và review giảm rủi ro.
