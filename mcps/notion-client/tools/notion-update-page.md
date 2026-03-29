# `notion-update-page`

## Overview
Update a Notion page's properties or content.
## Properties
Notion page properties are a JSON map of property names to SQLite values.
For pages in a database:
- ALWAYS use the "fetch" tool first to get the data source schema and the	exact property names.
- Provide a non-null value to update a property's value.
- Omitted properties are left unchanged.

**IMPORTANT**: Some property types require expanded formats:
- Date properties: Split into "date:{property}:start", "date:{property}:end" (optional), and "date:{property}:is_datetime" (0 or 1)
- Place properties: Split into "place:{property}:name", "place:{property}:address", "place:{property}:latitude", "place:{property}:longitude", and "place:{property}:google_place_id" (optional)
- Number properties: Use JavaScript numbers (not strings)
- Checkbox properties: Use "__YES__" for checked, "__NO__" for unchecked

**Special property naming**: Properties named "id" or "url" (case insensitive) must be prefixed with "userDefined:" (e.g., "userDefined:URL", "userDefined:id")
For pages outside of a database:
- The only allowed property is "title",	which is the title of the page in inline markdown format.

## Content
Notion page content is a string in Notion-flavored Markdown format.
**IMPORTANT**: For the complete Markdown specification, first fetch the MCP resource at `notion://docs/enhanced-markdown-spec`. Do NOT guess or hallucinate Markdown syntax.
Before updating a page's content with this tool, use the "fetch" tool first to get the existing content to find out the Markdown snippets to use in the "update_content" command's old_str fields.
### Preserving Child Pages and Databases
When using "replace_content", the operation will check if any child pages or databases would be deleted. If so, it will fail with an error listing the affected items.
To preserve child pages/databases, include them in new_str using `<page url="...">` or `<database url="...">` tags. Get the exact URLs from the "fetch" tool output.
**CRITICAL**: To intentionally delete child content: if the call failed with validation and requires `allow_deleting_content` to be true, DO NOT automatically assume the content should be deleted. ALWAYS show the list of pages to be deleted and ask for user confirmation before proceeding.
## Icon and Cover
You can set or remove a page's icon and cover alongside any command.
- "icon": An emoji character (e.g. "🚀"), a custom emoji by name (e.g. ":rocket_ship:"), or an external image URL. Use "none" to remove. Omit to leave unchanged.
- "cover": An external image URL. Use "none" to remove. Omit to leave unchanged.

## Examples
		<example description="Update page icon and cover">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_properties",
			"properties": {"title": "My Page"},
			"icon": "🚀",
			"cover": "https://example.com/cover.jpg"
		}
		</example>
		<example description="Update page properties">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_properties",
			"properties": {
				"title": "New Page Title",
				"status": "In Progress",
				"priority": 5,
				"checkbox": "__YES__",
				"date:deadline:start": "2024-12-25",
				"date:deadline:is_datetime": 0,
				"place:office:name": "HQ",
				"place:office:latitude": 37.7749,
				"place:office:longitude": -122.4194
			}
		}
		</example>
		<example description="Replace the entire content of a page">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "replace_content",
			"new_str": "# New Section
Updated content goes here"
		}
		</example>
		<example description="Update specific content in a page (search-and-replace)">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_content",
			"content_updates": [
				{
					"old_str": "# Old Section
Old content here",
					"new_str": "# New Section
Updated content goes here"
				}
			]
		}
		</example>
		<example description="Insert content after a specific location">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_content",
			"content_updates": [
				{
					"old_str": "## Previous section
Existing content",
					"new_str": "## Previous section
Existing content

## New Section
Content to insert goes here"
				}
			]
		}
		</example>
		<example description="Multiple content updates in a single call">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_content",
			"content_updates": [
				{
					"old_str": "Old text 1",
					"new_str": "New text 1"
				},
				{
					"old_str": "Old text 2",
					"new_str": "New text 2"
				}
			]
		}
		</example>
## Templates
You can apply a template to an existing page using the "apply_template" command. The template content is appended to the page asynchronously. Get template IDs from the <templates> section in the fetch tool results for a database, or use any page ID as a template.
		<example description="Apply a template to an existing page">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "apply_template",
			"template_id": "a5da15f6-b853-455d-8827-f906fb52db2b"
		}
		</example>
## Verification
You can verify or unverify a page using the "update_verification" command. Verification marks a page as reviewed and up-to-date. Requires a Business or Enterprise plan (or the page must be in a wiki).
		<example description="Verify a page for 90 days">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_verification",
			"verification_status": "verified",
			"verification_expiry_days": 90
		}
		</example>
		<example description="Verify a page indefinitely">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_verification",
			"verification_status": "verified"
		}
		</example>
		<example description="Remove verification from a page">
		{
			"page_id": "f336d0bc-b841-465b-8045-024475c079dd",
			"command": "update_verification",
			"verification_status": "unverified"
		}
		</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `page_id` | string | ✓ | The ID of the page to update, with or without dashes. |
| `command` | string | ✓ |  |
| `properties` | object | ✓ | Required for "update_properties" command. A JSON object that updates the page's properties. For pages in a database, use the SQLite schema definition shown in <database>. For pages outside of a database, the only allowed property is "title", which is the title of the page in inline markdown format. Use null to remove a property's value. |
| `new_str` | string |  | Required for "replace_content" command. The new content string to replace the entire page content with. |
| `content_updates` | array | ✓ | Required for "update_content" command. An array of search-and-replace operations, each with old_str (content to find) and new_str (replacement content). |
| `allow_deleting_content` | boolean |  |  |
| `template_id` | string |  | Required for "apply_template" command. The ID of a template to apply to this page. Template content is appended to any existing page content. |
| `verification_status` | string |  |  |
| `verification_expiry_days` | integer |  | Optional for "update_verification" command when verification_status is "verified". Number of days until verification expires (e.g. 7, 30, 90). Omit for indefinite verification. |
| `icon` | string |  | An emoji character (e.g. "🚀"), a custom emoji by name (e.g. ":rocket_ship:"), or an external image URL. Use "none" to remove the icon. Omit to leave unchanged. Can be set alongside any command. |
| `cover` | string |  | An external image URL for the page cover. Use "none" to remove the cover. Omit to leave unchanged. Can be set alongside any command. |
