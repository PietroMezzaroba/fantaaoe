<script setup>
import { ref, watch } from 'vue'
import { api, elo, percento, giorno, ora, delta, fa } from '../api.js'
import GraficoElo from '../componenti/GraficoElo.vue'
import StatoDati from '../componenti/StatoDati.vue'

const props = defineProps({ id: { type: String, required: true } })

const d = ref(null)
const caricamento = ref(true)
const errore = ref('')

async function carica() {
  caricamento.value = true
  errore.value = ''
  try {
    d.value = await api(`giocatori/${props.id}`)
    document.title = `${d.value.giocatore.nick}, FantaAoE`
  } catch (e) {
    errore.value = e.message
  } finally {
    caricamento.value = false
  }
}
watch(() => props.id, carica, { immediate: true })

const winrateCiv = (c) => (c.partite ? (c.vittorie / c.partite) * 100 : 0)
const profilo = (id) => `https://www.aoe2insights.com/user/${id}/`
</script>

<template>
  <nav class="briciole" aria-label="Percorso">
    <RouterLink to="/classifica">Classifica</RouterLink>
    <span aria-hidden="true">/</span>
    <span aria-current="page">{{ d?.giocatore.nick ?? 'Giocatore' }}</span>
  </nav>

  <StatoDati :caricamento="caricamento" :errore="errore" :righe="6" @riprova="carica">
    <template v-if="d">
      <div class="testa-pagina">
        <h1>{{ d.giocatore.nick }}</h1>
        <p v-if="d.giocatore.clan">Clan {{ d.giocatore.clan }}</p>
      </div>

      <dl class="misure">
        <div class="misura">
          <dt>ELO interno</dt>
          <dd class="segnale">{{ elo(d.giocatore.eloInterno) }}</dd>
          <small>dai TG Dito</small>
        </div>
        <div class="misura">
          <dt>Ranked 1v1</dt>
          <dd>{{ elo(d.giocatore.elo1v1) }}</dd>
          <small>massimo {{ elo(d.giocatore.elo1v1MassimoStorico) }}, giocata {{ fa(d.giocatore.ultimaRanked1v1) }}</small>
        </div>
        <div class="misura">
          <dt>Ranked TG</dt>
          <dd>{{ elo(d.giocatore.eloTg) }}</dd>
          <small>massimo {{ elo(d.giocatore.eloTgMassimoStorico) }}, giocata {{ fa(d.giocatore.ultimaRankedTg) }}</small>
        </div>
        <div class="misura">
          <dt>Diti giocati</dt>
          <dd>{{ d.giocatore.nDiti }}</dd>
          <small>{{ d.giocatore.nDitiVinti }} vinti, {{ d.giocatore.nDiti ? percento(d.giocatore.winrate) : 'n.d.' }}</small>
        </div>
      </dl>

      <section v-if="d.andamento.length > 1" aria-labelledby="titolo-andamento">
        <h2 id="titolo-andamento">Andamento dell'ELO interno</h2>
        <GraficoElo :punti="d.andamento" />
      </section>

      <div class="due">
        <section aria-labelledby="titolo-civ">
          <h2 id="titolo-civ">Civiltà più giocate</h2>
          <p v-if="!d.civ.length" class="muto">Nessuna partita valida con la civiltà registrata.</p>
          <div v-else class="tabella">
            <table>
              <thead>
                <tr><th scope="col">Civiltà</th><th scope="col" class="num">Partite</th><th scope="col">Vittorie</th></tr>
              </thead>
              <tbody>
                <tr v-for="c in d.civ" :key="c.civ">
                  <td>{{ c.civ }}</td>
                  <td class="num">{{ c.partite }}</td>
                  <td>
                    <div class="barretta">
                      <div class="barretta__traccia"><span :style="{ transform: `scaleX(${winrateCiv(c) / 100})` }" /></div>
                      <span class="num">{{ percento(winrateCiv(c)) }}</span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section aria-labelledby="titolo-account">
          <h2 id="titolo-account">Account ranked</h2>
          <div class="tabella">
            <table>
              <thead>
                <tr><th scope="col">Account</th><th scope="col" class="num">1v1</th><th scope="col" class="num">TG</th></tr>
              </thead>
              <tbody>
                <tr v-for="a in d.account" :key="a.aoe2Id">
                  <td><a :href="profilo(a.aoe2Id)" target="_blank" rel="noopener">{{ a.steamName || a.aoe2Id }}</a></td>
                  <td class="num">{{ elo(a.mmr1v1) }}</td>
                  <td class="num">{{ elo(a.mmrTg) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <section class="sezione" aria-labelledby="titolo-partite">
        <h2 id="titolo-partite">Ultime partite</h2>
        <p v-if="!d.partite.length" class="muto">Nessun Dito registrato per questo giocatore.</p>
        <div v-else class="tabella">
          <table>
            <thead>
              <tr>
                <th scope="col">Quando</th>
                <th scope="col">Esito</th>
                <th scope="col">Civiltà</th>
                <th scope="col" class="num">ELO dopo</th>
                <th scope="col" class="num nascondi-stretto">Durata</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="p in d.partite" :key="p.idGame">
                <td class="nome">
                  <RouterLink :to="`/partita/${p.idGame}`">{{ p.dito ? `Dito ${p.dito}` : giorno(p.startedAt) }}</RouterLink>
                  <span v-if="p.dito" class="clan">{{ giorno(p.startedAt) }}</span>
                  <span v-else class="clan">{{ ora(p.startedAt) }}</span>
                </td>
                <td :class="p.vinto ? 'vinto' : 'perso'">{{ p.vinto ? 'Vinta' : 'Persa' }}</td>
                <td class="tenue">{{ p.civ || 'n.d.' }}</td>
                <td class="num">
                  {{ elo(p.eloAfter) }}
                  <span class="clan" :class="p.eloDelta > 0 ? 'vinto' : 'perso'">{{ delta(p.eloDelta) }}</span>
                </td>
                <td class="num muto nascondi-stretto">{{ p.durataInGameMinuti }}<span class="u">&thinsp;min</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </template>
  </StatoDati>
</template>
