# skail USD quote (.NET 8)

[Português (Brasil)](README.pt-BR.md)

Demo 1 of skail: a **skailFunc** fetches the US dollar quote, waits **30 seconds with a durable delay**
(`SkailTask.DelaySeconds(30)`), fetches again, and keeps going until you press **Stop** (an external
event) or it reaches 100 quotes. A web page shows the progress live.

```
                 trigger / fire stop                        pulls work
Browser ─SSE─▶ web ───────────────────▶ skail server ◀─────────────────── worker
               ▲    SKAIL_BASE_URL       (environment)    SKAIL_SIDECAR      │
               └────────── POST /internal/runs/{id}/events ────────────────┘
```

- **worker** (`src/SkailUsdQuote.Worker`): the skailFunc `GetUsdQuote(runId, currency)`. The function only
  orchestrates; the I/O lives in commands (`FetchQuote`, `Notify`). It creates one
  `SkailTask.WaitForEvent("STOP_QUOTE", runId)` and races it against `SkailTask.DelaySeconds(30)` with
  `WhenAny` on every iteration.
- **web** (`src/SkailUsdQuote.Web`): static page + API. Starts the run (trigger), stops it (fire of
  `STOP_QUOTE`), keeps the run state in memory and streams it to the page (SSE).
- **contracts** (`src/SkailUsdQuote.Contracts`): DTOs and constants shared by both.

The page has a currency drop-down (Real, Euro, Yuan, Yen; Real is the default). It is locked while a run
is active. A single button toggles **Start** / **Stop**.

## How it connects to skail

The two services talk to your skail environment in different ways, so there are two addresses:

| Service | Variable | How | Used for |
|---|---|---|---|
| **worker** (the skailFunc) | `SKAIL_SIDECAR` | The skail runtime connection (SDK). It does **not** use the public HTTP API. | Pulling work for `GetUsdQuote` and its commands and recording their results. |
| **web** | `SKAIL_BASE_URL` | The HTTP API of the skail server (same address as `SKAIL_SIDECAR`, which the worker uses). | **Start**: `POST {SKAIL_BASE_URL}/api/v1/trigger/{name}/{version}/GetUsdQuote/{runId}` with body `["{runId}","{currency}"]`. **Stop**: `POST {SKAIL_BASE_URL}/api/v1/fire/STOP_QUOTE/{runId}` with body `{}`. |

Both use the same `SKAIL_NAMESPACE`, `SKAIL_KEY` and `SKAIL_WORKLOAD` (`name:version`, split into the route's
`{name}` and `{version}`). The web service sends `Accept` and `Content-Type: application/json`, the
`Skail-namespace` header and the key in both `skail-key` and `Authorization: Bearer`. `429`, `5xx` and
network errors are retried with the same run id; `401`/`403` are not (check `SKAIL_KEY` and
`SKAIL_NAMESPACE`). Without `SKAIL_BASE_URL`, Start fails with `502 SKAIL_BASE_URL is not set`.

The browser never calls skail: it only talks to the web service, so the key stays on the server side.

> The routes above are the ones the tested environment gateway accepted. The public docs show `/trigger/...`
> instead of `/api/v1/trigger/...` and only the `skail-key` header. If Start returns `skail answered 404`
> or `400`, compare the route and headers with what your environment's portal shows.

## Requirements

- .NET 8 SDK and VS Code (C# Dev Kit recommended)
- podman or docker
- PowerShell 7+ (`pwsh`), only if you use `scripts/run.ps1` (for example on Windows) instead of `scripts/run.sh`.
- A skail environment in the skail portal: its namespace, its key and the address of the skail server
  (the sidecar the worker connects to).
  In the portal, also register the workload `name:version`.
- A current browser (Chrome/Edge 111+, Firefox 113+, Safari 16.2+). The page uses Server-Sent Events,
  CSS `color-mix()` and modern JavaScript; Internet Explorer is not supported.

## Configure

```bash
cp .env.example .env     # .env holds secrets and is ignored by git
```

Fill in `SKAIL_WORKLOAD` (exactly as registered in the portal), `SKAIL_NAMESPACE` and `SKAIL_KEY`
(from your environment in the portal), `SKAIL_SIDECAR` (the address of the skail server for that
environment) and `SKAIL_BASE_URL` (same value as `SKAIL_SIDECAR`; the web app uses it for the HTTP API). The other values are runtime tuning defaults.

## Run with podman / docker

```bash
scripts/run.sh up        # build both images and start web + worker (CONTAINER_ENGINE=docker to force docker)
scripts/run.sh logs
scripts/run.sh down
```

On Windows, or anywhere with PowerShell 7+, run `pwsh scripts/run.ps1 <command>` instead: it takes the same commands as `run.sh` (`up`, `down`, `logs`, `restart`, `status`, ...), and `$env:CONTAINER_ENGINE = "docker"` forces docker.

Open <http://localhost:8080>, pick a currency and press **Start**.

Durability demo: press **Start**, restart the worker during the 30 s wait (`scripts/run.sh restart`) and
watch the run continue. Press **Stop** at any time, even in the middle of a wait: the function wakes up
immediately and ends without another fetch.

## VS Code

Open the folder. Recommended extensions are suggested automatically.

- `Ctrl+Shift+B` builds, the *test* task runs the tests, `containers: up/down/logs` tasks wrap `scripts/run.sh`.
- Debug configuration **Worker + Web** starts both projects reading `.env` (a breakpoint inside a command works).
  Outside containers the worker reaches the web app on `localhost` (`WEB_BASE_URL`, set in `launch.json`).

## Tests

```bash
dotnet test
```

Unit tests cover the contracts, quote providers, the progress notifier, the run store and Start/Stop
rules, the skail gateway (trigger/fire, retries) and the web API. They need neither network nor skail.
The durable behaviour of the function itself is verified end to end against a skail server.

## Notes

- `QUOTE_PROVIDER=fake` runs without internet (deterministic quotes). The default provider is AwesomeAPI,
  which may answer `429`: the command is retried by skail.
- Stopping is idempotent: pressing Stop twice, or after the run ended, has no effect.
- The run history lives in skail; the page state is in memory and is lost when `web` restarts.
