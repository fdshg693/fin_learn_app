# `notion-update-data-source`

Update a Notion data source's schema, title, or attributes using SQL DDL statements. Returns Markdown showing updated structure and schema.
Accepts a data source ID (collection ID from fetch response's <data-source> tag) or a single-source database ID. Multi-source databases require the specific data source ID.
The statements param accepts semicolon-separated DDL statements:
- ADD COLUMN "Name" <type> - add a new property
- DROP COLUMN "Name" - remove a property
- RENAME COLUMN "Old" TO "New" - rename a property
- ALTER COLUMN "Name" SET <type> - change type/options

Same type syntax as create_database. Key types:
- SELECT('opt':color, ...) / MULTI_SELECT('opt':color, ...)
- NUMBER [FORMAT 'dollar'] / FORMULA('expression')
- RELATION('ds_id') / RELATION('ds_id', DUAL) / RELATION('ds_id', DUAL 'synced_name' 'synced_id')
- ROLLUP('rel_prop', 'target_prop', 'function') / UNIQUE_ID [PREFIX 'X']
- Simple: TITLE, RICH_TEXT, DATE, PEOPLE, CHECKBOX, URL, EMAIL, PHONE_NUMBER, STATUS, FILES

<example description="Add properties">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "statements": "ADD COLUMN "Priority" SELECT('High':red, 'Medium':yellow, 'Low':green); ADD COLUMN "Due Date" DATE"}</example>
<example description="Rename property">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "statements": "RENAME COLUMN "Status" TO "Project Status""}</example>
<example description="Remove property">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "statements": "DROP COLUMN "Old Property""}</example>
<example description="Add self-relation">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "statements": "ADD COLUMN "Parent" RELATION('f336d0bc-b841-465b-8045-024475c079dd', DUAL 'Children' 'children'); ADD COLUMN "Children" RELATION('f336d0bc-b841-465b-8045-024475c079dd', DUAL 'Parent' 'parent')"}</example>
<example description="Update title">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "title": "Project Tracker 2024"}</example>
<example description="Trash data source">{"data_source_id": "f336d0bc-b841-465b-8045-024475c079dd", "in_trash": true}</example>
Notes: Cannot delete/create title properties. Max one unique_id property. Cannot update synced databases. Use "fetch" first to see current schema and get the data source ID from <data-source url="collection://..."> tags.

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `data_source_id` | string | ✓ | The data source to update. Accepts a collection:// URI from <data-source> tags, a bare UUID, or a database ID (only if the database has a single data source). |
| `statements` | string |  | Semicolon-separated SQL DDL statements to update the schema. Supports ADD COLUMN, DROP COLUMN, RENAME COLUMN, ALTER COLUMN SET. |
| `title` | string |  | The new title of the data source. |
| `description` | string |  | The new description of the data source. |
| `is_inline` | boolean |  |  |
| `in_trash` | boolean |  |  |
