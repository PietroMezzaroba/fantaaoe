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
    id    serial PRIMARY KEY,
    nome  text NOT NULL
);

-- Iscrizioni al torneo: un solo record per utente, creato quando l'utente conferma l'iscrizione dalla homepage.
CREATE TABLE IF NOT EXISTS fanta_torneo.utenti_iscritti (
    id_discord         text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord(discord_id) ON DELETE CASCADE,
    id_squadra         int REFERENCES fanta_torneo.squadre(id),
    nickname           text,
    fascia             text NOT NULL,
    seeding            int NOT NULL,
    prezzo_base        int,
    prezzo_asta        int,
    stato              text NOT NULL DEFAULT 'nessuno',
    note               text NOT NULL DEFAULT '',
    data_iscrizione    timestamptz NOT NULL DEFAULT now(),
    stato_validazione  text NOT NULL DEFAULT 'in_validazione',
    elo_calcolato      int NOT NULL DEFAULT 0  -- ELO dell'iscritto calcolato dall'app (0 = non ancora calcolato)
);

-- la posizione dell'iscritto non si duplica: sono i ruoli in utenti_ruoli (colonna rimossa se creata in precedenza)
ALTER TABLE fanta_torneo.utenti_iscritti DROP COLUMN IF EXISTS posizione;
-- per chi aveva già creato la tabella senza elo_calcolato
ALTER TABLE fanta_torneo.utenti_iscritti ADD COLUMN IF NOT EXISTS elo_calcolato int NOT NULL DEFAULT 0;

-- Capitano della squadra: un utente (nullable), che deve avere il ruolo "capitano" (controllato dall'API).
-- Il vincolo è su utenti_discord e non su utenti_iscritti: il capitano può non essere iscritto come giocatore.
ALTER TABLE fanta_torneo.squadre ADD COLUMN IF NOT EXISTS id_capitano text;
ALTER TABLE fanta_torneo.squadre DROP CONSTRAINT IF EXISTS squadre_id_capitano_fkey;
ALTER TABLE fanta_torneo.squadre ADD CONSTRAINT squadre_id_capitano_fkey
    FOREIGN KEY (id_capitano) REFERENCES fanta_torneo.utenti_discord(discord_id);

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

-- Configurazioni globali dell'applicazione (non legate a un utente): chiave -> valore, con la spiegazione di cosa fanno
CREATE TABLE IF NOT EXISTS fanta_torneo.configurazioni (
    chiave       text PRIMARY KEY,
    valore       text NOT NULL,
    descrizione  text NOT NULL
);

INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('credito_iniziale', '401', 'Credito virtuale iniziale di ogni capitano per l''asta: è il valore a cui il reset riporta il portafoglio')
ON CONFLICT (chiave) DO NOTHING;

INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('stato_globale_asta', 'iscrizione', 'Fase in cui si trova l''asta nel suo insieme (iniziale: iscrizione)'),
       ('fase_flusso_asta', 'chiamata_giocatore', 'Fase in cui si trova la chiamata di un singolo giocatore (iniziale: chiamata_giocatore)')
ON CONFLICT (chiave) DO NOTHING;

INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('timer_asta_secondi', '120', 'Secondi del timer dell''offerta: parte dalla prima puntata, si riporta a questo valore a ogni offerta e a zero chiude l''offerta')
ON CONFLICT (chiave) DO NOTHING;

INSERT INTO fanta_torneo.configurazioni (chiave, valore, descrizione)
VALUES ('valuta', 'Berlu', 'Nome della moneta dell''asta: compare al posto della parola "crediti" in tutte le pagine dell''asta')
ON CONFLICT (chiave) DO NOTHING;

-- Asta (da riscrivere da capo): le vecchie tabelle asta_stato e asta_offerte sono state eliminate

-- Portafoglio d'asta di un utente: credito virtuale iniziale e credito che gli resta
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_portafoglio_utente (
    id_utente         text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord (discord_id),
    credito_iniziale  int NOT NULL DEFAULT 400 CHECK (credito_iniziale >= 0),
    credito_residuo   int NOT NULL DEFAULT 400 CHECK (credito_residuo >= 0)
);

-- Ordine con cui i capitani chiamano i giocatori all'asta (0 = ordine non ancora assegnato)
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_ordine_capitani (
    id_utente  text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord (discord_id),
    ordine     int NOT NULL DEFAULT 0 CHECK (ordine >= 0)
);

-- Giocatori acquistati all'asta: a quale squadra, per quanti crediti e quando (un giocatore si acquista una volta sola)
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_giocatori_acquistati (
    id              serial PRIMARY KEY,
    id_squadra      int  NOT NULL REFERENCES fanta_torneo.squadre (id),
    id_iscritto     text NOT NULL UNIQUE REFERENCES fanta_torneo.utenti_iscritti (id_discord),
    costo_acquisto  int  NOT NULL CHECK (costo_acquisto >= 0),
    data_acquisto   timestamptz NOT NULL DEFAULT now()
);

-- Giocatori chiamati all'asta: il giocatore chiamato (id_utente, una sola chiamata per giocatore), chi ha fatto la prima
-- chiamata, lo stato della chiamata (es. 'in fase di offerta', 'asta completata') e il costo iniziale
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_giocatori_chiamati (
    id_utente         text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord (discord_id),
    id_chiamato_da    text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    stato             text NOT NULL,
    costo_iniziale    int  NOT NULL CHECK (costo_iniziale >= 0),
    data_inserimento  timestamptz NOT NULL DEFAULT now()
);

-- Offerte fatte all'asta: per quale giocatore (id_utente), chi offre (id_chiamato_da), quanto e quando
CREATE TABLE IF NOT EXISTS fanta_torneo.asta_offerte (
    id_incrementale  serial PRIMARY KEY,
    id_utente        text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    id_chiamato_da   text NOT NULL REFERENCES fanta_torneo.utenti_discord (discord_id),
    puntata          int  NOT NULL CHECK (puntata >= 0),
    data_puntata     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_disp_data ON fanta_torneo.disponibilita (data);
CREATE INDEX IF NOT EXISTS idx_ticket_stato ON fanta_torneo.ticket (stato, creato_il DESC);

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

-- Anagrafica delle civiltà (id del gioco, nome, nome del file dell'avatar e descrizione)
CREATE TABLE IF NOT EXISTS fanta_torneo.civilta (
    id      int PRIMARY KEY,
    nome    text NOT NULL,
    avatar  text NOT NULL,
    descrizione text NOT NULL
);
-- per chi aveva già creato la tabella senza descrizione
ALTER TABLE fanta_torneo.civilta ADD COLUMN IF NOT EXISTS descrizione text;
UPDATE fanta_torneo.civilta SET descrizione = nome WHERE descrizione IS NULL;
ALTER TABLE fanta_torneo.civilta ALTER COLUMN descrizione SET NOT NULL;

-- per ora la descrizione è uguale al nome
INSERT INTO fanta_torneo.civilta (id, nome, avatar, descrizione)
SELECT id, nome, avatar, nome FROM (VALUES
    (0, 'Armenians', 'Armenians'), (1, 'Aztec', 'Aztec'), (2, 'Bengalis', 'Bengalis'),
    (3, 'Berbers', 'Berbers'), (4, 'Bohemians', 'Bohemians'), (5, 'Britons', 'Britons'),
    (6, 'Bulgarians', 'Bulgarians'), (7, 'Burgundians', 'Burgundians'), (8, 'Burmese', 'Burmese'),
    (9, 'Byzantines', 'Byzantines'), (10, 'Celts', 'Celts'), (11, 'Chinese', 'Chinese'),
    (12, 'Cumans', 'Cumans'), (13, 'Dravidians', 'Dravidians'), (14, 'Ethiopians', 'Ethiopians'),
    (15, 'Franks', 'Franks'), (16, 'Georgians', 'Georgians'), (17, 'Goths', 'Goths'),
    (18, 'Gurjaras', 'Gurjaras'), (19, 'Huns', 'Huns'), (20, 'Incas', 'Incas'),
    (21, 'Indians', 'Indians'), (22, 'Italians', 'Italians'), (23, 'Japanese', 'Japanese'),
    (24, 'Khmer', 'Khmer'), (25, 'Koreans', 'Koreans'), (26, 'Lithuanians', 'Lithuanians'),
    (27, 'Magyars', 'Magyars'), (28, 'Malay', 'Malay'), (29, 'Malians', 'Malians'),
    (30, 'Mayans', 'Mayans'), (31, 'Mongols', 'Mongols'), (32, 'Persians', 'Persians'),
    (33, 'Poles', 'Poles'), (34, 'Portuguese', 'Portuguese'), (35, 'Romans', 'Romans'),
    (36, 'Saracens', 'Saracens'), (37, 'Sicilians', 'Sicilians'), (38, 'Slavs', 'Slavs'),
    (39, 'Spanish', 'Spanish'), (40, 'Tatars', 'Tatars'), (41, 'Teutons', 'Teutons'),
    (42, 'Turks', 'Turks'), (43, 'Vietnamese', 'Vietnamese'), (44, 'Vikings', 'Vikings'),
    (48, 'Shu', 'Shu'), (49, 'Wu', 'Wu'), (50, 'Wei', 'Wei'),
    (51, 'Jurchens', 'Jurchens'), (52, 'Khitans', 'Khitans'),
    (58, 'Muisca', 'Muisca'), (59, 'Mapuche', 'Mapuche'), (60, 'Tupi', 'Tupi'),
    (61, 'Saxons', 'Saxons'), (62, 'Varangians', 'Varangians'), (63, 'Danes', 'Danes')
) AS v(id, nome, avatar)
ON CONFLICT (id) DO NOTHING;

-- =====================================================================
-- 3. LOGIN SIMULATO (solo sviluppo)
-- =====================================================================

-- Utenti per il login simulato (SOLO sviluppo: password in chiaro, in produzione questa tabella non esiste)
CREATE TABLE IF NOT EXISTS fanta_torneo.utenti_dev (
    id          serial PRIMARY KEY,
    id_discord  text NOT NULL UNIQUE,
    username    text NOT NULL UNIQUE,
    password    text NOT NULL
);

-- 50 utenti di test: Giocatore_1 ... Giocatore_50, password "password" (gli id_discord sono finti)
INSERT INTO fanta_torneo.utenti_dev (id_discord, username, password)
SELECT (200000000000000000 + n)::text, 'Giocatore_' || n, 'password'
FROM generate_series(1, 50) AS n
ON CONFLICT (username) DO NOTHING;

-- Prima gli stessi 50 utenti in utenti_discord (la tabella principale degli utenti, riempita in produzione dal login)
INSERT INTO fanta_torneo.utenti_discord (discord_id, discord_nome)
SELECT id_discord, username FROM fanta_torneo.utenti_dev
ON CONFLICT (discord_id) DO NOTHING;

-- Impostazioni di ogni utente (i ruoli stanno in utenti_ruoli)
CREATE TABLE IF NOT EXISTS fanta_torneo.utenti_info (
    id_discord  text PRIMARY KEY REFERENCES fanta_torneo.utenti_discord (discord_id) ON DELETE CASCADE,
    audio_attivo  boolean NOT NULL DEFAULT TRUE,  -- impostazione: audio di sottofondo
    layout_scuro  boolean NOT NULL DEFAULT TRUE,  -- impostazione: tema scuro (FALSE = chiaro)
    cursore       varchar(20) NOT NULL DEFAULT 'accattivante'  -- impostazione: 'accattivante' o 'classico'
        CONSTRAINT utenti_info_cursore_check CHECK (cursore IN ('accattivante', 'classico')),
    animazione_iniziale boolean NOT NULL DEFAULT TRUE  -- impostazione: elmo della home animato (FALSE = fermo)
);
-- per chi aveva già creato la tabella senza le impostazioni
ALTER TABLE fanta_torneo.utenti_info
    ADD COLUMN IF NOT EXISTS audio_attivo boolean NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS layout_scuro boolean NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS cursore varchar(20) NOT NULL DEFAULT 'accattivante'
        CONSTRAINT utenti_info_cursore_check CHECK (cursore IN ('accattivante', 'classico')),
    ADD COLUMN IF NOT EXISTS animazione_iniziale boolean NOT NULL DEFAULT TRUE;

INSERT INTO fanta_torneo.utenti_info (id_discord)
SELECT id_discord FROM fanta_torneo.utenti_dev
ON CONFLICT (id_discord) DO NOTHING;

-- Ruoli possibili (anagrafica). "Nessun ruolo" = nessuna riga in utenti_ruoli per quell'utente.
CREATE TABLE IF NOT EXISTS fanta_torneo.ruoli (
    id_ruolo     serial PRIMARY KEY,
    chiave_ruolo text NOT NULL UNIQUE,  -- valore tecnico: admin, streamer, capitano, giocatore, ...
    nome         text NOT NULL          -- nome in chiaro: Admin, Streamer, Capitano, Giocatore, ...
);

INSERT INTO fanta_torneo.ruoli (chiave_ruolo, nome)
VALUES ('admin', 'Admin'), ('streamer', 'Streamer'), ('capitano', 'Capitano'), ('giocatore', 'Giocatore'), ('utente', 'Utente')
ON CONFLICT (chiave_ruolo) DO NOTHING;

-- Ruoli assegnati a ogni utente: un utente può averne più di uno
CREATE TABLE IF NOT EXISTS fanta_torneo.utenti_ruoli (
    id_utente         text NOT NULL REFERENCES fanta_torneo.utenti_info (id_discord) ON DELETE CASCADE,
    id_ruolo          int  NOT NULL REFERENCES fanta_torneo.ruoli (id_ruolo),
    data_assegnazione timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (id_utente, id_ruolo)
);

-- data_assegnazione = momento dell'inserimento o dell'ultima modifica del record
CREATE OR REPLACE FUNCTION fanta_torneo.utenti_ruoli_aggiorna_data() RETURNS trigger AS $$
BEGIN
    NEW.data_assegnazione := now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS utenti_ruoli_data ON fanta_torneo.utenti_ruoli;
CREATE TRIGGER utenti_ruoli_data BEFORE UPDATE ON fanta_torneo.utenti_ruoli
    FOR EACH ROW EXECUTE FUNCTION fanta_torneo.utenti_ruoli_aggiorna_data();

-- Chi aveva ancora il vecchio ruolo singolo in utenti_info lo porta in utenti_ruoli ("nulla" = nessuna riga) e poi lo perde
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'fanta_torneo' AND table_name = 'utenti_info' AND column_name = 'ruolo') THEN
        INSERT INTO fanta_torneo.utenti_ruoli (id_utente, id_ruolo)
        SELECT i.id_discord, r.id_ruolo
        FROM fanta_torneo.utenti_info i
        JOIN fanta_torneo.ruoli r ON r.chiave_ruolo = i.ruolo
        ON CONFLICT (id_utente, id_ruolo) DO NOTHING;
        ALTER TABLE fanta_torneo.utenti_info DROP COLUMN ruolo;
    END IF;
END $$;

-- Ruoli degli utenti di prova: Giocatore_1 admin; 2-5 capitani; 6-40 giocatori; 41-50 nessun ruolo
INSERT INTO fanta_torneo.utenti_ruoli (id_utente, id_ruolo)
SELECT u.id_discord, r.id_ruolo
FROM fanta_torneo.utenti_dev u
CROSS JOIN LATERAL (SELECT substring(u.username FROM 'Giocatore_(\d+)')::int AS n) x
JOIN fanta_torneo.ruoli r ON r.chiave_ruolo = CASE WHEN x.n = 1 THEN 'admin'
                                                    WHEN x.n BETWEEN 2 AND 5 THEN 'capitano'
                                                    WHEN x.n BETWEEN 6 AND 40 THEN 'giocatore' END
WHERE x.n IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM fanta_torneo.utenti_ruoli ur WHERE ur.id_utente = u.id_discord)
ON CONFLICT (id_utente, id_ruolo) DO NOTHING;

COMMIT;
