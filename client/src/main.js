import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import './stile.css'

import Classifica from './pagine/Classifica.vue'
import Giocatore from './pagine/Giocatore.vue'
import Partite from './pagine/Partite.vue'
import Partita from './pagine/Partita.vue'
import Civilta from './pagine/Civilta.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Classifica, meta: { titolo: 'Classifica' } },
    { path: '/giocatore/:id', component: Giocatore, props: true, meta: { titolo: 'Giocatore' } },
    { path: '/partite', component: Partite, meta: { titolo: 'Partite' } },
    { path: '/partita/:id', component: Partita, props: true, meta: { titolo: 'Partita' } },
    { path: '/civilta', component: Civilta, meta: { titolo: 'Civiltà' } },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  scrollBehavior: (to, from, salvata) => salvata || { top: 0 },
})

router.afterEach((to) => {
  document.title = to.meta.titolo ? `${to.meta.titolo} · FantaAoE` : 'FantaAoE'
})

createApp(App).use(router).mount('#app')

if ('serviceWorker' in navigator && import.meta.env.PROD) {
  navigator.serviceWorker.register('/sw.js').catch(() => {})
}
