--
-- PostgreSQL database dump
--

\restrict PcO4AQMa8hYIbeWvBKUVZGv0OJGc9kc8hzFjnfHaGaCoRfCcXA3dQQ6LT1l009T

-- Dumped from database version 19.0
-- Dumped by pg_dump version 19.0

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: fanta_torneo; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA fanta_torneo;


--
-- Name: utenti_ruoli_aggiorna_data(); Type: FUNCTION; Schema: fanta_torneo; Owner: -
--

CREATE FUNCTION fanta_torneo.utenti_ruoli_aggiorna_data() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    NEW.data_assegnazione := now();
    RETURN NEW;
END;
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: asta_giocatori_acquistati; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.asta_giocatori_acquistati (
    id integer NOT NULL,
    id_squadra integer NOT NULL,
    id_iscritto text NOT NULL,
    costo_acquisto integer NOT NULL,
    data_acquisto timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT asta_giocatori_acquistati_costo_acquisto_check CHECK ((costo_acquisto >= 0))
);


--
-- Name: asta_giocatori_acquistati_id_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.asta_giocatori_acquistati_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: asta_giocatori_acquistati_id_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.asta_giocatori_acquistati_id_seq OWNED BY fanta_torneo.asta_giocatori_acquistati.id;


--
-- Name: asta_ordine_capitani; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.asta_ordine_capitani (
    id_utente text NOT NULL,
    ordine integer DEFAULT 0 NOT NULL,
    CONSTRAINT asta_ordine_capitani_ordine_check CHECK ((ordine >= 0))
);


--
-- Name: asta_portafoglio_utente; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.asta_portafoglio_utente (
    id_utente text NOT NULL,
    credito_iniziale integer DEFAULT 400 NOT NULL,
    credito_residuo integer DEFAULT 400 NOT NULL,
    CONSTRAINT asta_portafoglio_utente_credito_iniziale_check CHECK ((credito_iniziale >= 0)),
    CONSTRAINT asta_portafoglio_utente_credito_residuo_check CHECK ((credito_residuo >= 0))
);


--
-- Name: civilta; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.civilta (
    id integer NOT NULL,
    nome text NOT NULL,
    avatar text NOT NULL,
    descrizione text NOT NULL
);


--
-- Name: configurazioni; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.configurazioni (
    chiave text NOT NULL,
    valore text NOT NULL,
    descrizione text NOT NULL
);


--
-- Name: disponibilita; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.disponibilita (
    id integer NOT NULL,
    discord_id text NOT NULL,
    data date NOT NULL,
    stato text NOT NULL,
    fascia_oraria text,
    note text,
    aggiornata_il timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT disponibilita_stato_check CHECK ((stato = ANY (ARRAY['disponibile'::text, 'forse'::text, 'indisponibile'::text])))
);


--
-- Name: disponibilita_id_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.disponibilita_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: disponibilita_id_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.disponibilita_id_seq OWNED BY fanta_torneo.disponibilita.id;


--
-- Name: ruoli; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.ruoli (
    id_ruolo integer NOT NULL,
    chiave_ruolo text NOT NULL,
    nome text NOT NULL
);


--
-- Name: ruoli_id_ruolo_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.ruoli_id_ruolo_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: ruoli_id_ruolo_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.ruoli_id_ruolo_seq OWNED BY fanta_torneo.ruoli.id_ruolo;


--
-- Name: squadre; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.squadre (
    id integer NOT NULL,
    nome text NOT NULL,
    id_capitano text
);


--
-- Name: squadre_id_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.squadre_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: squadre_id_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.squadre_id_seq OWNED BY fanta_torneo.squadre.id;


--
-- Name: ticket; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.ticket (
    id integer NOT NULL,
    autore_discord_id text NOT NULL,
    autore_nome text NOT NULL,
    tipo text NOT NULL,
    titolo text NOT NULL,
    descrizione text NOT NULL,
    stato text DEFAULT 'aperto'::text NOT NULL,
    risposta_admin text,
    gestito_da text,
    creato_il timestamp with time zone DEFAULT now() NOT NULL,
    aggiornato_il timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT ticket_stato_check CHECK ((stato = ANY (ARRAY['aperto'::text, 'in_corso'::text, 'risolto'::text, 'respinto'::text]))),
    CONSTRAINT ticket_tipo_check CHECK ((tipo = ANY (ARRAY['restart'::text, 'segnalazione'::text, 'infrazione'::text, 'altro'::text])))
);


--
-- Name: ticket_id_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.ticket_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: ticket_id_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.ticket_id_seq OWNED BY fanta_torneo.ticket.id;


--
-- Name: utenti_dev; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.utenti_dev (
    id integer NOT NULL,
    id_discord text NOT NULL,
    username text NOT NULL,
    password text NOT NULL
);


--
-- Name: utenti_dev_id_seq; Type: SEQUENCE; Schema: fanta_torneo; Owner: -
--

CREATE SEQUENCE fanta_torneo.utenti_dev_id_seq
    AS integer
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;


--
-- Name: utenti_dev_id_seq; Type: SEQUENCE OWNED BY; Schema: fanta_torneo; Owner: -
--

ALTER SEQUENCE fanta_torneo.utenti_dev_id_seq OWNED BY fanta_torneo.utenti_dev.id;


--
-- Name: utenti_discord; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.utenti_discord (
    discord_id text NOT NULL,
    discord_nome text NOT NULL,
    discord_avatar text,
    player_id integer,
    collegato_da text DEFAULT 'auto'::text NOT NULL,
    verificato boolean DEFAULT false NOT NULL,
    aggiornato_il timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: utenti_info; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.utenti_info (
    id_discord text NOT NULL,
    audio_attivo boolean DEFAULT true NOT NULL,
    layout_scuro boolean DEFAULT true NOT NULL,
    cursore character varying(20) DEFAULT 'accattivante'::character varying NOT NULL,
    animazione_iniziale boolean DEFAULT true NOT NULL,
    CONSTRAINT utenti_info_cursore_check CHECK (((cursore)::text = ANY ((ARRAY['accattivante'::character varying, 'classico'::character varying])::text[])))
);


--
-- Name: utenti_iscritti; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.utenti_iscritti (
    id_discord text NOT NULL,
    id_squadra integer,
    nickname text,
    fascia text NOT NULL,
    seeding integer NOT NULL,
    prezzo_base integer,
    prezzo_asta integer,
    stato text DEFAULT 'nessuno'::text NOT NULL,
    note text DEFAULT ''::text NOT NULL,
    data_iscrizione timestamp with time zone DEFAULT now() NOT NULL,
    stato_validazione text DEFAULT 'in_validazione'::text NOT NULL,
    elo_calcolato integer DEFAULT 0 NOT NULL
);


--
-- Name: utenti_ruoli; Type: TABLE; Schema: fanta_torneo; Owner: -
--

CREATE TABLE fanta_torneo.utenti_ruoli (
    id_utente text NOT NULL,
    id_ruolo integer NOT NULL,
    data_assegnazione timestamp with time zone DEFAULT now() NOT NULL
);


--
-- Name: asta_giocatori_acquistati id; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_giocatori_acquistati ALTER COLUMN id SET DEFAULT nextval('fanta_torneo.asta_giocatori_acquistati_id_seq'::regclass);


--
-- Name: disponibilita id; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.disponibilita ALTER COLUMN id SET DEFAULT nextval('fanta_torneo.disponibilita_id_seq'::regclass);


--
-- Name: ruoli id_ruolo; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.ruoli ALTER COLUMN id_ruolo SET DEFAULT nextval('fanta_torneo.ruoli_id_ruolo_seq'::regclass);


--
-- Name: squadre id; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.squadre ALTER COLUMN id SET DEFAULT nextval('fanta_torneo.squadre_id_seq'::regclass);


--
-- Name: ticket id; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.ticket ALTER COLUMN id SET DEFAULT nextval('fanta_torneo.ticket_id_seq'::regclass);


--
-- Name: utenti_dev id; Type: DEFAULT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_dev ALTER COLUMN id SET DEFAULT nextval('fanta_torneo.utenti_dev_id_seq'::regclass);


--
-- Data for Name: asta_giocatori_acquistati; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.asta_giocatori_acquistati VALUES
	(9, 8, '200000000000000002', 0, '2026-11-25 12:45:01.219259+00'),
	(10, 9, '200000000000000003', 0, '2026-11-25 12:45:24.259998+00'),
	(11, 10, '200000000000000004', 0, '2026-11-25 12:45:30.962357+00'),
	(12, 11, '200000000000000005', 0, '2026-11-25 12:45:32.226151+00'),
	(13, 19, '200000000000000006', 0, '2026-11-25 12:45:34.551714+00'),
	(14, 20, '200000000000000007', 0, '2026-11-25 12:45:36.139612+00'),
	(15, 21, '200000000000000008', 0, '2026-11-25 12:45:37.66793+00'),
	(16, 22, '200000000000000009', 0, '2026-11-25 12:45:38.999854+00'),
	(17, 23, '200000000000000010', 0, '2026-11-25 12:45:40.325456+00'),
	(18, 24, '200000000000000011', 0, '2026-11-25 12:45:41.664552+00'),
	(19, 8, '200000000000000016', 5, '2026-11-25 12:49:22.799678+00'),
	(20, 8, '200000000000000020', 16, '2026-11-25 12:49:22.799678+00'),
	(21, 9, '200000000000000038', 45, '2026-11-25 12:49:22.799678+00'),
	(22, 9, '200000000000000012', 15, '2026-11-25 12:49:22.799678+00'),
	(23, 9, '200000000000000019', 49, '2026-11-25 12:49:22.799678+00'),
	(24, 9, '200000000000000023', 58, '2026-11-25 12:49:22.799678+00'),
	(25, 10, '200000000000000036', 44, '2026-11-25 12:49:22.799678+00'),
	(26, 10, '200000000000000031', 30, '2026-11-25 12:49:22.799678+00'),
	(27, 11, '200000000000000027', 7, '2026-11-25 12:49:22.799678+00'),
	(28, 11, '200000000000000034', 31, '2026-11-25 12:49:22.799678+00'),
	(29, 19, '200000000000000040', 55, '2026-11-25 12:49:22.799678+00'),
	(30, 19, '200000000000000032', 1, '2026-11-25 12:49:22.799678+00'),
	(31, 20, '200000000000000037', 55, '2026-11-25 12:49:22.799678+00'),
	(32, 20, '200000000000000035', 14, '2026-11-25 12:49:22.799678+00'),
	(33, 20, '200000000000000028', 34, '2026-11-25 12:49:22.799678+00'),
	(34, 21, '200000000000000018', 48, '2026-11-25 12:49:22.799678+00'),
	(35, 21, '200000000000000039', 46, '2026-11-25 12:49:22.799678+00'),
	(36, 22, '200000000000000025', 48, '2026-11-25 12:49:22.799678+00'),
	(37, 22, '200000000000000024', 2, '2026-11-25 12:49:22.799678+00'),
	(38, 23, '200000000000000015', 12, '2026-11-25 12:49:22.799678+00'),
	(39, 23, '200000000000000033', 52, '2026-11-25 12:49:22.799678+00'),
	(40, 23, '200000000000000026', 3, '2026-11-25 12:49:22.799678+00'),
	(41, 24, '200000000000000030', 52, '2026-11-25 12:49:22.799678+00'),
	(42, 24, '200000000000000041', 20, '2026-11-25 12:49:22.799678+00'),
	(43, 9, '200000000000000014', 11, '2026-11-25 12:58:27.899039+00'),
	(44, 9, '200000000000000029', 24, '2026-11-25 12:58:27.899039+00'),
	(45, 9, '200000000000000021', 35, '2026-11-25 12:58:27.899039+00'),
	(46, 9, '200000000000000017', 39, '2026-11-25 12:58:27.899039+00');


--
-- Data for Name: asta_ordine_capitani; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.asta_ordine_capitani VALUES
	('200000000000000003', 1),
	('200000000000000004', 2),
	('200000000000000002', 3),
	('200000000000000005', 4),
	('200000000000000006', 5),
	('200000000000000008', 6),
	('200000000000000009', 7),
	('200000000000000007', 8),
	('200000000000000010', 9),
	('200000000000000011', 10);


--
-- Data for Name: asta_portafoglio_utente; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.asta_portafoglio_utente VALUES
	('200000000000000002', 401, 380),
	('200000000000000003', 401, 125),
	('200000000000000004', 401, 327),
	('200000000000000005', 401, 363),
	('200000000000000011', 401, 329),
	('200000000000000006', 401, 345),
	('200000000000000007', 401, 298),
	('200000000000000008', 401, 307),
	('200000000000000009', 401, 351),
	('200000000000000010', 401, 334);


--
-- Data for Name: civilta; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.civilta VALUES
	(24, 'Khmer', 'Khmer', 'Khmer'),
	(48, 'Shu', 'Shu', 'Shu'),
	(49, 'Wu', 'Wu', 'Wu'),
	(50, 'Wei', 'Wei', 'Wei'),
	(17, 'Goths', 'Goths', 'Goti'),
	(58, 'Muisca', 'Muisca', 'Muisca'),
	(59, 'Mapuche', 'Mapuche', 'Mapuche'),
	(60, 'Tupi', 'Tupi', 'Tupi'),
	(0, 'Armenians', 'Armenians', 'Armeni'),
	(1, 'Aztec', 'Aztec', 'Aztechi'),
	(2, 'Bengalis', 'Bengalis', 'Bengalesi'),
	(3, 'Berbers', 'Berbers', 'Berberi'),
	(4, 'Bohemians', 'Bohemians', 'Boemi'),
	(5, 'Britons', 'Britons', 'Britanni'),
	(6, 'Bulgarians', 'Bulgarians', 'Bulgari'),
	(7, 'Burgundians', 'Burgundians', 'Borgognoni'),
	(8, 'Burmese', 'Burmese', 'Birmani'),
	(9, 'Byzantines', 'Byzantines', 'Bizantini'),
	(10, 'Celts', 'Celts', 'Celti'),
	(11, 'Chinese', 'Chinese', 'Cinesi'),
	(12, 'Cumans', 'Cumans', 'Cumani'),
	(13, 'Dravidians', 'Dravidians', 'Dravidi'),
	(14, 'Ethiopians', 'Ethiopians', 'Etiopi'),
	(15, 'Franks', 'Franks', 'Franchi'),
	(16, 'Georgians', 'Georgians', 'Georgiani'),
	(18, 'Gurjaras', 'Gurjaras', 'Gurjara'),
	(19, 'Huns', 'Huns', 'Unni'),
	(20, 'Incas', 'Incas', 'Inca'),
	(21, 'Indians', 'Indians', 'Indiani'),
	(22, 'Italians', 'Italians', 'Italiani'),
	(23, 'Japanese', 'Japanese', 'Giapponesi'),
	(25, 'Koreans', 'Koreans', 'Coreani'),
	(26, 'Lithuanians', 'Lithuanians', 'Lituani'),
	(27, 'Magyars', 'Magyars', 'Magieri'),
	(28, 'Malay', 'Malay', 'Malesi'),
	(29, 'Malians', 'Malians', 'Maliani'),
	(30, 'Mayans', 'Mayans', 'Maya'),
	(31, 'Mongols', 'Mongols', 'Mongoli'),
	(32, 'Persians', 'Persians', 'Persiani'),
	(33, 'Poles', 'Poles', 'Polacchi'),
	(34, 'Portuguese', 'Portuguese', 'Portoghesi'),
	(35, 'Romans', 'Romans', 'Romani'),
	(36, 'Saracens', 'Saracens', 'Saraceni'),
	(37, 'Sicilians', 'Sicilians', 'Siciliani'),
	(38, 'Slavs', 'Slavs', 'Slavi'),
	(39, 'Spanish', 'Spanish', 'Spagnoli'),
	(40, 'Tatars', 'Tatars', 'Tartari'),
	(41, 'Teutons', 'Teutons', 'Teutoni'),
	(42, 'Turks', 'Turks', 'Turchi'),
	(43, 'Vietnamese', 'Vietnamese', 'Vietnamici'),
	(44, 'Vikings', 'Vikings', 'Vichinghi'),
	(51, 'Jurchens', 'Jurchens', 'Juzhen'),
	(52, 'Khitans', 'Khitans', 'Kitai'),
	(61, 'Saxons', 'Saxons', 'Sassoni'),
	(62, 'Varangians', 'Varangians', 'Variaghi'),
	(63, 'Danes', 'Danes', 'Danesi');


--
-- Data for Name: configurazioni; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.configurazioni VALUES
	('credito_iniziale', '401', 'Credito virtuale iniziale di ogni capitano per l''asta: è il valore a cui il reset riporta il portafoglio');


--
-- Data for Name: disponibilita; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--



--
-- Data for Name: ruoli; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.ruoli VALUES
	(1, 'admin', 'Admin'),
	(2, 'streamer', 'Streamer'),
	(3, 'capitano', 'Capitano'),
	(4, 'giocatore', 'Giocatore'),
	(5, 'utente', 'Utente');


--
-- Data for Name: squadre; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.squadre VALUES
	(8, 'Squadra_1', '200000000000000002'),
	(9, 'Squadra_2', '200000000000000003'),
	(10, 'Squadra_3', '200000000000000004'),
	(11, 'Squadra_4', '200000000000000005'),
	(19, 'Squadra_5', '200000000000000006'),
	(20, 'Squadra_6', '200000000000000007'),
	(21, 'Squadra_7', '200000000000000008'),
	(22, 'Squadra_8', '200000000000000009'),
	(23, 'Squadra_9', '200000000000000010'),
	(24, 'Squadra_10', '200000000000000011');


--
-- Data for Name: ticket; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--



--
-- Data for Name: utenti_dev; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.utenti_dev VALUES
	(56, '200000000000000051', 'Giocatore_31', 'password'),
	(57, '200000000000000052', 'Giocatore_32', 'password'),
	(58, '200000000000000053', 'Giocatore_33', 'password'),
	(59, '200000000000000054', 'Giocatore_34', 'password'),
	(60, '200000000000000055', 'Giocatore_35', 'password'),
	(61, '200000000000000056', 'Giocatore_36', 'password'),
	(62, '200000000000000057', 'Giocatore_37', 'password'),
	(63, '200000000000000058', 'Giocatore_38', 'password'),
	(64, '200000000000000059', 'Giocatore_39', 'password'),
	(65, '200000000000000060', 'Giocatore_40', 'password'),
	(66, '200000000000000061', 'Giocatore_41', 'password'),
	(67, '200000000000000062', 'Giocatore_42', 'password'),
	(68, '200000000000000063', 'Giocatore_43', 'password'),
	(69, '200000000000000064', 'Giocatore_44', 'password'),
	(70, '200000000000000065', 'Giocatore_45', 'password'),
	(71, '200000000000000066', 'Giocatore_46', 'password'),
	(72, '200000000000000067', 'Giocatore_47', 'password'),
	(73, '200000000000000068', 'Giocatore_48', 'password'),
	(74, '200000000000000069', 'Giocatore_49', 'password'),
	(75, '200000000000000070', 'Giocatore_50', 'password'),
	(3, '200000000000000001', 'Admin_1', 'password'),
	(4, '200000000000000002', 'Capitano_1', 'password'),
	(5, '200000000000000003', 'Capitano_2', 'password'),
	(6, '200000000000000004', 'Capitano_3', 'password'),
	(7, '200000000000000005', 'Capitano_4', 'password'),
	(8, '200000000000000006', 'Capitano_5', 'password'),
	(9, '200000000000000007', 'Capitano_6', 'password'),
	(10, '200000000000000008', 'Capitano_7', 'password'),
	(11, '200000000000000009', 'Capitano_8', 'password'),
	(12, '200000000000000010', 'Capitano_9', 'password'),
	(13, '200000000000000011', 'Capitano_10', 'password'),
	(14, '200000000000000012', 'Giocatore_1', 'password'),
	(15, '200000000000000013', 'Giocatore_2', 'password'),
	(16, '200000000000000014', 'Giocatore_3', 'password'),
	(17, '200000000000000015', 'Giocatore_4', 'password'),
	(18, '200000000000000016', 'Giocatore_5', 'password'),
	(19, '200000000000000017', 'Giocatore_6', 'password'),
	(20, '200000000000000018', 'Giocatore_7', 'password'),
	(21, '200000000000000019', 'Giocatore_8', 'password'),
	(22, '200000000000000020', 'Giocatore_9', 'password'),
	(23, '200000000000000021', 'Giocatore_10', 'password'),
	(24, '200000000000000022', 'Giocatore_11', 'password'),
	(25, '200000000000000023', 'Giocatore_12', 'password'),
	(26, '200000000000000024', 'Giocatore_13', 'password'),
	(27, '200000000000000025', 'Giocatore_14', 'password'),
	(28, '200000000000000026', 'Giocatore_15', 'password'),
	(29, '200000000000000027', 'Giocatore_16', 'password'),
	(30, '200000000000000028', 'Giocatore_17', 'password'),
	(31, '200000000000000029', 'Giocatore_18', 'password'),
	(32, '200000000000000030', 'Giocatore_19', 'password'),
	(33, '200000000000000031', 'Giocatore_20', 'password'),
	(34, '200000000000000032', 'Giocatore_21', 'password'),
	(35, '200000000000000033', 'Giocatore_22', 'password'),
	(36, '200000000000000034', 'Giocatore_23', 'password'),
	(37, '200000000000000035', 'Giocatore_24', 'password'),
	(38, '200000000000000036', 'Giocatore_25', 'password'),
	(39, '200000000000000037', 'Giocatore_26', 'password'),
	(40, '200000000000000038', 'Giocatore_27', 'password'),
	(41, '200000000000000039', 'Giocatore_28', 'password'),
	(42, '200000000000000040', 'Giocatore_29', 'password'),
	(43, '200000000000000041', 'Giocatore_30', 'password'),
	(44, '200000000000000042', 'Utente_1', 'password'),
	(45, '200000000000000043', 'Utente_2', 'password'),
	(46, '200000000000000044', 'Utente_3', 'password'),
	(47, '200000000000000045', 'Utente_4', 'password'),
	(48, '200000000000000046', 'Utente_5', 'password'),
	(49, '200000000000000047', 'Utente_6', 'password'),
	(50, '200000000000000048', 'Utente_7', 'password'),
	(51, '200000000000000049', 'Streamer_1', 'password'),
	(52, '200000000000000050', 'Streamer_2', 'password');


--
-- Data for Name: utenti_discord; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.utenti_discord VALUES
	('200000000000000003', 'Capitano_2', NULL, NULL, 'auto', false, '2026-11-25 13:11:25.3743+00'),
	('200000000000000002', 'Capitano_1', NULL, NULL, 'auto', false, '2026-11-25 17:23:16.166718+00'),
	('200000000000000008', 'Capitano_7', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000009', 'Capitano_8', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000010', 'Capitano_9', NULL, NULL, 'auto', true, '2026-11-25 10:02:22.729539+00'),
	('200000000000000011', 'Capitano_10', NULL, NULL, 'auto', true, '2026-11-25 10:02:25.220233+00'),
	('200000000000000001', 'Admin_1', NULL, NULL, 'auto', true, '2026-11-25 17:23:38.376799+00'),
	('200000000000000013', 'Giocatore_2', NULL, NULL, 'auto', true, '2026-11-25 10:02:30.672629+00'),
	('200000000000000014', 'Giocatore_3', NULL, NULL, 'auto', true, '2026-11-25 10:02:36.50074+00'),
	('200000000000000012', 'Giocatore_1', NULL, NULL, 'auto', true, '2026-11-25 13:23:26.966393+00'),
	('200000000000000015', 'Giocatore_4', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000016', 'Giocatore_5', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000017', 'Giocatore_6', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000018', 'Giocatore_7', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000019', 'Giocatore_8', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000020', 'Giocatore_9', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000021', 'Giocatore_10', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000022', 'Giocatore_11', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000023', 'Giocatore_12', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000024', 'Giocatore_13', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000025', 'Giocatore_14', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000026', 'Giocatore_15', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000027', 'Giocatore_16', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000028', 'Giocatore_17', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000029', 'Giocatore_18', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000030', 'Giocatore_19', NULL, NULL, 'auto', false, '2026-11-25 09:45:48.93399+00'),
	('200000000000000031', 'Giocatore_20', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000032', 'Giocatore_21', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000033', 'Giocatore_22', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000049', 'Streamer_1', NULL, NULL, 'auto', false, '2026-11-25 16:35:21.078929+00'),
	('200000000000000034', 'Giocatore_23', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000035', 'Giocatore_24', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000036', 'Giocatore_25', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000037', 'Giocatore_26', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000038', 'Giocatore_27', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000039', 'Giocatore_28', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000040', 'Giocatore_29', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000041', 'Giocatore_30', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000042', 'Utente_1', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000043', 'Utente_2', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000004', 'Capitano_3', NULL, NULL, 'auto', false, '2026-11-25 10:06:31.975174+00'),
	('200000000000000005', 'Capitano_4', NULL, NULL, 'auto', false, '2026-11-25 10:06:34.471316+00'),
	('200000000000000006', 'Capitano_5', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000007', 'Capitano_6', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000044', 'Utente_3', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000045', 'Utente_4', NULL, NULL, 'auto', false, '2026-11-25 09:45:48.679349+00'),
	('200000000000000046', 'Utente_5', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000047', 'Utente_6', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000048', 'Utente_7', NULL, NULL, 'auto', false, '2026-11-24 18:37:12.627018+00'),
	('200000000000000050', 'Streamer_2', NULL, NULL, 'auto', false, '2026-11-24 21:30:25.549214+00'),
	('200000000000000051', 'Giocatore_31', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000052', 'Giocatore_32', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000053', 'Giocatore_33', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000054', 'Giocatore_34', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000055', 'Giocatore_35', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000056', 'Giocatore_36', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000057', 'Giocatore_37', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000058', 'Giocatore_38', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000059', 'Giocatore_39', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000060', 'Giocatore_40', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000061', 'Giocatore_41', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000062', 'Giocatore_42', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000063', 'Giocatore_43', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000064', 'Giocatore_44', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000065', 'Giocatore_45', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000066', 'Giocatore_46', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000067', 'Giocatore_47', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000068', 'Giocatore_48', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000069', 'Giocatore_49', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00'),
	('200000000000000070', 'Giocatore_50', NULL, NULL, 'auto', false, '2026-11-25 17:07:36.37755+00');


--
-- Data for Name: utenti_info; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.utenti_info VALUES
	('200000000000000004', true, true, 'accattivante', true),
	('200000000000000005', true, true, 'accattivante', true),
	('200000000000000003', true, true, 'accattivante', true),
	('200000000000000006', true, true, 'accattivante', true),
	('200000000000000007', true, true, 'accattivante', true),
	('200000000000000008', true, true, 'accattivante', true),
	('200000000000000009', true, true, 'accattivante', true),
	('200000000000000011', true, true, 'accattivante', true),
	('200000000000000013', true, true, 'accattivante', true),
	('200000000000000014', true, true, 'accattivante', true),
	('200000000000000015', true, true, 'accattivante', true),
	('200000000000000016', true, true, 'accattivante', true),
	('200000000000000017', true, true, 'accattivante', true),
	('200000000000000018', true, true, 'accattivante', true),
	('200000000000000019', true, true, 'accattivante', true),
	('200000000000000020', true, true, 'accattivante', true),
	('200000000000000021', true, true, 'accattivante', true),
	('200000000000000022', true, true, 'accattivante', true),
	('200000000000000023', true, true, 'accattivante', true),
	('200000000000000024', true, true, 'accattivante', true),
	('200000000000000025', true, true, 'accattivante', true),
	('200000000000000026', true, true, 'accattivante', true),
	('200000000000000027', true, true, 'accattivante', true),
	('200000000000000028', true, true, 'accattivante', true),
	('200000000000000029', true, true, 'accattivante', true),
	('200000000000000030', true, true, 'accattivante', true),
	('200000000000000031', true, true, 'accattivante', true),
	('200000000000000032', true, true, 'accattivante', true),
	('200000000000000033', true, true, 'accattivante', true),
	('200000000000000034', true, true, 'accattivante', true),
	('200000000000000035', true, true, 'accattivante', true),
	('200000000000000036', true, true, 'accattivante', true),
	('200000000000000037', true, true, 'accattivante', true),
	('200000000000000038', true, true, 'accattivante', true),
	('200000000000000039', true, true, 'accattivante', true),
	('200000000000000040', true, true, 'accattivante', true),
	('200000000000000041', true, true, 'accattivante', true),
	('200000000000000042', true, true, 'accattivante', true),
	('200000000000000043', true, true, 'accattivante', true),
	('200000000000000044', true, true, 'accattivante', true),
	('200000000000000045', true, true, 'accattivante', true),
	('200000000000000046', true, true, 'accattivante', true),
	('200000000000000047', true, true, 'accattivante', true),
	('200000000000000048', true, true, 'accattivante', true),
	('200000000000000010', false, false, 'classico', false),
	('200000000000000001', false, false, 'classico', false),
	('200000000000000002', false, false, 'classico', false),
	('200000000000000012', false, false, 'classico', false),
	('200000000000000049', false, false, 'classico', false),
	('200000000000000051', true, true, 'accattivante', true),
	('200000000000000052', true, true, 'accattivante', true),
	('200000000000000053', true, true, 'accattivante', true),
	('200000000000000054', true, true, 'accattivante', true),
	('200000000000000055', true, true, 'accattivante', true),
	('200000000000000056', true, true, 'accattivante', true),
	('200000000000000057', true, true, 'accattivante', true),
	('200000000000000058', true, true, 'accattivante', true),
	('200000000000000059', true, true, 'accattivante', true),
	('200000000000000060', true, true, 'accattivante', true),
	('200000000000000061', true, true, 'accattivante', true),
	('200000000000000062', true, true, 'accattivante', true),
	('200000000000000063', true, true, 'accattivante', true),
	('200000000000000064', true, true, 'accattivante', true),
	('200000000000000065', true, true, 'accattivante', true),
	('200000000000000050', true, true, 'accattivante', true),
	('200000000000000066', true, true, 'accattivante', true),
	('200000000000000067', true, true, 'accattivante', true),
	('200000000000000068', true, true, 'accattivante', true),
	('200000000000000069', true, true, 'accattivante', true),
	('200000000000000070', true, true, 'accattivante', true);


--
-- Data for Name: utenti_iscritti; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.utenti_iscritti VALUES
	('200000000000000002', NULL, 'Capitano_1', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 11:32:57.087738+00', 'in_validazione', 2443),
	('200000000000000010', NULL, 'Capitano_9', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-24 21:41:00.249748+00', 'validato', 1786),
	('200000000000000003', NULL, 'Capitano_2', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1020),
	('200000000000000004', NULL, 'Capitano_3', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1327),
	('200000000000000005', NULL, 'Capitano_4', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2242),
	('200000000000000006', NULL, 'Capitano_5', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2412),
	('200000000000000007', NULL, 'Capitano_6', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1906),
	('200000000000000008', NULL, 'Capitano_7', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2392),
	('200000000000000009', NULL, 'Capitano_8', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2165),
	('200000000000000011', NULL, 'Capitano_10', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2025),
	('200000000000000012', NULL, 'Giocatore_1', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2132),
	('200000000000000013', NULL, 'Giocatore_2', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1528),
	('200000000000000014', NULL, 'Giocatore_3', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1100),
	('200000000000000015', NULL, 'Giocatore_4', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2087),
	('200000000000000016', NULL, 'Giocatore_5', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1824),
	('200000000000000017', NULL, 'Giocatore_6', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1989),
	('200000000000000018', NULL, 'Giocatore_7', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2185),
	('200000000000000019', NULL, 'Giocatore_8', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1427),
	('200000000000000020', NULL, 'Giocatore_9', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1446),
	('200000000000000021', NULL, 'Giocatore_10', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 904),
	('200000000000000022', NULL, 'Giocatore_11', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2073),
	('200000000000000039', NULL, 'Giocatore_28', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1628),
	('200000000000000040', NULL, 'Giocatore_29', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2336),
	('200000000000000041', NULL, 'Giocatore_30', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1506),
	('200000000000000051', NULL, 'Giocatore_31', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1786),
	('200000000000000052', NULL, 'Giocatore_32', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1531),
	('200000000000000053', NULL, 'Giocatore_33', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1158),
	('200000000000000054', NULL, 'Giocatore_34', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 2307),
	('200000000000000055', NULL, 'Giocatore_35', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 2393),
	('200000000000000056', NULL, 'Giocatore_36', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1800),
	('200000000000000057', NULL, 'Giocatore_37', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1350),
	('200000000000000058', NULL, 'Giocatore_38', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 912),
	('200000000000000059', NULL, 'Giocatore_39', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1850),
	('200000000000000060', NULL, 'Giocatore_40', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1098),
	('200000000000000061', NULL, 'Giocatore_41', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 2311),
	('200000000000000062', NULL, 'Giocatore_42', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 2270),
	('200000000000000063', NULL, 'Giocatore_43', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1621),
	('200000000000000064', NULL, 'Giocatore_44', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1347),
	('200000000000000065', NULL, 'Giocatore_45', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 936),
	('200000000000000023', NULL, 'Giocatore_12', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2094),
	('200000000000000024', NULL, 'Giocatore_13', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1758),
	('200000000000000025', NULL, 'Giocatore_14', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2392),
	('200000000000000026', NULL, 'Giocatore_15', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2253),
	('200000000000000027', NULL, 'Giocatore_16', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1962),
	('200000000000000028', NULL, 'Giocatore_17', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1440),
	('200000000000000029', NULL, 'Giocatore_18', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1662),
	('200000000000000030', NULL, 'Giocatore_19', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2410),
	('200000000000000031', NULL, 'Giocatore_20', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 906),
	('200000000000000032', NULL, 'Giocatore_21', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1994),
	('200000000000000033', NULL, 'Giocatore_22', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1341),
	('200000000000000034', NULL, 'Giocatore_23', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2266),
	('200000000000000035', NULL, 'Giocatore_24', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1689),
	('200000000000000036', NULL, 'Giocatore_25', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1029),
	('200000000000000037', NULL, 'Giocatore_26', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 2135),
	('200000000000000038', NULL, 'Giocatore_27', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 12:13:19.418393+00', 'in_validazione', 1489),
	('200000000000000066', NULL, 'Giocatore_46', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1115),
	('200000000000000067', NULL, 'Giocatore_47', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1925),
	('200000000000000068', NULL, 'Giocatore_48', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 2117),
	('200000000000000069', NULL, 'Giocatore_49', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'validato', 1014),
	('200000000000000070', NULL, 'Giocatore_50', 'da_calcolare', 0, NULL, NULL, 'nessuno', '', '2026-11-25 17:07:36.37755+00', 'in_validazione', 964);


--
-- Data for Name: utenti_ruoli; Type: TABLE DATA; Schema: fanta_torneo; Owner: -
--

INSERT INTO fanta_torneo.utenti_ruoli VALUES
	('200000000000000011', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000010', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000009', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000008', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000007', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000006', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000005', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000004', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000003', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000002', 3, '2026-11-25 12:03:54.053319+00'),
	('200000000000000011', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000010', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000009', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000008', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000007', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000006', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000005', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000004', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000003', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000002', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000050', 2, '2026-11-25 12:03:54.053319+00'),
	('200000000000000049', 2, '2026-11-25 12:03:54.053319+00'),
	('200000000000000050', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000049', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000041', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000040', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000039', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000038', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000037', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000036', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000035', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000034', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000033', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000032', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000031', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000030', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000029', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000028', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000027', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000026', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000025', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000024', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000023', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000022', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000021', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000020', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000019', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000018', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000017', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000016', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000015', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000014', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000013', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000012', 4, '2026-11-25 12:03:54.053319+00'),
	('200000000000000048', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000047', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000046', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000045', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000044', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000043', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000042', 5, '2026-11-25 12:03:54.053319+00'),
	('200000000000000001', 1, '2026-11-25 12:06:17.39183+00'),
	('200000000000000001', 5, '2026-11-25 12:06:17.39183+00'),
	('200000000000000051', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000052', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000053', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000054', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000055', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000056', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000057', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000058', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000059', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000060', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000061', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000062', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000063', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000064', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000065', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000066', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000067', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000068', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000069', 4, '2026-11-25 17:07:36.37755+00'),
	('200000000000000070', 4, '2026-11-25 17:07:36.37755+00');


--
-- Name: asta_giocatori_acquistati_id_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.asta_giocatori_acquistati_id_seq', 46, true);


--
-- Name: disponibilita_id_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.disponibilita_id_seq', 1, false);


--
-- Name: ruoli_id_ruolo_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.ruoli_id_ruolo_seq', 5, true);


--
-- Name: squadre_id_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.squadre_id_seq', 27, true);


--
-- Name: ticket_id_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.ticket_id_seq', 1, false);


--
-- Name: utenti_dev_id_seq; Type: SEQUENCE SET; Schema: fanta_torneo; Owner: -
--

SELECT pg_catalog.setval('fanta_torneo.utenti_dev_id_seq', 75, true);


--
-- Name: asta_giocatori_acquistati asta_giocatori_acquistati_id_iscritto_key; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_giocatori_acquistati
    ADD CONSTRAINT asta_giocatori_acquistati_id_iscritto_key UNIQUE (id_iscritto);


--
-- Name: asta_giocatori_acquistati asta_giocatori_acquistati_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_giocatori_acquistati
    ADD CONSTRAINT asta_giocatori_acquistati_pkey PRIMARY KEY (id);


--
-- Name: asta_ordine_capitani asta_ordine_capitani_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_ordine_capitani
    ADD CONSTRAINT asta_ordine_capitani_pkey PRIMARY KEY (id_utente);


--
-- Name: asta_portafoglio_utente asta_portafoglio_utente_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_portafoglio_utente
    ADD CONSTRAINT asta_portafoglio_utente_pkey PRIMARY KEY (id_utente);


--
-- Name: civilta civilta_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.civilta
    ADD CONSTRAINT civilta_pkey PRIMARY KEY (id);


--
-- Name: configurazioni configurazioni_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.configurazioni
    ADD CONSTRAINT configurazioni_pkey PRIMARY KEY (chiave);


--
-- Name: disponibilita disponibilita_discord_id_data_key; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.disponibilita
    ADD CONSTRAINT disponibilita_discord_id_data_key UNIQUE (discord_id, data);


--
-- Name: disponibilita disponibilita_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.disponibilita
    ADD CONSTRAINT disponibilita_pkey PRIMARY KEY (id);


--
-- Name: ruoli ruoli_chiave_ruolo_key; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.ruoli
    ADD CONSTRAINT ruoli_chiave_ruolo_key UNIQUE (chiave_ruolo);


--
-- Name: ruoli ruoli_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.ruoli
    ADD CONSTRAINT ruoli_pkey PRIMARY KEY (id_ruolo);


--
-- Name: squadre squadre_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.squadre
    ADD CONSTRAINT squadre_pkey PRIMARY KEY (id);


--
-- Name: ticket ticket_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.ticket
    ADD CONSTRAINT ticket_pkey PRIMARY KEY (id);


--
-- Name: utenti_dev utenti_dev_id_discord_key; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_dev
    ADD CONSTRAINT utenti_dev_id_discord_key UNIQUE (id_discord);


--
-- Name: utenti_dev utenti_dev_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_dev
    ADD CONSTRAINT utenti_dev_pkey PRIMARY KEY (id);


--
-- Name: utenti_dev utenti_dev_username_key; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_dev
    ADD CONSTRAINT utenti_dev_username_key UNIQUE (username);


--
-- Name: utenti_discord utenti_discord_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_discord
    ADD CONSTRAINT utenti_discord_pkey PRIMARY KEY (discord_id);


--
-- Name: utenti_info utenti_info_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_info
    ADD CONSTRAINT utenti_info_pkey PRIMARY KEY (id_discord);


--
-- Name: utenti_iscritti utenti_iscritti_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_iscritti
    ADD CONSTRAINT utenti_iscritti_pkey PRIMARY KEY (id_discord);


--
-- Name: utenti_ruoli utenti_ruoli_pkey; Type: CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_ruoli
    ADD CONSTRAINT utenti_ruoli_pkey PRIMARY KEY (id_utente, id_ruolo);


--
-- Name: idx_disp_data; Type: INDEX; Schema: fanta_torneo; Owner: -
--

CREATE INDEX idx_disp_data ON fanta_torneo.disponibilita USING btree (data);


--
-- Name: idx_ticket_stato; Type: INDEX; Schema: fanta_torneo; Owner: -
--

CREATE INDEX idx_ticket_stato ON fanta_torneo.ticket USING btree (stato, creato_il DESC);


--
-- Name: utenti_ruoli utenti_ruoli_data; Type: TRIGGER; Schema: fanta_torneo; Owner: -
--

CREATE TRIGGER utenti_ruoli_data BEFORE UPDATE ON fanta_torneo.utenti_ruoli FOR EACH ROW EXECUTE FUNCTION fanta_torneo.utenti_ruoli_aggiorna_data();


--
-- Name: asta_giocatori_acquistati asta_giocatori_acquistati_id_iscritto_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_giocatori_acquistati
    ADD CONSTRAINT asta_giocatori_acquistati_id_iscritto_fkey FOREIGN KEY (id_iscritto) REFERENCES fanta_torneo.utenti_iscritti(id_discord);


--
-- Name: asta_giocatori_acquistati asta_giocatori_acquistati_id_squadra_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_giocatori_acquistati
    ADD CONSTRAINT asta_giocatori_acquistati_id_squadra_fkey FOREIGN KEY (id_squadra) REFERENCES fanta_torneo.squadre(id);


--
-- Name: asta_ordine_capitani asta_ordine_capitani_id_utente_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_ordine_capitani
    ADD CONSTRAINT asta_ordine_capitani_id_utente_fkey FOREIGN KEY (id_utente) REFERENCES fanta_torneo.utenti_discord(discord_id);


--
-- Name: asta_portafoglio_utente asta_portafoglio_utente_id_utente_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.asta_portafoglio_utente
    ADD CONSTRAINT asta_portafoglio_utente_id_utente_fkey FOREIGN KEY (id_utente) REFERENCES fanta_torneo.utenti_discord(discord_id);


--
-- Name: squadre squadre_id_capitano_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.squadre
    ADD CONSTRAINT squadre_id_capitano_fkey FOREIGN KEY (id_capitano) REFERENCES fanta_torneo.utenti_discord(discord_id);


--
-- Name: utenti_info utenti_info_id_discord_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_info
    ADD CONSTRAINT utenti_info_id_discord_fkey FOREIGN KEY (id_discord) REFERENCES fanta_torneo.utenti_discord(discord_id) ON DELETE CASCADE;


--
-- Name: utenti_iscritti utenti_iscritti_id_discord_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_iscritti
    ADD CONSTRAINT utenti_iscritti_id_discord_fkey FOREIGN KEY (id_discord) REFERENCES fanta_torneo.utenti_discord(discord_id) ON DELETE CASCADE;


--
-- Name: utenti_iscritti utenti_iscritti_id_squadra_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_iscritti
    ADD CONSTRAINT utenti_iscritti_id_squadra_fkey FOREIGN KEY (id_squadra) REFERENCES fanta_torneo.squadre(id);


--
-- Name: utenti_ruoli utenti_ruoli_id_ruolo_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_ruoli
    ADD CONSTRAINT utenti_ruoli_id_ruolo_fkey FOREIGN KEY (id_ruolo) REFERENCES fanta_torneo.ruoli(id_ruolo);


--
-- Name: utenti_ruoli utenti_ruoli_id_utente_fkey; Type: FK CONSTRAINT; Schema: fanta_torneo; Owner: -
--

ALTER TABLE ONLY fanta_torneo.utenti_ruoli
    ADD CONSTRAINT utenti_ruoli_id_utente_fkey FOREIGN KEY (id_utente) REFERENCES fanta_torneo.utenti_info(id_discord) ON DELETE CASCADE;


--
-- PostgreSQL database dump complete
--

\unrestrict PcO4AQMa8hYIbeWvBKUVZGv0OJGc9kc8hzFjnfHaGaCoRfCcXA3dQQ6LT1l009T

