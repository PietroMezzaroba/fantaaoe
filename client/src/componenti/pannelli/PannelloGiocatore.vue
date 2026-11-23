<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, apiPost, apiPut, elo, percento, data } from '../../api.js'
import { sessione } from '../../sessione.js'

const scheda = ref('profilo')
const caricamento = ref(true)
const errore = ref(null)
const messaggio = ref(null)

// Profilo
const profilo = ref(null)
const giocatori = ref([])
const ricerca = ref('')
const salvandoLegame = ref(false)
const iscrivendosi = ref(false)

async function iscrivitiTorneo() {
  iscrivendosi.value = true
  errore.value = null
  try {
    await apiPost('profilo/iscriviti', {})
    profilo.value = await api('profilo')
    avvisa('Richiesta di iscrizione inviata! In attesa di conferma admin.')
  } catch (e) {
    errore.value = e.message
  } finally {
    iscrivendosi.value = false
  }
}

// Disponibilità (prossimi 14 giorni)
const disponibilita = ref({})
const salvandoData = ref(null)

// Ticket
const ticket = ref([])
const nuovoTicket = ref({ tipo: 'segnalazione', titolo: '', descrizione: '' })
const inviandoTicket = ref(false)

const prossimiGiorni = computed(() => {
  const out = []
  const oggi = new Date()
  oggi.setHours(12, 0, 0, 0)
  const fmtGiorno = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric', month: 'short' })
  for (let i = 0; i < 14; i++) {
    const d = new Date(oggi)
    d.setDate(oggi.getDate() + i)
    const iso = d.toISOString().slice(0, 10)
    out.push({ iso, etichetta: fmtGiorno.format(d), weekend: d.getDay() === 0 || d.getDay() === 6 })
  }
  return out
})

const giocatoriFiltrati = computed(() => {
  const q = ricerca.value.trim().toLowerCase()
  if (!q) return giocatori.value.slice(0, 15)
  return giocatori.value.filter((g) => g.nick.toLowerCase().includes(q) || (g.clan || '').toLowerCase().includes(q)).slice(0, 15)
})

function avvisa(testo) {
  messaggio.value = testo
  setTimeout(() => { if (messaggio.value === testo) messaggio.value = null }, 3500)
}

async function caricaTutto() {
  caricamento.value = true
  errore.value = null
  try {
    const [p, d, t, g] = await Promise.all([
      api('profilo'),
      api('disponibilita/mie'),
      api('ticket'),
      api('giocatori?tutti=true'),
    ])
    profilo.value = p
    giocatori.value = g
    const mappa = {}
    for (const r of d) {
      const iso = String(r.data).slice(0, 10)
      mappa[iso] = { stato: r.stato, fasciaOraria: r.fasciaOraria || '21:00', note: r.note || '' }
    }
    disponibilita.value = mappa
    ticket.value = t
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}

async function collegaGiocatore(playerId) {
  salvandoLegame.value = true
  errore.value = null
  try {
    await apiPost('profilo/collega', { playerId })
    profilo.value = await api('profilo')
    ricerca.value = ''
    avvisa(playerId ? 'Profilo collegato alla classifica.' : 'Collegamento rimosso.')
  } catch (e) {
    errore.value = e.message
  } finally {
    salvandoLegame.value = false
  }
}

async function impostaDisp(iso, stato) {
  salvandoData.value = iso
  errore.value = null
  const corrente = disponibilita.value[iso] || { fasciaOraria: '21:00', note: '' }
  try {
    await apiPut('disponibilita', {
      data: iso,
      stato,
      fasciaOraria: stato === 'indisponibile' ? null : (corrente.fasciaOraria || '21:00'),
      note: corrente.note || null,
    })
    disponibilita.value = {
      ...disponibilita.value,
      [iso]: { stato, fasciaOraria: corrente.fasciaOraria || '21:00', note: corrente.note || '' },
    }
  } catch (e) {
    errore.value = e.message
  } finally {
    salvandoData.value = null
  }
}

async function inviaTicket() {
  inviandoTicket.value = true
  errore.value = null
  try {
    await apiPost('ticket', nuovoTicket.value)
    nuovoTicket.value = { tipo: 'segnalazione', titolo: '', descrizione: '' }
    ticket.value = await api('ticket')
    avvisa('Ticket inviato agli admin.')
  } catch (e) {
    errore.value = e.message
  } finally {
    inviandoTicket.value = false
  }
}

const etichettaStatoTicket = (s) => ({
  aperto: 'Aperto',
  in_corso: 'In lavorazione',
  risolto: 'Risolto',
  respinto: 'Respinto',
}[s] || s)

onMounted(caricaTutto)
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div>
        <h1>Giocatore iscritto</h1>
        <p class="muto">Ciao {{ sessione.nome }} — il tuo profilo, le disponibilità e i ticket del FantaAge.</p>
      </div>
      <div class="scelta" role="tablist" aria-label="Sezioni giocatore">
        <button type="button" :aria-pressed="scheda === 'profilo'" @click="scheda = 'profilo'">Il tuo profilo</button>
        <button type="button" :aria-pressed="scheda === 'disponibilita'" @click="scheda = 'disponibilita'">Disponibilità</button>
        <button type="button" :aria-pressed="scheda === 'ticket'" @click="scheda = 'ticket'">
          Ticket <span v-if="ticket.length" class="u">({{ ticket.length }})</span>
        </button>
      </div>
    </div>

    <p v-if="messaggio" class="avviso-riga avviso-riga--ok" role="status">{{ messaggio }}</p>
    <p v-if="errore" class="avviso-riga avviso-riga--err" role="alert">{{ errore }}</p>

    <div v-if="caricamento" class="scheletro" aria-label="Caricamento">
      <span style="--w: 45%"></span>
      <span style="--w: 75%"></span>
      <span style="--w: 60%"></span>
    </div>

    <!-- ==================== PROFILO ==================== -->
    <section v-else-if="scheda === 'profilo'" class="blocco-pannello">
      <div v-if="profilo?.giocatore" class="riquadro angoli">
        <div class="riga-spazio">
          <div>
            <h2>{{ profilo.giocatore.nick }} <span v-if="profilo.giocatore.clan" class="clan">{{ profilo.giocatore.clan }}</span></h2>
            <p class="piccolo muto">
              Account Discord: <strong>{{ sessione.nome }}</strong>
              <span v-if="profilo.legame?.verificato" class="vinto"> · Verificato</span>
              <span v-else class="muto"> · In attesa di conferma admin</span>
            </p>
          </div>
          <div class="azioni-inline">
            <RouterLink :to="`/giocatore/${profilo.giocatore.playerId}`" class="btn btn--linea">Scheda completa</RouterLink>
            <button type="button" class="btn btn--linea" :disabled="salvandoLegame" @click="collegaGiocatore(null)">Cambia</button>
          </div>
        </div>

        <dl class="misure">
          <div class="misura">
            <dt>ELO interno</dt>
            <dd class="segnale">{{ elo(profilo.giocatore.eloInterno) }}</dd>
            <small>{{ profilo.giocatore.nDiti }} Diti ({{ percento(profilo.giocatore.winrate) }})</small>
          </div>
          <div class="misura">
            <dt>Ranked 1v1</dt>
            <dd>{{ elo(profilo.giocatore.elo1v1) }}</dd>
            <small>Max {{ elo(profilo.giocatore.elo1v1MassimoStorico) }}</small>
          </div>
          <div class="misura">
            <dt>Ranked TG</dt>
            <dd>{{ elo(profilo.giocatore.eloTg) }}</dd>
            <small>Max {{ elo(profilo.giocatore.eloTgMassimoStorico) }}</small>
          </div>
          <div class="misura">
            <dt>FantaAge 2026</dt>
            <dd v-if="profilo.iscrizione">
              <template v-if="profilo.iscrizione.stato === 'in_attesa'">In attesa</template>
              <template v-else-if="profilo.iscrizione.stato === 'respinto'">Respinta</template>
              <template v-else>Fascia {{ profilo.iscrizione.fascia }}</template>
            </dd>
            <dd v-else class="muto">—</dd>
            <small v-if="profilo.iscrizione">
              <template v-if="profilo.iscrizione.stato === 'in_attesa'">Richiesta inviata agli admin</template>
              <template v-else-if="profilo.iscrizione.squadraNome">
                {{ profilo.iscrizione.squadraNome }} · {{ profilo.iscrizione.prezzoAsta }} Berlu
              </template>
              <template v-else>
                Seed {{ profilo.iscrizione.seeding ?? 'n.d.' }} · base {{ profilo.iscrizione.prezzoBase }} Berlu
              </template>
            </small>
            <small v-else>
              <button type="button" class="btn btn--sm" :disabled="iscrivendosi" @click="iscrivitiTorneo">
                {{ iscrivendosi ? 'Invio...' : 'Iscriviti al FantaAge' }}
              </button>
            </small>
          </div>
        </dl>

        <div v-if="profilo.civ?.length" class="sezione">
          <h3>Civiltà più giocate</h3>
          <div class="chip-lista">
            <span v-for="c in profilo.civ" :key="c.civ" class="chip">
              <strong>{{ c.civ }}</strong>
              <span class="muto">{{ c.partite }}G · {{ Math.round((c.vittorie / c.partite) * 100) }}%</span>
            </span>
          </div>
        </div>
      </div>

      <div v-else class="riquadro angoli">
        <h2>Collega il tuo profilo della classifica</h2>
        <p class="muto piccolo">
          Cerca il tuo nick della classifica dei TG Dito per collegarlo al tuo account Discord (<strong>{{ sessione.nome }}</strong>).
        </p>
        <div class="strumenti">
          <input v-model="ricerca" type="search" class="cerca" placeholder="Cerca il tuo nick (es. UUU, Pier, Tiniae...)" />
        </div>
        <ul class="lista-scelta">
          <li v-for="g in giocatoriFiltrati" :key="g.playerId">
            <div>
              <strong>{{ g.nick }}</strong>
              <span v-if="g.clan" class="clan">{{ g.clan }}</span>
              <span class="muto piccolo"> · ELO {{ elo(g.eloInterno) }} · 1v1 {{ elo(g.elo1v1) }}</span>
            </div>
            <button type="button" class="btn btn--linea" :disabled="salvandoLegame" @click="collegaGiocatore(g.playerId)">
              Sono io
            </button>
          </li>
        </ul>
      </div>
    </section>

    <!-- ==================== DISPONIBILITÀ ==================== -->
    <section v-else-if="scheda === 'disponibilita'" class="blocco-pannello">
      <div class="riquadro angoli">
        <h2>Calendario delle disponibilità (prossimi 14 giorni)</h2>
        <p class="muto piccolo">Segna le sere in cui puoi giocare: il tuo capitano le vede subito nella sua schermata squadra.</p>

        <div class="disp-lista">
          <div v-for="g in prossimiGiorni" :key="g.iso" class="disp-riga" :class="{ 'disp-riga--we': g.weekend }">
            <div class="disp-giorno">
              <strong>{{ g.etichetta }}</strong>
              <span class="muto piccolo">{{ g.iso }}</span>
            </div>
            <div class="scelta">
              <button
                type="button"
                :aria-pressed="disponibilita[g.iso]?.stato === 'disponibile'"
                :disabled="salvandoData === g.iso"
                @click="impostaDisp(g.iso, 'disponibile')"
              >Disponibile</button>
              <button
                type="button"
                :aria-pressed="disponibilita[g.iso]?.stato === 'forse'"
                :disabled="salvandoData === g.iso"
                @click="impostaDisp(g.iso, 'forse')"
              >Forse</button>
              <button
                type="button"
                :aria-pressed="disponibilita[g.iso]?.stato === 'indisponibile'"
                :disabled="salvandoData === g.iso"
                @click="impostaDisp(g.iso, 'indisponibile')"
              >No</button>
            </div>
            <span class="disp-stato piccolo" :class="{
              vinto: disponibilita[g.iso]?.stato === 'disponibile',
              perso: disponibilita[g.iso]?.stato === 'indisponibile',
              muto: !disponibilita[g.iso]?.stato || disponibilita[g.iso]?.stato === 'forse',
            }">
              {{ disponibilita[g.iso]?.stato ? disponibilita[g.iso].stato : 'non segnato' }}
            </span>
          </div>
        </div>
      </div>
    </section>

    <!-- ==================== TICKET ==================== -->
    <section v-else-if="scheda === 'ticket'" class="blocco-pannello due">
      <form class="riquadro angoli modulo" @submit.prevent="inviaTicket">
        <h2>Apri un ticket</h2>
        <p class="muto piccolo">Richieste di restart, segnalazioni o infrazioni: gli admin rispondono qui.</p>

        <label class="campo">
          <span>Tipo</span>
          <select v-model="nuovoTicket.tipo" class="cerca">
            <option value="restart">Richiesta di restart</option>
            <option value="segnalazione">Segnalazione</option>
            <option value="infrazione">Infrazione</option>
            <option value="altro">Altro</option>
          </select>
        </label>

        <label class="campo">
          <span>Titolo</span>
          <input v-model="nuovoTicket.titolo" type="text" class="cerca" required minlength="3" placeholder="Es. Restart game 2 vs Team Pier" />
        </label>

        <label class="campo">
          <span>Dettagli</span>
          <textarea v-model="nuovoTicket.descrizione" class="cerca area" rows="4" required minlength="5" placeholder="Spiega cosa è successo (mappa, minuto, giocatori)..."></textarea>
        </label>

        <button type="submit" class="btn" :disabled="inviandoTicket">
          {{ inviandoTicket ? 'Invio...' : 'Invia ticket' }}
        </button>
      </form>

      <div class="riquadro angoli">
        <h2>I tuoi ticket</h2>
        <p v-if="!ticket.length" class="muto piccolo">Non hai ancora aperto nessun ticket.</p>
        <ul v-else class="ticket-lista">
          <li v-for="t in ticket" :key="t.id" class="ticket-item">
            <div class="riga-spazio">
              <strong>#{{ t.id }} · {{ t.titolo }}</strong>
              <span class="badge" :class="`badge--${t.stato}`">{{ etichettaStatoTicket(t.stato) }}</span>
            </div>
            <p class="piccolo muto">{{ t.tipo }} · {{ data(t.creatoIl) }}</p>
            <p class="piccolo">{{ t.descrizione }}</p>
            <div v-if="t.rispostaAdmin" class="risposta-admin">
              <strong>Risposta admin ({{ t.gestitoDa || 'Staff' }})</strong>
              <p class="piccolo">{{ t.rispostaAdmin }}</p>
            </div>
          </li>
        </ul>
      </div>
    </section>
  </div>
</template>
