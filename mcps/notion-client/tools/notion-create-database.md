# `notion-create-database`

Creates a new Notion database using SQL DDL syntax.
If no title property provided, "Name" is auto-added. Returns Markdown with schema, SQLite definition, and data source ID in <data-source> tag for use with update_data_source and query_data_sources tools.
The schema param accepts a CREATE TABLE statement defining columns.
Type syntax:
- Simple: TITLE, RICH_TEXT, DATE, PEOPLE, CHECKBOX, URL, EMAIL, PHONE_NUMBER, STATUS, FILES
- SELECT('opt':color, ...) / MULTI_SELECT('opt':color, ...)
- NUMBER [FORMAT 'dollar'] / FORMULA('expression')
- RELATION('data_source_id') — one-way relation
- RELATION('data_source_id', DUAL) — two-way relation
- RELATION('data_source_id', DUAL 'synced_name') — two-way with synced property name
- RELATION('data_source_id', DUAL 'synced_name' 'synced_id') — two-way with synced name and ID (for self-relations)
- ROLLUP('rel_prop', 'target_prop', 'function')
- UNIQUE_ID [PREFIX 'X'] / CREATED_TIME / LAST_EDITED_TIME
- Any column: COMMENT 'description text' Colors: default, gray, brown, orange, yellow, green, blue, purple, pink, red

<example description="Minimal">{"schema": "CREATE TABLE ("Name" TITLE)"}</example>
<example description="Task DB">{"title": "Tasks", "schema": "CREATE TABLE ("Task Name" TITLE, "Status" SELECT('To Do':red, 'Done':green), "Due Date" DATE)"}</example>
<example description="With parent and options">{"parent": {"page_id": "f336d0bc-b841-465b-8045-024475c079dd"}, "title": "Projects", "schema": "CREATE TABLE ("Name" TITLE, "Budget" NUMBER FORMAT 'dollar', "Tags" MULTI_SELECT('eng':blue, 'design':pink), "Task ID" UNIQUE_ID PREFIX 'PRJ')"}</example>
<example description="Self-relation (two-step: create database first, then use its data source ID with update_data_source to add self-relations)">{"title": "Tasks", "schema": "CREATE TABLE ("Name" TITLE, "Parent" RELATION('ds_id', DUAL 'Children' 'children'), "Children" RELATION('ds_id', DUAL 'Parent' 'parent'))"}</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `schema` | string | ✓ | SQL DDL CREATE TABLE statement defining the database schema. Column names must be double-quoted, type options use single quotes. |
| `parent` | object | ✓ | The parent under which to create the new database. If omitted, the database will be created as a private page at the workspace level. |
| `title` | string |  | The title of the new database. |
| `description` | string |  | The description of the new database. |
