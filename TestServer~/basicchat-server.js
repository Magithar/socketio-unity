const { Server } = require("socket.io");
const http = require("http");

const server = http.createServer();
const io = new Server(server, { cors: { origin: "*" } });

io.on("connection", (socket) => {
  console.log("connected:", socket.id);

  socket.on("chat", (msg) => {
    console.log("chat:", msg);
    socket.emit("chat", msg); // echo back to sender
  });

  socket.on("disconnect", () => console.log("disconnected:", socket.id));
});

server.listen(3002, () => console.log("BasicChat server listening on :3002"));
