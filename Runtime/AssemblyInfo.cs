using System.Runtime.CompilerServices;

// Expose internal types to Editor assembly for testing
[assembly: InternalsVisibleTo("SocketIOUnity.Editor")]

// Expose internal types to Test assembly for regression tests (v1.0.1)
[assembly: InternalsVisibleTo("SocketIOUnity.Tests")]

// Expose internal types to Stress Test assembly (v1.3.0)
[assembly: InternalsVisibleTo("SocketIOUnity.Tests.Stress")]
