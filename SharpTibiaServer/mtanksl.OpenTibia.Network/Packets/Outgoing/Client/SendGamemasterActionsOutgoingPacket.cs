using OpenTibia.Common.Objects;
using OpenTibia.IO;

namespace OpenTibia.Network.Packets.Outgoing
{
    public class SendGamemasterActionsOutgoingPacket : IOutgoingPacket
    {
        public SendGamemasterActionsOutgoingPacket(int violationReasons)
        {
            ViolationReasons = violationReasons;
        }

        public int ViolationReasons { get; set; }

        public void Write(IByteArrayStreamWriter writer, IHasFeatureFlag features)
        {
            writer.Write( (byte)0x0B);

            for (int i = 0; i < ViolationReasons; i++)
            {
                /*
                    For 7.40:

                    DisplayNotation = 1
                    DisplayNamelock = 2
                    DisplayAccountBan = 4
                    DisplayNamelockAccountBan = 8
                    DisplayAccountBanFinalWarning = 16
                    DisplayNamelockAccountBanFinalWarning = 32
                    EnableIPAddressBanishment = 64
                    All = 127
                */

                /*
                    For 8.60:
            
                    Notation = 1
                    Name Report = 2
                    Banishment = 4
                    Name Report + Banishment = 8
                    Banishment + FinalWarning = 16
                    Name Report + Banishment + FinalWarning = 32
                    Statment Report = 64
                    EnableIPAddressBanishment = 128
                    All = 255
                */

                writer.Write( (byte)0xFF);
            }
        }
    }
}