const http = require("http");
const { Server } = require("socket.io");

const PORT = 3000;

// ======================================================
// HTTP SERVER (REQUIRED FOR UNITY / NATIVE WS)
// ======================================================
const httpServer = http.createServer();

const io = new Server(httpServer, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

console.log(`🚀 Socket.IO server starting on port ${PORT}`);


// ======================================================
// ROOT NAMESPACE  ("/") — NO AUTH
// ======================================================
io.on("connection", (socket) => {
  console.log("✅ / ROOT CONNECTED:", socket.id);

  // ---- Text event
  socket.emit("hello", {
    message: "welcome",
    socketId: socket.id
  });

  // ---- Single binary (2s)
  setTimeout(() => {
    const buffer = Buffer.from("Hello");
    console.log("📤 / file (single binary)");
    socket.emit("file", buffer);
  }, 2000);

  // ---- Multi binary (4s)
  setTimeout(() => {
    const buf1 = Buffer.from([1, 2, 3]);
    const buf2 = Buffer.from([4, 5, 6]);
    console.log("📤 / multi (2 binaries)");
    socket.emit("multi", buf1, buf2);
  }, 4000);

  // ---- Binary + ACK (6s)
  setTimeout(() => {
    const payload = Buffer.from("ACK_TEST");
    console.log("📤 / binary-ack");

    socket.emit("binary-ack", payload, (ack) => {
      console.log("📥 / ACK from client:", ack);
    });
  }, 6000);

  // ---- Client → Server
  socket.on("ping-test", (msg) => {
    console.log("📩 / ping-test:", msg);
    socket.emit("pong-test", { serverTime: Date.now() });
  });

  socket.on("upload", (buffer, ack) => {
    console.log("📩 / upload received:", buffer.length, "bytes");
    if (ack) ack({ ok: true, size: buffer.length });
  });

  // ---- Basic Chat (for BasicChat sample)
  socket.on("chat", (msg) => {
    console.log("📩 / chat:", msg);
    socket.emit("chat", msg);  // Echo back
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ / ROOT DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// /admin — AUTH REQUIRED
// ======================================================
io.of("/admin").use((socket, next) => {
  const token = socket.handshake.auth?.token;
  console.log(`🔐 /admin auth token: "${token}"`);

  if (token === "test-secret") {
    console.log("✅ /admin AUTH OK");
    next();
  } else {
    console.log("❌ /admin AUTH FAIL");
    next(new Error("unauthorized"));
  }
});

io.of("/admin").on("connection", (socket) => {
  console.log("✅ /admin CONNECTED:", socket.id);

  socket.on("ping", (payload, ack) => {
    console.log("📩 /admin ping");
    if (ack) ack({ ok: true, adminTime: Date.now() });
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ /admin DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// /admin-bad — ALWAYS REJECT
// ======================================================
io.of("/admin-bad").use((socket, next) => {
  const token = socket.handshake.auth?.token;
  console.log(`🔐 /admin-bad token: "${token}"`);
  console.log("❌ /admin-bad AUTH INTENTIONAL FAIL");
  next(new Error("unauthorized"));
});


// ======================================================
// /public — NO AUTH
// ======================================================
io.of("/public").on("connection", (socket) => {
  console.log("✅ /public CONNECTED:", socket.id);

  socket.on("disconnect", () => {
    console.log("❌ /public DISCONNECTED:", socket.id);
  });
});


// ======================================================
// /webgl — WEBGL TESTING (NO AUTH)
// ======================================================
io.of("/webgl").on("connection", (socket) => {
  console.log("✅ /webgl CONNECTED:", socket.id);

  // Welcome message
  socket.emit("welcome", {
    message: "WebGL client connected!",
    socketId: socket.id,
    serverTime: Date.now()
  });

  // Ping → Pong (for latency testing)
  socket.on("ping", (payload) => {
    console.log("📩 /webgl ping:", payload);
    socket.emit("pong", {
      clientTime: payload,
      serverTime: new Date().toISOString(),
      roundtrip: "calculate on client"
    });
  });

  // Message echo
  socket.on("message", (msg) => {
    console.log("📩 /webgl message:", msg);
    socket.emit("message", {
      echo: msg,
      from: "server",
      timestamp: Date.now()
    });
  });

  // Simple text event
  socket.on("test", (data) => {
    console.log("📩 /webgl test:", data);
    socket.emit("test-response", { received: data, ok: true });
  });

  // Broadcast to all WebGL clients
  socket.on("broadcast", (msg) => {
    console.log("📢 /webgl broadcast:", msg);
    io.of("/webgl").emit("broadcast", {
      from: socket.id,
      message: msg
    });
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ /webgl DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// /playersync — PLAYER SYNC SAMPLE (NO AUTH)
// ======================================================

const players = {};

io.of("/playersync").on("connection", (socket) => {
  console.log("✅ /playersync CONNECTED:", socket.id);

  // Register player at origin
  players[socket.id] = { x: 0, y: 0, z: 0 };

  // 🔥 Send server-assigned ID to this client
  socket.emit("player_id", socket.id);
  console.log("📤 /playersync → player_id:", socket.id);

  // 🔥 Send existing players to the new player
  socket.emit("existing_players", players);
  console.log("📤 /playersync → existing_players:", Object.keys(players).length, "players");

  // 🔥 Notify other players that someone joined
  socket.broadcast.emit("player_join", socket.id);
  console.log("📢 /playersync → broadcast player_join:", socket.id);

  // Receive movement from client
  socket.on("player_move", (data) => {
    if (data && data.position) {
      players[socket.id] = data.position;

      // Broadcast to all other players
      socket.broadcast.emit("player_move", {
        id: socket.id,
        position: data.position
      });
    }
  });

  socket.on("disconnect", () => {
    console.log("❌ /playersync DISCONNECTED:", socket.id);

    // Remove player from list
    delete players[socket.id];

    // Notify other players
    socket.broadcast.emit("player_leave", socket.id);
  });
});


// ======================================================
// START SERVER
// ======================================================
httpServer.listen(PORT, () => {
  console.log(`✅ HTTP + WebSocket listening on ${PORT}`);

  console.log("\n📋 TEST SCENARIOS");
  console.log("1️⃣ /            → no auth + binary");
  console.log("2️⃣ /admin       → token='test-secret'");
  console.log("3️⃣ /admin-bad   → always unauthorized");
  console.log("4️⃣ /public      → no auth");
  console.log("5️⃣ /webgl       → WebGL browser testing\n");
  console.log("  /playersync  ← HERO FEATURE\n");
});
