# `notion-update-view`

Update a view's name, filters, sorts, or display configuration.
Use "fetch" to get view IDs from database responses. Only include fields
you want to change. The "configure" param uses the same DSL as create_view.
Use CLEAR to remove settings:
- CLEAR FILTER — remove all filters
- CLEAR SORT — remove all sorts
- CLEAR GROUP BY — remove grouping

See notion://docs/view-dsl-spec resource for full syntax.
<example description="Rename">{"view_id": "abc123", "name": "Sprint Board"}</example>
<example description="Update filter">{"view_id": "abc123", "configure": "FILTER "Status" = "Done""}</example>
<example description="Clear filter, add sort">{"view_id": "abc123", "configure": "CLEAR FILTER; SORT BY "Created" DESC"}</example>
<example description="Update grouping">{"view_id": "abc123", "configure": "GROUP BY "Priority"; SHOW "Name", "Status""}</example>

## パラメータ

| パラメータ | 型 | 必須 | 説明 |
|------------|------|:----:|------|
| `view_id` | string | ✓ | The view to update. Accepts a view:// URI, a Notion URL with ?v= parameter, or a bare UUID. |
| `name` | string |  | New name for the view. |
| `configure` | string |  | View configuration DSL string. Supports FILTER, SORT BY, GROUP BY, CALENDAR BY, TIMELINE BY, MAP BY, CHART, FORM, SHOW, HIDE, COVER, WRAP CELLS, FREEZE COLUMNS, and CLEAR directives. |
