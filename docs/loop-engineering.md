# Dùng hook script như vậy có phải là loop engineering không?

Ghi chú ngày 2026-10-28, viết sau khi chuyển `verify-on-edit.mjs` từ `PostToolUse` sang `Stop` (Tuần 2). Các phần
"trong repo" đã cập nhật theo trạng thái hiện tại. Xem thêm [journal.md](journal.md).

## Câu trả lời ngắn

Đúng, nhưng hook chỉ là **một phần** của loop engineering: phần nối vòng lặp lại với nhau.

Loop engineering là thiết kế vòng lặp **agent sửa → máy kiểm → kết quả quay lại agent → agent sửa tiếp**, sao cho agent
tự đi đến code đúng mà không cần người nhắc.

## Năm thành phần của một vòng lặp

| Thành phần | Câu hỏi | Trong repo |
|---|---|---|
| **Verifier** | Máy kiểm cái gì? | xUnit đến từng cent (có case midpoint), architecture test, analyzer + warnings as errors, Sonar cognitive complexity (S3776 / `sonarjs/cognitive-complexity`), ESLint, `ngc`, Vitest, concurrency test, Playwright |
| **Trigger** | Kiểm lúc nào, tự động hay chờ agent nhớ? | Hook `Stop` khi agent sắp kết thúc lượt, hook `PreToolUse` trước mỗi `git commit`, `check-utf8.mjs` trên cùng hai event |
| **Feedback** | Agent nhận được gì, đủ để sửa mà không phải chạy lại? | Script lọc output: Expected/Actual, danh sách vi phạm của arch test, `file:dòng`, tối đa 40 dòng |
| **Điểm dừng** | Bao giờ thì xong, bao giờ thì bỏ cuộc? | Exit 2 chặn agent cho tới khi check xanh. **Chưa có** giới hạn số vòng |
| **Kiểm chính vòng lặp** | Verifier có bắt được lỗi thật không? | Cố ý tạo lỗi (`ToEven`, `double`, `any`, sai template…), đếm số 409 trong concurrency test, ghi vào journal |

## Hook không phải phần quan trọng nhất

Không có hook, agent vẫn có thể tự chạy `dotnet test`, chỉ là có khi quên. Còn nếu verifier yếu, hook chạy bao nhiêu lần
cũng chỉ ra màu xanh giả. Ví dụ: test tiền mà không có case midpoint thì không phân biệt được `AwayFromZero` và `ToEven`.

Phần giá trị nhất là **biến rule review thành check tự động**: "tiền làm tròn `AwayFromZero`", "không dùng `double`",
"Domain không tham chiếu Infrastructure", "concurrency trả 409". Hook chỉ bảo đảm các check đó luôn được chạy.

## Chuyển từ `PostToolUse` sang `Stop` cũng là loop engineering

Chọn **nhịp kiểm** (sau mỗi edit hay mỗi lượt) và **giảm nhiễu** là chỉnh vòng lặp dựa trên quan sát:

- Ở Tuần 2 Ngày 6, hook `PostToolUse` chạy khoảng 30 lần trong một lượt, mỗi lần khoảng 2–7 s.
- 3 trên 5 lần đỏ là nhiễu. Hai edit phụ thuộc nhau (code mới và `using` của nó) được làm trong cùng một lượt, nên hook
  kiểm đúng lúc code còn dở.
- Ở `Stop`, hook chạy một lần trên toàn bộ thay đổi chưa commit, nên không còn thấy trạng thái dở dang.
- Đánh đổi: lỗi thật (như arch test bắt `PercentageLimits`) được báo ở cuối lượt chứ không ngay sau file gây lỗi.

## Những chỗ vòng lặp còn hở

- **Integration test (gồm concurrency) và Vitest** không nằm trong hook: chúng chậm hoặc cần DB. CLAUDE.md chỉ nhắc agent
  chạy trước khi xong. Tuần 4 Ngày 1 (GitHub Actions làm quality gate trước khi merge) sẽ đóng chỗ này.
- **Không giới hạn số vòng**: nếu agent bế tắc, hook `Stop` chặn mãi cho tới khi người dùng ngắt.
- **Hook dựa trên `git status`**: còn file chưa commit thì lượt chỉ hỏi đáp cũng phải chờ check.

## Câu trả lời khi phỏng vấn

> "Loop engineering là làm cho agent tự kiểm được việc của nó. Tôi biến rule review thành test và analyzer, dùng hook để
> chạy chúng tự động, lọc output để agent sửa được ngay, rồi chứng minh vòng lặp hoạt động bằng cách cố ý tạo lỗi và xem
> verifier có bắt được không."

Ví dụ cụ thể nên kể:

- Đổi `AwayFromZero` thành `ToEven`: hook báo `Expected: 0.77 USD / Actual: 0.76 USD` kèm tên test.
- Arch test bắt hằng số `decimal` trong Application ngay khi file vừa được tạo.
- Chuyển hook sang `Stop` sau khi đo được 3 trên 5 lần đỏ là nhiễu.
