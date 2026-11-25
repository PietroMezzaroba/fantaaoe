<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, apiPost, elo } from '../../api.js'
import { sessione } from '../../sessione.js'

const scheda = ref('rosa')

const squadre = ref([])
const iscritti = ref([])
const squadraSelezionata = ref(null)
const datiSquadra = ref(null)
const errore = ref(null)
const messaggio = ref(null)

const eAdmin = computed(() => sessione.ruoli.includes('admin'))

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

onMounted(caricaDati)
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div>
        <h1>Capitano</h1>
        <p class="muto">
          Rosa della squadra e disponibilità settimanali.
        </p>
      </div>
      <div class="scelta" role="tablist">
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

    <!-- ==================== LA TUA ROSA & DISPONIBILITÀ ==================== -->
    <section v-if="scheda === 'rosa'" class="blocco-pannello">
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
