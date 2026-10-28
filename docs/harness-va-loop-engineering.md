# Harness engineering và loop engineering khác nhau thế nào?

Ghi chú ngày 2026-10-28. Xem thêm [harness-engineering.md](harness-engineering.md),
[loop-engineering.md](loop-engineering.md) (năm thành phần của một vòng lặp) và [journal.md](journal.md).

## Tóm tắt

**Harness engineering** là thiết kế toàn bộ môi trường quanh agent. **Loop engineering** là một phần trong đó: thiết kế
vòng phản hồi để agent tự sửa cho đến khi mọi check pass. Nói gọn, mọi loop đều thuộc harness, nhưng harness có nhiều
thứ không phải loop.

## So sánh

| | Harness engineering | Loop engineering |
|---|---|---|
| Câu hỏi chính | Agent cần biết gì, được làm gì, bị cấm gì, ai duyệt? | Làm sao agent tự biết mình sai và sửa mà không cần người? |
| Tác động lúc nào | **Trước và trong** khi làm: dẫn đường, giới hạn | **Sau** mỗi lần làm: kiểm tra, trả lỗi, lặp lại |
| Thành phần | Instruction file, skills, sub-agents + quyền tool, hooks, verifier, cổng duyệt người | Verifier + cách đưa lỗi về cho agent (hook, output gọn) |
| Thước đo tốt | Agent ít đi lệch, hỏi đúng lúc | Lỗi bị bắt sớm, rõ, ít nhiễu; agent tự sửa đúng chỗ |
| Hỏng thì sao | Agent tự quyết rule, dùng sai kiến trúc | Test xanh mà vẫn sai, hoặc check ồn làm agent sửa lung tung |

## Trong repo này

- **Harness nhưng không phải loop:** `CLAUDE.md`, skill, `planner` chỉ đọc, `tester` chỉ sửa `tests/`, hook chặn commit
  lên `main`, cổng duyệt contract. Những thứ này chặn lỗi trước khi nó xảy ra, hoặc giao quyết định cho người.
- **Loop:** hook `verify-on-edit` chạy build và test khi agent kết thúc lượt và trước `git commit`, rồi trả lỗi đã lọc
  gọn (Expected/Actual, `file:line`). Các verifier đứng sau hook này là xUnit đến từng cent, arch test quét IL,
  analyzers, ESLint, Vitest. Thêm vòng nhìn UI bằng Playwright.

## Bài học riêng của loop engineering (Tuần 2)

- **Verifier phải bắt được lỗi thật.** Kiểm bằng đột biến: bản arch test đầu tiên bỏ lọt phép làm tròn qua `double`.
- **Kiểm bất biến, không chỉ kiểm code chạy được.** "Tổng các phần bằng tổng order" là cần nhưng chưa đủ, vì nó vẫn
  xanh khi phần đầu bị âm.
- **Output phải dùng được.** Bản đầu của hook dùng `dotnet test -v q`, chỉ in tên test fail, không có Expected/Actual,
  nên agent phải chạy lại test mới biết sai bao nhiêu.
- **Nhiễu tốn vòng lặp.** 3/5 lần hook đỏ ở Tuần 2 Ngày 6 là do trạng thái dở dang giữa hai edit. Cách sửa là đổi
  nhịp kiểm: chuyển hook từ `PostToolUse` (sau mỗi edit) sang `Stop` (một lần mỗi lượt).

## Chúng bổ sung cho nhau ra sao

Loop chỉ bắt được cái verifier có kiểm. Business rule chưa ai nói (tip tính trước hay sau thuế, thứ tự line trong part)
không verifier nào bắt được, vì không có đáp án để so. Phần đó harness phải lo: planner hỏi trước, người duyệt.
Ngược lại, harness có instruction tốt đến đâu mà thiếu loop thì agent vẫn có thể "nghĩ là đúng" mà không ai kiểm.

Vì thế JD tách hai ý: loop engineering là kỹ năng cốt lõi, còn harness là cả hệ thống chứa nó.
