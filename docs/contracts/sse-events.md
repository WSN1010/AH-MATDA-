# SSE Job Events

`GET /api/jobs/{jobId}/events` returns `text/event-stream`.

Each persisted event uses the Job-local monotonic `sequence` as its SSE `id`:

```text
id: 3
event: stage.completed
data: {"jobId":"...","sequence":3,"eventType":"stage.completed","stage":"validation","status":"completed","summary":"...","occurredAt":"...","retryable":false,"correlationId":"..."}
```

Clients reconnect with `Last-Event-ID` or the equivalent `?after={sequence}` query. The API first replays events after that sequence, polls storage every second, emits `: heartbeat` at least every 15 seconds while idle, and closes after the terminal event is delivered.

Event types currently emitted are `job.queued`, `job.started`, `job.retrying`, `job.failed`, `job.succeeded`, `stage.started`, and `stage.completed`. Clients must treat unknown event types as forward-compatible progress events.
