<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, apiPost, elo } from '../../api.js'
import { sessione } from '../../sessione.js'
import { usaAstaLive } from '../../astaLive.js'

const scheda = ref('asta')
const { dati: asta, connesso } = usaAstaLive()

const squadre = ref([])
const iscritti = ref([])
const squadraSelezionata = ref(null)
const datiSquadra = ref(null)
const offertaCustom = ref('')
const inviando = ref(false)
const errore = ref(null)
const messaggio = ref(null)

const eAdmin = computed(() => sessione.ruoli.includes('admin'))

const miaSquadra = computed(() => {
  if (!asta.value?.squadre?.length) return null
  if (squadraSelezionata.value) {
    return asta.value.squadre.find((s) => s.id === squadraSelezionata.value) || null
  }
  return asta.value.squadre.find((s) => s.capitanoDiscordId === sessione.discordId) || null
})

const squadraMigliore = computed(() =>
  asta.value?.squadre?.find((s) => s.id === asta.value.squadraMiglioreId) || null
)

const squadraTurno = computed(() =>
  asta.value?.squadre?.find((s) => s.id === asta.value.turnoSquadraId) || null
)

const offertaMinima = computed(() => {
  if (!asta.value?.giocatore) return 1
  return asta.value.offertaAttuale === 0
    ? Math.max(1, asta.value.giocatore.prezzoBase || 1)
    : asta.value.offertaAttuale + 1
})

const offertaMassima = computed(() => {
  if (!miaSquadra.value || !asta.value) return 0
  const maxRosa = asta.value.maxRosa || 10
  const postiDopo = Math.max(0, maxRosa - miaSquadra.value.nGiocatori - 1)
  return Math.max(0, miaSquadra.value.berluResidui - postiDopo)
})

const possoOffrire = computed(() =>
  asta.value?.stato === 'in_corso' &&
  asta.value?.giocatore &&
  miaSquadra.value &&
  miaSquadra.value.id !== asta.value.squadraMiglioreId &&
  miaSquadra.value.nGiocatori < (asta.value.maxRosa || 10) &&
  offertaMassima.value >= offertaMinima.value
)

const prossimi7Giorni = computed(() => {
  const out = []
  const oggi = new Date()
  oggi.setHours(12, 0, 0, 0)
  const fmt = new Intl.DateTimeFormat('it-IT', { weekday: 'short', day: 'numeric' })
  for (let i = 0; i < 7; i++) {
    const d = new Date(oggi)
    d.setDate(oggi.getDate() + i)
    out.push({ iso: d.toISOString().slice(0, 10), etichetta: fmt.format(d) })
  }
  return out
})

function dispPer(discordId, iso) {
  if (!discordId || !datiSquadra.value?.disponibilita) return null
  return datiSquadra.value.disponibilita.find(
    (d) => d.discordId === discordId && String(d.data).slice(0, 10) === iso
  )?.stato || null
}

function avvisa(testo) {
  messaggio.value = testo
  setTimeout(() => { if (messaggio.value === testo) messaggio.value = null }, 3000)
}

async function caricaDati() {
  try {
    const [sq, isc] = await Promise.all([api('torneo/squadre'), api('torneo/iscritti')])
    squadre.value = sq
    iscritti.value = isc
    if (!squadraSelezionata.value && sq.length) {
      const propria = sq.find((s) => s.capitanoDiscordId === sessione.discordId)
      squadraSelezionata.value = propria ? propria.id : (eAdmin.value ? sq[0].id : null)
    }
    await caricaSquadra()
  } catch (e) {
    errore.value = e.message
  }
}

async function caricaSquadra() {
  try {
    const q = squadraSelezionata.value && eAdmin.value ? `?squadraId=${squadraSelezionata.value}` : ''
    datiSquadra.value = await api(`capitano/squadra${q}`)
  } catch (e) {
    errore.value = e.message
  }
}

async function faiOfferta(importo) {
  const val = Number(importo)
  if (!Number.isFinite(val) || val < 1) return
  inviando.value = true
  errore.value = null
  try {
    await apiPost('asta/offerta', {
      importo: val,
      squadraId: eAdmin.value ? miaSquadra.value?.id : null,
    })
    offertaCustom.value = ''
    avvisa(`Offerta di ${val} Berlu inviata.`)
  } catch (e) {
    errore.value = e.message
  } finally {
    inviando.value = false
  }
}

onMounted(caricaDati)
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div>
        <h1>Capitano</h1>
        <p class="muto">
          Asta dal vivo, rosa della squadra e disponibilità settimanali.
          <span class="live-dot" :class="{ 'live-dot--on': connesso }">
            {{ connesso ? 'Live connesso' : 'Riconnessione...' }}
          </span>
        </p>
      </div>
      <div class="scelta" role="tablist">
        <button type="button" :aria-pressed="scheda === 'asta'" @click="scheda = 'asta'">Asta dal vivo</button>
        <button type="button" :aria-pressed="scheda === 'rosa'" @click="scheda = 'rosa'; caricaSquadra()">La tua rosa</button>
        <button type="button" :aria-pressed="scheda === 'seeding'" @click="scheda = 'seeding'; caricaDati()">Seeding e prezzi</button>
      </div>
    </div>

    <div v-if="eAdmin && squadre.length" class="strumenti">
      <label class="piccolo muto">Agisci come squadra (modalità Admin):</label>
      <select v-model.number="squadraSelezionata" class="cerca select-compatta" @change="caricaSquadra">
        <option v-for="s in squadre" :key="s.id" :value="s.id">{{ s.nome }} ({{ s.capitanoNome }})</option>
      </select>
    </div>

    <p v-if="messaggio" class="avviso-riga avviso-riga--ok" role="status">{{ messaggio }}</p>
    <p v-if="errore" class="avviso-riga avviso-riga--err" role="alert">{{ errore }}</p>

    <!-- ==================== ASTA LIVE ==================== -->
    <section v-if="scheda === 'asta'" class="blocco-pannello">
      <div class="due">
        <div class="riquadro angoli">
          <div class="riga-spazio">
            <span class="badge" :class="`badge--asta-${asta?.stato || 'attesa'}`">
              {{ asta?.stato === 'in_corso' ? 'Asta in corso' : asta?.stato === 'pausa' ? 'In pausa' : asta?.stato === 'chiusa' ? 'Asta chiusa' : 'In attesa di chiamata' }}
            </span>
            <span v-if="squadraTurno" class="piccolo muto">
              Round {{ asta?.roundNumero }} · Turno: <strong>{{ squadraTurno.nome }}</strong>
            </span>
          </div>

          <p v-if="asta?.messaggio" class="avviso-riga" style="margin-top: var(--space-sm)">{{ asta.messaggio }}</p>

          <template v-if="asta?.giocatore">
            <div class="asta-scheda">
              <span class="badge">Fascia {{ asta.giocatore.fascia }}</span>
              <h2 class="asta-nick">{{ asta.giocatore.nick }}</h2>
              <p class="muto piccolo">
                Seed {{ asta.giocatore.seeding ?? 'n.d.' }} ·
                ELO interno <strong>{{ elo(asta.giocatore.eloInterno) }}</strong> ·
                1v1 <strong>{{ elo(asta.giocatore.elo1v1) }}</strong> ·
                TG <strong>{{ elo(asta.giocatore.eloTg) }}</strong>
              </p>
            </div>

            <div class="asta-prezzo">
              <div>
                <span class="piccolo muto">Offerta attuale</span>
                <strong class="asta-cifra">{{ asta.offertaAttuale }} <span class="u">Berlu</span></strong>
              </div>
              <div>
                <span class="piccolo muto">In vantaggio</span>
                <strong>{{ squadraMigliore ? squadraMigliore.nome : 'Nessuna offerta' }}</strong>
              </div>
            </div>

            <div v-if="miaSquadra" class="asta-controlli">
              <p class="piccolo muto">
                <strong>{{ miaSquadra.nome }}</strong>:
                {{ miaSquadra.berluResidui }} Berlu rimasti ·
                rosa {{ miaSquadra.nGiocatori }}/{{ asta.maxRosa }} ·
                max offerta <strong>{{ offertaMassima }}</strong>
              </p>

              <div class="azioni-inline">
                <button type="button" class="btn" :disabled="!possoOffrire || inviando" @click="faiOfferta(offertaMinima)">
                  Offri {{ offertaMinima }}
                </button>
                <button type="button" class="btn btn--linea" :disabled="!possoOffrire || inviando || offertaMinima + 1 > offertaMassima" @click="faiOfferta(offertaMinima + 1)">
                  +2 ({{ offertaMinima + 1 }})
                </button>
                <button type="button" class="btn btn--linea" :disabled="!possoOffrire || inviando || offertaMinima + 4 > offertaMassima" @click="faiOfferta(offertaMinima + 4)">
                  +5 ({{ offertaMinima + 4 }})
                </button>
                <button type="button" class="btn btn--linea" :disabled="!possoOffrire || inviando || offertaMinima + 9 > offertaMassima" @click="faiOfferta(offertaMinima + 9)">
                  +10 ({{ offertaMinima + 9 }})
                </button>
              </div>

              <form class="azioni-inline" style="margin-top: var(--space-sm)" @submit.prevent="faiOfferta(offertaCustom)">
                <input
                  v-model="offertaCustom"
                  type="number"
                  class="cerca"
                  :min="offertaMinima"
                  :max="offertaMassima"
                  :placeholder="`Offerta libera (${offertaMinima}–${offertaMassima})`"
                  :disabled="!possoOffrire"
                />
                <button type="submit" class="btn btn--linea" :disabled="!possoOffrire || inviando || !offertaCustom">Rilancia</button>
              </form>
            </div>
            <p v-else class="piccolo muto">Il tuo account Discord non è ancora collegato a una squadra capitano.</p>
          </template>

          <p v-else class="muto" style="margin-top: var(--space-md)">
            Nessun giocatore sul piatto al momento. Quando l'admin chiama un giocatore, comparirà qui in tempo reale.
          </p>

          <div v-if="asta?.offerte?.length" class="sezione">
            <h3>Ultime offerte</h3>
            <ul class="offerte-log">
              <li v-for="o in asta.offerte" :key="o.id">
                <strong>{{ o.squadraNome }}</strong>
                <span class="num vinto">{{ o.importo }} Berlu</span>
              </li>
            </ul>
          </div>
        </div>

        <div class="riquadro angoli">
          <h2>Budget squadre</h2>
          <div class="tabella">
            <table>
              <thead>
                <tr>
                  <th>Squadra</th>
                  <th>Capitano</th>
                  <th class="num">Rosa</th>
                  <th class="num">Berlu</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="s in (asta?.squadre || [])"
                  :key="s.id"
                  :class="{ vivo: s.id === asta?.squadraMiglioreId }"
                >
                  <td class="forte">
                    {{ s.nome }}
                    <span v-if="s.id === asta?.turnoSquadraId" class="piccolo vinto"> · turno</span>
                  </td>
                  <td class="tenue">{{ s.capitanoNome }}</td>
                  <td class="num">{{ s.nGiocatori }}/{{ asta?.maxRosa || 10 }}</td>
                  <td class="num forte">{{ s.berluResidui }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>

    <!-- ==================== LA TUA ROSA & DISPONIBILITÀ ==================== -->
    <section v-else-if="scheda === 'rosa'" class="blocco-pannello">
      <div v-if="datiSquadra?.squadra" class="riquadro angoli">
        <div class="riga-spazio">
          <div>
            <h2>{{ datiSquadra.squadra.nome }}</h2>
            <p class="muto piccolo">
              Capitano: <strong>{{ datiSquadra.squadra.capitanoNome }}</strong> ·
              Berlu residui: <strong>{{ datiSquadra.squadra.berluResidui }}</strong> / {{ datiSquadra.squadra.berluIniziali }}
            </p>
          </div>
          <a
            v-if="datiSquadra.squadra.canaleDiscordUrl"
            :href="datiSquadra.squadra.canaleDiscordUrl"
            target="_blank"
            rel="noopener"
            class="btn btn--linea"
          >Canale Discord squadra</a>
        </div>

        <div class="tabella" style="margin-top: var(--space-md)">
          <p v-if="!datiSquadra.rosa?.length" class="muto piccolo">Nessun giocatore ancora assegnato a questa squadra.</p>
          <table v-else>
            <thead>
              <tr>
                <th>Giocatore</th>
                <th>Fascia</th>
                <th class="num">ELO int.</th>
                <th class="num">1v1</th>
                <th class="num">Prezzo</th>
                <th v-for="g in prossimi7Giorni" :key="g.iso" class="num">{{ g.etichetta }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in datiSquadra.rosa" :key="r.id">
                <td class="nome">
                  <RouterLink v-if="r.playerId" :to="`/giocatore/${r.playerId}`">{{ r.nick }}</RouterLink>
                  <span v-else>{{ r.nick }}</span>
                </td>
                <td>{{ r.fascia }}</td>
                <td class="num">{{ elo(r.eloInterno) }}</td>
                <td class="num">{{ elo(r.elo1v1) }}</td>
                <td class="num forte">{{ r.prezzoAsta ?? '—' }}</td>
                <td v-for="g in prossimi7Giorni" :key="g.iso" class="num">
                  <span v-if="dispPer(r.discordId, g.iso) === 'disponibile'" class="vinto" title="Disponibile">Sì</span>
                  <span v-else-if="dispPer(r.discordId, g.iso) === 'forse'" class="muto" title="Forse">?</span>
                  <span v-else-if="dispPer(r.discordId, g.iso) === 'indisponibile'" class="perso" title="Indisponibile">No</span>
                  <span v-else class="muto">·</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
      <div v-else class="riquadro angoli">
        <p class="muto">Nessuna squadra collegata al tuo account capitano. Un admin può associarla dal pannello Admin → Squadre.</p>
      </div>
    </section>

    <!-- ==================== SEEDING E PREZZI ==================== -->
    <section v-else-if="scheda === 'seeding'" class="blocco-pannello">
      <div class="riquadro angoli">
        <h2>Iscritti, seeding e prezzi d'asta</h2>
        <div class="tabella">
          <p v-if="!iscritti.length" class="muto piccolo">Nessun giocatore iscritto al momento.</p>
          <table v-else>
            <thead>
              <tr>
                <th class="num">Seed</th>
                <th>Fascia</th>
                <th>Giocatore</th>
                <th class="num">1v1</th>
                <th class="num">ELO int.</th>
                <th>Squadra</th>
                <th class="num">Prezzo</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in iscritti" :key="i.id">
                <td class="num pos">{{ i.seeding ?? '—' }}</td>
                <td><span class="badge">{{ i.fascia }}</span></td>
                <td class="nome">
                  <RouterLink v-if="i.playerId" :to="`/giocatore/${i.playerId}`">{{ i.nick }}</RouterLink>
                  <span v-else>{{ i.nick }}</span>
                </td>
                <td class="num">{{ elo(i.elo1v1) }}</td>
                <td class="num">{{ elo(i.eloInterno) }}</td>
                <td class="tenue">{{ i.squadraNome || 'Disponibile' }}</td>
                <td class="num forte">{{ i.prezzoAsta ?? `base ${i.prezzoBase}` }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>
  </div>
</template>
