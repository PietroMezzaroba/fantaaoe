<script setup>
// «La tua pagina», parte di chi gioca (ogni utente connesso): il profilo della classifica, l'iscrizione al FantaAge,
// la squadra in cui sei finito all'asta, le sere in cui puoi giocare e i ticket per gli admin.
// Dati: /api/profilo, /api/profilo/iscrizione, /api/asta/squadre (la riga con «tu»), /api/disponibilita/mie, /api/ticket.
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { api, apiPost, apiPut, elo, percento, data } from '../../api.js'
import { sessione } from '../../sessione.js'
import { ascoltaAsta } from '../../astaLive.js'
import { moneta, caricaValuta } from '../../valuta.js'
import StatoDati from '../StatoDati.vue'

const profilo = ref(null)
const iscrizione = ref(null) // { aperte, giaIscritto }
const squadre = ref([])
const disponibilita = ref({})
const ticket = ref([])
const caricamento = ref(true)
const errore = ref('')
const avviso = ref('')

function avvisa(testo) {
  avviso.value = testo
  setTimeout(() => { if (avviso.value === testo) avviso.value = '' }, 4000)
}

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    const [p, i, s, d, t] = await Promise.all([
      api('profilo'), api('profilo/iscrizione'), api('asta/squadre'), api('disponibilita/mie'), api('ticket'),
    ])
    profilo.value = p
    iscrizione.value = i
    squadre.value = s
    disponibilita.value = Object.fromEntries(d.map((r) => [String(r.data).slice(0, 10), { stato: r.stato, fasciaOraria: r.fasciaOraria, note: r.note }]))
    ticket.value = t
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

let smetti = () => {}
onMounted(() => {
  caricaValuta()
  carica()
  // se ti comprano mentre guardi la pagina, la squadra compare da sola
  smetti = ascoltaAsta({ suEvento: (e) => { if (['acquisto', 'eliminato'].includes(e.tipo)) api('asta/squadre').then((s) => { squadre.value = s }).catch(() => {}) } })
})
onBeforeUnmount(() => smetti())

// ---------- profilo della classifica ----------
const giocatore = computed(() => profilo.value?.giocatore ?? null)
const daConfermare = computed(() => giocatore.value && profilo.value?.legame && !profilo.value.legame.verificato)
const operando = ref(false)
async function conferma() {
  operando.value = true
  try {
    await apiPost('profilo/conferma', {})
    profilo.value = await api('profilo')
    avvisa('Profilo confermato.')
  } catch (e) { errore.value = e.message } finally { operando.value = false }
}
async function collega(playerId) {
  operando.value = true
  try {
    await apiPost('profilo/collega', { playerId })
    profilo.value = await api('profilo')
    cerca.value = ''
    avvisa(playerId ? 'Profilo collegato.' : 'Collegamento tolto: scegli il tuo nick.')
  } catch (e) { errore.value = e.message } finally { operando.value = false }
}
// ricerca del proprio nick (solo se il profilo non è collegato)
const cerca = ref('')
const tutti = ref([])
async function caricaTutti() {
  if (!tutti.value.length) tutti.value = await api('giocatori?tutti=true').catch(() => [])
}
const trovati = computed(() => {
  const q = cerca.value.trim().toLowerCase()
  if (q.length < 2) return []
  return tutti.value.filter((g) => g.nick.toLowerCase().includes(q) || (g.clan || '').toLowerCase().includes(q)).slice(0, 8)
})

// ---------- iscrizione e squadra ----------
const iscritto = computed(() => profilo.value?.iscrizione ?? null)
const miaSquadra = computed(() => {
  for (const s of squadre.value) {
    const io = s.giocatori.find((g) => g.tu)
    if (io) return { ...s, io }
  }
  return null
})
const statoIscrizione = computed(() => ({
  in_validazione: 'in attesa che un admin la approvi',
  validato: 'approvata',
  approvato: 'approvata',
  respinto: 'respinta: scrivi agli admin con un ticket',
}[iscritto.value?.stato] ?? iscritto.value?.stato ?? ''))
async function iscriviti() {
  operando.value = true
  try {
    await apiPost('profilo/iscriviti', {})
    ;[profilo.value, iscrizione.value] = await Promise.all([api('profilo'), api('profilo/iscrizione')])
    avvisa('Iscrizione inviata: ora la approva un admin.')
  } catch (e) { errore.value = e.message } finally { operando.value = false }
}

// ---------- disponibilità: i prossimi 14 giorni ----------
const giorni = computed(() => {
  const fmt = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short' })
  const oggi = new Date()
  oggi.setHours(12, 0, 0, 0)
  return Array.from({ length: 14 }, (_, i) => {
    const d = new Date(oggi)
    d.setDate(oggi.getDate() + i)
    const iso = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
    return { iso, etichetta: fmt.format(d), weekend: d.getDay() === 0 || d.getDay() === 6 }
  })
})
const salvando = ref(null)
async function segna(iso, stato) {
  salvando.value = iso
  const prima = disponibilita.value[iso]
  // subito a video, poi al server; se il server dice no si torna com'era
  disponibilita.value = { ...disponibilita.value, [iso]: { ...(prima ?? {}), stato } }
  try {
    await apiPut('disponibilita', { data: iso, stato, fasciaOraria: stato === 'indisponibile' ? null : (prima?.fasciaOraria || '21:00'), note: prima?.note || null })
  } catch (e) {
    disponibilita.value = { ...disponibilita.value, [iso]: prima }
    errore.value = e.message
  } finally {
    salvando.value = null
  }
}
const segnate = computed(() => giorni.value.filter((g) => disponibilita.value[g.iso]?.stato).length)

// ---------- ticket ----------
const nuovo = ref({ tipo: 'segnalazione', titolo: '', descrizione: '' })
const inviando = ref(false)
async function inviaTicket() {
  inviando.value = true
  try {
    await apiPost('ticket', nuovo.value)
    nuovo.value = { tipo: 'segnalazione', titolo: '', descrizione: '' }
    ticket.value = await api('ticket')
    avvisa('Ticket inviato agli admin.')
  } catch (e) { errore.value = e.message } finally { inviando.value = false }
}
const statoTicket = (s) => ({ aperto: 'Aperto', in_corso: 'In lavorazione', risolto: 'Risolto', respinto: 'Respinto' }[s] || s)
</script>

<template>
  <section class="io-sezione" aria-labelledby="io-giocatore">
    <div class="io-sezione__testa">
      <h2 id="io-giocatore">Tu nel FantaAge</h2>
    </div>
    <p v-if="avviso" class="avviso-riga avviso-riga--ok" role="status">{{ avviso }}</p>

    <StatoDati :caricamento="caricamento" :errore="errore" :vuoto="false" :righe="4" @riprova="carica">
      <!-- iscrizione e squadra -->
      <div class="io-riquadro" :class="{ 'io-riquadro--accento': miaSquadra }">
        <template v-if="miaSquadra">
          <p class="io-grande">
            Sei nella squadra <strong>{{ miaSquadra.nome }}</strong><template v-if="miaSquadra.capitano"> di {{ miaSquadra.capitano }}</template>.
          </p>
          <p class="muto">
            <template v-if="miaSquadra.io.capitano">Sei il capitano.</template>
            <template v-else>Ti hanno preso all'asta per {{ miaSquadra.io.costo }} {{ moneta.nome }}.</template>
          </p>
        </template>
        <template v-else-if="iscritto">
          <p class="io-grande">Sei iscritto alla sesta edizione.</p>
          <p class="muto">
            Iscrizione {{ statoIscrizione }}.
            <template v-if="iscritto.fascia && iscritto.fascia !== 'da_calcolare'">Fascia {{ iscritto.fascia }}, seed {{ iscritto.seeding }}.</template>
            All'asta qui comparirà la tua squadra.
          </p>
        </template>
        <template v-else-if="iscrizione?.aperte">
          <p class="io-grande">Le iscrizioni alla sesta edizione sono aperte.</p>
          <button type="button" class="btn" :disabled="operando" @click="iscriviti">Iscriviti</button>
        </template>
        <template v-else>
          <p class="io-grande">Non sei iscritto alla sesta edizione.</p>
          <p class="muto">Le iscrizioni ora sono chiuse.</p>
        </template>
      </div>

      <!-- profilo della classifica -->
      <div class="io-riquadro">
        <div v-if="daConfermare" class="io-conferma">
          <p>Ti abbiamo riconosciuto come <strong>{{ giocatore.nick }}</strong> della classifica dei TG Dito. Sei tu?</p>
          <div class="azioni-inline">
            <button type="button" class="btn" :disabled="operando" @click="conferma">Sì, sono io</button>
            <button type="button" class="btn btn--linea" :disabled="operando" @click="collega(null)">No, scelgo io</button>
          </div>
        </div>
        <template v-if="giocatore">
          <div class="riga-spazio">
            <h3 class="io-titoletto">{{ giocatore.nick }}<span v-if="giocatore.clan" class="clan">{{ giocatore.clan }}</span></h3>
            <RouterLink :to="`/giocatore/${giocatore.playerId}`" class="btn btn--linea btn--sm">La tua scheda</RouterLink>
          </div>
          <dl class="misure io-misure">
            <div class="misura"><dt>ELO interno</dt><dd class="segnale">{{ elo(giocatore.eloInterno) }}</dd><small>{{ giocatore.nDiti }} Diti, {{ percento(giocatore.winrate) }} vinti</small></div>
            <div class="misura"><dt>Ranked 1v1</dt><dd>{{ elo(giocatore.elo1v1) }}</dd><small>massimo {{ elo(giocatore.elo1v1MassimoStorico) }}</small></div>
            <div class="misura"><dt>Ranked TG</dt><dd>{{ elo(giocatore.eloTg) }}</dd><small>massimo {{ elo(giocatore.eloTgMassimoStorico) }}</small></div>
            <div v-if="profilo.civ?.length" class="misura"><dt>Civiltà preferita</dt><dd class="io-civ">{{ profilo.civ[0].civ }}</dd><small>{{ profilo.civ[0].partite }} partite</small></div>
          </dl>
        </template>
        <template v-else>
          <h3 class="io-titoletto">Collega il tuo nick della classifica</h3>
          <p class="muto piccolo">Così vedi qui i tuoi ELO e gli admin ti mettono nella fascia giusta.</p>
          <input v-model="cerca" type="search" class="cerca" placeholder="Scrivi il tuo nick" aria-label="Cerca il tuo nick" @focus="caricaTutti" />
          <ul v-if="trovati.length" class="lista-scelta">
            <li v-for="g in trovati" :key="g.playerId">
              <span><strong>{{ g.nick }}</strong><span v-if="g.clan" class="clan">{{ g.clan }}</span> <span class="muto piccolo">ELO {{ elo(g.eloInterno) }}</span></span>
              <button type="button" class="btn btn--linea btn--sm" :disabled="operando" @click="collega(g.playerId)">Sono io</button>
            </li>
          </ul>
        </template>
      </div>

      <!-- disponibilità -->
      <div class="io-riquadro">
        <div class="riga-spazio">
          <h3 class="io-titoletto">Quando puoi giocare</h3>
          <span class="muto piccolo">{{ segnate }} sere segnate su 14</span>
        </div>
        <p class="muto piccolo">Il tuo capitano le vede subito nella sua pagina.</p>
        <ul class="io-sere">
          <li v-for="g in giorni" :key="g.iso" :class="{ 'io-sere--we': g.weekend }">
            <span class="io-sere__giorno">{{ g.etichetta }}</span>
            <div class="scelta" role="group" :aria-label="`Disponibilità di ${g.etichetta}`">
              <button type="button" :aria-pressed="disponibilita[g.iso]?.stato === 'disponibile'" :disabled="salvando === g.iso" @click="segna(g.iso, 'disponibile')">Sì</button>
              <button type="button" :aria-pressed="disponibilita[g.iso]?.stato === 'forse'" :disabled="salvando === g.iso" @click="segna(g.iso, 'forse')">Forse</button>
              <button type="button" :aria-pressed="disponibilita[g.iso]?.stato === 'indisponibile'" :disabled="salvando === g.iso" @click="segna(g.iso, 'indisponibile')">No</button>
            </div>
          </li>
        </ul>
      </div>

      <!-- ticket -->
      <div class="io-riquadro">
        <div class="riga-spazio">
          <h3 class="io-titoletto">Ticket agli admin</h3>
          <span class="muto piccolo">{{ ticket.length ? `${ticket.length} tuoi` : 'nessuno per ora' }}</span>
        </div>
        <ul v-if="ticket.length" class="ticket-lista">
          <li v-for="t in ticket" :key="t.id" class="ticket-item">
            <div class="riga-spazio">
              <strong>{{ t.titolo }}</strong>
              <span class="badge" :class="`badge--${t.stato}`">{{ statoTicket(t.stato) }}</span>
            </div>
            <p class="piccolo muto">{{ data(t.creatoIl) }}</p>
            <p class="piccolo">{{ t.descrizione }}</p>
            <div v-if="t.rispostaAdmin" class="risposta-admin">
              <strong>Risposta di {{ t.gestitoDa || 'uno degli admin' }}</strong>
              <p class="piccolo">{{ t.rispostaAdmin }}</p>
            </div>
          </li>
        </ul>
        <details class="io-nuovo-ticket">
          <summary>Apri un ticket</summary>
          <form class="modulo" @submit.prevent="inviaTicket">
            <label class="campo"><span>Tipo</span>
              <select v-model="nuovo.tipo" class="cerca">
                <option value="restart">Richiesta di restart</option>
                <option value="segnalazione">Segnalazione</option>
                <option value="infrazione">Infrazione</option>
                <option value="altro">Altro</option>
              </select>
            </label>
            <label class="campo"><span>Titolo</span><input v-model="nuovo.titolo" type="text" class="cerca" required minlength="3" /></label>
            <label class="campo"><span>Cosa è successo</span><textarea v-model="nuovo.descrizione" class="cerca area" rows="3" required minlength="5"></textarea></label>
            <button type="submit" class="btn" :disabled="inviando">{{ inviando ? 'Invio…' : 'Invia agli admin' }}</button>
          </form>
        </details>
      </div>
    </StatoDati>
  </section>
</template>
