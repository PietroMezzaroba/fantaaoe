import { createApp, nextTick } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import Radice from './Radice.vue'
import './stile.css'
import './movimento.css'
import { applicaTemaIniziale } from './tema.js'
import { api } from './api.js'

import Landing from './pagine/Landing.vue'
import Classifica from './pagine/Classifica.vue'
import Giocatore from './pagine/Giocatore.vue'
import Partite from './pagine/Partite.vue'
import Partita from './pagine/Partita.vue'
import Civilta from './pagine/Civilta.vue'
import Torneo from './pagine/Torneo.vue'
import ProfiloUtente from './pagine/ProfiloUtente.vue'
import LaTuaPagina from './pagine/LaTuaPagina.vue'
import Admin from './pagine/Admin.vue'
import Asta from './pagine/Asta.vue'
import AstaCapitano from './pagine/AstaCapitano.vue'
import AstaGestione from './pagine/AstaGestione.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Landing, meta: { titolo: 'FantaAge, Age of Empires Italia', pieno: true } },
    // «La tua pagina» prende il posto dei vecchi pannelli per ruolo (Casa.vue e Pannello.vue restano nel repo, non più raggiungibili)
    { path: '/io', component: LaTuaPagina, meta: { titolo: 'La tua pagina, FantaAge' } },
    { path: '/pannelli', redirect: '/io' },
    { path: '/pannello/:ruolo', redirect: '/io' },
    { path: '/profilo', component: ProfiloUtente, meta: { titolo: 'Il tuo profilo, FantaAge' } },
    { path: '/admin', component: Admin, meta: { titolo: 'Admin, FantaAge', pieno: true } },
    { path: '/asta', component: Asta, meta: { titolo: 'Asta, FantaAge' } },
    { path: '/asta/inizia', component: AstaCapitano, meta: { titolo: "Inizia l'asta, FantaAge", pieno: true } },
    { path: '/asta/gestisci', component: AstaGestione, meta: { titolo: "Gestisci l'asta, FantaAge", pieno: true } },
    { path: '/torneo', component: Torneo, meta: { titolo: 'Torneo 2026, FantaAge' } },
    { path: '/classifica', component: Classifica, meta: { titolo: 'Classifica, FantaAge' } },
    { path: '/giocatore/:id', component: Giocatore, props: true },
    { path: '/partite', component: Partite, meta: { titolo: 'Partite, FantaAge' } },
    { path: '/partita/:id', component: Partita, props: true },
    { path: '/civilta', component: Civilta, meta: { titolo: 'Civiltà, FantaAge' } },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  scrollBehavior: (to, from, salvata) => salvata || (to.path === from.path ? false : { top: 0 }),
})

// "Inizia l'asta" si può raggiungere solo ad asta attiva (stato_globale_asta = 'asta attiva'); altrimenti si torna alla home
router.beforeEach(async (to) => {
  if (to.path !== '/asta/inizia') return true
  try {
    return (await api('asta/stato')).attiva ? true : '/'
  } catch {
    return '/'
  }
})

router.afterEach((to) => {
  if (to.meta.titolo) document.title = to.meta.titolo
})

// Passaggio tra le pagine con la View Transitions API: la pagina vecchia esce e la nuova entra (movimento.css),
// la barra in alto resta ferma. Dove il browser non la ha, o con "riduci movimento", il cambio è secco come prima.
let chiudiPassaggio = null
router.beforeResolve((to, from) => {
  if (!document.startViewTransition || from.matched.length === 0 || to.path === from.path) return
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return
  return new Promise((pronta) => {
    const passaggio = document.startViewTransition(() => new Promise((fatto) => {
      chiudiPassaggio = fatto
      pronta()
    }))
    // se il passaggio salta (scheda nascosta, errore) la navigazione va avanti lo stesso
    passaggio.ready.catch(() => {})
    passaggio.finished.catch(() => {})
  })
})
router.afterEach(async () => {
  if (!chiudiPassaggio) return
  await nextTick()
  chiudiPassaggio()
  chiudiPassaggio = null
})
router.onError(() => {
  chiudiPassaggio?.()
  chiudiPassaggio = null
})

applicaTemaIniziale()

createApp(Radice).use(router).mount('#app')

if ('serviceWorker' in navigator && import.meta.env.PROD) {
  navigator.serviceWorker.register('/sw.js').catch(() => {})
}
