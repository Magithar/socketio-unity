/**
 * Lobby Server — Socket.IO multiplayer lobby for SocketIOUnity
 *
 * Features:
 *   - Room creation and joining via 6-character codes
 *   - Persistent player IDs — separate from socket.id, survive reconnect
 *   - 10-second reconnect grace period (player slot held on disconnect)
 *   - Host migration when host disconnects
 *   - Room cleanup when last player leaves
 *
 * Events (client → server):
 *   create_room       { name }               → ack { ok, roomId, playerId, sessionToken }
 *   join_room         { roomId, name }        → ack { ok, roomId, playerId, sessionToken }
 *   reconnect_player  { playerId, roomId, sessionToken } → ack { ok, roomId, playerId }
 *   player_ready      { ready }
 *   start_match       { sceneName? }
 *   leave_room        {}                      → ack { ok }
 *
 * Events (server → client):
 *   room_state        JSON snapshot of full room
 *   match_started     { sceneName }
 *   player_removed    { playerId, name, reason }  reason: "left" | "reconnect_timeout"
 *
 * DEVELOPMENT SERVER ONLY — no auth, rate-limiting, or abuse protection.
 */

'use strict';

const express    = require('express');
const http       = require('http');
const { Server } = require('socket.io');

const app        = express();
const httpServer = http.createServer(app);
const io         = new Server(httpServer, {
    cors: { origin: '*', methods: ['GET', 'POST'] },
    // Heartbeat — Socket.IO natively pings every client on this interval.
    // If no pong is received within pingTimeout the socket is disconnected,
    // triggering the disconnect handler and the reconnect grace period.
    pingInterval: 25_000,  // ms between pings  (default: 25 000)
    pingTimeout:  20_000,  // ms to wait for pong (default: 20 000)
});

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const PORT               = 3001;
const RECONNECT_GRACE_MS = 10_000;
const ROOM_CODE_CHARS    = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
const ROOM_CODE_LENGTH   = 6;

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------

/**
 * rooms: Map<roomId, Room>
 *
 * Room: {
 *   roomId:  string,
 *   hostId:  string,          // playerId of current host
 *   version: number,
 *   players: Map<playerId, Player>
 * }
 *
 * Player: {
 *   id:             string,   // persistent playerId (survives reconnect)
 *   socketId:       string,   // current socket.id (changes on reconnect)
 *   sessionToken:   string,   // secret issued at join; required to reconnect
 *   traceId:        string,   // short log correlation ID — stable across reconnects
 *   name:           string,
 *   ready:          boolean,
 *   status:         'connected' | 'disconnected',
 *   roomId:         string,
 *   reconnectTimer: ReturnType<typeof setTimeout> | null
 * }
 */
const rooms          = new Map();
const socketToPlayer = new Map(); // socket.id → { playerId, roomId }

// ---------------------------------------------------------------------------
// Helpers
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

function generatePlayerId() {
    return Math.random().toString(36).slice(2, 10) + Date.now().toString(36);
}

function generateSessionToken() {
    // 48 chars of base-36 — unguessable within a session's lifetime
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

/** Structured log: [Lobby][T:traceId][Room:roomId][P:playerId] msg */
function lobbyLog(traceId, roomId, playerId, msg) {
    const t = traceId  ? `[T:${traceId}]`   : '';
    const r = roomId   ? `[Room:${roomId}]`  : '';
    const p = playerId ? `[P:${playerId}]`   : '';
    console.log(`[Lobby]${t}${r}${p} ${msg}`);
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

/**
 * Permanently removes a player from their room.
 * Emits player_removed to remaining members, migrates host if needed,
 * deletes the room if empty, and broadcasts the new room_state.
 */
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
        lobbyLog(player.traceId, roomId, playerId, `🗑  room deleted (empty) reason=${reason}`);
        return;
    }

    // Notify remaining players why this player disappeared
    io.of('/lobby').to(roomId).emit('player_removed', JSON.stringify({
        playerId, name: player.name, reason,
    }));

    // Prefer a connected player as new host; fall back to any remaining player
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
    console.log(`[Lobby] 🔌 socket connected: ${socket.id}`);

    // ------------------------------------------------------------------
    // create_room
    // ------------------------------------------------------------------
    socket.on('create_room', (data, ack) => {
        const { name } = parsePayload(data);
        if (!name || !name.trim())
            return ack(JSON.stringify({ ok: false, error: 'Name required' }));

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

        lobbyLog(traceId, roomId, playerId, `🏠 room created by "${player.name}" socket=${shortSocket(socket.id)}`);
        ack(JSON.stringify({ ok: true, roomId, playerId, sessionToken }));
        broadcastRoomState(roomId);
    });

    // ------------------------------------------------------------------
    // join_room
    // ------------------------------------------------------------------
    socket.on('join_room', (data, ack) => {
        const { roomId: rawId, name } = parsePayload(data);
        const roomId = (rawId || '').toUpperCase();

        if (!name || !name.trim())
            return ack(JSON.stringify({ ok: false, error: 'Name required' }));

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

        lobbyLog(traceId, roomId, playerId, `🚪 "${player.name}" joined socket=${shortSocket(socket.id)}`);
        ack(JSON.stringify({ ok: true, roomId, playerId, sessionToken }));
        broadcastRoomState(roomId);
    });

    // ------------------------------------------------------------------
    // reconnect_player — restore a session within the grace window
    // ------------------------------------------------------------------
    socket.on('reconnect_player', (data, ack) => {
        const { playerId, roomId, sessionToken } = parsePayload(data);

        const room = rooms.get(roomId);
        if (!room)
            return ack(JSON.stringify({ ok: false, error: 'Room no longer exists' }));

        const player = room.players.get(playerId);
        if (!player)
            return ack(JSON.stringify({ ok: false, error: 'Player session expired' }));

        // Validate session token — prevents playerId spoofing
        if (!sessionToken || sessionToken !== player.sessionToken)
            return ack(JSON.stringify({ ok: false, error: 'Invalid session token' }));

        // Cancel the grace-period eviction timer
        if (player.reconnectTimer) {
            clearTimeout(player.reconnectTimer);
            player.reconnectTimer = null;
        }

        // Rebind to the new socket
        const oldSocketId = player.socketId;
        if (oldSocketId && oldSocketId !== socket.id)
            socketToPlayer.delete(oldSocketId);

        player.socketId = socket.id;
        player.status   = 'connected';

        socketToPlayer.set(socket.id, { playerId, roomId });
        socket.join(roomId);

        lobbyLog(player.traceId, roomId, playerId,
            `♻️  "${player.name}" reconnected socket ${shortSocket(oldSocketId)} → ${shortSocket(socket.id)}`);
        ack(JSON.stringify({ ok: true, roomId, playerId }));
        broadcastRoomState(roomId);
    });

    // ------------------------------------------------------------------
    // player_ready
    // ------------------------------------------------------------------
    socket.on('player_ready', data => {
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
    // ------------------------------------------------------------------
    socket.on('start_match', data => {
        const entry = socketToPlayer.get(socket.id);
        if (!entry) return;
        const { playerId, roomId } = entry;

        const room = rooms.get(roomId);
        if (!room || room.hostId !== playerId) return;

        const { sceneName = null } = parsePayload(data);
        const host = room.players.get(playerId);
        lobbyLog(host?.traceId, roomId, playerId, `🎮 match started scene=${sceneName}`);
        io.of('/lobby').to(roomId).emit('match_started', JSON.stringify({ sceneName }));
    });

    // ------------------------------------------------------------------
    // leave_room — intentional exit, no grace period
    // ------------------------------------------------------------------
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

    // ------------------------------------------------------------------
    // disconnect — start grace period; evict on expiry
    // ------------------------------------------------------------------
    socket.on('disconnect', () => {
        const entry = socketToPlayer.get(socket.id);
        if (!entry) {
            console.log(`[Lobby] 🔌 socket disconnected (no session): ${socket.id}`);
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
            lobbyLog(player.traceId, roomId, playerId,
                `❌ "${player.name}" grace expired — removing`);
            player.reconnectTimer = null;
            removePlayerFromRoom(playerId, roomId, 'reconnect_timeout');
        }, RECONNECT_GRACE_MS);
    });
});

// ---------------------------------------------------------------------------
// Start
// ---------------------------------------------------------------------------

httpServer.listen(PORT, () => {
    console.log(`🚀 Lobby server running on http://localhost:${PORT}`);
    console.log(`🛰  Socket.IO namespace: /lobby`);
});
