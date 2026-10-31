using System;
using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.Net.Messages;

namespace Lazpoly.App.Save
{
    /// <summary>
    /// Binary save format: a magic number and format version, then the generators, the AI seats and
    /// the game state written the same way the network writes it.
    /// </summary>
    public static class SavedGameCodec
    {
        public const int FormatVersion = 1;
        private const ulong Magic = 0x4C415A504F4C5953UL; // "LAZPOLYS"

        public static byte[] Encode(SavedGame save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Snapshot == null) throw new ArgumentException("Save has no game state.", nameof(save));

            var w = new NetWriter();
            w.WriteULong(Magic);
            w.WriteInt(FormatVersion);
            w.WriteULong(save.BoardChecksum);
            w.WriteULong(save.DiceState);
            w.WriteULong(save.DiceIncrement);
            w.WriteULong(save.AiState);
            w.WriteULong(save.AiIncrement);

            var ids = new List<int>(save.AiPlayers.Keys);
            ids.Sort();
            w.WriteInt(ids.Count);
            foreach (int id in ids)
            {
                w.WriteInt(id);
                w.WriteByte((byte)save.AiPlayers[id].Difficulty);
                w.WriteInt(save.AiPlayers[id].LastProposalTurn);
            }

            MessageCodec.WriteSnapshot(w, save.Snapshot);
            return w.ToArray();
        }

        /// <summary>Reads a save. Throws <see cref="FormatException"/> for anything that is not a valid save of this version.</summary>
        public static SavedGame Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var r = new NetReader(data);
            if (r.ReadULong() != Magic) throw new FormatException("Not a Lazpoly save file.");
            int version = r.ReadInt();
            if (version != FormatVersion) throw new FormatException($"Save format {version} is not supported.");

            var save = new SavedGame
            {
                BoardChecksum = r.ReadULong(),
                DiceState = r.ReadULong(),
                DiceIncrement = r.ReadULong(),
                AiState = r.ReadULong(),
                AiIncrement = r.ReadULong(),
            };
            if ((save.DiceIncrement & 1UL) == 0UL || (save.AiIncrement & 1UL) == 0UL) throw new FormatException("Invalid generator state.");

            int count = r.ReadCount();
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt();
                byte difficulty = r.ReadByte();
                int lastProposalTurn = r.ReadInt();
                if (!Enum.IsDefined(typeof(AiDifficulty), (int)difficulty)) throw new FormatException($"Invalid AI difficulty {difficulty}.");
                if (save.AiPlayers.ContainsKey(id)) throw new FormatException($"Player {id} listed twice.");
                save.AiPlayers.Add(id, new SavedAiPlayer { Difficulty = (AiDifficulty)difficulty, LastProposalTurn = lastProposalTurn });
            }

            save.Snapshot = MessageCodec.ReadSnapshot(r);
            if (!r.AtEnd) throw new FormatException("Unexpected data after the save.");
            return save;
        }
    }
}
