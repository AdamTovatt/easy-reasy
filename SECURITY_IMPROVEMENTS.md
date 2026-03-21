# Security Improvements — EasyReasy.Auth

Tracked improvements identified during review. Items are checked off as they are completed.

## Done

### Password Hashing (V4 — breaking)
- [x] **#2 — Minimum iteration count enforcement** — V4 verification rejects hashes with iteration count below 10,000
- [x] **#7 — Username+password concatenation ambiguity** — Removed entirely in V4 (no username parameter)
- [x] **#9 — NeedsRehash for old formats** — V2/V3 dropped entirely, clean break to V4
- [x] **#12 — Max password length** — 1024 UTF-8 byte limit prevents CPU DoS via PBKDF2

### Progressive Delay Middleware
- [x] **#1 — X-Forwarded-For spoofing** — Added `trustedProxyCount` parameter, reads from the right, defaults to 0 (ignores header)
- [x] **#3 — Delay happens after response** — Moved delay to before `_next` so attacker must wait before getting the response
- [x] **#4 — No max delay cap** — Added 30-second maximum delay cap
- [x] **#11 — Hardcoded paths and parameters** — Extracted all constants into `ProgressiveDelayOptions` with configurable `DelayIncrement`, `FreeFailures`, `MaxDelay`, and `TrustedProxyCount`
- [x] **#17 — No TTL/eviction on failure dictionary** — Added `FailureEntryLifetime` (default 1 hour) with per-lookup staleness checks and periodic sweep using `TimeProvider` for testability

### Request/Response Security
- [x] **#5 — Cache-Control: no-store header** — Added to all auth endpoint responses (apikey, login, refresh)
- [x] **#6 — ToString() leaks secrets** — Redacted secrets in `ToString()` across all request and response models
- [x] **#16 — FromJson exceptions leak secrets** — Sanitized exception messages and stripped inner exceptions to exclude raw JSON input

### JWT Token Security
- [x] **#8 — No jti claim** — Added GUID-based `jti` (JWT ID) to all issued tokens for individual revocation support
- [x] **#13 — Audience validation disabled** — Added opt-in `Audience` property on `EasyReasyAuthOptions`; when set, tokens include an `aud` claim and validation requires a matching audience
- [x] **#14 — Clock skew too generous** — Reduced from default 5 minutes to 30 seconds (behavioral change for existing deployments)
- [x] **#15 — No nbf (not-before) claim** — Added `notBefore: DateTime.UtcNow` to all issued tokens

### Refresh Tokens
- [ ] **#10 — TOCTOU race in refresh rotation** — Concurrent requests with the same refresh token could both succeed before either marks it consumed. Partially mitigated by `MarkAsConsumedAsync` returning bool, but depends on store implementation being truly atomic.
