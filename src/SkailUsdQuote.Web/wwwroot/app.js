const SYMBOLS = { BRL: 'R$', EUR: '€', CNY: '¥', JPY: '¥' };
const STAGE_LABELS = { Starting: 'Starting…', Quoted: 'Waiting 30s', Stopped: 'Stopped', Completed: 'Completed', Failed: 'Failed' };
const INTERVAL_SECONDS = 30;
const KEY = 'usd-quote.runId';

const $ = (id) => document.getElementById(id);
const els = {
  currency: $('currency'), toggle: $('toggle'), state: $('state'), counter: $('counter'), countdown: $('countdown'),
  error: $('error'), pair: $('pair'), rate: $('rate'), variation: $('variation'), time: $('time'),
  history: $('history'), runId: $('runId'),
};

let run = null;
let source = null;
let countdownTimer = null;
let lastQuoteIteration = 0;
let secondsLeft = 0;

function fmt(currency, value) {
  const digits = currency === 'JPY' ? 2 : 4;
  return `${SYMBOLS[currency] ?? ''} ${Number(value).toLocaleString(undefined, { minimumFractionDigits: digits, maximumFractionDigits: digits })}`;
}

function showError(message) {
  els.error.hidden = !message;
  els.error.textContent = message ?? '';
}

function render() {
  const ui = run?.ui ?? { action: 'Start', label: 'Start', enabled: true, currencyEnabled: true };
  els.toggle.textContent = ui.label;
  els.toggle.disabled = !ui.enabled;
  els.toggle.classList.toggle('stop', ui.action === 'Stop');
  els.currency.disabled = !ui.currencyEnabled;
  if (run) els.currency.value = run.currency;

  const stage = run?.stage;
  els.state.textContent = stage ? (STAGE_LABELS[stage] ?? stage) : 'Idle';
  els.state.className = 'badge ' + (stage === 'Failed' ? 'failed' : ['Stopped', 'Completed'].includes(stage) ? 'done' : stage ? 'active' : 'idle');
  els.counter.textContent = run ? `${run.iteration}/${run.maxIterations}` : '';
  els.runId.textContent = run?.runId ?? '—';
  showError(run?.error);

  const quotes = run?.quotes ?? [];
  const last = quotes.at(-1);
  const prev = quotes.at(-2);
  els.pair.textContent = `USD → ${run?.currency ?? els.currency.value}`;
  els.rate.textContent = last ? fmt(last.currency, last.rate) : '—';
  els.time.textContent = last ? new Date(last.at).toLocaleString() : '';
  if (last && prev) {
    const diff = last.rate - prev.rate;
    const pct = prev.rate ? (diff / prev.rate) * 100 : 0;
    els.variation.textContent = `${diff >= 0 ? '▲' : '▼'} ${diff.toFixed(4)} (${pct.toFixed(3)}%)`;
    els.variation.className = 'variation ' + (diff >= 0 ? 'up' : 'down');
  } else {
    els.variation.textContent = '';
    els.variation.className = 'variation';
  }

  els.history.replaceChildren(...quotes.slice(-10).reverse().map((q, i) => {
    const row = document.createElement('tr');
    const n = quotes.length - i;
    for (const text of [n, fmt(q.currency, q.rate), new Date(q.at).toLocaleTimeString()]) {
      const cell = document.createElement('td');
      cell.textContent = text;
      row.append(cell);
    }
    return row;
  }));

  updateCountdown();
}

// the countdown restarts on each new quote; it is only a visual hint of the 30s durable delay
function updateCountdown() {
  const waiting = run && run.stage === 'Quoted' && !run.stopRequested;
  if (waiting && run.iteration !== lastQuoteIteration) {
    lastQuoteIteration = run.iteration;
    secondsLeft = INTERVAL_SECONDS;
  }
  if (!waiting) {
    els.countdown.textContent = '';
    clearInterval(countdownTimer);
    countdownTimer = null;
    return;
  }
  const tick = () => { els.countdown.textContent = secondsLeft > 0 ? `next quote in ${secondsLeft}s` : 'fetching…'; };
  tick();
  if (!countdownTimer) {
    countdownTimer = setInterval(() => { secondsLeft = Math.max(0, secondsLeft - 1); tick(); }, 1000);
  }
}

function connect(runId) {
  source?.close();
  source = new EventSource(`/api/runs/${runId}/stream`);
  source.onmessage = (event) => {
    run = JSON.parse(event.data);
    render();
    if (['Stopped', 'Completed', 'Failed'].includes(run.stage)) {
      source.close();
      localStorage.removeItem(KEY);
    }
  };
  // EventSource reconnects by itself; the stream sends the current snapshot first
}

async function start() {
  els.toggle.disabled = true;
  showError(null);
  try {
    const res = await fetch('/api/runs', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ currency: els.currency.value }),
    });
    if (!res.ok) throw new Error((await res.json().catch(() => ({}))).error ?? `HTTP ${res.status}`);
    run = await res.json();
    lastQuoteIteration = 0;
    localStorage.setItem(KEY, run.runId);
    render();
    connect(run.runId);
  } catch (e) {
    showError(`Could not start: ${e.message}`);
    render();
  }
}

async function stop() {
  els.toggle.disabled = true;
  try {
    const res = await fetch(`/api/runs/${run.runId}/stop`, { method: 'POST' });
    if (!res.ok) throw new Error((await res.json().catch(() => ({}))).error ?? `HTTP ${res.status}`);
    run = await (await fetch(`/api/runs/${run.runId}`)).json();
  } catch (e) {
    showError(`Could not stop: ${e.message}`);
    run = await (await fetch(`/api/runs/${run.runId}`)).json().catch(() => run);
  }
  render();
}

els.toggle.addEventListener('click', () => (run?.ui.action === 'Stop' ? stop() : start()));

// reload keeps the button and the locked currency right: the state always comes from the server
const saved = localStorage.getItem(KEY);
if (saved) {
  fetch(`/api/runs/${saved}`).then((r) => (r.ok ? r.json() : null)).then((r) => {
    if (!r) return localStorage.removeItem(KEY);
    run = r;
    render();
    if (!['Stopped', 'Completed', 'Failed'].includes(r.stage)) connect(r.runId);
  });
}
render();
