import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import Radice from './Radice.vue'
import './stile.css'
import { applicaTemaIniziale } from './tema.js'

import Landing from './pagine/Landing.vue'
import Casa from './pagine/Casa.vue'
import Pannello from './pagine/Pannello.vue'
import Classifica from './pagine/Classifica.vue'
import Giocatore from './pagine/Giocatore.vue'
import Partite from './pagine/Partite.vue'
import Partita from './pagine/Partita.vue'
import Civilta from './pagine/Civilta.vue'
import Torneo from './pagine/Torneo.vue'
import ProfiloUtente from './pagine/ProfiloUtente.vue'
import Admin from './pagine/Admin.vue'
import Asta from './pagine/Asta.vue'
import AstaCapitano from './pagine/AstaCapitano.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Landing, meta: { titolo: 'FantaAge, Age of Empires Italia', pieno: true } },
    { path: '/pannelli', component: Casa, meta: { titolo: 'Pannelli, FantaAge' } },
    { path: '/pannello/:ruolo', component: Pannello, props: true },
    { path: '/profilo', component: ProfiloUtente, meta: { titolo: 'Il tuo profilo, FantaAge' } },
    { path: '/admin', component: Admin, meta: { titolo: 'Admin, FantaAge', pieno: true } },
    { path: '/asta', component: Asta, meta: { titolo: 'Asta, FantaAge' } },
    { path: '/asta/inizia', component: AstaCapitano, meta: { titolo: "Inizia l'asta, FantaAge", pieno: true } },
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

router.afterEach((to) => {
  if (to.meta.titolo) document.title = to.meta.titolo
})

applicaTemaIniziale()

createApp(Radice).use(router).mount('#app')

if ('serviceWorker' in navigator && import.meta.env.PROD) {
  navigator.serviceWorker.register('/sw.js').catch(() => {})
}
