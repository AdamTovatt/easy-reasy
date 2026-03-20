# Security Improvements — EasyReasy.Auth

Tracked improvements identified during review. Items are checked off as they are completed.

## Done

- [x] **#2 — Minimum iteration count enforcement** — V4 verification rejects hashes with iteration count below 10,000
- [x] **#7 — Username+password concatenation ambiguity** — Removed entirely in V4 (no username parameter)
- [x] **#9 — NeedsRehash for old formats** — V2/V3 dropped entirely, clean break to V4
- [x] **#12 — Max password length** — 1024 UTF-8 byte limit prevents CPU DoS via PBKDF2

- [x] **#1 — X-Forwarded-For spoofing** — Added `trustedProxyCount` parameter, reads from the right, defaults to 0 (ignores header)
- [x] **#3 — Delay happens after response** — Moved delay to before `_next` so attacker must wait before getting the response
- [x] **#4 — No max delay cap** — Added 30-second maximum delay cap

## Todo

### Progressive Delay Middleware
- [ ] **#11 — Hardcoded paths and parameters** — `/api/auth/*` paths, 500ms delay step, 10 free failures are all hardcoded constants. Make configurable.
- [ ] **#17 — No TTL/eviction on failure dictionary** — The IP failure count dictionary grows forever. Add periodic cleanup of stale entries (needs a time abstraction like `TimeProvider`).

### JWT Token Security
- [ ] **#8 — No `jti` claim** — Tokens have no unique ID, cannot be individually revoked. Add a GUID-based `jti` to all issued tokens.
- [ ] **#13 — Audience validation disabled** — Tokens issued for service A are accepted by service B. Add opt-in audience validation parameter.
- [ ] **#14 — Clock skew too generous** — Default 5-minute skew from the JWT library means expired tokens work for 5 extra minutes. Reduce to ~30 seconds.
- [ ] **#15 — No `nbf` (not-before) claim** — Issued tokens lack a not-before timestamp.

### Data Leakage
- [ ] **#6 — `ToString()` leaks secrets** — `LoginAuthRequest.ToString()` (via `ToJson()`) includes the plaintext password in output. Redact sensitive fields.
- [ ] **#16 — `FromJson` exceptions may leak secrets** — If JSON parsing fails, raw input (which could contain tokens/passwords) might appear in exception messages. Sanitize exception messages.

### HTTP Security
- [ ] **#5 — No `Cache-Control: no-store` header** — Auth endpoint responses (containing tokens) could be cached by proxies or browsers. Add `Cache-Control: no-store` to auth responses.

### Refresh Tokens
- [ ] **#10 — TOCTOU race in refresh rotation** — Concurrent requests with the same refresh token could both succeed before either marks it consumed. Partially mitigated by `MarkAsConsumedAsync` returning bool, but depends on store implementation being truly atomic.
