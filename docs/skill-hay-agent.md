# Đóng gói vào skill hay tạo agent?

Ghi chú ngày 2026-10-28, sau khi đọc bài [Building agents: When to fold subagents into your main agent loop](https://claude.com/resources/articles/building-agents-when-to-fold-subagents-into-your-main-agent-loop) của Anthropic. Xem thêm [multi-agent-va-sub-agent.md](multi-agent-va-sub-agent.md).

## Xu hướng hiện nay là đóng gói vào skill thay vì agent?

Đúng, với **kiến thức và quy trình** thì xu hướng là đóng gói vào skill. Nhưng skill không thay thế agent hoàn toàn, vì hai thứ giải quyết hai vấn đề khác nhau:

- **Skill = cách làm.** Gồm quy trình, quy ước, template và những quyết định cần phán đoán. Skill được nạp vào **chính context đang chạy**, và chỉ nạp khi cần. Không tốn context riêng, không có bước bàn giao, không mất thông tin.
- **Subagent = cô lập.** Context riêng, quyền riêng, chạy song song, kiểm chứng độc lập. Chỉ đáng dùng khi thật sự cần một trong những thứ đó.

## Vì sao xu hướng nghiêng về skill

- Model mới tự lập plan và tự kiểm tra kết quả tốt. Không còn cần chia việc cho nhiều "chuyên gia" chỉ để mỗi prompt ngắn lại.
- Progressive disclosure giải quyết chuyện context phình to: agent chỉ thấy mô tả ngắn của skill, cần thì mới nạp nội dung đầy đủ. Đây từng là lý do chính để tách agent.
- Bàn giao giữa các agent làm mất thông tin. Gom về một loop thì giữ được toàn bộ context.
- Skill dễ chia chủ sở hữu (mỗi team một skill, có người chịu trách nhiệm), dễ test, dễ chuyển sang model mới.
- Skill đang trở thành định dạng chung: Anthropic đưa Agent Skills thành chuẩn mở, và một số công cụ khác cũng đọc được cấu trúc `SKILL.md`. Điểm này cần kiểm tra lại với nguồn mới nhất.

## Mô hình đang hình thành

| Thành phần | Chứa gì |
|---|---|
| Skill | Kiến thức chuyên môn và quy trình |
| Tool / MCP | Hành động và ràng buộc cứng (validate, cổng duyệt) |
| Subagent | Chỉ dùng khi cần cô lập: kiểm chứng black-box, việc song song độc lập, giới hạn quyền |

Hai thứ không loại trừ nhau: subagent có thể nạp skill. Ví dụ `implementer` nạp `add-endpoint` và `add-angular-feature`.

## Áp vào repo này

Trước khi tạo agent mới, hỏi một câu: tách ra để có **context/quyền riêng**, hay chỉ để có **hướng dẫn riêng**?

- Chỉ cần hướng dẫn riêng thì dùng skill. Ví dụ: kiến thức .NET và kiến thức Angular. Vì vậy repo không tách `implementer` thành agent backend và agent frontend.
- Cần context hoặc quyền riêng thì dùng agent:
  - `reviewer` cần một góc nhìn mới, không mang giả định của người viết code.
  - `tester` không được sửa code chính.
  - Hai `implementer` chạy song song ở Tuần 3 Ngày 7.
