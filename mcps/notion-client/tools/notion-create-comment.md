# `notion-create-comment`

Add a comment to a page or specific content.
Creates a new comment. Provide `page_id` to identify the page, then choose ONE targeting mode:
- `page_id` alone: Page-level comment on the entire page
- `page_id` + `selection_with_ellipsis`: Comment on specific block content
- `discussion_id`: Reply to an existing discussion thread (page_id is still required)

For content targeting, use `selection_with_ellipsis` with ~10 chars from start and end: "# Section Ti...tle content"
<example description="Page-level comment">
{"page_id": "uuid", "rich_text": [{"text": {"content": "Comment"}}]}
</example>
<example description="Comment on specific content">
{"page_id": "uuid", "selection_with_ellipsis": "# Meeting No...es heading",
 "rich_text": [{"text": {"content": "Comment on this section"}}]}
</example>
<example description="Reply to discussion">
{"page_id": "uuid", "discussion_id": "discussion://pageId/blockId/discussionId",
 "rich_text": [{"text": {"content": "Reply"}}]}
</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `rich_text` | array | ✓ | An array of rich text objects that represent the content of the comment. |
| `page_id` | string | ✓ | The ID of the page to comment on (with or without dashes). |
| `discussion_id` | string |  | The ID or URL of an existing discussion to reply to (e.g., discussion://pageId/blockId/discussionId). |
| `selection_with_ellipsis` | string |  | Unique start and end snippet of the content to comment on. DO NOT provide the entire string. Instead, provide up to the first ~10 characters, an ellipsis, and then up to the last ~10 characters. Make sure you provide enough of the start and end snippet to uniquely identify the content. For example: "# Section heading...last paragraph." |
