# `notion-create-view`

Create a new view on a Notion database.
Use "fetch" first to get the database_id and data_source_id (from <data-source> tags in the response).
Supported types: table, board, list, calendar, timeline, gallery, form, chart, map, dashboard.
The optional "configure" param accepts a DSL for filters, sorts, grouping,
and display options. See the notion://docs/view-dsl-spec resource for full
syntax. Key directives:
- FILTER "Property" = "value" — filter rows
- SORT BY "Property" ASC — sort rows
- GROUP BY "Property" — group by property (required for board views)
- CALENDAR BY "Property" — date property (required for calendar views)
- TIMELINE BY "Start" TO "End" — date range (required for timeline views)
- MAP BY "Property" — location property (required for map views)
- CHART column|bar|line|donut|number — chart type with optional AGGREGATE, COLOR, HEIGHT, SORT, STACK BY, CAPTION
- FORM CLOSE|OPEN — close/open form submissions
- FORM ANONYMOUS true|false — toggle anonymous submissions
- FORM PERMISSIONS none|reader|editor — set submission permissions
- SHOW "Prop1", "Prop2" — set visible properties
- COVER "Property" — cover image property

<example description="Table view">{"database_id": "abc123", "data_source_id": "def456", "name": "All Tasks", "type": "table"}</example>
<example description="Board grouped by Status">{"database_id": "abc123", "data_source_id": "def456", "name": "Task Board", "type": "board", "configure": "GROUP BY "Status""}</example>
<example description="Filtered + sorted table">{"database_id": "abc123", "data_source_id": "def456", "name": "Active", "type": "table", "configure": "FILTER "Status" = "In Progress"; SORT BY "Due Date" ASC"}</example>
<example description="Calendar view">{"database_id": "abc123", "data_source_id": "def456", "name": "Calendar", "type": "calendar", "configure": "CALENDAR BY "Due Date""}</example>
<example description="Dashboard">{"database_id": "abc123", "data_source_id": "def456", "name": "Overview", "type": "dashboard"}</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `database_id` | string | ✓ | The database to create a view in. Accepts a Notion URL or a bare UUID. |
| `data_source_id` | string | ✓ | The data source (collection) ID. Accepts a collection:// URI from <data-source> tags or a bare UUID. |
| `name` | string | ✓ | The name of the view. |
| `type` | string | ✓ |  |
| `configure` | string |  | View configuration DSL string. Supports FILTER, SORT BY, GROUP BY, CALENDAR BY, TIMELINE BY, MAP BY, CHART, FORM, SHOW, HIDE, COVER, WRAP CELLS, and FREEZE COLUMNS directives. See notion://docs/view-dsl-spec. |
