-- Setup database locale di sviluppo per FantaAge (PostgreSQL 14+).
-- Crea gli stessi schemi di produzione:
--   1) "fanta"        : tabelle equivalenti alle viste dei TG Dito (con dati di esempio)
--   2) "fanta_torneo" : tabelle scrivibili del torneo (identiche a produzione)
--
-- Uso:
--   createdb fantaaoe
--   psql -d fantaaoe -f db/setup-locale.sql
--
-- Poi imposta la stringa di connessione (es. in appsettings.Development.json o env):
--   ConnectionStrings__Aoe="Host=localhost;Database=fantaaoe;Username=postgres;Password=postgres;Search Path=fanta,fanta_torneo"

\set ON_ERROR_STOP on
BEGIN;

CREATE SCHEMA IF NOT EXISTS fanta;
CREATE SCHEMA IF NOT EXISTS fanta_torneo;

-- =====================================================================
-- 1. SCHEMA "fanta" (in produzione sono viste sui TG Dito in sola lettura)
-- =====================================================================

CREATE TABLE IF NOT EXISTS fanta.giocatori (
    player_id                  int PRIMARY KEY,
    nick                       text NOT NULL,
    clan                       text,
    elo_interno                int NOT NULL DEFAULT 1000,
    elo_exact                  double precision NOT NULL DEFAULT 1000,
    n_diti                     int NOT NULL DEFAULT 0,
    n_diti_vinti               int NOT NULL DEFAULT 0,
    winrate                    double precision NOT NULL DEFAULT 0,
    ultimo_dito                timestamptz,
    elo_1v1                    int,
    account_1v1                bigint,
    ultima_ranked_1v1          timestamptz,
    elo_tg                     int,
    account_tg                 bigint,
    ultima_ranked_tg           timestamptz,
    elo_1v1_massimo_storico    int,
    elo_tg_massimo_storico     int,
    n_account                  bigint NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS fanta.account (
    aoe2_id         bigint PRIMARY KEY,
    player_id       int NOT NULL REFERENCES fanta.giocatori(player_id) ON DELETE CASCADE,
    steam_name      text,
    nick            text,
    clan            text,
    country         text DEFAULT 'IT',
    mmr_1v1         int,
    max_1v1_mmr     int,
    mmr_tg          int,
    max_tg_mmr      int,
    games_1v1       int DEFAULT 0,
    games_tg        int DEFAULT 0,
    last_game_1v1   timestamptz,
    last_game_tg    timestamptz,
    status          text DEFAULT 'active'
);

CREATE TABLE IF NOT EXISTS fanta.partite (
    id_game                bigint PRIMARY KEY,
    match_seq              int,
    started_at             timestamptz,
    ended_at               timestamptz,
    durata_irl_minuti      int NOT NULL DEFAULT 35,
    durata_in_game_minuti  int NOT NULL DEFAULT 55,
    map_name               text DEFAULT 'Arabia',
    valida                 boolean NOT NULL DEFAULT true,
    dito                   int
);

CREATE TABLE IF NOT EXISTS fanta.partite_giocatori (
    id_game     bigint NOT NULL REFERENCES fanta.partite(id_game) ON DELETE CASCADE,
    aoe2_id     bigint NOT NULL,
    player_id   int REFERENCES fanta.giocatori(player_id) ON DELETE SET NULL,
    nick        text,
    vinto       int NOT NULL DEFAULT 0,
    civ_id      int,
    civ         text,
    elo_before  double precision,
    elo_delta   double precision,
    elo_after   double precision,
    PRIMARY KEY (id_game, aoe2_id)
);

-- =====================================================================
-- 2. SCHEMA "fanta_torneo" (identico a produzione)
-- =====================================================================

CREATE TABLE IF NOT EXISTS fanta_torneo.utenti_discord (
    discord_id      text PRIMARY KEY,
    discord_nome    text NOT NULL,
    discord_avatar  text,
    player_id       int,
    collegato_da    text NOT NULL DEFAULT 'auto',
    verificato      boolean NOT NULL DEFAULT false,
    aggiornato_il   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fanta_torneo.squadre (
    id                   serial PRIMARY KEY,
    nome                 text NOT NULL UNIQUE,
    capitano_discord_id  text,
    capitano_nome        text NOT NULL,
    berlu_iniziali       int NOT NULL DEFAULT 401,
    berlu_residui        int NOT NULL DEFAULT 401,
    ordine_chiamata      int NOT NULL DEFAULT 1,
    canale_discord_url   text,
    creata_il            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fanta_torneo.iscritti (
    id           serial PRIMARY KEY,
    player_id    int UNIQUE,
    discord_id   text UNIQUE,
    nick         text NOT NULL,
    fascia       char(1) NOT NULL DEFAULT 'C' CHECK (fascia IN ('A','B','C')),
    seeding      int,
    prezzo_base  int NOT NULL DEFAULT 1,
    squadra_id   int REFERENCES fanta_torneo.squadre(id) ON DELETE SET NULL,
    prezzo_asta  int,
    stato        text NOT NULL DEFAULT 'approvato' CHECK (stato IN ('in_attesa','approvato','respinto')),
    note         text,
    iscritto_il  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fanta_torneo.disponibilita (
    id             serial PRIMARY KEY,
    discord_id     text NOT NULL,
    data           date NOT NULL,
    stato          text NOT NULL CHECK (stato IN ('disponibile','forse','indisponibile')),
    fascia_oraria  text,
    note           text,
    aggiornata_il  timestamptz NOT NULL DEFAULT now(),
    UNIQUE (discord_id, data)
);

CREATE TABLE IF NOT EXISTS fanta_torneo.ticket (
    id                 serial PRIMARY KEY,
    autore_discord_id  text NOT NULL,
    autore_nome        text NOT NULL,
    tipo               text NOT NULL CHECK (tipo IN ('restart','segnalazione','infrazione','altro')),
    titolo             text NOT NULL,
    descrizione        text NOT NULL,
    stato              text NOT NULL DEFAULT 'aperto' CHECK (stato IN ('aperto','in_corso','risolto','respinto')),
    risposta_admin     text,
    gestito_da         text,
    creato_il          timestamptz NOT NULL DEFAULT now(),
    aggiornato_il      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fanta_torneo.asta_stato (
    id                   int PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    stato                text NOT NULL DEFAULT 'attesa' CHECK (stato IN ('attesa','in_corso','pausa','chiusa')),
    iscritto_id          int REFERENCES fanta_torneo.iscritti(id) ON DELETE SET NULL,
    offerta_attuale      int NOT NULL DEFAULT 0,
    squadra_migliore_id  int REFERENCES fanta_torneo.squadre(id) ON DELETE SET NULL,
    turno_squadra_id     int REFERENCES fanta_torneo.squadre(id) ON DELETE SET NULL,
    round_numero         int NOT NULL DEFAULT 1,
    messaggio            text,
    scadenza_il          timestamptz,
    durata_timer_sec     int NOT NULL DEFAULT 15,
    aggiornato_il        timestamptz NOT NULL DEFAULT now()
);

INSERT INTO fanta_torneo.asta_stato (id, stato) VALUES (1, 'attesa') ON CONFLICT (id) DO NOTHING;

CREATE TABLE IF NOT EXISTS fanta_torneo.asta_offerte (
    id           serial PRIMARY KEY,
    iscritto_id  int NOT NULL REFERENCES fanta_torneo.iscritti(id) ON DELETE CASCADE,
    squadra_id   int NOT NULL REFERENCES fanta_torneo.squadre(id) ON DELETE CASCADE,
    importo      int NOT NULL,
    annullata    boolean NOT NULL DEFAULT false,
    creata_da    text NOT NULL,
    creata_il    timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS fanta_torneo.torneo_incontri (
    id                 serial PRIMARY KEY,
    fase               text NOT NULL DEFAULT 'girone' CHECK (fase IN ('girone','quarti','semifinale','finale')),
    giornata           int NOT NULL DEFAULT 1,
    girone             text NOT NULL DEFAULT 'Unico',
    squadra_a_id       int NOT NULL REFERENCES fanta_torneo.squadre(id) ON DELETE CASCADE,
    squadra_b_id       int NOT NULL REFERENCES fanta_torneo.squadre(id) ON DELETE CASCADE,
    vinte_a            int,
    vinte_b            int,
    data_programmata   timestamptz,
    stato              text NOT NULL DEFAULT 'programmato' CHECK (stato IN ('programmato','giocato','rinviato')),
    note               text,
    aggiornato_il      timestamptz NOT NULL DEFAULT now(),
    CHECK (squadra_a_id <> squadra_b_id)
);

CREATE INDEX IF NOT EXISTS idx_disp_data ON fanta_torneo.disponibilita (data);
CREATE INDEX IF NOT EXISTS idx_ticket_stato ON fanta_torneo.ticket (stato, creato_il DESC);
CREATE INDEX IF NOT EXISTS idx_asta_offerte_iscritto ON fanta_torneo.asta_offerte (iscritto_id, id DESC);
CREATE INDEX IF NOT EXISTS idx_incontri_fase ON fanta_torneo.torneo_incontri (fase, giornata, id);

-- =====================================================================
-- 3. DATI DI ESEMPIO (solo se il DB è vuoto)
-- =====================================================================

INSERT INTO fanta.giocatori (player_id, nick, clan, elo_interno, elo_exact, n_diti, n_diti_vinti, winrate, ultimo_dito, elo_1v1, elo_tg, elo_1v1_massimo_storico, elo_tg_massimo_storico, n_account)
VALUES
    (1, 'GiocatoreA1', 'ITA', 1480, 1480.4, 120, 74, 61.7, now() - interval '2 days', 1950, 1820, 2010, 1890, 1),
    (2, 'GiocatoreA2', 'HSL', 1420, 1420.1,  98, 56, 57.1, now() - interval '4 days', 1840, 1760, 1900, 1810, 1),
    (3, 'GiocatoreB1', 'MLT', 1290, 1290.0,  85, 44, 51.8, now() - interval '5 days', 1580, 1520, 1640, 1600, 1),
    (4, 'GiocatoreB2', NULL,  1240, 1240.5,  72, 36, 50.0, now() - interval '8 days', 1490, 1450, 1530, 1500, 1),
    (5, 'GiocatoreC1', 'ITA', 1110, 1110.2,  60, 27, 45.0, now() - interval '3 days', 1280, 1310, 1340, 1360, 1),
    (6, 'GiocatoreC2', NULL,  1050, 1050.0,  45, 19, 42.2, now() - interval '10 days', 1190, 1210, 1250, 1280, 1)
ON CONFLICT (player_id) DO NOTHING;

INSERT INTO fanta.account (aoe2_id, player_id, steam_name, nick, country, mmr_1v1, max_1v1_mmr, mmr_tg, max_tg_mmr, games_1v1, games_tg)
VALUES
    (100001, 1, 'GiocatoreA1', 'GiocatoreA1', 'IT', 1950, 2010, 1820, 1890, 420, 310),
    (100002, 2, 'GiocatoreA2', 'GiocatoreA2', 'IT', 1840, 1900, 1760, 1810, 350, 280),
    (100003, 3, 'GiocatoreB1', 'GiocatoreB1', 'IT', 1580, 1640, 1520, 1600, 260, 210),
    (100004, 4, 'GiocatoreB2', 'GiocatoreB2', 'IT', 1490, 1530, 1450, 1500, 190, 180),
    (100005, 5, 'GiocatoreC1', 'GiocatoreC1', 'IT', 1280, 1340, 1310, 1360, 140, 160),
    (100006, 6, 'GiocatoreC2', 'GiocatoreC2', 'IT', 1190, 1250, 1210, 1280, 110, 120)
ON CONFLICT (aoe2_id) DO NOTHING;

INSERT INTO fanta.partite (id_game, match_seq, started_at, ended_at, durata_irl_minuti, durata_in_game_minuti, map_name, valida, dito)
VALUES
    (900001, 1, now() - interval '2 days', now() - interval '2 days' + interval '38 minutes', 38, 62, 'Arabia', true, 220),
    (900002, 2, now() - interval '5 days', now() - interval '5 days' + interval '44 minutes', 44, 71, 'Arena',  true, 219)
ON CONFLICT (id_game) DO NOTHING;

INSERT INTO fanta.partite_giocatori (id_game, aoe2_id, player_id, nick, vinto, civ, elo_before, elo_delta, elo_after)
VALUES
    (900001, 100001, 1, 'GiocatoreA1', 1, 'Franks',   1468, +12, 1480),
    (900001, 100003, 3, 'GiocatoreB1', 1, 'Mayans',   1278, +12, 1290),
    (900001, 100005, 5, 'GiocatoreC1', 1, 'Italians', 1098, +12, 1110),
    (900001, 100002, 2, 'GiocatoreA2', 0, 'Britons',  1432, -12, 1420),
    (900001, 100004, 4, 'GiocatoreB2', 0, 'Huns',     1252, -12, 1240),
    (900001, 100006, 6, 'GiocatoreC2', 0, 'Mongols',  1062, -12, 1050)
ON CONFLICT (id_game, aoe2_id) DO NOTHING;

INSERT INTO fanta_torneo.squadre (nome, capitano_nome, berlu_iniziali, berlu_residui, ordine_chiamata)
VALUES
    ('Team Verde (Demo)', 'Capitano 1', 401, 401, 1),
    ('Team Rosso (Demo)', 'Capitano 2', 401, 401, 2)
ON CONFLICT (nome) DO NOTHING;

INSERT INTO fanta_torneo.iscritti (player_id, nick, fascia, seeding, prezzo_base, stato)
VALUES
    (1, 'GiocatoreA1', 'A', 1, 1, 'approvato'),
    (2, 'GiocatoreA2', 'A', 2, 1, 'approvato'),
    (3, 'GiocatoreB1', 'B', 3, 1, 'approvato'),
    (4, 'GiocatoreB2', 'B', 4, 1, 'approvato'),
    (5, 'GiocatoreC1', 'C', 5, 1, 'approvato'),
    (6, 'GiocatoreC2', 'C', 6, 1, 'approvato')
ON CONFLICT (player_id) DO NOTHING;

COMMIT;
