// Collegamento in tempo reale con l'asta (SignalR, /hubs/asta). Il server avvisa quando c'è una nuova chiamata o offerta:
// non c'è nessun polling. Una sola connessione condivisa da tutti i componenti, aperta al primo ascoltatore e chiusa all'ultimo.
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const ascoltatori = new Set()
let pagineConElenco = 0 // quante pagine con l'elenco dei capitani sono aperte (di solito 0 o 1)
let presenze = null // ultimo elenco dei connessi ricevuto (id Discord); null finché non arriva
let connessione = null
let tentativo = null

function avvia() {
  if (connessione) return
  connessione = new HubConnectionBuilder()
    .withUrl('/hubs/asta')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // riconnessione dopo una caduta
    .withKeepAliveInterval(5000) // in linea con il server: ping ogni 5 s, collegamento perso dopo 15 s di silenzio
    .withServerTimeout(15000)
    .configureLogging(LogLevel.Warning)
    .build()
  connessione.on('astaAggiornata', (dati) => ascoltatori.forEach((a) => a.suEvento?.(dati)))
  // chi è connesso alla pagina dell'asta: il server manda l'elenco appena ci si connette e a ogni entrata/uscita
  connessione.on('astaPresenze', (ids) => {
    presenze = ids
    ascoltatori.forEach((a) => a.suPresenze?.(ids))
  })
  // dopo una riconnessione si possono aver perso eventi: i componenti rileggono i dati una volta
  connessione.onreconnected(() => {
    inviaPresenza()
    ascoltatori.forEach((a) => a.suRiconnesso?.())
  })
  connetti()
}

// se il primo collegamento non riesce si riprova dopo qualche secondo (solo finché il server non risponde)
async function connetti() {
  try {
    await connessione?.start()
    inviaPresenza()
  } catch {
    if (connessione && ascoltatori.size) tentativo = setTimeout(connetti, 5000)
  }
}

// Chiudendo o lasciando la pagina (anche per il logout) il collegamento si chiude subito e in modo pulito: il server toglie
// la presenza senza aspettare un timeout, e il client non tenta di riconnettersi mentre la pagina se ne va.
window.addEventListener('pagehide', () => {
  clearTimeout(tentativo)
  connessione?.stop()
})
// pagina riaperta dalla cache del browser (tasto indietro): il collegamento era stato chiuso, si ricarica
window.addEventListener('pageshow', (e) => {
  if (e.persisted) location.reload()
})

// Dichiara al server se qui c'è una pagina con l'elenco dei capitani aperta (è il "connesso" verde dei capitani)
function inviaPresenza() {
  if (connessione?.state === 'Connected') connessione.invoke('Presenza', pagineConElenco > 0).catch(() => {})
}

// Da chiamare quando si apre la pagina con l'elenco dei capitani; restituisce la funzione da chiamare quando si esce.
export function presenzaPagina() {
  pagineConElenco++
  inviaPresenza()
  return () => {
    pagineConElenco--
    inviaPresenza()
  }
}

// Si mette in ascolto: { suEvento(dati), suRiconnesso(), suPresenze(ids) }. Restituisce la funzione che smette di ascoltare.
export function ascoltaAsta(ascoltatore) {
  ascoltatori.add(ascoltatore)
  avvia()
  if (presenze) ascoltatore.suPresenze?.(presenze) // chi arriva dopo parte subito con l'elenco già noto
  return () => {
    ascoltatori.delete(ascoltatore)
    if (!ascoltatori.size && connessione) {
      clearTimeout(tentativo)
      connessione.stop()
      connessione = null
      presenze = null
    }
  }
}
