# Idfy.Api integration guide

How to call Idfy.Api from another service: request formats, response shapes,
the async polling flow, errors and limits. For every endpoint's request and
response, see the [API reference](API-REFERENCE.md).

Idfy.Api is an internal wrapper around IDfy EVE v3. It checks your input,
forwards it to IDfy, and returns IDfy's task object. The details of that
object are covered under [Response envelope](#response-envelope).

- [Before you start](#before-you-start)
- [Sending images](#sending-images)
- [Response envelope](#response-envelope)
- [Sync vs async verification](#sync-vs-async-verification)
- [Errors](#errors)
- [Retries, timeouts and credits](#retries-timeouts-and-credits)
- [Endpoint reference](API-REFERENCE.md)
- [Integration checklist](#integration-checklist)

---

## Before you start

| Topic | Details |
|-------|---------|
| **Base URL** | The environment's host, e.g. `https://idfy-api.internal`. Locally: `http://localhost:5142`. HTTP requests are redirected to HTTPS when HTTPS is configured, so use the `https://` URL. |
| **Authentication** | None. The API is only reachable from inside the network. |
| **Browser callers** | Your origin must be listed in the server's `Cors:AllowedOrigins`. Server-to-server calls are not affected by CORS. |
| **Rate limit** | 100 requests per 60 seconds per client IP by default (fixed window). Poll calls count toward the limit. Going over it returns `429`. |
| **Request bodies** | JSON (`Content-Type: application/json`) with **camelCase** fields, or `multipart/form-data` for `/upload` endpoints. |
| **Response bodies** | Successful task responses use **snake_case** fields (IDfy's format). Errors are `application/problem+json`. |
| **OpenAPI** | `GET /openapi/v1.json` is available in the Development environment only. |
| **Examples** | [`src/Idfy.Api/Idfy.Api.http`](../src/Idfy.Api/Idfy.Api.http) contains a runnable request for every endpoint. |

### JSON request rules

- **Unknown fields are rejected** with `400`. Send only the fields documented
  for the endpoint.
- **Duplicate fields are rejected** with `400`.
- **Dates** must be `YYYY-MM-DD` (e.g. `"1985-02-15"`).
- **`taskId` / `groupId`** (optional on every JSON endpoint) must be GUIDs. The
  server generates any you leave out. They are returned as `task_id` /
  `group_id`, so you can use them to match responses to your own records.
  `/upload` endpoints don't accept them and always generate new ones.

---

## Sending images

Image endpoints accept an image in one of three ways:

| Method | Field | Notes |
|--------|-------|-------|
| **Public URL** | `document` (JSON) | Must be an absolute `http://` or `https://` URL that IDfy can reach. IDfy downloads the image itself, so Idfy.Api doesn't check its size or resolution. |
| **Base64** | `document` (JSON) | Raw Base64 **without** a `data:image/...;base64,` prefix. A value with the prefix fails validation. |
| **File upload** | `file` (multipart) | Use the `/upload` variant of the endpoint. The part's `Content-Type` must be `image/*`, otherwise the API returns `415`. |

For endpoints that take a second image (passport or voter ID back side, face
compare), use `document2` in JSON or `file2` in multipart.

### Limits

Idfy.Api checks these for Base64 and uploaded images. It doesn't check images
sent as URLs.

| Limit | Value | Error |
|-------|-------|-------|
| Base64 length | 3,000,000 characters (an image of about **2.25 MB**) | `413` |
| Uploaded file size | Also limited by the Base64 cap: files over about **2.25 MB** are rejected | `413` |
| Request body | 11 MB (JSON); 10 MB (multipart) | `413` |
| Resolution: all endpoints except face compare | Width and height each **150–10,000 px** | `422` |
| Resolution: face compare | Width and height each **150–4,096 px** | `422` |

Idfy.Api checks resolution only for JPEG and PNG. It passes other formats
through, and IDfy decides whether to accept them.

---

## Response envelope

Every successful sync call, and every completed poll, returns IDfy's task
object with `200 OK`. Fields are snake_case:

```json
{
  "action": "extract",
  "type": "ind_pan",
  "status": "completed",
  "task_id": "8f0c6e1a-5b0e-4d5a-9a4c-0d6f3b8e2a11",
  "group_id": "1d2b7c9e-3f4a-4b6c-8d0e-2a1b3c4d5e6f",
  "request_id": "a957d82c-987c-4fa9-b69f-6765dc3c1035",
  "created_at": "2026-09-25T10:15:02+05:30",
  "completed_at": "2026-09-25T10:15:04+05:30",
  "result": { "...": "endpoint-specific, see the reference below" }
}
```

- Check `status` before you read `result`. IDfy reports task-level outcomes
  here, and a `200` means IDfy returned a task, not that the check passed.
- Fields with no value are returned as `null`. Date fields that IDfy extracts
  from a card are **strings**, and may be `""` when the date isn't printed on
  the card.
- Responses contain personal data (names, ID numbers, addresses, dates of
  birth). Store and log them according to your own PII rules.

---

## Sync vs async verification

The verify-with-source endpoints (driving licence, voter ID, passport and
PAN-Aadhaar link) each come in two forms:

| Form | Call | Returns |
|------|------|---------|
| **Sync** | `POST /api/{type}/verify/sync` | `200` with the task object when IDfy finishes. The call can take up to about 60 s. |
| **Async** | `POST /api/{type}/verify` | `200` with `{"request_id": "..."}` right away. |
| **Poll** | `GET /api/{type}/verify/{requestId}` | `202` while IDfy is still processing; `200` with the task object when it's done. |

Use async when you can't hold a request open for a minute, or when you're
verifying many IDs at once.

### Async flow

```text
POST /api/voter-id/verify            {"idNumber": "ABC1234567"}
  200  {"request_id": "a957d82c-987c-4fa9-b69f-6765dc3c1035"}

GET  /api/voter-id/verify/a957d82c-987c-4fa9-b69f-6765dc3c1035
  202  {"requestId": "a957d82c-987c-4fa9-b69f-6765dc3c1035", "status": "processing"}

GET  /api/voter-id/verify/a957d82c-987c-4fa9-b69f-6765dc3c1035
  200  { "status": "completed", "request_id": "...", "result": { ... } }
```

> [!NOTE]
> Field names differ between these responses. The submit response uses
> snake_case `request_id`, because it comes from IDfy. The `202` poll response
> uses camelCase `requestId`, because Idfy.Api generates it.

**Polling guidance**

- Start polling after about 2 s, then back off (for example 2 s, 4 s, 8 s,
  capped at 15 s).
- Stop after a deadline you choose, such as 5 minutes, and treat the check as
  failed.
- Every poll counts toward the per-IP [rate limit](#before-you-start).
- Poll the same type you submitted. A driving-licence `requestId` must be
  polled on `/api/driving-license/verify/{requestId}`.

---

## Errors

All errors are [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) problem
details (`Content-Type: application/problem+json`). Every error includes a
`traceId`, which is the same value as the `TraceId` column in the server's log
tables. Quote it when you report a problem, because it links your request to
the server's logs and to the IDfy call it triggered.

### Status codes

| Status | Cause | What to do |
|--------|-------|------------|
| `400` | A field failed validation. The body has an `errors` map. | Fix the field named in `errors`. |
| `400` | A duplicate field. `detail` names the field. | Send each field once. |
| `400` | Malformed JSON, an unknown field, a wrong type, or an unparseable date. In production the body has no `detail`. | Check the body against the endpoint's field table. |
| `400` | Empty upload, or an Aadhaar upload without `consent=true`. | Fix the request. |
| `400` / `413` / `422` / `429` from IDfy | IDfy rejected the input, for example as an unreadable image, an invalid URL, or an oversized payload, or IDfy rate-limited the call. `title` is IDfy's error code. | Fix the input, or back off on `429`. |
| `413` | The Base64 image or the request body is over the [limit](#limits). | Compress or resize the image, or send a URL. |
| `415` | An uploaded file isn't an `image/*`. | Send the correct part `Content-Type`. |
| `422` | The image resolution is outside the [limits](#limits). | Resize the image. |
| `429` | The Idfy.Api per-IP rate limit was exceeded. | Back off and retry later. |
| `500` | Unexpected server error. Details are hidden. | Report the `traceId`. |
| `501` | `detectDocSide`, `detectFace` or `detectScanned` was requested but isn't configured on this server. | Leave the flag out, or ask the API owners to configure it. |
| `502` | IDfy is unreachable or failing, or there's a server-side account problem (credentials or insufficient credits). | Retry later. Report it if it persists. |
| `504` | IDfy didn't answer within the timeout (about 60 s). | See [Retries](#retries-timeouts-and-credits). |

### Examples

Validation error (`errors` keys use PascalCase field names):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "PanNumber": ["PanNumber must be a valid PAN (e.g. ABCDE1234F)."],
    "AadhaarNumber": ["AadhaarNumber must be 12 digits."]
  },
  "traceId": "d8cf25ab65130707c123eb6057c22365"
}
```

Error returned from IDfy (`title` is IDfy's error code, `detail` is IDfy's
message):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "INVALID_URL",
  "status": 400,
  "detail": "<IDfy's message>",
  "idfyStatus": 400,
  "idfyRequestId": "b1f0c3d2-...",
  "traceId": "d8cf25ab..."
}
```

Upstream failure (a server-side problem, not caused by your request):

```json
{
  "title": "UPSTREAM_ERROR",
  "status": 502,
  "detail": "The document verification service is unavailable.",
  "idfyStatus": 403,
  "idfyRequestId": null,
  "traceId": "d8cf25ab..."
}
```

Image checks:

```json
{ "title": "Image resolution out of range.", "status": 422,
  "detail": "Image is 100x100px; width and height must each be between 150 and 10000px.", "traceId": "d8cf25ab..." }

{ "title": "Unsupported Media Type", "status": 415,
  "detail": "Uploaded file must be an image, got 'text/plain'.", "traceId": "d8cf25ab..." }
```

---

## Retries, timeouts and credits

- **Set your client timeout to at least 70 s.** Idfy.Api waits up to about 60 s
  for IDfy, and sync verify and OCR calls can take that long.
- **Idfy.Api never retries calls to IDfy.** Each call that reaches IDfy may use
  IDfy credits. Resending an extraction or verification can be charged twice.
- **Safe to retry:** `429` (after a backoff), and a `502` with no
  `idfyStatus`, which means Idfy.Api couldn't reach IDfy at all.
- **Retry with care:** `504`. IDfy may still have finished the task and charged
  for it. For verify-with-source, the async flow avoids this.
- **Don't retry without changes:** `400`, `413`, `415`, `422` and `501`. The
  same request will fail again.

---

## Endpoint reference

Every endpoint's full request and response, with example bodies captured from
the running API, is in the [API reference](API-REFERENCE.md).

---

## Integration checklist

- [ ] Client timeout is at least 70 s for sync endpoints.
- [ ] JSON requests use camelCase fields and contain no extra fields.
- [ ] Response parsing reads snake_case fields and checks `status` before
      reading `result`.
- [ ] Base64 images have no `data:` prefix and are under about 2.25 MB, or the
      images are sent as URLs.
- [ ] Aadhaar calls send `consent: true` only after the holder has actually
      consented.
- [ ] Async verification reads `request_id` from the submit response and
      polls with backoff and a deadline.
- [ ] Errors are handled by status code, and `traceId` is logged for support.
- [ ] Extraction and verification calls are not retried automatically on
      `504` (to avoid being charged twice).
