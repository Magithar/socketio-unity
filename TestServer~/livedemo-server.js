/**
 * LiveDemo Server — combined lobby + playersync for the WebGL demo
 *
 * Merges lobby-server.js (/lobby namespace) and playersync-server.js
 * (/playersync namespace) into a single process for easy deployment.
 *
 * Deploy to Render/Railway/Fly with:
 *   Build: npm install
 *   Start: node livedemo-server.js
 */

'use strict';

const express    = require('express');
const http       = require('http');
const { Server } = require('socket.io');

const app        = express();
const httpServer = http.createServer(app);
const io         = new Server(httpServer, {
    cors: { origin: '*', methods: ['GET', 'POST'] },
    pingInterval: 25_000,
    pingTimeout:  20_000,
});

const PORT = process.env.PORT || 3000;

// =========================================================================
// Health check
// =========================================================================

app.get('/', (_req, res) => {
    res.json({ status: 'ok', namespaces: ['/lobby', '/playersync'] });
});

// =========================================================================
// /lobby namespace  (from lobby-server.js)
// =========================================================================

const RECONNECT_GRACE_MS = 10_000;
const ROOM_CODE_CHARS    = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const ROOM_CODE_LENGTH   = 6;

const rooms          = new Map();
const socketToPlayer = new Map();

function generateRoomId() {
    let id;
    do {
        id = Array.from({ length: ROOM_CODE_LENGTH }, () =>
            ROOM_CODE_CHARS[Math.floor(Math.random() * ROOM_CODE_CHARS.length)]
        ).join('');
    } while (rooms.has(id));
    return id;
}

function generatePlayerId() {
    return Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}

function generateSessionToken() {
    return Math.random().toString(36).slice(2) +
           Math.random().toString(36).slice(2) +
           Date.now().toString(36);
}

function generateTraceId() {
    return Math.random().toString(36).slice(2, 8);
}

function shortSocket(id) {
    return id ? id.slice(0, 6) : '?';
}

function lobbyLog(traceId, roomId, playerId, msg) {
    const t = traceId  ? `[T:${traceId}]`   : '';
    const r = roomId   ? `[Room:${roomId}]`  : '';
    const p = playerId ? `[P:${playerId}]`   : '';
    console.log(`[Lobby]${t}${r}${p} ${msg}`);
}

function validateName(name) {
    if (!name || typeof name !== 'string') return false;
    const trimmed = name.trim();
    return trimmed.length > 0 && trimmed.length <= 32;
}

const lastEventTime = new Map();

function isRateLimited(socketId, eventName) {
    let events = lastEventTime.get(socketId);
    if (!events) {
        events = new Map();
        lastEventTime.set(socketId, events);
    }
    const now  = Date.now();
    const last = events.get(eventName) || 0;
    if (now - last < 100) return true;
    events.set(eventName, now);
    return false;
}

function parsePayload(data) {
    if (typeof data === 'string') {
        try { return JSON.parse(data); } catch { return {}; }
    }
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
            id:     p.id,
            name:   p.name,
            ready:  p.ready,
            status: p.status,
        })),
    };

    io.of('/lobby').to(roomId).emit('room_state', JSON.stringify(state));
    console.log(`[Lobby][Room:${roomId}] state broadcast v${state.version} players=${state.players.length} host=${room.hostId}`);
}

function removePlayerFromRoom(playerId, roomId, reason = 'left') {
    const room = rooms.get(roomId);
    if (!room) return;

    const player = room.players.get(playerId);
    if (!player) return;

    if (player.reconnectTimer) {
        clearTimeout(player.reconnectTimer);
        player.reconnectTimer = null;
    }

    room.players.delete(playerId);

    if (room.players.size === 0) {
        rooms.delete(roomId);
        lobbyLog(player.traceId, roomId, playerId, `room deleted (empty) reason=${reason}`);
        return;
    }

    io.of('/lobby').to(roomId).emit('player_removed', JSON.stringify({
        playerId, name: player.name, reason,
    }));

    if (room.hostId === playerId) {
        const nextHost =
            [...room.players.values()].find(p => p.status === 'connected') ||
            room.players.values().next().value;
        room.hostId = nextHost.id;
        lobbyLog(nextHost.traceId, roomId, null, `host migrated ${playerId} → ${nextHost.id}`);
    }

    broadcastRoomState(roomId);
}

const lobby = io.of('/lobby');

lobby.on('connection', socket => {
    console.log(`[Lobby] socket connected: ${socket.id}`);

    socket.on('create_room', (data, ack) => {
        const { name } = parsePayload(data);
        if (!validateName(name))
            return ack(JSON.stringify({ ok: false, error: 'Name required (max 32 chars)' }));

        const roomId       = generateRoomId();
        const playerId     = generatePlayerId();
        const sessionToken = generateSessionToken();
        const traceId      = generateTraceId();
        const player       = {
            id: playerId, socketId: socket.id, sessionToken, traceId, name: name.trim(),
            ready: false, status: 'connected', roomId, reconnectTimer: null,
        };
        const room = {
            roomId, hostId: playerId, version: 0,
            players: new Map([[playerId, player]]),
        };

        rooms.set(roomId, room);
        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(traceId, roomId, playerId, `room created by "${player.name}" socket=${shortSocket(socket.id)}`);
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

        const playerId     = generatePlayerId();
        const sessionToken = generateSessionToken();
        const traceId      = generateTraceId();
        const player       = {
            id: playerId, socketId: socket.id, sessionToken, traceId, name: name.trim(),
            ready: false, status: 'connected', roomId, reconnectTimer: null,
        };

        room.players.set(playerId, player);
        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(traceId, roomId, playerId, `"${player.name}" joined socket=${shortSocket(socket.id)}`);
        socket.emit('player_identity', JSON.stringify({ playerId, sessionToken }));
        ack(JSON.stringify({ ok: true, roomId, playerId, sessionToken }));
        broadcastRoomState(roomId);
    });

    socket.on('reconnect_player', (data, ack) => {
        const { playerId, roomId, sessionToken } = parsePayload(data);

        const room = rooms.get(roomId);
        if (!room)
            return ack(JSON.stringify({ ok: false, error: 'Room no longer exists' }));

        const player = room.players.get(playerId);
        if (!player)
            return ack(JSON.stringify({ ok: false, error: 'Player session expired' }));

        if (!sessionToken || sessionToken !== player.sessionToken)
            return ack(JSON.stringify({ ok: false, error: 'Invalid session token' }));

        if (player.reconnectTimer) {
            clearTimeout(player.reconnectTimer);
            player.reconnectTimer = null;
        }

        const oldSocketId = player.socketId;
        if (oldSocketId && oldSocketId !== socket.id)
            socketToPlayer.delete(oldSocketId);

        player.socketId = socket.id;
        player.status   = 'connected';

        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(player.traceId, roomId, playerId,
            `"${player.name}" reconnected socket ${shortSocket(oldSocketId)} → ${shortSocket(socket.id)}`);
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

    socket.on('start_match', data => {
        if (isRateLimited(socket.id, 'start_match')) return;
        const entry = socketToPlayer.get(socket.id);
        if (!entry) return;
        const { playerId, roomId } = entry;

        const room = rooms.get(roomId);
        if (!room || room.hostId !== playerId) return;

        const { sceneName = null } = parsePayload(data);
        const host = room.players.get(playerId);
        lobbyLog(host?.traceId, roomId, playerId, `match started scene=${sceneName}`);
        io.of('/lobby').to(roomId).emit('match_started', JSON.stringify({ sceneName }));
    });

    socket.on('leave_room', (data, ack) => {
        const entry = socketToPlayer.get(socket.id);
        if (!entry) { if (ack) ack(JSON.stringify({ ok: true })); return; }
        const { playerId, roomId } = entry;

        const leavingPlayer = rooms.get(roomId)?.players.get(playerId);
        socketToPlayer.delete(socket.id);
        socket.leave(roomId);
        removePlayerFromRoom(playerId, roomId, 'left');

        lobbyLog(leavingPlayer?.traceId, roomId, playerId, `"${leavingPlayer?.name}" left`);
        if (ack) ack(JSON.stringify({ ok: true }));
    });

    socket.on('disconnect', () => {
        lastEventTime.delete(socket.id);

        const entry = socketToPlayer.get(socket.id);
        if (!entry) {
            console.log(`[Lobby] socket disconnected (no session): ${socket.id}`);
            return;
        }
        const { playerId, roomId } = entry;
        socketToPlayer.delete(socket.id);

        const room   = rooms.get(roomId);
        const player = room?.players.get(playerId);
        if (!player) return;

        player.status = 'disconnected';
        lobbyLog(player.traceId, roomId, playerId,
            `"${player.name}" disconnected — grace ${RECONNECT_GRACE_MS / 1000}s started`);
        broadcastRoomState(roomId);

        player.reconnectTimer = setTimeout(() => {
            lobbyLog(player.traceId, roomId, playerId,
                `"${player.name}" grace expired — removing`);
            player.reconnectTimer = null;
            removePlayerFromRoom(playerId, roomId, 'reconnect_timeout');
        }, RECONNECT_GRACE_MS);
    });
});

// =========================================================================
// /playersync namespace  (from playersync-server.js)
// =========================================================================

const players = {};

io.of('/playersync').on('connection', (socket) => {
    console.log('[PlayerSync] connected:', socket.id);

    players[socket.id] = { x: 0, y: 0, z: 0 };

    socket.emit('player_id', socket.id);
    socket.emit('existing_players', players);
    socket.broadcast.emit('player_join', socket.id);

    socket.on('player_move', (data) => {
        if (data && data.position) {
            players[socket.id] = data.position;
            socket.broadcast.emit('player_move', {
                id: socket.id,
                position: data.position,
            });
        }
    });

    socket.on('disconnect', () => {
        console.log('[PlayerSync] disconnected:', socket.id);
        delete players[socket.id];
        socket.broadcast.emit('player_leave', socket.id);
    });
});

// =========================================================================
// Start
// =========================================================================

httpServer.listen(PORT, () => {
    console.log(`LiveDemo server running on http://localhost:${PORT}`);
    console.log(`Namespaces: /lobby, /playersync`);
});
