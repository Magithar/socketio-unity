/**
 * Mirror Integration Server — Socket.IO + Mirror hybrid test server
 *
 * Extends the lobby server with:
 *   - /game namespace for in-match backend events (score_update, player_killed)
 *   - hostAddress forwarded in match_started so Mirror clients can call StartClient()
 *   - HTTP test endpoints to fire game events from a browser during local testing
 *
 * Namespaces:
 *   /lobby  — matchmaking, rooms, session identity (same as lobby-server.js)
 *   /game   — in-match server-authoritative events
 *
 * Events (client → server, /lobby):
 *   create_room       { name }                          → ack { ok, roomId, playerId, sessionToken }
 *   join_room         { roomId, name }                  → ack { ok, roomId, playerId, sessionToken }
 *   reconnect_player  { playerId, roomId, sessionToken } → ack { ok, roomId, playerId }
 *   player_ready      { ready }
 *   start_match       { sceneName?, hostAddress? }       — host only; hostAddress = host LAN IP for P2P
 *   leave_room        {}                                → ack { ok }
 *
 * Events (server → client, /lobby):
 *   player_identity   { playerId, sessionToken }
 *   room_state        JSON snapshot
 *   match_started     { sceneName, hostAddress }
 *   player_removed    { playerId, name, reason }
 *
 * Events (server → client, /game):
 *   score_update      { playerId, score }
 *   player_killed     { victimId, killerId? }
 *   round_end         { winnerId? }
 *
 * HTTP test endpoints (DEVELOPMENT ONLY):
 *   GET /test                        — list active rooms and players
 *   GET /test/score?roomId=X&playerId=Y&score=Z  — emit score_update to room
 *   GET /test/kill?roomId=X&victimId=Y&killerId=Z — emit player_killed to room
 *   GET /test/round-end?roomId=X&winnerId=Y       — emit round_end to room
 *
 * DEVELOPMENT SERVER ONLY — no auth, rate-limiting, or abuse protection.
 */

'use strict';

const express    = require('express');
const http       = require('http');
const { Server } = require('socket.io');
const os         = require('os');

const app        = express();
const httpServer = http.createServer(app);
const io         = new Server(httpServer, {
    cors: { origin: '*', methods: ['GET', 'POST'] },
    pingInterval: 25_000,
    pingTimeout:  20_000,
});

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const PORT               = parseInt(process.env.PORT, 10) || 3002;
const TEST_TOKEN          = process.env.TEST_TOKEN || null;
const RECONNECT_GRACE_MS = 10_000;

const MIRROR_SERVER_ADDRESS = process.env.MIRROR_SERVER_ADDRESS || null;
const MIRROR_KCP_PORT       = parseInt(process.env.MIRROR_KCP_PORT, 10) || null;
const MIRROR_WS_PORT        = parseInt(process.env.MIRROR_WS_PORT,  10) || null;
const ROOM_CODE_CHARS    = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const ROOM_CODE_LENGTH   = 6;

// ---------------------------------------------------------------------------
// Helpers — local IP (shown in startup log so you can paste it into Unity)
// ---------------------------------------------------------------------------

function getLocalIP() {
    const ifaces = os.networkInterfaces();
    for (const name of Object.keys(ifaces)) {
        for (const iface of ifaces[name]) {
            if (iface.family === 'IPv4' && !iface.internal)
                return iface.address;
        }
    }
    return '127.0.0.1';
}

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

const rooms          = new Map(); // roomId → Room
const socketToPlayer = new Map(); // socket.id → { playerId, roomId }

// ---------------------------------------------------------------------------
// Utility
// ---------------------------------------------------------------------------

function generateRoomId() {
    let id;
    do {
        id = Array.from({ length: ROOM_CODE_LENGTH }, () =>
            ROOM_CODE_CHARS[Math.floor(Math.random() * ROOM_CODE_CHARS.length)]
        ).join('');
    } while (rooms.has(id));
    return id;
}

function generateId() {
    return Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}

function generateTraceId() {
    return Math.random().toString(36).slice(2, 8);
}

function shortSocket(id) { return id ? id.slice(0, 6) : '?'; }

function lobbyLog(traceId, roomId, playerId, msg) {
    const t = traceId  ? `[T:${traceId}]`   : '';
    const r = roomId   ? `[Room:${roomId}]`  : '';
    const p = playerId ? `[P:${playerId}]`   : '';
    console.log(`[Mirror]${t}${r}${p} ${msg}`);
}

function validateName(name) {
    if (!name || typeof name !== 'string') return false;
    const trimmed = name.trim();
    return trimmed.length > 0 && trimmed.length <= 32;
}

const lastEventTime = new Map();
function isRateLimited(socketId, eventName) {
    let events = lastEventTime.get(socketId);
    if (!events) { events = new Map(); lastEventTime.set(socketId, events); }
    const now = Date.now(), last = events.get(eventName) || 0;
    if (now - last < 100) return true;
    events.set(eventName, now);
    return false;
}

function parsePayload(data) {
    if (typeof data === 'string') { try { return JSON.parse(data); } catch { return {}; } }
    return data || {};
}

function broadcastRoomState(roomId) {
    const room = rooms.get(roomId);
    if (!room) return;
    const state = {
        roomId:  room.roomId,
        hostId:  room.hostId,
        version: ++room.version,
        players: Array.from(room.players.values()).map(p => ({
            id: p.id, name: p.name, ready: p.ready, status: p.status,
        })),
    };
    io.of('/lobby').to(roomId).emit('room_state', JSON.stringify(state));
    console.log(`[Mirror][Room:${roomId}] state broadcast v${state.version} players=${state.players.length} host=${room.hostId}`);
}

function removePlayerFromRoom(playerId, roomId, reason = 'left') {
    const room = rooms.get(roomId);
    if (!room) return;
    const player = room.players.get(playerId);
    if (!player) return;

    if (player.reconnectTimer) { clearTimeout(player.reconnectTimer); player.reconnectTimer = null; }
    room.players.delete(playerId);

    if (room.players.size === 0) {
        rooms.delete(roomId);
        lobbyLog(player.traceId, roomId, playerId, `🗑  room deleted (empty) reason=${reason}`);
        return;
    }

    io.of('/lobby').to(roomId).emit('player_removed', JSON.stringify({ playerId, name: player.name, reason }));

    if (room.hostId === playerId) {
        const nextHost =
            [...room.players.values()].find(p => p.status === 'connected') ||
            room.players.values().next().value;
        room.hostId = nextHost.id;
        lobbyLog(nextHost.traceId, roomId, null, `👑 host migrated ${playerId} → ${nextHost.id}`);
    }

    broadcastRoomState(roomId);
}

// ---------------------------------------------------------------------------
// Namespace: /lobby
// ---------------------------------------------------------------------------

const lobby = io.of('/lobby');

lobby.on('connection', socket => {
    console.log(`[Mirror] 🔌 socket connected: ${socket.id}`);

    socket.on('create_room', (data, ack) => {
        const { name } = parsePayload(data);
        if (!validateName(name))
            return ack(JSON.stringify({ ok: false, error: 'Name required (max 32 chars)' }));

        const roomId       = generateRoomId();
        const playerId     = generateId();
        const sessionToken = generateId() + generateId();
        const traceId      = generateTraceId();
        const player = {
            id: playerId, socketId: socket.id, sessionToken, traceId,
            name: name.trim(), ready: false, status: 'connected', roomId, reconnectTimer: null,
        };
        rooms.set(roomId, { roomId, hostId: playerId, version: 0, players: new Map([[playerId, player]]) });
        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(traceId, roomId, playerId, `🏠 room created by "${player.name}" socket=${shortSocket(socket.id)}`);
        socket.emit('player_identity', JSON.stringify({ playerId, sessionToken }));
        ack(JSON.stringify({ ok: true, roomId, playerId, sessionToken }));
        broadcastRoomState(roomId);
    });

    socket.on('join_room', (data, ack) => {
        const { roomId: rawId, name } = parsePayload(data);
        const roomId = (rawId || '').toUpperCase();

        if (!validateName(name))
            return ack(JSON.stringify({ ok: false, error: 'Name required (max 32 chars)' }));

        const room = rooms.get(roomId);
        if (!room)
            return ack(JSON.stringify({ ok: false, error: 'Room not found' }));

        const playerId     = generateId();
        const sessionToken = generateId() + generateId();
        const traceId      = generateTraceId();
        const player = {
            id: playerId, socketId: socket.id, sessionToken, traceId,
            name: name.trim(), ready: false, status: 'connected', roomId, reconnectTimer: null,
        };

        room.players.set(playerId, player);
        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(traceId, roomId, playerId, `🚪 "${player.name}" joined socket=${shortSocket(socket.id)}`);
        socket.emit('player_identity', JSON.stringify({ playerId, sessionToken }));
        ack(JSON.stringify({ ok: true, roomId, playerId, sessionToken }));
        broadcastRoomState(roomId);
    });

    socket.on('reconnect_player', (data, ack) => {
        const { playerId, roomId, sessionToken } = parsePayload(data);
        const room = rooms.get(roomId);
        if (!room) return ack(JSON.stringify({ ok: false, error: 'Room no longer exists' }));
        const player = room.players.get(playerId);
        if (!player) return ack(JSON.stringify({ ok: false, error: 'Player session expired' }));
        if (!sessionToken || sessionToken !== player.sessionToken)
            return ack(JSON.stringify({ ok: false, error: 'Invalid session token' }));

        if (player.reconnectTimer) { clearTimeout(player.reconnectTimer); player.reconnectTimer = null; }
        const oldSocketId = player.socketId;
        if (oldSocketId && oldSocketId !== socket.id) socketToPlayer.delete(oldSocketId);
        player.socketId = socket.id;
        player.status   = 'connected';
        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(player.traceId, roomId, playerId,
            `♻️  "${player.name}" reconnected socket ${shortSocket(oldSocketId)} → ${shortSocket(socket.id)}`);
        ack(JSON.stringify({ ok: true, roomId, playerId }));
        broadcastRoomState(roomId);
    });

    socket.on('player_ready', data => {
        if (isRateLimited(socket.id, 'player_ready')) return;
        const entry = socketToPlayer.get(socket.id);
        if (!entry) return;
        const { playerId, roomId } = entry;
        const room   = rooms.get(roomId);
        const player = room?.players.get(playerId);
        if (!player) return;
        const { ready } = parsePayload(data);
        player.ready = typeof ready === 'boolean' ? ready : !player.ready;
        broadcastRoomState(roomId);
    });

    // ------------------------------------------------------------------
    // start_match — host only
    // hostAddress: host's LAN IP for P2P, or dedicated server address.
    // Null is valid — MirrorGameOrchestrator falls back to localhost in editor builds.
    // ------------------------------------------------------------------
    socket.on('start_match', data => {
        if (isRateLimited(socket.id, 'start_match')) return;
        const entry = socketToPlayer.get(socket.id);
        if (!entry) return;
        const { playerId, roomId } = entry;

        const room = rooms.get(roomId);
        if (!room || room.hostId !== playerId) return;

        const { sceneName = null, hostAddress: clientHostAddress = null } = parsePayload(data);

        // Dedicated server env vars take priority over client-provided P2P address.
        const hostAddress = MIRROR_SERVER_ADDRESS || clientHostAddress;
        const kcpPort     = MIRROR_SERVER_ADDRESS ? MIRROR_KCP_PORT : null;
        const wsPort      = MIRROR_SERVER_ADDRESS ? MIRROR_WS_PORT  : null;

        const host = room.players.get(playerId);
        lobbyLog(host?.traceId, roomId, playerId,
            `🎮 match started scene=${sceneName ?? '(none)'} hostAddress=${hostAddress ?? '(none)'} kcpPort=${kcpPort ?? '(none)'} wsPort=${wsPort ?? '(none)'}`);
        io.of('/lobby').to(roomId).emit('match_started', JSON.stringify({ sceneName, hostAddress, kcpPort, wsPort }));
    });

    socket.on('leave_room', (data, ack) => {
        const entry = socketToPlayer.get(socket.id);
        if (!entry) { if (ack) ack(JSON.stringify({ ok: true })); return; }
        const { playerId, roomId } = entry;
        const leavingPlayer = rooms.get(roomId)?.players.get(playerId);
        socketToPlayer.delete(socket.id);
        socket.leave(roomId);
        removePlayerFromRoom(playerId, roomId, 'left');
        lobbyLog(leavingPlayer?.traceId, roomId, playerId, `🚶 "${leavingPlayer?.name}" left`);
        if (ack) ack(JSON.stringify({ ok: true }));
    });

    socket.on('disconnect', () => {
        lastEventTime.delete(socket.id);
        const entry = socketToPlayer.get(socket.id);
        if (!entry) {
            console.log(`[Mirror] 🔌 socket disconnected (no session): ${socket.id}`);
            return;
        }
        const { playerId, roomId } = entry;
        socketToPlayer.delete(socket.id);
        const room   = rooms.get(roomId);
        const player = room?.players.get(playerId);
        if (!player) return;

        player.status = 'disconnected';
        lobbyLog(player.traceId, roomId, playerId,
            `⚠️  "${player.name}" disconnected — grace ${RECONNECT_GRACE_MS / 1000}s started`);
        broadcastRoomState(roomId);

        player.reconnectTimer = setTimeout(() => {
            lobbyLog(player.traceId, roomId, playerId, `❌ "${player.name}" grace expired — removing`);
            player.reconnectTimer = null;
            removePlayerFromRoom(playerId, roomId, 'reconnect_timeout');
        }, RECONNECT_GRACE_MS);
    });
});

// ---------------------------------------------------------------------------
// Namespace: /game
// In-match server-authoritative events.
// Clients subscribe via GameEventBridge.
// Events are fired from the HTTP test endpoints below.
// ---------------------------------------------------------------------------

io.of('/game').on('connection', socket => {
    console.log(`[Mirror][/game] 🎮 client connected: ${socket.id}`);
    socket.on('disconnect', () => {
        console.log(`[Mirror][/game] client disconnected: ${socket.id}`);
    });
});

// Helper: emit a /game event to every socket in a lobby room.
// The /game namespace has no concept of Socket.IO rooms, so we look up
// each player's socketId from the lobby room and emit individually.
function emitToRoom(roomId, event, payload) {
    const room = rooms.get(roomId);
    if (!room) return false;
    const gameNs = io.of('/game');
    for (const player of room.players.values()) {
        const sock = gameNs.sockets.get(player.socketId);
        if (sock) sock.emit(event, JSON.stringify(payload));
    }
    return true;
}

// ---------------------------------------------------------------------------
// HTTP test endpoints
// Open these in a browser while the game is running to fire events.
// ---------------------------------------------------------------------------

// Require ?token=... on all /test/* endpoints when TEST_TOKEN is set (prod deploys).
// When unset (local dev), endpoints are open.
function requireTestToken(req, res, next) {
    if (!TEST_TOKEN) return next();
    if (req.query.token === TEST_TOKEN) return next();
    return res.status(401).json({ ok: false, error: 'Invalid or missing token' });
}

app.use('/test', requireTestToken);

// GET /test — active rooms overview
app.get('/test', (req, res) => {
    const data = [...rooms.values()].map(room => ({
        roomId:  room.roomId,
        hostId:  room.hostId,
        version: room.version,
        players: [...room.players.values()].map(p => ({
            id: p.id, name: p.name, ready: p.ready, status: p.status,
        })),
    }));
    res.json(data);
});

// GET /test/score?roomId=X&playerId=Y&score=Z
// Emits score_update { playerId, score } to the /game namespace for that room.
app.get('/test/score', (req, res) => {
    const { roomId, playerId, score = '10' } = req.query;
    if (!roomId || !playerId)
        return res.status(400).json({ ok: false, error: 'roomId and playerId required' });

    const payload = { playerId, score: parseInt(score, 10) || 0 };
    const sent    = emitToRoom(roomId, 'score_update', payload);

    if (!sent) return res.status(404).json({ ok: false, error: 'Room not found' });
    console.log(`[Mirror][/test/score] → room=${roomId} playerId=${playerId} score=${payload.score}`);
    res.json({ ok: true, event: 'score_update', payload });
});

// GET /test/kill?roomId=X&victimId=Y&killerId=Z
// Emits player_killed { victimId, killerId } to the /game namespace for that room.
app.get('/test/kill', (req, res) => {
    const { roomId, victimId, killerId = null } = req.query;
    if (!roomId || !victimId)
        return res.status(400).json({ ok: false, error: 'roomId and victimId required' });

    const payload = { victimId, killerId: killerId || null };
    const sent    = emitToRoom(roomId, 'player_killed', payload);

    if (!sent) return res.status(404).json({ ok: false, error: 'Room not found' });
    console.log(`[Mirror][/test/kill] → room=${roomId} victimId=${victimId} killerId=${killerId}`);
    res.json({ ok: true, event: 'player_killed', payload });
});

// GET /test/round-end?roomId=X&winnerId=Y
// Emits round_end { winnerId } to the /game namespace for that room.
app.get('/test/round-end', (req, res) => {
    const { roomId, winnerId = null } = req.query;
    if (!roomId)
        return res.status(400).json({ ok: false, error: 'roomId required' });

    const payload = { winnerId: winnerId || null };
    const sent    = emitToRoom(roomId, 'round_end', payload);

    if (!sent) return res.status(404).json({ ok: false, error: 'Room not found' });
    console.log(`[Mirror][/test/round-end] → room=${roomId} winnerId=${winnerId}`);
    res.json({ ok: true, event: 'round_end', payload });
});

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------

httpServer.listen(PORT, () => {
    const localIP = getLocalIP();
    console.log(`🚀 Mirror integration server running on http://localhost:${PORT}`);
    console.log(`🛰  Namespaces: /lobby (matchmaking)  /game (in-match events)`);
    console.log(`🌐 LAN IP: ${localIP}  ← use this as hostAddress for P2P testing`);
    console.log('');
    console.log('Test endpoints:');
    console.log(`  http://localhost:${PORT}/test                                              — list rooms`);
    console.log(`  http://localhost:${PORT}/test/score?roomId=XXXX&playerId=YYY&score=50      — fire score_update`);
    console.log(`  http://localhost:${PORT}/test/kill?roomId=XXXX&victimId=YYY               — fire player_killed`);
    console.log(`  http://localhost:${PORT}/test/round-end?roomId=XXXX&winnerId=YYY          — fire round_end`);
});
