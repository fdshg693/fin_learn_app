# `notion-create-pages`

## Overview
Creates one or more Notion pages, with the specified properties and content.
## Parent
All pages created with a single call to this tool will have the same parent. The parent can be a Notion page ("page_id") or data source ("data_source_id"). If the parent is omitted, the pages are created as standalone, workspace-level private pages, and the person that created them can organize them later as they see fit.
If you have a database URL, ALWAYS pass it to the "fetch" tool first to get the schema and URLs of each data source under the database. You can't use the "database_id" parent type if the database has more than one data source, so you'll need to identify which "data_source_id" to use based on the situation and the results from the fetch tool (data source URLs look like collection://<data_source_id>).
If you know the pages should be created under a data source, do NOT use the database ID or URL under the "page_id" parameter; "page_id" is only for regular, non-database pages.
## Content
Notion page content is a string in Notion-flavored Markdown format.
Don't include the page title at the top of the page's content. Only include it under "properties".
**IMPORTANT**: For the complete Markdown specification, always first fetch the MCP resource at `notion://docs/enhanced-markdown-spec`. Do NOT guess or hallucinate Markdown syntax. This spec is also applicable to other tools like update-page and fetch.
## Properties
Notion page properties are a JSON map of property names to SQLite values.
When creating pages in a database:
- Use the correct property names from the data source schema shown in the fetch tool results.
- Always include a title property. Data sources always have exactly one title property, but it may not be named "title", so, again, rely on the fetched data source schema.

For pages outside of a database:
- The only allowed property is "title",	which is the title of the page in inline markdown format. Always include a "title" property.

**IMPORTANT**: Some property types require expanded formats:
- Date properties: Split into "date:{property}:start", "date:{property}:end" (optional), and "date:{property}:is_datetime" (0 or 1)
- Place properties: Split into "place:{property}:name", "place:{property}:address", "place:{property}:latitude", "place:{property}:longitude", and "place:{property}:google_place_id" (optional)
- Number properties: Use JavaScript numbers (not strings)
- Checkbox properties: Use "__YES__" for checked, "__NO__" for unchecked

**Special property naming**: Properties named "id" or "url" (case insensitive) must be prefixed with "userDefined:" (e.g., "userDefined:URL", "userDefined:id")
## Templates
When creating a page in a database, you can apply a template to pre-populate it with content and property values. Use the "fetch" tool on a database to see available templates in the <templates> section of each data source.
When using a template:
- Pass the template's ID as "template_id" in the page object.
- Do NOT include "content" when using a template, as the template provides it.
- You can still set "properties" alongside the template to override template defaults.
- Template application is asynchronous. The page is created immediately but starts blank; the template content will appear shortly after.

## Icon and Cover
Each page can optionally have an icon and a cover image.
- "icon": An emoji character (e.g. "🚀"), a custom emoji by name (e.g. ":rocket_ship:"), or an external image URL. Use "none" to remove. Omit to leave unchanged.
- "cover": An external image URL. Use "none" to remove. Omit to leave unchanged.

## Examples
		<example description="Create a page with an icon and cover">
		{
			"pages": [
				{
					"properties": {"title": "My Page"},
					"icon": "🚀",
					"cover": "https://example.com/cover.jpg"
				}
			]
		}
		</example>
		<example description="Create a page from a database template">
		{
			"parent": {"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd"},
			"pages": [
				{
					"template_id": "a5da15f6-b853-455d-8827-f906fb52db2b",
					"properties": {
						"Task Name": "New urgent bug"
					}
				}
			]
		}
		</example>
		<example description="Create a standalone page with a title and content">
		{
			"pages": [
				{
					"properties": {"title": "Page title"},
					"content": "# Section 1 {color="blue"}
Section 1 content
<details>
<summary>Toggle block</summary>
	Hidden content inside toggle
</details>"
				}
			]
		}
		</example>
		<example description="Create a page under a database's data source">
		{
			"parent": {"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd"},
			"pages": [
				{
					"properties": {
						"Task Name": "Task 123",
						"Status": "In Progress",
						"Priority": 5,
						"Is Complete": "__YES__",
						"date:Due Date:start": "2024-12-25",
						"date:Due Date:is_datetime": 0
					}
				}
			]
		}
		</example>
		<example description="Create a page with an existing page as a parent">
		{
			"parent": {"page_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"},
			"pages": [
				{
					"properties": {"title": "Page title"},
					"content": "# Section 1
Section 1 content
# Section 2
Section 2 content"
				}
			]
		}
		</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `pages` | array | ✓ | The pages to create. |
| `parent` | unknown | ✓ | The parent under which the new pages will be created. This can be a page (page_id), a database page (database_id), or a data source/collection under a database (data_source_id). If omitted, the new pages will be created as private pages at the workspace level. Use data_source_id when you have a collection:// URL from the fetch tool. |
