# Harness engineering là gì?

Ghi chú ngày 2026-10-28, sau Tuần 3 Ngày 7. Xem thêm [skill-hay-agent.md](skill-hay-agent.md),
[multi-agent-va-sub-agent.md](multi-agent-va-sub-agent.md) và [journal.md](journal.md).

## Định nghĩa

**Harness engineering** là thiết kế môi trường bao quanh AI agent, để agent làm đúng việc và tự phát hiện khi làm sai.
Bạn không viết code thay agent. Bạn xây thứ dẫn đường và kiểm tra nó: hướng dẫn, công cụ, giới hạn quyền và verifier
tự động.

So sánh dễ hiểu: model giống con ngựa, harness là bộ yên cương. Ngựa khỏe đến đâu mà không có cương thì vẫn chạy lệch
đường.

## Các thành phần trong repo này

| Thành phần | Vai trò | Trong repo |
|---|---|---|
| Instruction file | Quy ước agent không tự đoán được | `CLAUDE.md`: Money, làm tròn, `xmin` + 409, không tự đặt business rule |
| Skills | Quy trình lặp lại + file mẫu | `add-endpoint`, `add-angular-feature`, `verify-ui` |
| Sub-agents | Tách vai, giới hạn tool theo vai | `planner` chỉ đọc, `implementer` được sửa, `reviewer` chỉ đọc, `tester` chỉ sửa `tests/` |
| Hooks | Check tự chạy, agent không bỏ qua được | build + test khi kết thúc lượt và trước `git commit`; chặn file không phải UTF-8; chặn commit lên `main` |
| Verifier (loop engineering) | Tín hiệu đúng/sai để agent tự sửa | xUnit đến từng cent, arch test quét IL, analyzers, ESLint, Vitest, Playwright |
| Visual loop | Agent tự nhìn UI đã render | Playwright MCP: snapshot, screenshot, đo cột ở 375 px |
| Cổng duyệt | Người quyết chỗ agent không được quyết | trả lời câu hỏi của planner, duyệt contract, duyệt sửa lỗi |

## Nguyên tắc rút ra (đều có trong `journal.md`)

1. **Biến rule thành check.** Rule chỉ nằm trong chữ thì agent vẫn có thể bỏ qua. Ví dụ "không dùng `double` cho tiền"
   chỉ chắc chắn khi có arch test quét IL.
2. **Phải kiểm cả verifier bằng đột biến.** Tuần 2 Ngày 3, phép làm tròn qua `double` lọt cả 63 test nghiệp vụ lẫn bản
   arch test đầu tiên.
3. **Test xanh chưa có nghĩa là đúng.** Tuần 1 Ngày 3 có 37 test xanh mà vẫn mất tiền khi chạy song song. Verifier
   phải kiểm bất biến nghiệp vụ (số lượng lưu trong DB), không chỉ kiểm code chạy được.
4. **Rule cụ thể thắng rule chung.** "Không tự đặt giới hạn" vẫn sót `MaxPageSize`. Thêm ví dụ cụ thể thì agent mới
   dừng lại hỏi.
5. **Chặn bằng tool mạnh hơn chặn bằng chữ.** `planner` không có tool ghi file, nên muốn hay không cũng phải hỏi trước
   khi làm.
6. **Harness cũng tiến hóa theo lỗi.** Mỗi lỗi mới thành một dòng rule, một check hoặc một bước trong skill. Ví dụ
   Tuần 3 Ngày 7 thêm rule "mỗi mảng trong contract phải nói thứ tự" và "mọi 409 dùng một `detail`" vào `planner.md`.

## Khi trả lời phỏng vấn

Câu này là phần cốt lõi của mục "Working with AI" trong JD. Trả lời bằng một ví dụ thật trong repo: một lỗi agent gây
ra, check nào bắt được nó, và bạn đã thêm gì vào harness sau đó.
