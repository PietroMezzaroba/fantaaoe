<script setup>
import { ref, computed } from 'vue'
import { usaAstaLive } from '../../astaLive.js'

const { dati: asta, connesso } = usaAstaLive()
const copiato = ref(false)
const sfondoScuro = ref(false)

const urlOverlay = computed(() => {
  const base = `${window.location.origin}/overlay/asta`
  return sfondoScuro.value ? `${base}?sfondo=scuro` : base
})

async function copiaUrl() {
  try {
    await navigator.clipboard.writeText(urlOverlay.value)
    copiato.value = true
    setTimeout(() => { copiato.value = false }, 2500)
  } catch {
    /* fallback manuale */
  }
}
</script>

<template>
  <div class="pannello-app">
    <div class="pannello-testa">
      <div>
        <h1>Streamer</h1>
        <p class="muto">
          Overlay dell'asta pronto per OBS Studio: si aggiorna da solo in tempo reale.
          <span class="live-dot" :class="{ 'live-dot--on': connesso }">
            {{ connesso ? 'Live connesso' : 'Riconnessione...' }}
          </span>
        </p>
      </div>
      <a :href="urlOverlay" target="_blank" rel="noopener" class="btn">Apri overlay a schermo intero</a>
    </div>

    <div class="due">
      <div class="riquadro angoli">
        <h2>Sorgente Browser per OBS</h2>
        <p class="piccolo muto">
          In OBS aggiungi <strong>Browser Source</strong>, incolla questo indirizzo e imposta
          <strong>1920 × 1080</strong>. Lo sfondo è trasparente di default.
        </p>

        <div class="strumenti">
          <input type="text" class="cerca" :value="urlOverlay" readonly @focus="$event.target.select()" />
          <button type="button" class="btn" @click="copiaUrl">{{ copiato ? 'Copiato!' : 'Copia link' }}</button>
        </div>

        <label class="piccolo" style="display: flex; align-items: center; gap: 8px; cursor: pointer;">
          <input v-model="sfondoScuro" type="checkbox" />
          <span>Usa sfondo blu notte pieno (invece che trasparente)</span>
        </label>

        <div class="sezione">
          <h3>Stato attuale dell'asta</h3>
          <p class="piccolo muto">
            Fase: <strong>{{ asta?.stato || 'attesa' }}</strong> ·
            Giocatore: <strong>{{ asta?.giocatore?.nick || 'nessuno' }}</strong> ·
            Offerta: <strong>{{ asta?.offertaAttuale ?? 0 }} Berlu</strong>
          </p>
        </div>
      </div>

      <div class="riquadro angoli">
        <h2>Anteprima dal vivo</h2>
        <div class="overlay-preview">
          <iframe :src="urlOverlay" title="Anteprima overlay OBS"></iframe>
        </div>
      </div>
    </div>
  </div>
</template>
