<script setup>
// Area di gestione riservata agli admin: una scheda a tab a tutta larghezza con le tabelle del database.
import { ref, reactive, computed, watch } from 'vue'
import { api, apiPost, apiPut, apiDelete, data } from '../api.js'
import { sessione } from '../sessione.js'
import StatoDati from '../componenti/StatoDati.vue'
import FinestraModale from '../componenti/FinestraModale.vue'
import TabellaDati from '../componenti/TabellaDati.vue'

const tab = ref('squadre')
const èAdmin = computed(() => sessione.autenticato && sessione.ruoli.includes('admin'))

const tabelle = {
  squadre: { percorso: 'admin/squadre', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  iscritti: { percorso: 'admin/utenti-iscritti', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  utenti: { percorso: 'admin/utenti', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  portafoglio: { percorso: 'admin/portafoglio-capitani', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  configurazioni: { percorso: 'admin/configurazioni', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  flusso: { percorso: 'admin/flusso-asta', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
  ruoli: { percorso: 'admin/ruoli', dati: ref([]), caricamento: ref(false), errore: ref(''), caricata: false },
}

// valori del database -> testo a video (si aggiungono qui i nuovi valori); un valore non in mappa si vede com'è
const etichetteFascia = { da_calcolare: 'Da calcolare' }
const etichetteStato = { nessuno: '-' }
const etichetteValidazione = { in_validazione: 'In validazione', validato: 'Validato' }
const etichetta = (mappa, valore) => mappa[valore] ?? valore

// colonne delle tre tabelle (il filtro a riga lavora su quello che si vede in cella)
const colonneSquadre = [
  { chiave: 'id', titolo: 'Id', classe: 'num' },
  { chiave: 'nome', titolo: 'Nome', classe: 'forte' },
  { chiave: 'capitanoNome', titolo: 'Capitano' },
  { chiave: 'nGiocatori', titolo: 'Giocatori', classe: 'num' },
]
const colonneIscritti = [
  { chiave: 'nickname', titolo: 'Nickname', classe: 'forte' },
  { chiave: 'eloCalcolato', titolo: 'ELO', classe: 'num' },
  { chiave: 'ruoli', titolo: 'Ruoli', formato: (i) => i.ruoliNomi?.join(', ') },
  { chiave: 'squadraNome', titolo: 'Squadra' },
  { chiave: 'fascia', titolo: 'Fascia', formato: (i) => etichetta(etichetteFascia, i.fascia) },
  { chiave: 'seeding', titolo: 'Seeding', classe: 'num' },
  { chiave: 'prezzoBase', titolo: 'Prezzo base', classe: 'num' },
  { chiave: 'prezzoAsta', titolo: 'Prezzo asta', classe: 'num' },
  { chiave: 'stato', titolo: 'Stato', formato: (i) => etichetta(etichetteStato, i.stato) },
  { chiave: 'statoValidazione', titolo: 'Validazione', formato: (i) => etichetta(etichetteValidazione, i.statoValidazione) },
  { chiave: 'dataIscrizione', titolo: 'Iscritto il', formato: (i) => (i.dataIscrizione ? data(i.dataIscrizione) : '') },
  { chiave: 'note', titolo: 'Note', classe: 'centro' }, // la cella mostra un'icona; il filtro cerca nel testo della nota
]
const colonnePortafoglio = [
  { chiave: 'nome', titolo: 'Nome utente', classe: 'forte' },
  { chiave: 'creditoIniziale', titolo: 'Credito iniziale', classe: 'num' },
  { chiave: 'creditoResiduo', titolo: 'Credito residuo', classe: 'num' },
]
const colonneFlusso = [
  { chiave: 'nome', titolo: 'Capitano', classe: 'forte' },
  { chiave: 'elo', titolo: 'ELO', formato: (c) => c.eloCalcolato ?? '—' }, // utenti_iscritti.elo_calcolato; '—' se il capitano non è iscritto
  { chiave: 'ordine', titolo: 'Ordine', classe: 'num', formato: (c) => String(ordineBozza[c.idDiscord] ?? c.ordine) },
]
const colonneUtenti = [
  { chiave: 'nome', titolo: 'Nome', classe: 'forte' },
  { chiave: 'ruoli', titolo: 'Ruoli', formato: (u) => u.ruoliNomi?.join(', ') },
  { chiave: 'verificato', titolo: 'Verificato', tipo: 'bool', classe: 'centro' },
]

// i filtri a riga di ogni tabella stanno qui, così restano dopo un salvataggio o un cambio di scheda
const filtriTabelle = { squadre: reactive({}), iscritti: reactive({}), utenti: reactive({}), portafoglio: reactive({}) }

async function carica(chiave) {
  const t = tabelle[chiave]
  // lo scheletro di caricamento solo la prima volta: nelle riletture dopo una modifica la tabella resta al suo posto
  if (!t.caricata) t.caricamento.value = true
  t.errore.value = ''
  try {
    t.dati.value = await api(t.percorso)
    t.caricata = true
  } catch (e) {
    t.errore.value = e.message
  } finally {
    t.caricamento.value = false
  }
}

// carica i dati della scheda la prima volta che la si apre (e quando la sessione risulta admin)
watch([tab, èAdmin], () => {
  if (èAdmin.value && !tabelle[tab.value].caricata) carica(tab.value)
  if (èAdmin.value && tab.value === 'portafoglio') carica('configurazioni')
}, { immediate: true })

// --- Squadre: creazione, modifica, eliminazione (tutte in finestre modali) ---
const modale = ref(null) // null | { tipo: 'form', squadra: oggetto | null } | { tipo: 'elimina', squadra }
const nome = ref('')
const idCapitano = ref('') // id_discord dell'iscritto scelto, '' = nessuno
const occupato = ref(false)
const erroreModale = ref('')

function apri(m) {
  erroreModale.value = ''
  modale.value = m
}
function chiudi() {
  if (occupato.value) return
  modale.value = null
}

function apriForm(squadra = null) {
  nome.value = squadra?.nome ?? ''
  idCapitano.value = squadra?.idCapitano ?? ''
  // i possibili capitani sono gli utenti col ruolo "capitano": si rilegge l'elenco per avere i ruoli aggiornati
  carica('utenti')
  apri({ tipo: 'form', squadra })
}

// esegue la chiamata; se va bene chiude la finestra e ricarica la tabella, altrimenti mostra l'errore nella finestra
async function esegui(chiamata, tabella = 'squadre') {
  occupato.value = true
  erroreModale.value = ''
  try {
    await chiamata()
    modale.value = null
    await carica(tabella)
  } catch (e) {
    erroreModale.value = e.message
  } finally {
    occupato.value = false
  }
}

function salva() {
  const n = nome.value.trim()
  if (!n) return
  const squadra = modale.value.squadra
  const corpo = { nome: n, idCapitano: idCapitano.value || null }
  if (squadra) esegui(() => apiPut(`admin/squadre/${squadra.id}`, corpo))
  else esegui(() => apiPost('admin/squadre', corpo))
}

function elimina() {
  const squadra = modale.value.squadra
  esegui(() => apiDelete(`admin/squadre/${squadra.id}`))
}

// --- Utenti: flag "verificato" e ruoli (una checkbox per ruolo) ---
const utenteForm = ref({ verificato: false, ruoli: [] })

function apriUtente(u) {
  utenteForm.value = { verificato: !!u.verificato, ruoli: [...(u.ruoli ?? [])] }
  // l'elenco dei ruoli possibili si carica la prima volta
  if (!tabelle.ruoli.caricata) carica('ruoli')
  apri({ tipo: 'utente', utente: u })
}

function salvaUtente() {
  const u = modale.value.utente
  esegui(
    () => apiPut(`admin/utenti/${encodeURIComponent(u.idDiscord)}`, { verificato: utenteForm.value.verificato, ruoli: utenteForm.value.ruoli }),
    'utenti',
  )
}

// --- Iscritti: validazione e note ---
const iscrittoForm = ref({ validato: false, note: '' })

function apriIscritto(i) {
  iscrittoForm.value = { validato: i.statoValidazione === 'validato', note: i.note ?? '' }
  apri({ tipo: 'iscritto', iscritto: i })
}

function salvaIscritto() {
  const i = modale.value.iscritto
  esegui(
    () => apiPut(`admin/utenti-iscritti/${encodeURIComponent(i.idDiscord)}`, {
      statoValidazione: iscrittoForm.value.validato ? 'validato' : 'in_validazione',
      note: iscrittoForm.value.note,
    }),
    'iscritti',
  )
}

// --- Portafoglio capitani: reset del credito al credito iniziale configurato ---
const creditoIniziale = computed(
  () => tabelle.configurazioni.dati.value.find((c) => c.chiave === 'credito_iniziale')?.valore ?? '…',
)

function apriReset(c) {
  apri({ tipo: 'resetCredito', capitano: c })
}

function resettaCredito() {
  const c = modale.value.capitano
  esegui(() => apiPost(`admin/portafoglio-capitani/${encodeURIComponent(c.idDiscord)}/reset`, {}), 'portafoglio')
}

// --- Flusso asta: ordine dei capitani (0 = non ordinato, da 1 a N le posizioni) ---
const ordineBozza = reactive({}) // id_discord -> valore nel campo (anche non ancora valido)
const erroreFlusso = ref('')
const salvandoFlusso = ref(false)
const numeroCapitani = computed(() => tabelle.flusso.dati.value.length)

// quando arrivano i dati dal server la bozza riparte dai valori salvati
watch(() => tabelle.flusso.dati.value, (righe) => {
  for (const k of Object.keys(ordineBozza)) delete ordineBozza[k]
  for (const c of righe) ordineBozza[c.idDiscord] = c.ordine
  erroreFlusso.value = ''
}, { immediate: true })

const flussoModificato = computed(() => tabelle.flusso.dati.value.some((c) => ordineBozza[c.idDiscord] !== c.ordine))

// controlla le posizioni prima di inviarle; restituisce il testo dell'errore oppure ''
function validaFlusso() {
  const n = numeroCapitani.value
  const usate = new Map()
  for (const c of tabelle.flusso.dati.value) {
    const v = ordineBozza[c.idDiscord]
    if (!Number.isInteger(v) || v < 0 || v > n) return `L'ordine di ${c.nome} deve essere un numero intero da 0 a ${n}.`
    if (v > 0) {
      if (usate.has(v)) return `La posizione ${v} è assegnata sia a ${usate.get(v)} sia a ${c.nome}: ogni posizione da 1 a ${n} può essere usata una volta sola (0 = non ordinato).`
      usate.set(v, c.nome)
    }
  }
  return ''
}

async function salvaFlusso() {
  erroreFlusso.value = validaFlusso()
  if (erroreFlusso.value) return
  salvandoFlusso.value = true
  try {
    await apiPut('admin/flusso-asta', {
      ordini: tabelle.flusso.dati.value.map((c) => ({ idUtente: c.idDiscord, ordine: ordineBozza[c.idDiscord] })),
    })
    await carica('flusso')
  } catch (e) {
    erroreFlusso.value = e.message
  } finally {
    salvandoFlusso.value = false
  }
}

// possibili capitani: gli utenti con il ruolo "capitano" (più quello già scelto, per non perderlo dall'elenco)
const candidatiCapitano = computed(() =>
  tabelle.utenti.dati.value.filter((u) => u.ruoli?.includes('capitano') || u.idDiscord === idCapitano.value),
)

const vFocus = { mounted: (el) => { el.focus(); el.select?.() } }

const valore = (v) => (v === null || v === undefined || v === '' ? '—' : v)
</script>

<template>
  <section class="admin">
    <div class="testa-pagina">
      <h1>Admin</h1>
      <p>Gestione dei dati del torneo.</p>
    </div>

    <p v-if="!sessione.caricata" class="muto" aria-busy="true">Caricamento…</p>
    <div v-else-if="!èAdmin" class="avviso" role="alert">
      <strong>Area riservata.</strong>
      <span>Per vedere questa pagina serve il profilo admin.</span>
    </div>

    <div v-else class="scheda">
      <div class="scheda__tab" role="tablist" aria-label="Dati">
        <button
          id="tab-squadre"
          type="button"
          role="tab"
          :aria-selected="tab === 'squadre'"
          aria-controls="pannello-squadre"
          @click="tab = 'squadre'"
        >
          Squadre
        </button>
        <button
          id="tab-iscritti"
          type="button"
          role="tab"
          :aria-selected="tab === 'iscritti'"
          aria-controls="pannello-iscritti"
          @click="tab = 'iscritti'"
        >
          Iscritti
        </button>
        <button
          id="tab-utenti"
          type="button"
          role="tab"
          :aria-selected="tab === 'utenti'"
          aria-controls="pannello-utenti"
          @click="tab = 'utenti'"
        >
          Utenti
        </button>
        <button
          id="tab-portafoglio"
          type="button"
          role="tab"
          :aria-selected="tab === 'portafoglio'"
          aria-controls="pannello-portafoglio"
          @click="tab = 'portafoglio'"
        >
          Portafoglio capitani
        </button>
        <button
          id="tab-flusso"
          type="button"
          role="tab"
          :aria-selected="tab === 'flusso'"
          aria-controls="pannello-flusso"
          @click="tab = 'flusso'"
        >
          Flusso asta
        </button>
      </div>

      <div v-if="tab === 'squadre'" id="pannello-squadre" class="scheda__corpo" role="tabpanel" aria-labelledby="tab-squadre">
        <div class="barra-azioni">
          <button type="button" class="btn" @click="apriForm()">Nuova squadra</button>
        </div>

        <StatoDati
          :caricamento="tabelle.squadre.caricamento.value"
          :errore="tabelle.squadre.errore.value"
          :vuoto="!tabelle.squadre.dati.value.length"
          testo-vuoto="Non c'è ancora nessuna squadra."
          @riprova="carica('squadre')"
        >
          <TabellaDati :filtri="filtriTabelle.squadre" :colonne="colonneSquadre" :righe="tabelle.squadre.dati.value" chiave-riga="id" titolo="Squadre" con-azioni>
            <template #azioni="{ riga }">
              <button type="button" class="btn btn--linea" @click="apriForm(riga)">Modifica</button>
              <button type="button" class="btn btn--linea" @click="apri({ tipo: 'elimina', squadra: riga })">Elimina</button>
            </template>
          </TabellaDati>
        </StatoDati>
      </div>

      <div v-else-if="tab === 'iscritti'" id="pannello-iscritti" class="scheda__corpo" role="tabpanel" aria-labelledby="tab-iscritti">
        <StatoDati
          :caricamento="tabelle.iscritti.caricamento.value"
          :errore="tabelle.iscritti.errore.value"
          :vuoto="!tabelle.iscritti.dati.value.length"
          testo-vuoto="Non c'è ancora nessun iscritto."
          @riprova="carica('iscritti')"
        >
          <TabellaDati :filtri="filtriTabelle.iscritti" :colonne="colonneIscritti" :righe="tabelle.iscritti.dati.value" chiave-riga="idDiscord" titolo="Iscritti al torneo" con-azioni>
            <template #cella-note="{ riga }">
              <span v-if="riga.note" class="nota" :title="riga.note" role="img" :aria-label="`Nota: ${riga.note}`">
                <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true"><path d="M5 3h10l4 4v14H5z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round" /><path d="M14 3v5h5M8 12h8M8 16h6" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" /></svg>
              </span>
              <span v-else class="muto">-</span>
            </template>
            <template #azioni="{ riga }">
              <button type="button" class="btn btn--linea" @click="apriIscritto(riga)">Modifica</button>
            </template>
          </TabellaDati>
        </StatoDati>
      </div>

      <div v-else-if="tab === 'utenti'" id="pannello-utenti" class="scheda__corpo" role="tabpanel" aria-labelledby="tab-utenti">
        <StatoDati
          :caricamento="tabelle.utenti.caricamento.value"
          :errore="tabelle.utenti.errore.value"
          :vuoto="!tabelle.utenti.dati.value.length"
          testo-vuoto="Non c'è ancora nessun utente."
          @riprova="carica('utenti')"
        >
          <TabellaDati
            :filtri="filtriTabelle.utenti"
            :colonne="colonneUtenti"
            :righe="tabelle.utenti.dati.value"
            chiave-riga="idDiscord"
            titolo="Utenti"
            etichetta-si="Verificati"
            etichetta-no="Non verificati"
            con-azioni
          >
            <template #cella-verificato="{ riga }">
              <span v-if="riga.verificato" class="stato stato--si" title="Verificato" role="img" aria-label="Verificato">
                <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true"><circle cx="12" cy="12" r="11" fill="currentColor" /><path d="M6.5 12.5l3.7 3.7 7.3-7.6" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" /></svg>
              </span>
              <span v-else class="stato stato--no" title="Non verificato" role="img" aria-label="Non verificato">
                <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true"><circle cx="12" cy="12" r="11" fill="currentColor" /><path d="M7 7l10 10M17 7L7 17" fill="none" stroke="#fff" stroke-width="2.4" stroke-linecap="round" /></svg>
              </span>
            </template>
            <template #azioni="{ riga }">
              <button type="button" class="btn btn--linea" @click="apriUtente(riga)">Modifica</button>
            </template>
          </TabellaDati>
        </StatoDati>
      </div>

      <div v-else-if="tab === 'portafoglio'" id="pannello-portafoglio" class="scheda__corpo" role="tabpanel" aria-labelledby="tab-portafoglio">
        <StatoDati
          :caricamento="tabelle.portafoglio.caricamento.value"
          :errore="tabelle.portafoglio.errore.value"
          :vuoto="!tabelle.portafoglio.dati.value.length"
          testo-vuoto="Non c'è nessun utente con il ruolo capitano."
          @riprova="carica('portafoglio')"
        >
          <TabellaDati
            :filtri="filtriTabelle.portafoglio"
            :colonne="colonnePortafoglio"
            :righe="tabelle.portafoglio.dati.value"
            chiave-riga="idDiscord"
            titolo="Portafoglio dei capitani"
            con-azioni
          >
            <template #azioni="{ riga }">
              <button type="button" class="btn btn--linea" @click="apriReset(riga)">Resetta credito</button>
            </template>
          </TabellaDati>
        </StatoDati>
      </div>

      <div v-else id="pannello-flusso" class="scheda__corpo" role="tabpanel" aria-labelledby="tab-flusso">
        <StatoDati
          :caricamento="tabelle.flusso.caricamento.value"
          :errore="tabelle.flusso.errore.value"
          :vuoto="!tabelle.flusso.dati.value.length"
          testo-vuoto="Non c'è nessun utente con il ruolo capitano."
          @riprova="carica('flusso')"
        >
          <p class="muto piccolo flusso__guida">
            Assegna a ogni capitano la sua posizione da 1 a {{ numeroCapitani }}: è l'ordine con cui chiameranno i giocatori. Ogni posizione si usa una volta sola; 0 vuol dire non ancora ordinato.
          </p>
          <TabellaDati
            :colonne="colonneFlusso"
            :filtrabile="false"
            :righe="tabelle.flusso.dati.value"
            chiave-riga="idDiscord"
            titolo="Ordine dei capitani nel flusso dell'asta"
          >
            <template #cella-ordine="{ riga }">
              <input
                v-model.number="ordineBozza[riga.idDiscord]"
                type="number"
                min="0"
                :max="numeroCapitani"
                step="1"
                class="campo campo--ordine"
                :aria-label="`Ordine di ${riga.nome}`"
                @keydown.enter.prevent="salvaFlusso"
              />
            </template>
          </TabellaDati>
          <p v-if="erroreFlusso" class="esito esito--errore flusso__errore" role="alert">{{ erroreFlusso }}</p>
          <div class="flusso__azioni">
            <button type="button" class="btn" :disabled="salvandoFlusso || !flussoModificato" @click="salvaFlusso">Salva ordine</button>
          </div>
        </StatoDati>
      </div>
    </div>

    <FinestraModale v-if="modale?.tipo === 'form'" :titolo="modale.squadra ? 'Modifica squadra' : 'Nuova squadra'" @chiudi="chiudi">
      <form class="modulo" @submit.prevent="salva">
        <label class="modulo__campo">
          <span>Nome</span>
          <input v-model="nome" v-focus class="campo" maxlength="100" required autocomplete="off" />
        </label>
        <label class="modulo__campo">
          <span>Capitano</span>
          <select v-model="idCapitano" class="campo">
            <option value="">Nessun capitano</option>
            <option v-for="u in candidatiCapitano" :key="u.idDiscord" :value="u.idDiscord">{{ u.nome || u.idDiscord }}</option>
          </select>
          <small class="muto">Si può scegliere tra tutti gli utenti con il ruolo "capitano".</small>
        </label>
        <p v-if="erroreModale" class="esito esito--errore" role="alert">{{ erroreModale }}</p>
        <div class="modulo__azioni">
          <button type="button" class="btn btn--linea" :disabled="occupato" @click="chiudi">Annulla</button>
          <button type="submit" class="btn" :disabled="occupato || !nome.trim()">{{ modale.squadra ? 'Salva' : 'Crea squadra' }}</button>
        </div>
      </form>
    </FinestraModale>

    <FinestraModale v-else-if="modale?.tipo === 'resetCredito'" titolo="Resetta credito" @chiudi="chiudi">
      <div class="modulo">
        <p>
          Vuoi riportare a <strong>{{ creditoIniziale }}</strong> sia il credito iniziale sia il credito residuo di
          <strong>{{ modale.capitano.nome }}</strong>? Il credito attuale andrà perso.
        </p>
        <p v-if="erroreModale" class="esito esito--errore" role="alert">{{ erroreModale }}</p>
        <div class="modulo__azioni">
          <button v-focus type="button" class="btn btn--linea" :disabled="occupato" @click="chiudi">Annulla</button>
          <button type="button" class="btn" :disabled="occupato" @click="resettaCredito">Resetta</button>
        </div>
      </div>
    </FinestraModale>

    <FinestraModale v-else-if="modale?.tipo === 'iscritto'" :titolo="`Modifica iscritto: ${modale.iscritto.nickname || modale.iscritto.idDiscord}`" @chiudi="chiudi">
      <form class="modulo" @submit.prevent="salvaIscritto">
        <label class="interruttore">
          <input v-model="iscrittoForm.validato" v-focus type="checkbox" role="switch" class="interruttore__campo" />
          <span class="interruttore__traccia" aria-hidden="true"></span>
          <span class="interruttore__testo">
            <strong>Validazione</strong>
            <span class="piccolo muto">{{ iscrittoForm.validato ? 'Validato' : 'In validazione' }}</span>
          </span>
        </label>
        <label class="modulo__campo">
          <span>Note</span>
          <textarea v-model="iscrittoForm.note" class="campo campo--area" rows="4" maxlength="1000" placeholder="Nessuna nota"></textarea>
        </label>
        <p v-if="erroreModale" class="esito esito--errore" role="alert">{{ erroreModale }}</p>
        <div class="modulo__azioni">
          <button type="button" class="btn btn--linea" :disabled="occupato" @click="chiudi">Annulla</button>
          <button type="submit" class="btn" :disabled="occupato">Salva</button>
        </div>
      </form>
    </FinestraModale>

    <FinestraModale v-else-if="modale?.tipo === 'utente'" :titolo="`Modifica utente: ${modale.utente.nome}`" @chiudi="chiudi">
      <form class="modulo" @submit.prevent="salvaUtente">
        <label class="modulo__scelta">
          <input v-model="utenteForm.verificato" v-focus type="checkbox" />
          <span>Verificato</span>
        </label>
        <fieldset class="modulo__gruppo">
          <legend>Ruoli</legend>
          <p v-if="tabelle.ruoli.caricamento.value" class="muto" aria-busy="true">Caricamento…</p>
          <p v-else-if="tabelle.ruoli.errore.value" class="esito esito--errore" role="alert">{{ tabelle.ruoli.errore.value }}</p>
          <label v-for="r in tabelle.ruoli.dati.value" :key="r.idRuolo" class="modulo__scelta">
            <input v-model="utenteForm.ruoli" type="checkbox" :value="r.chiaveRuolo" />
            <span>{{ r.nome }}</span>
          </label>
        </fieldset>
        <p v-if="erroreModale" class="esito esito--errore" role="alert">{{ erroreModale }}</p>
        <div class="modulo__azioni">
          <button type="button" class="btn btn--linea" :disabled="occupato" @click="chiudi">Annulla</button>
          <button type="submit" class="btn" :disabled="occupato || !tabelle.ruoli.caricata">Salva</button>
        </div>
      </form>
    </FinestraModale>

    <FinestraModale v-else-if="modale?.tipo === 'elimina'" titolo="Elimina squadra" @chiudi="chiudi">
      <div class="modulo">
        <p>Vuoi eliminare la squadra <strong>{{ modale.squadra.nome }}</strong>? L'operazione non si può annullare.</p>
        <p v-if="modale.squadra.nAcquistati">
          Verranno cancellati anche i <strong>{{ modale.squadra.nAcquistati }}</strong>
          {{ modale.squadra.nAcquistati === 1 ? 'giocatore acquistato' : 'giocatori acquistati' }} dalla squadra.
        </p>
        <p v-if="erroreModale" class="esito esito--errore" role="alert">{{ erroreModale }}</p>
        <div class="modulo__azioni">
          <button v-focus type="button" class="btn btn--linea" :disabled="occupato" @click="chiudi">{{ erroreModale ? 'Chiudi' : 'Annulla' }}</button>
          <button v-if="!erroreModale" type="button" class="btn" :disabled="occupato" @click="elimina">Elimina</button>
        </div>
      </div>
    </FinestraModale>
  </section>
</template>

<style scoped>
.admin { padding: var(--space-xl) var(--gutter) var(--space-2xl); }
.scheda {
  width: 100%;
  border: var(--rule-hair) solid var(--color-rule-2);
  border-radius: var(--radius-card);
  background: var(--color-paper-2);
  overflow: hidden;
}
.scheda__tab {
  display: flex; gap: var(--space-2xs, 4px); padding: var(--space-xs) var(--space-xs) 0;
  border-bottom: var(--rule-hair) solid var(--color-rule-2);
}
.scheda__tab button {
  padding: var(--space-xs) var(--space-md); border: 0; background: transparent; color: var(--color-ink-2);
  font: inherit; cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -1px;
}
.scheda__tab button[aria-selected='true'] { color: var(--color-ink); border-bottom-color: currentColor; font-weight: 600; }
.scheda__corpo { padding: var(--space-md); }
.barra-azioni { display: flex; justify-content: flex-end; margin-bottom: var(--space-md); }
.campo {
  min-height: 40px; padding: 0 var(--space-sm); font: inherit; color: var(--color-ink);
  background: var(--color-paper); border: var(--rule-hair) solid var(--color-rule-2); border-radius: var(--radius-card);
}
.modulo { display: grid; gap: var(--space-md); }
.modulo p { margin: 0; color: var(--color-ink-2); }
.modulo__campo { display: grid; gap: var(--space-2xs, 4px); }
.modulo__campo > span { font-weight: 600; color: var(--color-ink); }
.modulo__azioni { display: flex; justify-content: flex-end; gap: var(--space-xs); }
.modulo__gruppo { display: grid; gap: var(--space-xs); margin: 0; padding: 0; border: 0; }
.modulo__gruppo legend { padding: 0; margin-bottom: var(--space-xs); font-weight: 600; color: var(--color-ink); }
.modulo__scelta { display: flex; align-items: center; gap: var(--space-xs); cursor: pointer; }
.modulo__scelta input { width: 1.1rem; height: 1.1rem; accent-color: var(--color-accent, currentColor); }
.nota { display: inline-flex; vertical-align: middle; color: var(--color-ink-2); }
.campo--area { padding: var(--space-xs) var(--space-sm); min-height: 6rem; resize: vertical; line-height: 1.4; }
.interruttore { display: flex; align-items: center; gap: var(--space-md); cursor: pointer; }
.interruttore__campo { position: absolute; opacity: 0; width: 1px; height: 1px; }
.interruttore__traccia {
  position: relative; flex: 0 0 auto; width: 44px; height: 24px; border-radius: var(--radius-pill);
  background: var(--color-slot); box-shadow: inset 0 0 0 var(--rule-hair) var(--color-rule-2);
  transition: background-color var(--dur-short) var(--ease-out);
}
.interruttore__traccia::after {
  content: ''; position: absolute; top: 3px; left: 3px; width: 18px; height: 18px; border-radius: 50%;
  background: var(--color-ink-2); transition: transform var(--dur-short) var(--ease-out), background-color var(--dur-short) var(--ease-out);
}
.interruttore__campo:checked + .interruttore__traccia { background: var(--color-accent); }
.interruttore__campo:checked + .interruttore__traccia::after { transform: translateX(20px); background: var(--color-accent-ink); }
.interruttore__campo:focus-visible + .interruttore__traccia { outline: 2px solid var(--color-focus); outline-offset: 2px; }
.interruttore__testo { display: grid; gap: var(--space-3xs); }
.flusso__guida { margin: 0 0 var(--space-md); }
.flusso__errore { margin: var(--space-md) 0 0; text-align: center; }
.flusso__azioni { display: flex; justify-content: center; margin-top: var(--space-md); }
.campo--ordine { width: 5rem; text-align: center; }
.stato { display: inline-flex; vertical-align: middle; }
.stato--si { color: oklch(62% 0.17 150); }
.stato--no { color: oklch(58% 0.21 25); }
.esito { margin: 0 0 var(--space-md); color: var(--color-ink-2); }
.esito--errore { color: var(--color-danger, #e5484d); font-weight: 600; }
</style>
