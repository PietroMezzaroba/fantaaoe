<script setup>
import { ref, watch } from 'vue'
import { api, elo, percento, data, dataConOra, delta, fa } from '../api.js'
import GraficoElo from '../componenti/GraficoElo.vue'

const props = defineProps({ id: { type: String, required: true } })

const d = ref(null)
const errore = ref('')

watch(
  () => props.id,
  async (id) => {
    d.value = null
    errore.value = ''
    try {
      d.value = await api(`giocatori/${id}`)
      document.title = `${d.value.giocatore.nick} · FantaAoE`
    } catch (e) {
      errore.value = e.message
    }
  },
  { immediate: true },
)

const winrateCiv = (c) => (c.partite ? (c.vittorie / c.partite) * 100 : 0)
const profilo = (id) => `https://www.aoe2insights.com/user/${id}/`
</script>

<template>
  <div v-if="errore" class="stato errore">{{ errore }}</div>
  <div v-else-if="!d" class="stato">Caricamento…</div>
  <template v-else>
    <p class="piccolo"><RouterLink to="/">← Classifica</RouterLink></p>
    <h1>
      {{ d.giocatore.nick }}
      <span v-if="d.giocatore.clan" class="tenue">{{ d.giocatore.clan }}</span>
    </h1>

    <div class="riquadri">
      <div class="riquadro oro">
        <div class="etichetta">ELO interno</div>
        <div class="valore">{{ elo(d.giocatore.eloInterno) }}</div>
        <div class="nota">TG Dito</div>
      </div>
      <div class="riquadro">
        <div class="etichetta">Ranked 1v1</div>
        <div class="valore">{{ elo(d.giocatore.elo1v1) }}</div>
        <div class="nota">max {{ elo(d.giocatore.elo1v1MassimoStorico) }} · {{ fa(d.giocatore.ultimaRanked1v1) }}</div>
      </div>
      <div class="riquadro">
        <div class="etichetta">Ranked TG</div>
        <div class="valore">{{ elo(d.giocatore.eloTg) }}</div>
        <div class="nota">max {{ elo(d.giocatore.eloTgMassimoStorico) }} · {{ fa(d.giocatore.ultimaRankedTg) }}</div>
      </div>
      <div class="riquadro">
        <div class="etichetta">Diti</div>
        <div class="valore">{{ d.giocatore.nDiti }}</div>
        <div class="nota">
          {{ d.giocatore.nDitiVinti }} vinti · {{ d.giocatore.nDiti ? percento(d.giocatore.winrate) : '—' }}
        </div>
      </div>
    </div>

    <template v-if="d.andamento.length > 1">
      <h2>Andamento ELO interno</h2>
      <GraficoElo :punti="d.andamento" />
    </template>

    <div class="due-colonne">
      <section>
        <h2>Civiltà più giocate</h2>
        <div v-if="!d.civ.length" class="tenue">Nessuna partita valida</div>
        <div v-else class="tabella-contenitore">
          <table>
            <thead>
              <tr><th>Civiltà</th><th class="num">Partite</th><th>Vittorie</th></tr>
            </thead>
            <tbody>
              <tr v-for="c in d.civ" :key="c.civ">
                <td>{{ c.civ }}</td>
                <td class="num">{{ c.partite }}</td>
                <td>
                  <div style="display: flex; align-items: center; gap: 8px">
                    <div class="barra" style="flex: 1"><span :style="{ width: winrateCiv(c) + '%' }" /></div>
                    <span class="num piccolo">{{ percento(winrateCiv(c)) }}</span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2>Account ranked</h2>
        <div class="tabella-contenitore">
          <table>
            <thead>
              <tr><th>Account</th><th class="num">1v1</th><th class="num">TG</th></tr>
            </thead>
            <tbody>
              <tr v-for="a in d.account" :key="a.aoe2Id">
                <td>
                  <a :href="profilo(a.aoe2Id)" target="_blank" rel="noopener">{{ a.steamName || a.aoe2Id }}</a>
                </td>
                <td class="num">{{ elo(a.mmr1v1) }}</td>
                <td class="num">{{ elo(a.mmrTg) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>

    <h2>Ultime partite</h2>
    <div v-if="!d.partite.length" class="tenue">Nessuna partita</div>
    <div v-else class="tabella-contenitore">
      <table>
        <thead>
          <tr>
            <th>Data</th>
            <th>Esito</th>
            <th>Civiltà</th>
            <th class="num">ELO</th>
            <th class="num nascondi-mobile">Durata</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in d.partite" :key="p.idGame">
            <td>
              <RouterLink :to="`/partita/${p.idGame}`">{{ dataConOra(p.startedAt) }}</RouterLink>
              <span v-if="p.dito" class="etichetta-dito piccolo"> #{{ p.dito }}</span>
            </td>
            <td :class="p.vinto ? 'vinto' : 'perso'">{{ p.vinto ? 'Vinta' : 'Persa' }}</td>
            <td>{{ p.civ || '—' }}</td>
            <td class="num">
              {{ elo(p.eloAfter) }}
              <span class="piccolo" :class="p.eloDelta > 0 ? 'vinto' : 'perso'">{{ delta(p.eloDelta) }}</span>
            </td>
            <td class="num tenue nascondi-mobile">{{ p.durataInGameMinuti }} min</td>
          </tr>
        </tbody>
      </table>
    </div>
    <p class="tenue piccolo">Ultimo Dito: {{ data(d.giocatore.ultimoDito) }}</p>
  </template>
</template>
