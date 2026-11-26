<script setup>
// "Gestisci l'asta" (streamer): copia di "Inizia l'asta" (AstaCapitano.vue), da modificare. Pagina a tutta larghezza divisa in due metà affiancate (50% ciascuna). Metà sinistra: le finestre delle squadre.
import FinestreSquadre from '../componenti/FinestreSquadre.vue'
import GiocatoriDisponibili from '../componenti/GiocatoriDisponibili.vue'
import PuntateCapitani from '../componenti/PuntateCapitani.vue'
</script>

<template>
  <div class="asta-capitano">
    <section class="asta-capitano__meta" aria-label="Squadre">
      <FinestreSquadre quattro streamer />
    </section>
    <section class="asta-capitano__meta" aria-label="Giocatori">
      <div class="asta-capitano__disponibili">
        <GiocatoriDisponibili />
      </div>
      <div class="asta-capitano__puntate">
        <PuntateCapitani :offerte="false" streamer />
      </div>
    </section>
  </div>
</template>

<style scoped>
/* tutta la larghezza; l'altezza è quella dello schermo sotto la barra, divisa in due colonne uguali (ognuna scorre per conto suo) */
.asta-capitano {
  display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-md);
  height: calc(100dvh - 5rem); min-height: 30rem; padding: var(--space-md);
}
.asta-capitano__meta { min-height: 0; overflow: auto; }
.asta-capitano__meta:last-child { display: flex; gap: var(--space-md); }
.asta-capitano__puntate { flex: 1 1 0; min-width: 0; height: 100%; min-height: 0; }
/* lo spazio dei giocatori occupa il 30% della larghezza della colonna destra (il suo contenitore), a partire dal bordo sinistro */
.asta-capitano__disponibili { flex: 0 0 30%; height: 100%; min-height: 0; }
</style>
