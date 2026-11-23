# What a user may upload: `GET /api/media/limits`

A client that is about to upload a file asks this first, so it can say "too large" or "no room" without sending the bytes
(a refusal that comes after the body has started can reach a client as a connection reset). Behind the door, like every
media route; the answer is the signed-in user's own and never names a user. Design: D37 in
[DESIGN.md](../DESIGN.md#2-decisions) (Proposed).

```json
{
  "maxUploadBytes": 536870912,
  "maxFiles": 5000,
  "maxBytes": 21474836480,
  "maxConcurrentUploads": 4,
  "files": 12,
  "bytes": 83886080,
  "filesRemaining": 4988,
  "bytesRemaining": 21390950400,
  "uploadsRunning": 0
}
```

| Member | Meaning |
|--------|---------|
| `maxUploadBytes` | The largest file one upload may carry (`AudioLab:Media:MaxUploadBytes`). |
| `maxFiles`, `maxBytes` | What the user's quota lets them keep. The hosted door replaces the quota, so these may differ by user. |
| `maxConcurrentUploads` | The most uploads the user may have running; one more is a `429`. |
| `files`, `bytes` | What the user has, **counting their running uploads** (the bytes an upload has received so far). |
| `filesRemaining`, `bytesRemaining` | What is left of the quota, never below 0. |
| `uploadsRunning` | How many uploads the user has running now. |

An upload fits when its size is at most `maxUploadBytes` and at most `bytesRemaining`, `filesRemaining` is above 0 and
`uploadsRunning` is below `maxConcurrentUploads`. The numbers are advice: another upload can finish between this answer
and yours, and the upload route still judges the file on its bytes. A user who is over a limit that was lowered after they
uploaded sees `0` remaining, not a negative number.
