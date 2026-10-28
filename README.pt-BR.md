# skail USD quote (.NET 8)

[English](README.md)

Demo 1 do skail: uma **skailFunc** consulta a cotação do dólar, espera **30 segundos com um delay durável**
(`SkailTask.DelaySeconds(30)`), consulta de novo e continua até você clicar em **Parar** (um evento externo)
ou chegar a 100 cotações. Uma página web mostra o andamento em tempo real.

```
                 trigger / fire parar                      busca trabalho
Navegador ─SSE─▶ web ───────────────────▶ servidor skail ◀───────────────── worker
                 ▲    SKAIL_BASE_URL       (environment)    SKAIL_SIDECAR      │
                 └────────── POST /internal/runs/{id}/events ─────────────────┘
```

- **worker** (`src/SkailUsdQuote.Worker`): a skailFunc `GetUsdQuote(runId, currency)`. A function só orquestra;
  o I/O fica nos commands (`FetchQuote`, `Notify`). Ela cria um único
  `SkailTask.WaitForEvent("STOP_QUOTE", runId)` e o coloca em `WhenAny` contra `SkailTask.DelaySeconds(30)`
  a cada iteração.
- **web** (`src/SkailUsdQuote.Web`): página estática + API. Inicia a execução (trigger), para (fire de
  `STOP_QUOTE`), guarda o estado em memória e o transmite para a página (SSE).
- **contracts** (`src/SkailUsdQuote.Contracts`): DTOs e constantes compartilhados.

A página tem uma lista de moedas (Real, Euro, Yuan, Iene; Real é o padrão), bloqueada enquanto há execução
ativa. Um único botão alterna entre **Iniciar** e **Parar**.

## Como conecta ao skail

Os dois serviços falam com o seu environment do skail de formas diferentes, por isso há dois endereços:

| Serviço | Variável | Como | Para quê |
|---|---|---|---|
| **worker** (a skailFunc) | `SKAIL_SIDECAR` | A conexão do runtime do skail (SDK). **Não** usa a API HTTP pública. | Buscar trabalho de `GetUsdQuote` e seus commands e registrar os resultados. |
| **web** | `SKAIL_BASE_URL` | A API HTTP do servidor skail (mesmo endereço de `SKAIL_SIDECAR`, que o worker usa). | **Iniciar**: `POST {SKAIL_BASE_URL}/api/v1/trigger/{nome}/{versão}/GetUsdQuote/{runId}` com corpo `["{runId}","{currency}"]`. **Parar**: `POST {SKAIL_BASE_URL}/api/v1/fire/STOP_QUOTE/{runId}` com corpo `{}`. |

Ambos usam o mesmo `SKAIL_NAMESPACE`, `SKAIL_KEY` e `SKAIL_WORKLOAD` (`nome:versão`, separado em `{nome}` e
`{versão}` da rota). O web envia `Accept` e `Content-Type: application/json`, o header `Skail-namespace` e a
chave em `skail-key` e em `Authorization: Bearer`. `429`, `5xx` e erros de rede são repetidos com o mesmo
run id; `401`/`403` não são (confira `SKAIL_KEY` e `SKAIL_NAMESPACE`). Sem `SKAIL_BASE_URL`, o Iniciar falha
com `502 SKAIL_BASE_URL is not set`.

O navegador nunca chama o skail: ele só fala com o web, então a chave fica no servidor.

> As rotas acima são as que o gateway do environment testado aceitou. A documentação pública mostra `/trigger/...`
> em vez de `/api/v1/trigger/...` e só o header `skail-key`. Se o Iniciar devolver `skail answered 404`
> ou `400`, compare a rota e os headers com o que o portal do seu environment mostra.

## Requisitos

- SDK .NET 8 e VS Code (C# Dev Kit recomendado)
- podman ou docker
- Um environment do skail no portal: o namespace, a chave e o endereço do servidor skail (o sidecar ao
  qual o worker se conecta).
  No portal, registre também o workload `nome:versão`.
- Um navegador atual (Chrome/Edge 111+, Firefox 113+, Safari 16.2+). A página usa Server-Sent Events,
  `color-mix()` do CSS e JavaScript moderno; o Internet Explorer não é suportado.

## Configurar

```bash
cp .env.example .env     # o .env guarda segredos e é ignorado pelo git
```

Preencha `SKAIL_WORKLOAD` (exatamente como registrado no portal), `SKAIL_NAMESPACE` e `SKAIL_KEY`
(do seu environment no portal), `SKAIL_SIDECAR` (o endereço do servidor skail desse environment) e
`SKAIL_BASE_URL` (mesmo valor de `SKAIL_SIDECAR`; o web o usa para a API HTTP). Os demais valores são ajustes padrão do runtime.

## Rodar com podman / docker

```bash
scripts/run.sh up        # builda as duas imagens e sobe web + worker (CONTAINER_ENGINE=docker para forçar docker)
scripts/run.sh logs
scripts/run.sh down
```

Abra <http://localhost:8080>, escolha a moeda e clique em **Iniciar**.

Demonstração de durabilidade: clique em **Iniciar**, reinicie o worker durante a espera de 30 s
(`scripts/run.sh restart`) e veja a execução continuar. Clique em **Parar** a qualquer momento, até no meio
da espera: a function acorda na hora e termina sem nova consulta.

## VS Code

Abra a pasta; as extensões recomendadas são sugeridas automaticamente.

- `Ctrl+Shift+B` compila, a task *test* roda os testes e as tasks `containers: up/down/logs` chamam o `scripts/run.sh`.
- A configuração de debug **Worker + Web** sobe os dois projetos lendo o `.env` (breakpoint dentro de um command funciona).
  Fora de container, o worker acessa o web em `localhost` (`WEB_BASE_URL`, definido no `launch.json`).

## Testes

```bash
dotnet test
```

Os testes unitários cobrem contratos, providers de cotação, o notificador de progresso, o estado das execuções e
as regras Iniciar/Parar, o gateway do skail (trigger/fire, retries) e a API web. Não precisam de rede nem do skail.
O comportamento durável da própria function é verificado de ponta a ponta contra um servidor skail.

## Observações

- `QUOTE_PROVIDER=fake` roda sem internet (cotações determinísticas). O provider padrão é a AwesomeAPI,
  que pode responder `429`: o command é reexecutado pelo skail.
- Parar é idempotente: clicar duas vezes, ou depois de a execução terminar, não tem efeito.
- O histórico da execução fica no skail; o estado da página fica em memória e se perde ao reiniciar o `web`.
