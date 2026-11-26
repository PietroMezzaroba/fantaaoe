// Collegamento in tempo reale con il sito e l'asta (SignalR, /hubs/asta).
// Una sola connessione WebSocket condivisa per tutta la scheda:
//  - astaAggiornata / astaPresenze: eventi in diretta dell'asta e presenze dei capitani;
//  - Pagina(percorso): comunica al server la pagina attuale per la dashboard admin "Persone online";
//  - sitoOnline: istantanea in diretta ricevuta solo dagli admin iscritti con SeguiOnline(true).
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const ascoltatori = new Set()
const ascoltatoriOnline = new Set()
let sitoAttivo = 0 // quante radici dell'app tengono viva la presenza sul sito (0 o 1)
let pagineConElenco = 0 // quante pagine con l'elenco dei capitani sono aperte (di solito 0 o 1)
let paginaCorrente = '/'
let presenze = null // ultimo elenco dei capitani sull'asta ricevuto (id Discord); null finché non arriva
let ultimoOnline = null // ultima istantanea "Persone online" (solo per admin)
let statoLive = 'spento' // 'spento' | 'connessione' | 'connesso' | 'riconnessione'
let connessione = null
let tentativo = null

function impostaStato(nuovo) {
  if (statoLive === nuovo) return
  statoLive = nuovo
  ascoltatoriOnline.forEach((a) => a.suStato?.(nuovo))
}

function serveConnessione() {
  return ascoltatori.size > 0 || ascoltatoriOnline.size > 0 || sitoAttivo > 0
}

function chiudiSeInutile() {
  if (serveConnessione() || !connessione) return
  clearTimeout(tentativo)
  const vecchia = connessione
  connessione = null
  presenze = null
  ultimoOnline = null
  impostaStato('spento')
  vecchia.stop().catch(() => {})
}

function sincronizzaStatoSulServer() {
  inviaPagina()
  inviaPresenza()
  inviaSeguiOnline()
}

function avvia() {
  if (connessione) return
  const c = new HubConnectionBuilder()
    .withUrl('/hubs/asta')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // riconnessione dopo una caduta
    .withKeepAliveInterval(5000) // in linea con il server: ping ogni 5 s, collegamento perso dopo 15 s di silenzio
    .withServerTimeout(15000)
    .configureLogging(LogLevel.Warning)
    .build()
  connessione = c

  c.on('astaAggiornata', (dati) => ascoltatori.forEach((a) => a.suEvento?.(dati)))
  // chi è connesso alla pagina dell'asta: il server manda l'elenco appena ci si connette e a ogni entrata/uscita
  c.on('astaPresenze', (ids) => {
    presenze = ids
    ascoltatori.forEach((a) => a.suPresenze?.(ids))
  })
  // istantanea di chi è online sul sito (inviata dal server solo al gruppo admin-online)
  c.on('sitoOnline', (istantanea) => {
    ultimoOnline = istantanea
    ascoltatoriOnline.forEach((a) => a.suOnline?.(istantanea))
  })

  c.onreconnecting(() => {
    if (connessione === c) impostaStato('riconnessione')
  })
  // dopo una riconnessione si possono aver perso eventi: si riallinea lo stato sul server e i componenti rileggono i dati
  c.onreconnected(() => {
    if (connessione !== c) return
    impostaStato('connesso')
    sincronizzaStatoSulServer()
    ascoltatori.forEach((a) => a.suRiconnesso?.())
  })
  c.onclose(() => {
    if (connessione !== c) return
    connessione = null
    impostaStato('spento')
    if (serveConnessione()) tentativo = setTimeout(avvia, 5000)
  })
  connetti(c)
}

// se il primo collegamento non riesce si riprova dopo qualche secondo (solo finché serve)
async function connetti(c) {
  if (connessione !== c) return
  impostaStato('connessione')
  try {
    await c.start()
    if (connessione !== c) return
    impostaStato('connesso')
    sincronizzaStatoSulServer()
  } catch {
    if (connessione === c && serveConnessione()) {
      impostaStato('riconnessione')
      tentativo = setTimeout(() => connetti(c), 5000)
    }
  }
}

// Chiudendo o lasciando la pagina (anche per il logout) il collegamento si chiude subito e in modo pulito: il server toglie
// la presenza senza aspettare un timeout, e il client non tenta di riconnettersi mentre la pagina se ne va.
window.addEventListener('pagehide', () => {
  clearTimeout(tentativo)
  const c = connessione
  connessione = null
  c?.stop()
})
// pagina riaperta dalla cache del browser (tasto indietro): il collegamento era stato chiuso, si ricarica
window.addEventListener('pageshow', (e) => {
  if (e.persisted) location.reload()
})

// Dichiara al server se qui c'è una pagina con l'elenco dei capitani aperta (è il "connesso" verde dei capitani)
function inviaPresenza() {
  if (connessione?.state === 'Connected') connessione.invoke('Presenza', pagineConElenco > 0).catch(() => {})
}

// Comunica al server la pagina attuale del sito
function inviaPagina() {
  if (connessione?.state === 'Connected') connessione.invoke('Pagina', paginaCorrente).catch(() => {})
}

// Iscrive o disiscrive questa connessione dagli aggiornamenti admin "Persone online"
function inviaSeguiOnline() {
  if (connessione?.state === 'Connected') connessione.invoke('SeguiOnline', ascoltatoriOnline.size > 0).catch(() => {})
}

// Da chiamare nella radice dell'app (App.vue): tiene attiva la presenza sul sito su tutte le pagine.
export function presenzaSito(percorsoIniziale = '/') {
  paginaCorrente = percorsoIniziale || '/'
  sitoAttivo++
  avvia()
  inviaPagina()
  return () => {
    sitoAttivo = Math.max(0, sitoAttivo - 1)
    chiudiSeInutile()
  }
}

// Aggiorna la pagina attuale al cambio di rotta (chiamato da App.vue)
export function aggiornaPaginaSito(percorso) {
  const norm = percorso || '/'
  if (paginaCorrente === norm) return
  paginaCorrente = norm
  inviaPagina()
}

// Riconnette la sessione SignalR quando cambia l'utente autenticato senza ricaricare la pagina (es. login di sviluppo)
export function riconnettiLive() {
  if (!serveConnessione()) return
  clearTimeout(tentativo)
  const vecchia = connessione
  connessione = null
  ultimoOnline = null
  if (vecchia) vecchia.stop().catch(() => {})
  avvia()
}

// Da chiamare quando si apre la pagina con l'elenco dei capitani; restituisce la funzione da chiamare quando si esce.
export function presenzaPagina() {
  pagineConElenco++
  inviaPresenza()
  return () => {
    pagineConElenco = Math.max(0, pagineConElenco - 1)
    inviaPresenza()
  }
}

// Si mette in ascolto dell'asta: { suEvento(dati), suRiconnesso(), suPresenze(ids) }. Restituisce la funzione che smette di ascoltare.
export function ascoltaAsta(ascoltatore) {
  ascoltatori.add(ascoltatore)
  avvia()
  if (presenze) ascoltatore.suPresenze?.(presenze) // chi arriva dopo parte subito con l'elenco già noto
  return () => {
    ascoltatori.delete(ascoltatore)
    chiudiSeInutile()
  }
}

// Solo per admin: riceve in diretta l'istantanea di chi è online sul sito ({ suOnline(dati), suStato(stato) }).
export function ascoltaOnline(ascoltatore) {
  const norm = typeof ascoltatore === 'function' ? { suOnline: ascoltatore } : ascoltatore
  const primo = ascoltatoriOnline.size === 0
  ascoltatoriOnline.add(norm)
  avvia()
  norm.suStato?.(statoLive)
  if (ultimoOnline) norm.suOnline?.(ultimoOnline)
  if (primo) inviaSeguiOnline()
  return () => {
    ascoltatoriOnline.delete(norm)
    if (ascoltatoriOnline.size === 0) {
      ultimoOnline = null
      inviaSeguiOnline()
    }
    chiudiSeInutile()
  }
}
