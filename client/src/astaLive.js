// Collegamento in tempo reale con l'asta (SignalR, /hubs/asta). Il server avvisa quando c'è una nuova chiamata o offerta:
// non c'è nessun polling. Una sola connessione condivisa da tutti i componenti, aperta al primo ascoltatore e chiusa all'ultimo.
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const ascoltatori = new Set()
let connessione = null
let tentativo = null

function avvia() {
  if (connessione) return
  connessione = new HubConnectionBuilder()
    .withUrl('/hubs/asta')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // riconnessione dopo una caduta
    .configureLogging(LogLevel.Warning)
    .build()
  connessione.on('astaAggiornata', (dati) => ascoltatori.forEach((a) => a.suEvento?.(dati)))
  // dopo una riconnessione si possono aver perso eventi: i componenti rileggono i dati una volta
  connessione.onreconnected(() => ascoltatori.forEach((a) => a.suRiconnesso?.()))
  connetti()
}

// se il primo collegamento non riesce si riprova dopo qualche secondo (solo finché il server non risponde)
async function connetti() {
  try {
    await connessione?.start()
  } catch {
    if (connessione && ascoltatori.size) tentativo = setTimeout(connetti, 5000)
  }
}

// Si mette in ascolto: { suEvento(dati), suRiconnesso() }. Restituisce la funzione che smette di ascoltare.
export function ascoltaAsta(ascoltatore) {
  ascoltatori.add(ascoltatore)
  avvia()
  return () => {
    ascoltatori.delete(ascoltatore)
    if (!ascoltatori.size && connessione) {
      clearTimeout(tentativo)
      connessione.stop()
      connessione = null
    }
  }
}
