using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.IO;

namespace OpenTibia.Network.Packets.Incoming
{
    public class BanishmentsAndNamelocksIncomingPacket : IIncomingPacket // = RuleViolationIncomingPacket
    {
        public string Name { get; set; }

        public byte Reason { get; set; }

        public byte Action { get; set; }

        public string Comment { get; set; }        

        public uint StatmentId { get; set; }

        public bool IPAddressBanishment { get; set; }

        public void Read(IByteArrayStreamReader reader, IHasFeatureFlag features)
        {
            if ( !features.HasFeatureFlag(FeatureFlag.MessageStatement) )
            {
                Name = reader.ReadString();

                /*
                    For 7.40:
            
                    0 = 1a) Offensive name
                    1 = 1b) Name containing part of sentense
                    2 = 1b) Name with nonsensical letter combination
                    3 = 1b) Invalid name format
                    4 = 1c) Name not describing person
                    ...
                    31 = Invalid payment
                */

                Reason = reader.ReadByte();

                Comment = reader.ReadString();

                /*
                    For 7.40:
            
                    0 = Notation
                    1 = Namelock
                    2 = AccountBan
                    3 = Namelock/AccountBan
                    4 = AccountBan + FinalWarning
                    5 = Namelock/AccountBan + FinalWarning
                */

                Action = reader.ReadByte();

                IPAddressBanishment = reader.ReadBool();
            }
            else
            {
                Name = reader.ReadString();

                /*
                    For 8.60:
                    0 = 1a) Offesive Name
                    1 = 1b) Invalid Name Format
                    ...
                    19 = Destructive Behaviour
                */

                Reason = reader.ReadByte();

                /*
                    For 8.60:
            
                    0 = Notation
                    1 = Name Report
                    2 = Banishment
                    3 = Name Report + Banishment
                    4 = Banishment + FinalWarning
                    5 = Name Report + Banishment + FinalWarning
                    6 = Statment Report
                */

                Action = reader.ReadByte();

                Comment = reader.ReadString();

                StatmentId = reader.ReadUInt(); // Only set for some Reasons

                IPAddressBanishment = reader.ReadBool();
            }
        }
    }
}