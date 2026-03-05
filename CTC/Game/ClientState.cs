using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CTC
{
    /// <summary>
    /// Holds all game data information for a single connection to a server
    /// or possibly a playing movie, either way this is usually passed to
    /// GameFrame.AddClient in order to create a new window displaying it.
    /// Phase 9: implements IDisposable to release the owned TibiaGameData
    /// (which in turn releases all GPU textures).
    /// </summary>
    public class ClientState : IDisposable
    {
        public readonly ClientViewport Viewport;
        public readonly TibiaGameData GameData;
        public readonly TibiaGameProtocol Protocol;
        private readonly PacketStream InStream;

        public ClientState(PacketStream InStream)
        {
            this.InStream = InStream;
            // Phase 14: use AppContext.BaseDirectory so the paths resolve
            // correctly for both `dotnet run` and self-contained publishes on
            // any platform (Windows, Linux, macOS).
            string datPath = Path.Combine(AppContext.BaseDirectory, "Tibia.dat");
            string sprPath = Path.Combine(AppContext.BaseDirectory, "Tibia.spr");
            FileStream datFile = new FileStream(datPath, FileMode.Open);
            FileStream sprFile = new FileStream(sprPath, FileMode.Open);
            GameData = new TibiaGameData(datFile, sprFile);
            Protocol = new TibiaGameProtocol(GameData);
            Viewport = new ClientViewport(GameData, Protocol);
        }

        public String HostName
        {
            get {
                return InStream.Name;
            }
        }

        private void ReadPackets(GameTime Time)
        {
            while (InStream.Poll(Time))
            {
                try
                {
                    NetworkMessage? nmsg = InStream.Read(Time);
                    if (nmsg == null)
                        return;
                    Protocol.parsePacket(nmsg);
                }
                catch (Exception ex)
                {
                    Log.Error("Protocol Error: " + ex.Message);
                }
            }
        }

        public void ForwardTo(TimeSpan Span)
        {
            if (!(InStream is TibiaMovieStream))
                throw new NotSupportedException("Can't fast-forward non-movie streams.");

            TibiaMovieStream Movie = (TibiaMovieStream)InStream;

            while (Movie.Elapsed.TotalSeconds < Span.TotalSeconds)
                Protocol.parsePacket(Movie.Read(null)!);
        }

        public void Update(GameTime Time)
        {
            ReadPackets(Time);
        }

        /// <summary>
        /// Phase 9: Releases GPU textures by disposing the owned TibiaGameData.
        /// Also disposes the underlying PacketStream if it implements IDisposable
        /// (e.g. <see cref="LivePacketStream"/> which owns a TCP connection).
        /// </summary>
        public void Dispose()
        {
            GameData?.Dispose();
            (InStream as IDisposable)?.Dispose();
        }
    }
}
