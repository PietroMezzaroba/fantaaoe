<script setup>
import { ref, computed, onMounted } from 'vue'
import { api, elo, giorno } from '../api.js'
import { ruoli } from '../ruoli.js'
import { sessione, puoEntrare } from '../sessione.js'
import NuvolaElmo from '../componenti/NuvolaElmo.vue'

const primi = ref([])
const attivi = ref(null)
const partite = ref(null)
const ultimo = ref(null)
const errore = ref(false)

onMounted(async () => {
  try {
    const [g, p] = await Promise.all([api('giocatori'), api('partite?perPagina=1')])
    attivi.value = g.length
    primi.value = g.slice(0, 5)
    partite.value = p.totale
    ultimo.value = p.partite[0] ?? null
  } catch {
    errore.value = true
  }
})

const numero = (n) => (n == null ? null : n.toLocaleString('it-IT'))
const vincitori = computed(() => ultimo.value?.giocatori.filter((g) => g.vinto) ?? [])
const sconfitti = computed(() => ultimo.value?.giocatori.filter((g) => !g.vinto) ?? [])
</script>

<template>
  <section class="eroe" aria-labelledby="titolo-eroe">
    <div class="container eroe__griglia">
      <div class="eroe__testo">
        <h1 id="titolo-eroe">Il fanta dei TG Dito</h1>
        <p class="lead">Asta, squadre e classifica della community Age of Empires Italia.</p>
        <div class="eroe__azioni">
          <RouterLink to="/pannelli" class="btn btn--grande">Entra nei pannelli</RouterLink>
          <RouterLink to="/classifica" class="btn btn--linea btn--grande">Apri la classifica</RouterLink>
        </div>
        <dl class="eroe__misure" aria-live="polite">
          <div>
            <dt>Partite registrate</dt>
            <dd class="num">{{ numero(partite) ?? 'n.d.' }}</dd>
          </div>
          <div>
            <dt>Attivi in 90 giorni</dt>
            <dd class="num">{{ numero(attivi) ?? 'n.d.' }}</dd>
          </div>
        </dl>
      </div>
      <div class="eroe__scena">
        <NuvolaElmo />
      </div>
    </div>
  </section>

  <div class="container fasce">
    <section class="fascia" aria-labelledby="titolo-oggi">
      <div class="fascia__testa">
        <h2 id="titolo-oggi">Il tabellone di oggi</h2>
        <RouterLink to="/classifica" class="piccolo">Tutta la classifica</RouterLink>
      </div>
      <p v-if="errore" class="muto">La classifica non risponde in questo momento. Riprova tra poco.</p>
      <ol v-else class="podio">
        <li v-for="(g, i) in primi" :key="g.playerId" class="podio__riga">
          <span class="podio__pos num">{{ i + 1 }}</span>
          <RouterLink :to="`/giocatore/${g.playerId}`" class="podio__nome">{{ g.nick }}</RouterLink>
          <span class="podio__elo num">{{ elo(g.eloInterno) }}</span>
          <span class="podio__altri num muto">1v1 {{ elo(g.elo1v1) }}, TG {{ elo(g.eloTg) }}</span>
        </li>
      </ol>
    </section>

    <section v-if="ultimo" class="fascia" aria-labelledby="titolo-ultimo">
      <div class="fascia__testa">
        <h2 id="titolo-ultimo">{{ ultimo.partita.dito ? `L'ultimo Dito, il ${ultimo.partita.dito}` : "L'ultima partita" }}</h2>
        <RouterLink :to="`/partita/${ultimo.partita.idGame}`" class="piccolo">Apri la partita</RouterLink>
      </div>
      <p class="muto">{{ giorno(ultimo.partita.startedAt) }}, {{ ultimo.partita.durataInGameMinuti }}&thinsp;minuti di gioco</p>
      <div class="duello">
        <div class="duello__lato">
          <span class="vinto piccolo">Vincitori</span>
          <ul>
            <li v-for="g in vincitori" :key="g.aoe2Id">
              <span>{{ g.nick || g.aoe2Id }}</span><span class="muto">{{ g.civ }}</span>
            </li>
          </ul>
        </div>
        <div class="duello__lato">
          <span class="perso piccolo">Sconfitti</span>
          <ul>
            <li v-for="g in sconfitti" :key="g.aoe2Id">
              <span>{{ g.nick || g.aoe2Id }}</span><span class="muto">{{ g.civ }}</span>
            </li>
          </ul>
        </div>
      </div>
    </section>

    <section class="fascia" aria-labelledby="titolo-ruoli">
      <div class="fascia__testa">
        <h2 id="titolo-ruoli">Un pannello per ruolo</h2>
        <RouterLink to="/pannelli" class="piccolo">Tutti i pannelli</RouterLink>
      </div>
      <ul class="porte">
        <li v-for="r in ruoli" :key="r.chiave" class="porta">
          <RouterLink :to="`/pannello/${r.chiave}`">
            <span class="porta__nome">{{ r.nome }}</span>
            <span class="porta__cosa">{{ r.cosa }}</span>
            <span class="porta__accesso" :class="{ 'porta__accesso--tuo': sessione.autenticato && puoEntrare(r.chiave) }">
              {{ sessione.autenticato ? (puoEntrare(r.chiave) ? 'Entra' : 'Non è il tuo ruolo') : 'Con Discord' }}
            </span>
          </RouterLink>
        </li>
      </ul>
    </section>
  </div>
</template>
