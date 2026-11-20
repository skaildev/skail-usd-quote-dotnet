# skail USD quote (.NET 8)

[English](README.md)

Um app de console com uma function do skail que consulta a cotação do dólar, espera 30 segundos com um delay durável e consulta de novo, até você apertar **Enter**.

Ele mostra três recursos do skail:

- **delay durável**: feche o app no meio de uma espera, abra de novo e a execução continua de onde estava;
- **evento externo** (`STOP_TRACKING`), que para a execução no meio de uma espera;
- **novas tentativas automáticas**: quando a API de cotação falha, o skail tenta de novo sozinho.

## Requisitos

- Um destes: [Docker](https://docs.docker.com/get-docker/), [Podman](https://podman.io/docs/installation) ou o [SDK do .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) (ou mais novo). O script de execução usa o primeiro que encontrar.
- Um environment do skail com um workload registrado para o `SKAIL_WORKLOAD` do `.env` (`usdquote:v1.0.0` por padrão). O [Quickstart](https://docs.skail.dev/comece-aqui/quickstart) mostra como criar a conta, o environment e o workload.

O projeto usa o `Skail.Platform.Runtime` **2.3.0-rc.4**.

## Rodar

```bash
cp .env.example .env  # preencha SKAIL_SIDECAR, SKAIL_NAMESPACE e SKAIL_KEY (portal > Environments & keys > Connect)
scripts/run.sh        # 1 USD em BRL
scripts/run.sh EUR    # ou EUR, CNY, JPY
```

No Windows, no PowerShell: `copy .env.example .env` e depois `powershell -ExecutionPolicy Bypass -File scripts\run.ps1`, com os mesmos argumentos. O script funciona no Windows PowerShell que já vem no Windows; não precisa do PowerShell 7.

O script roda a demo com o que a máquina tiver: Docker, depois Podman, depois o SDK do .NET. Se não achar nenhum, ele diz o que instalar. Sem o script, os comandos são `dotnet run` (com os argumentos depois de `--`, como em `dotnet run -- <argumento>`).

```text
Run 130142cd-353b-4637-a53c-fc82435e3f6d started: 1 USD in BRL every 30 s, until you stop it.
Press Enter to stop the run, or Ctrl+C to exit and leave it running in skail.
[130142cd] #1  1 USD = 5.3578 BRL  (fetched 15:09:27, source 15:09:00)
[130142cd] #2  1 USD = 5.3578 BRL  (fetched 15:09:58, source 15:09:00)

STOP_TRACKING sent: the run ends during the current wait. Press Ctrl+C to exit.
[130142cd] Stopped after 2 quotes.
```

- **Enter** para a execução: o app dispara o `STOP_TRACKING`.
- **Ctrl+C** fecha o app; a execução continua no skail, esperando.
- **fetched** é quando a function consultou a cotação; **source** é quando a AwesomeAPI publicou a taxa, que só muda a cada poucos minutos.

## Como funciona

O app faz duas coisas no mesmo processo: hospeda o worker que executa a function (`builder.UseSkail()`) e inicia e para a execução pela API HTTP do skail:

```http
POST {SKAIL_SIDECAR}/api/v1/trigger/usdquote/v1.0.0/TrackUsdQuote/{runId}
["{runId}", "BRL"]

POST {SKAIL_SIDECAR}/api/v1/fire/STOP_TRACKING/{runId}
{}
```

A function, em [`QuoteFunctions.cs`](QuoteFunctions.cs):

```csharp
[SkailFunction(retryCount: 0)]
public async SkailTask<int> TrackUsdQuote(string runId, string currency)
{
    var stopEvent = SkailTask.WaitForEvent(Events.StopTracking, runId);

    for (var quoteNumber = 1; ; quoteNumber++)
    {
        var quote = await FetchQuote(currency);
        await ReportQuote(runId, quoteNumber, quote);

        var interval = SkailTask.DelaySeconds(IntervalSeconds);
        if (await SkailTask.WhenAny(stopEvent, interval) == stopEvent)
        {
            await ReportStopped(runId, quoteNumber);
            return quoteNumber;
        }
    }
}
```

- A function só orquestra. Cada vez que a execução acorda (depois de um delay, de um evento ou de um reinício do app), o skail roda a function de novo desde o início (replay).
- O I/O fica nos commands: o `FetchQuote` chama a API de cotação, e o `ReportQuote` e o `ReportStopped` imprimem no console. Um command concluído não roda de novo, então o replay não consulta de novo nem imprime a mesma linha duas vezes.
- A espera por `STOP_TRACKING` é criada uma vez e reaproveitada em todos os `WhenAny` contra o delay de 30 segundos: o que vier primeiro vence.

| Arquivo | O que tem |
|---|---|
| `QuoteFunctions.cs` | A function e os commands dela. |
| `Quotes/AwesomeApiQuoteProvider.cs` | A chamada à AwesomeAPI. |
| `Runs/QuoteRun.cs` | Inicia a execução e dispara o `STOP_TRACKING` no Enter. |
| `Skail/SkailApiClient.cs` | Trigger e fire pela API HTTP do skail. |
| `Program.cs` | Lê o `.env` e o argumento e liga tudo. |
| `scripts/run.sh`, `scripts/run.ps1`, `Dockerfile` | Rodam a demo com Docker, Podman ou o .NET, o que houver na máquina. |

## Veja a durabilidade

1. Rode o app e espere a primeira cotação.
2. Durante a espera de 30 segundos, aperte **Ctrl+C**. O app fecha; a execução continua no skail.
3. Abra o app de novo com o id da execução que ele imprimiu: `scripts/run.sh <id da execução>`. Com um id, o app não inicia outra execução: só hospeda o worker de novo, e o skail entrega a ele a execução que estava esperando.
4. A próxima cotação chega no horário. Aperte **Enter** para parar.

No portal, abra o [Monitor](https://docs.skail.dev/referencia/portal/monitor) do seu environment e clique na execução: cada cotação aparece como `FetchQuote` e `ReportQuote`, cada espera como um `WhenAny` com o seu `Delay`, e a parada como `ReportStopped`.

## Quando a API de cotação responde 429

A AwesomeAPI responde `429` quando a cota acaba. O `FetchQuote` lança a exceção, e o skail tenta de novo com espera crescente (15 s, 30 s, 1 min, 2 min, 4 min), até 5 vezes (`[SkailCommand(retryCount: 5)]`). O console fica parado nesse meio-tempo; no Monitor, o `FetchQuote` mostra cada tentativa. Quando a cota volta, a próxima tentativa dá certo e a execução segue. Se ela não voltar a tempo, a execução termina como Failed no Monitor (`retryCount: 0` na function).

## Observações

- A execução não tem limite: ela segue até você parar. Pare as execuções que você iniciar, para elas não ficarem rodando no seu environment.
- Apps rodando o mesmo `SKAIL_WORKLOAD` no mesmo environment dividem as mesmas execuções: use um workload (ou environment) por pessoa.

## Aviso

**Esta é uma demonstração, não é código de produção.** Ela existe só para mostrar como o [skail](https://docs.skail.dev) funciona e é fornecida "como está", sem garantia de nenhum tipo. Usa uma API de cotação gratuita de terceiros ([AwesomeAPI](https://docs.awesomeapi.com.br/api-de-moedas)) que pode limitar ou recusar requisições a qualquer momento. Não use as cotações para tomar nenhuma decisão.

## Licença

[Apache-2.0](LICENSE)
