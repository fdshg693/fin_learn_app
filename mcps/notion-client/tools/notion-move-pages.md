# `notion-move-pages`

Move one or more Notion pages or databases to a new parent.

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `page_or_database_ids` | array | ✓ | An array of up to 100 page or database IDs to move. IDs are v4 UUIDs and can be supplied with or without dashes (e.g. extracted from a <page> or <database> URL given by the "search" or "fetch" tool). Data Sources under Databases can't be moved individually. |
| `new_parent` | unknown | ✓ | The new parent under which the pages will be moved. This can be a page, the workspace, a database, or a specific data source under a database when there are multiple. Moving pages to the workspace level adds them as private pages and should rarely be used. |
