const http = require("http");
const { Server } = require("socket.io");

const PORT = 3000;
const httpServer = http.createServer();

const io = new Server(httpServer, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

// ======================================================
// /playersync — PLAYER SYNC SAMPLE (NO AUTH)
// ======================================================

const players = {};

io.of("/playersync").on("connection", (socket) => {
  console.log("✅ /playersync CONNECTED:", socket.id);

  // Register player at origin
  players[socket.id] = { x: 0, y: 0, z: 0 };

  // Send server-assigned ID to this client
  socket.emit("player_id", socket.id);
  console.log("📤 /playersync → player_id:", socket.id);

  // Send existing players to the new player
  socket.emit("existing_players", players);
  console.log("📤 /playersync → existing_players:", Object.keys(players).length, "players");

  // Notify other players that someone joined
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

httpServer.listen(PORT, () => {
  console.log(`✅ Server running on http://localhost:${PORT}`);
});
